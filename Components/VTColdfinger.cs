using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
	public class VTColdfinger : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<VTColdfinger> List = new List<VTColdfinger>();
		public static new VTColdfinger Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Start()
        {
            EnsureState(_State);
        }

		protected void PreStop()
		{
			Stop();
		}

		public VTColdfinger()
		{
			List.Add(this);
			OnStart += Start;
			OnPreStop += PreStop;
		}

		#endregion Component Implementation


		[XmlType(AnonymousType = true)]
		public enum States { Standby, Stop, Thaw, Freeze, Raise, Regulate }

		[JsonProperty]
		public HacsComponent<Heater> HeaterRef { get; set; }
        public Heater Heater => HeaterRef?.Component;

		[JsonProperty]
		public HacsComponent<FTColdfinger> ColdfingerRef { get; set; }
        public FTColdfinger Coldfinger => ColdfingerRef?.Component;

		[JsonProperty]
		public HacsComponent<TempSensor> TopTempSensorRef { get; set; }
		public TempSensor TopTempSensor => TopTempSensorRef?.Component;

		[JsonProperty]
		public HacsComponent<TempSensor> WireTempSensorRef { get; set; }
		public TempSensor WireTempSensor => WireTempSensorRef?.Component;

		[JsonProperty]
        public int WireTempLimit = 60;      // degC

		[JsonProperty]
		public int RegulatedSetpoint = 50;

		[JsonProperty]
		public int MaxHeaterPower { get; set; }
		[JsonProperty]
		public int MaxWarmHeaterPower { get; set; }

		States __State = States.Standby;
		[JsonProperty]
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

		[JsonProperty]
		public bool Dirty { get; set; }

		public double Temperature => Heater.Temperature;

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
            return Heater.Temperature < 0 & WireTempSensor.Temperature < WireTempLimit - 20 ?
                MaxHeaterPower :
                MaxWarmHeaterPower;

        }

        Heater.Devices heaterDevice()
		{
			return RegulatedSetpoint > 25 ? // Reference temperature for VTT
                Heater.Devices.VTC_WARM :
                Heater.Devices.VTC;
		}

		void configureHeater()
		{
			Heater.SetDevice(heaterDevice());
			Heater.SetPowerMax(heaterMax());
			Heater.SetSetpoint(RegulatedSetpoint);
		}

		bool heaterConfigured()
		{
			return Heater.Mode == Components.Heater.Modes.Manual ? (Heater.Setpoint == RegulatedSetpoint) :
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
			else if (Heater.Mode != Components.Heater.Modes.Manual)
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
			return $"{Name}: {State} \r\n" +
				Utility.IndentLines(
					TopTempSensor.ToString() + "\r\n" +
					Heater.ToString() + "\r\n" +
					WireTempSensor.ToString() + "\r\n" +
					Coldfinger.ToString()
				);
		}
	}
}
