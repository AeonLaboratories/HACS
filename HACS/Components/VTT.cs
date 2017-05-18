using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	public class VTT : Component
	{
		public static new List<VTT> List;
		public static new VTT Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType = true)]
		public enum States { Standby, Stop, Thaw, Freeze, Raise, Regulate }

		[XmlElement(ElementName = "Heater")]
		public string HeaterName { get; set; }
		[XmlIgnore] public Heater Heater { get; set; }

		[XmlElement(ElementName = "Coldfinger")]
		public string ColdfingerName { get; set; }
		[XmlIgnore] public FTColdfinger Coldfinger { get; set; }

		[XmlElement(ElementName = "TopTempSensor")]
		public string TopTempSensorName { get; set; }
		[XmlIgnore] public TempSensor TopTempSensor { get; set; }

		[XmlElement(ElementName = "WireTempSensor")]
		public string WireTempSensorName { get; set; }
		[XmlIgnore] public TempSensor WireTempSensor { get; set; }
		public int WireTempLimit = 60;		// degC

		public int RegulatedSetpoint = 50;

		public int MaxHeaterPower { get; set; }
		public int MaxWarmHeaterPower { get; set; }

		States __State;
		States _State
		{
			get { return __State; }
			set { __State = value; Update(); }
		}
		/// <summary>
		/// Readonly except at system startup.
		/// </summary>
		public States State
		{
			get { return _State; }
			set { if (!Initialized) _State = value; }
		}

		public bool Dirty { get; set; }

		public double Temperature { get { return Heater.Temperature; } }

		public VTT()
		{
			_State = States.Standby;
		}

		public VTT(string name) : this()
		{
			Name = name;
		}

		public override void Connect()
		{
			Heater = Heater.Find(HeaterName);
			Coldfinger = FTColdfinger.Find(ColdfingerName);
			TopTempSensor = TempSensor.Find(TopTempSensorName);
			WireTempSensor = TempSensor.Find(WireTempSensorName);
		}

		public void Connect(Heater h, FTColdfinger ftc, TempSensor tts, TempSensor wts)
		{
			Heater = h;
			Coldfinger = ftc;
			TopTempSensor = tts;
			WireTempSensor = wts;
		}

		public override void Initialize()
		{
			EnsureState(_State);
			Initialized = true; 
		}

		public void EnsureState(States state)
		{
			switch (state)
			{
			case States.Freeze:
				Freeze();
				break;
			case States.Raise:
				Raise();
				break;
			case States.Regulate:
				Regulate();
				break;
			case States.Standby:
				Standby();
				break;
			case States.Stop:
				Stop();
				break;
			case States.Thaw:
				Thaw();
				break;
			}
		}

		public void Standby()
		{ _State = States.Standby; }

		public void Stop()
		{ _State = States.Stop; }

		public void Thaw()
		{
			if (Coldfinger.State != FTColdfinger.States.Thaw)
				Coldfinger.Thaw();
			_State = States.Thaw;
		}

		public void Freeze()
		{
			if (Heater.IsOn)
				Heater.TurnOff();
			if (Coldfinger.State != FTColdfinger.States.Freeze)
				Coldfinger.Freeze();
			_State = States.Freeze;
		}

		public void Raise()
		{
			if (Coldfinger.State != FTColdfinger.States.Raise)
				Coldfinger.Raise();
			_State = States.Raise;
		}

		public void Regulate()
		{
			Regulate(Heater.Setpoint);
		}

		public void Regulate(int setpoint)
		{
			RegulatedSetpoint = setpoint;
			_State = States.Regulate;
		}

		int heaterMax()
		{
			return Heater.Temperature > 15 ?
				MaxWarmHeaterPower :
				MaxHeaterPower;
		}

		Heater.Devices heaterDevice()
		{
			return RegulatedSetpoint > 25 ? // Reference temperature for VTT
				Heater.Devices.VTT :
				Heater.Devices.VTT;
		}

		void configureHeater()
		{
			Heater.SetDevice(heaterDevice());
			Heater.SetPowerMax(heaterMax());
			Heater.SetSetpoint(RegulatedSetpoint);
		}

		bool heaterConfigured()
		{
			return Heater.Mode == Heater.Modes.Manual ? (Heater.Setpoint == RegulatedSetpoint) :
				(Heater.Target.Setpoint == RegulatedSetpoint &&
				Heater.Target.PowerMax == heaterMax() &&
				Heater.DeviceType == heaterDevice());
		}

		void manageHeaterAndColdfinger()
		{
			bool safeToOperateHeater = true;

			if (!heaterConfigured())
			{
				safeToOperateHeater = false;
				configureHeater();
			}
			else if (Heater.Mode != Heater.Modes.Manual)
			{
				if (Heater.Target.Setpoint < Coldfinger.AirTemperature)
				{
					if (Coldfinger.State != FTColdfinger.States.Raise)
						Coldfinger.Raise();
					if (Coldfinger.Temperature > -150)
						safeToOperateHeater = false;
				}
				else
				{
					if (Coldfinger.Temperature < Coldfinger.AirTemperature - Coldfinger.NearAirTemperature)
						Coldfinger.Thaw();
				}
			}

			if (WireTempSensor.Temperature > WireTempLimit ||
				Temperature > WireTempLimit ||
				TopTempSensor.Temperature > WireTempLimit)
				safeToOperateHeater = false;

			if (!safeToOperateHeater && Heater.IsOn)
				Heater.TurnOff();
			else if (!Heater.IsOn)
				Heater.TurnOn();
		}

		public void Update()
		{
			if (!Initialized) return;

			switch (_State)
			{

			case States.Regulate:
				manageHeaterAndColdfinger();				
				break;
			case States.Raise:
				break;
			case States.Freeze:
				break;
			case States.Thaw:
				if (Coldfinger.State == FTColdfinger.States.Standby)
					Standby();
				break;

			case States.Stop:
				if (Heater.IsOn)
					Heater.TurnOff();
				if (Coldfinger.State != FTColdfinger.States.Standby)
					Coldfinger.Stop();
				if (!Heater.IsOn && Coldfinger.State == FTColdfinger.States.Standby)
					Standby();
				break;

			case States.Standby:
			default:
				break;
			}
		}

		public override string ToString()
		{
			return Name + ": " + State.ToString() + "\r\n" +
				Utility.IndentLines(
					TopTempSensor.ToString() + "\r\n" +
					Heater.ToString() + "\r\n" +
					WireTempSensor.ToString() + "\r\n" +
					Coldfinger.ToString()
				);
		}
	}
}
