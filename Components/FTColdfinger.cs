using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
    public class FTColdfinger : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<FTColdfinger> List = new List<FTColdfinger>();
		public static new FTColdfinger Find(string name) { return List.Find(x => x?.Name == name); }

        protected virtual void Initialize()
        {
            ResetAdaptation(FreezeTarget);
            EnsureState(_State);
        }

		public FTColdfinger()
		{
			List.Add(this);
			OnInitialize += Initialize;
		}

		#endregion Component Implementation


		[XmlType(AnonymousType=true)]
		public enum States { Standby, Stop, Thaw, Freeze, Raise }

		Stopwatch valveOpenStopwatch = new Stopwatch();
		double valveOpenTemp;

		States __State = States.Standby;
		States _State
		{
			get { return __State; }
			set { __State = value; Update(); }
		}
		/// <summary>
		/// Readonly except at system startup.
		/// </summary>
		[JsonProperty]
		public States State
		{
			get { return _State; }
			set { if (!Initialized) _State = value; }
		}

        States PriorState = States.Standby;

		[JsonProperty]
		public HacsComponent<TempSensor> LevelSensorRef { get; set; }
		TempSensor LevelSensor => LevelSensorRef?.Component;

		[JsonProperty]
		public HacsComponent<HacsComponent> LNValveRef { get; set; }
		public IValve LNValve => LNValveRef?.Component as IValve;

		[JsonProperty]
		public HacsComponent<OnOffDevice> AirSupplyRef { get; set; }
		public OnOffDevice AirSupply => AirSupplyRef?.Component;

		[JsonProperty]
		public HacsComponent<LnManifold> LnManifoldRef { get; set; }
		LnManifold LnManifold => LnManifoldRef?.Component;

		[JsonProperty]
		public HacsComponent<HacsComponent> AirTemperatureSensorRef { get; set; }
        public HacsComponent AirTemperatureSensor => AirTemperatureSensorRef?.Component;

		public double Temperature => LevelSensor.Temperature;
		public double AirTemperature
		{ 
			get 
			{
				TempSensor ts = AirTemperatureSensor as TempSensor;
				if (ts != null) return ts.Temperature;
				Meter m = AirTemperatureSensor as Meter;
				if (m != null) return m;
				Heater h = AirTemperatureSensor as Heater;
				if (h != null) return h.Controller.CJ0Temperature;
				return LevelSensor.Controller.CJ0Temperature; 
			} 
		}

		// "...Trigger" is the temperature error that initiates LN flow.
		// The LN valve is opened when the temperature is <trigger> or more
		// degrees warmer than <Target>.
		[JsonProperty]
		public int FreezeTarget { get; set; }
		[JsonProperty]
		public int FreezeTrigger { get; set; }

		[JsonProperty]
		public int RaiseTarget { get; set; }
		[JsonProperty]
		public int RaiseTrigger { get; set; }

        [XmlIgnore] public double ColdestLNSensorTemperature;
		[XmlIgnore] public double Target;

		// FTC within this many degrees of cold junction temperature == "Near"
		[JsonProperty]//, DefaultValue(5)]
		public double NearAirTemperature { get; set; } = 5;
		[JsonProperty]//, DefaultValue(1)]
		public double AirDeadband { get; set; } = 1;

		public bool isNearAirTemperature()
		{ return Math.Abs(Temperature - AirTemperature) <= Math.Abs(NearAirTemperature); }

		/// <summary>
		/// Coldfinger temperature is no cooler than NearAirTemperature degrees
		/// below air temperature.
		/// </summary>
		/// <returns>true, if Temperature > AirTemperature - NearAirTemperature</returns>
		public bool isThawed()
		{ return Temperature > AirTemperature - NearAirTemperature; }

		public override string ToString()
		{
			return Name + ": " + State.ToString() + ", Target: " + Target + "°C\r\n" +
				Utility.IndentLines(
					AirSupply.ToString() + "\r\n" +
					LevelSensor.ToString() + "\r\n" +
					LNValve.ToString()
				);
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

		public void Standby()
		{ PriorState = _State; _State = States.Standby; }

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
            PriorState = _State;
            _State = States.Freeze;
		}

		public void Raise()
		{
			AirOff();
			ResetAdaptation(RaiseTarget);
            PriorState = _State;
            _State = States.Raise;
		}

		public void Thaw()
		{
			LNOff();
            PriorState = _State;
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
            if (PriorState == States.Standby)
            {
                AirSupply.TurnOn();
                return;
            }
            
			Target = AirTemperature - NearAirTemperature;
			if (AirSupply.IsOn)
			{
				if (Temperature > Target + AirDeadband)
					AirSupply.TurnOff();
			}
			else
			{
				if (Temperature < Target)
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
			ColdestLNSensorTemperature = valveOpenTemp = Temperature;
			if (State == States.Raise && LNValve.Operations.Contains("Trickle"))
				LNValve.DoOperation("Trickle");
			else
				LNValve.Open();
			valveOpenStopwatch.Restart();
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
				//if (AirSupply.MillisecondsOff > 60000) Stop();
				if (!AirSupply.IsOn) Stop();
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
