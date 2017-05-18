using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	public class FTColdfinger : Component
    {
		public static new List<FTColdfinger> List;
		public static new FTColdfinger Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType=true)]
		public enum States { Standby, Stop, Thaw, Freeze, Raise }

		Stopwatch valveOpenStopwatch = new Stopwatch();
		double valveOpenTemp;

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

		[XmlElement("LevelSensor")]
		public string LevelSensorName { get; set; }
		TempSensor LevelSensor;

		[XmlElement("LNValve")]
		public string LNValveName { get; set; }
		[XmlIgnore] public Valve LNValve;

		[XmlElement("AirSupply")]
		public string AirSupplyName { get; set; }
		[XmlIgnore] public OnOffDevice AirSupply;

		[XmlElement("LNTank")]
		public string LNTankName { get; set; }
		Tank LNTank;

		[XmlElement("AirTemperatureSensor")]
		public string AirTemperatureSensorName { get; set; }
		[XmlIgnore] public object AirTemperatureSensor = null;

		public double Temperature { get { return LevelSensor.Temperature; } }
        public double AirTemperature
        { 
            get 
            {
                TempSensor ts = AirTemperatureSensor as TempSensor;
                if (ts != null) return ts.Temperature;
                Meter m = AirTemperatureSensor as Meter;
                if (m != null) return m;
                Heater h = AirTemperatureSensor as Heater;
				if (h != null) return ThermalController.Find(h.ControllerName).CJ0Temperature;
                return ThermalController.Find(LevelSensor.ControllerName).CJ0Temperature; 
            } 
        }

		// "...Trigger" is the temperature error that initiates LN flow.
		// The LN valve is opened when the temperature is <trigger> or more
		// degrees warmer than <Target>.
		public int FreezeTarget { get; set; }
		public int FreezeTrigger { get; set; }

		public int RaiseTarget { get; set; }
		public int RaiseTrigger { get; set; }

		[XmlIgnore] public double ColdestLNSensorTemperature;
		[XmlIgnore] public double Target;
		
		// FTC within this many degrees of cold junction temperature == "Near"
		public double NearAirTemperature { get; set; }
        public double AirDeadband  { get; set; }

        public bool isNearAirTemperature()
        { return Math.Abs(Temperature - AirTemperature) <= Math.Abs(NearAirTemperature); }

		public override string ToString()
		{
			return Name + ": " + State.ToString() + ", Target: " + Target + "°C\r\n" +
				Utility.IndentLines(
					AirSupply.ToString() + "\r\n" +
					LevelSensor.ToString() + "\r\n" +
					LNValve.ToString()
				);
		}

		public FTColdfinger()
		{
			_State = States.Standby;
			NearAirTemperature = 5;
			AirDeadband = 1;
		}

		public FTColdfinger(string name) : this()
		{
			Name = name;
			RaiseTarget = -195;
			FreezeTarget = -195;
			RaiseTrigger = 3;
			FreezeTrigger = 7;	
		}

		public override void Connect()
		{
			LevelSensor = TempSensor.Find(LevelSensorName);
			LNValve = Valve.Find(LNValveName);
			LNTank = Tank.Find(LNTankName);
			AirSupply = OnOffDevice.Find(AirSupplyName);
		}

		public void Connect(TempSensor levelSensor, Valve lnValve, Tank lnTank, OnOffDevice airSupply)
		{
			LevelSensor = levelSensor;
			LNValve = lnValve;
			LNTank = lnTank;
			AirSupply = airSupply;
		}

		public void EnsureState(States state)
		{
			switch (state)
			{
			case States.Standby:
				Standby();
				break;
			case States.Stop:
				Stop();
				break;
			case States.Thaw:
				Thaw();
				break;
			case States.Freeze:
				Freeze();
				break;
			case States.Raise:
				Raise();
				break;
			default:
				break;
			}
		}

		public override void Initialize()
		{ 
			ResetAdaptation(FreezeTarget);
			EnsureState(_State);	
			Initialized = true;
		}
		
		public void Standby()
        { _State = States.Standby; }

		public void Stop()
		{
			LNOff();
			AirOff();
			Standby();
		}

        public void Freeze()
        {
			AirOff();
			ResetAdaptation(FreezeTarget);
			_State = States.Freeze;
		}

        public void Raise()
        {
			AirOff();
			ResetAdaptation(RaiseTarget);
			_State = States.Raise;
		}

        public void Thaw()
        {
			LNOff();
			_State = States.Thaw;
		}

		// The LN level management loop adapts the Target temperature to
		// compensate for inaccurate temperature sensing of LN. This function
		// resets the adaptation.
		public void ResetAdaptation(double target)
		{
			Target = target;
			ColdestLNSensorTemperature = Target + 3;
		}

        public void AirOn()
        {
            Target = AirTemperature - NearAirTemperature;
            if (AirSupply.IsOn)
            {
				if (Temperature > Target + AirDeadband)
					AirSupply.TurnOff();
			}
            else
            {
				if (Temperature < Target - AirDeadband) 
					AirSupply.TurnOn();
            }
        }

        public void AirOff()
        {
            AirSupply.TurnOff();
        }

		public void manageLNLevel(int target, int trigger)
		{
			if (valveOpenStopwatch.IsRunning)
			{
				// Track the coldest temperature observed since the valve was opened
				if (Temperature < ColdestLNSensorTemperature)
					ColdestLNSensorTemperature = Temperature;

				if (valveOpenStopwatch.ElapsedMilliseconds > 20000)
				{
					LNOff();	// never leave the LN valve open longer than 20 seconds
					Target = target;	// reset Target to default on timeout
				}
				else if (Temperature <= Target)
				{
					LNOff();
				}
			}
			else
			{
				if (Temperature > Target + trigger)
				{
					// adjust Target to 2 degrees warmer than coldest observed temperature
					Target = ColdestLNSensorTemperature + 2;
					// but no warmer than 3 degrees over the default
					if (Target > target + 3) Target = target + 3;
					LNOn();
				}
			}
		}

		public void LNOn()
		{
			if (LNTank.LevelSensor.Temperature < -140)
			{
				ColdestLNSensorTemperature = valveOpenTemp = Temperature;
				if (State == States.Raise && LNValve.FindAction(Valve.OpenABit) != null)
					LNValve.DoAction(Valve.OpenABit);
				else
					LNValve.Open();
				valveOpenStopwatch.Restart();
			}
		}

		public void LNOff()
		{
			LNValve.Close();
			valveOpenStopwatch.Reset();
		}

        public void Update()
        {
			if (!Initialized) return;

            switch (_State)
	        {
            case States.Freeze:
				manageLNLevel(FreezeTarget, FreezeTrigger);
				break;
            case States.Raise:
				manageLNLevel(RaiseTarget, RaiseTrigger);
                break;
            case States.Thaw:
                AirOn();
                if (AirSupply.MillisecondsOff > 60000) Stop();
                break;
			case States.Stop:
                break;
            default:
            case States.Standby:
                break;
	        }            
        }
    }
}
