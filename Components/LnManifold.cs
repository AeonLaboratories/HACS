using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Utilities;

namespace HACS.Components
{
    // TODO: make this class a DeviceManager that implements ISwitch?
    public class LNManifold : StateManager<LNManifold.TargetStates, LNManifold.States>, ILNManifold
    {
        #region HacsComponent
        [HacsConnect]
        protected virtual void Connect()
        {
            LNSupplyValve = Find<IValve>(lnSupplyValveName);
            Liters = Find<IMeter>(litersName);

            LevelSensor = Find<IThermometer>(levelSensorName);
            OverflowSensor = Find<IThermometer>(overflowSensorName);
            AmbientThermometer = Find<IChamber>("Ambient")?.Thermometer;
        }

        [HacsPreStop]
        protected virtual void PreStop()
        {
            InhibitLN = true;
            switch (StopAction)
            {
                case StopAction.TurnOff:
                    LNSupplyValve.Close();
                    break;
                case StopAction.TurnOn:
                    // really??;
                    break;
                case StopAction.None:
                default:
                    break;
            }
        }
        bool InhibitLN = false;

        #endregion HacsComponent


        [JsonProperty("LNSupplyValve")]
        string LNSupplyValveName { get => LNSupplyValve?.Name; set => lnSupplyValveName = value; }
        string lnSupplyValveName;
        public IValve LNSupplyValve
        {
            get => lnSupplyValve;
            set => Ensure(ref lnSupplyValve, value, NotifyPropertyChanged);
        }
        IValve lnSupplyValve;

        [JsonProperty("Liters")]
        string LitersName { get => Liters?.Name; set => litersName = value; }
        string litersName;
        public IMeter Liters
        {
            get => liters;
            set => Ensure(ref liters, value, NotifyPropertyChanged);
        }
        IMeter liters;

        [JsonProperty("LevelSensor")]
        string LevelSensorName { get => LevelSensor?.Name; set => levelSensorName = value; }
        string levelSensorName;
        public IThermometer LevelSensor
        {
            get => levelSensor;
            set => Ensure(ref levelSensor, value, NotifyPropertyChanged);
        }
        IThermometer levelSensor;

        [JsonProperty("OverflowSensor")]
        string OverflowSensorName { get => OverflowSensor?.Name; set => overflowSensorName = value; }
        string overflowSensorName;
        public IThermometer OverflowSensor
        {
            get => overflowSensor;
            set => Ensure(ref overflowSensor, value, NotifyPropertyChanged);
        }
        IThermometer overflowSensor;

        /// <summary>
        /// Overflow is detected if the overflow sensor is this many
        /// degrees or more colder than ambient.
        /// </summary>
        [JsonProperty, DefaultValue(5)]
        public int OverflowTrigger
        {
            get => overflowTrigger;
            set => Ensure(ref overflowTrigger, value);
        }
        int overflowTrigger = 5;

        [JsonProperty, DefaultValue(10)]
        public int MinimumLiters
        {
            get => litersMinimum;
            set => Ensure(ref litersMinimum, value);
        }
        int litersMinimum = 10;

        /// <summary>
        /// LN fill cycle stops when LevelSensor <= this temperature
        /// </summary>
        [JsonProperty, DefaultValue(-192)]
        public int TargetTemperature
        {
            get => targetTemperature;
            set => Ensure(ref targetTemperature, value);
        }
        int targetTemperature = -192;

        [JsonProperty, DefaultValue(5)]
        public int FillTrigger
        {
            get => fillTrigger;
            set => Ensure(ref fillTrigger, value);
        }
        int fillTrigger = 5;

        [JsonProperty, DefaultValue(180)]
        public int SecondsSlowToFill
        {
            get => secondsSlowToFill;
            set => Ensure(ref secondsSlowToFill, value);
        }
        int secondsSlowToFill = 180;

        [JsonProperty, DefaultValue(-150)]
        public int ColdTemperature
        {
            get => coldTemperature;
            set => Ensure(ref coldTemperature, value);
        }
        int coldTemperature = -150;

        public bool IsCold => LevelSensor.Temperature < ColdTemperature;
        public Action OverflowDetected
        {
            get => overflowDetected;
            set => Ensure(ref overflowDetected, value);
        }
        Action overflowDetected;
        public Action SlowToFill
        {
            get => slowToFill;
            set => Ensure(ref slowToFill, value);
        }
        Action slowToFill;


        public enum TargetStates
        {
            /// <summary>
            /// Monitor dependent devices, activate as needed.
            /// </summary>
            Monitor,
            /// <summary>
            /// Keep reservoir filled, regardless of dependent devices.
            /// </summary>
            StayActive,
            /// <summary>
            /// Do nothing automatically.
            /// </summary>
            Standby
        }

        public enum States
        {
            /// <summary>
            /// Monitoring dependent devices for LN demand.
            /// </summary>
            Monitoring,
            /// <summary>
            /// Adding LN to the supply reservoir.
            /// </summary>
            Filling,
            /// <summary>
            /// Supply reservoir is full within limits.
            /// </summary>
            Full,
            /// <summary>
            /// Automatic operations are suspended but can
            /// be invoked manually.
            /// </summary>
            Standby
        }



        public bool StayingActive => TargetState == TargetStates.StayActive;
        public bool OverflowIsDetected =>
            overflowTemperature < ambient - Math.Abs(OverflowTrigger);
        public int SecondsFilling => (int)sw.Elapsed.TotalSeconds;
        public bool IsSlowToFill
        {
            get
            {
                if (SecondsFilling > SecondsSlowToFill * (WarmStart ? 2 : 1))
                {
                    sw.Restart();
                    return true;
                }
                return false;
            }
        }
        public bool SupplyEmpty => Liters != null && MinimumLiters > 0 && Liters.Value < MinimumLiters;

        
        IThermometer AmbientThermometer;
        double ambient => AmbientThermometer?.Temperature ?? 22.0;
        double overflowTemperature => OverflowSensor?.Temperature ?? 25.0;
        protected bool WarmStart = true;
        Stopwatch sw = new Stopwatch();
        bool full => LevelSensor.Temperature <= TargetTemperature;
        bool needed => TargetState == TargetStates.StayActive || Coldfinger.AnyNeed(this);



        public override States State
        {
            get
            {
                if (TargetState == TargetStates.Standby)
                    return States.Standby;
                if (LNSupplyValve?.IsOpened ?? false)
                    return States.Filling;
                if (TargetState == TargetStates.Monitor)
                    return States.Monitoring;
                return States.Full;
            }
        }


        /// <summary>
        /// Whether the LN valve is on or off.
        /// </summary>
        public bool IsOn => LNSupplyValve.IsOpened;
        public bool IsOff => LNSupplyValve.IsClosed;

        public OnOffState OnOffState => IsOn.ToOnOffState();

        /// <summary>
        /// Turn on (Keep the reservoir full, i.e., StayActive.)
        /// </summary>
        public bool TurnOn() { StayActive(); return true; }

        /// <summary>
        /// Turn off (Don't stay active but monitor FTCs for demand.)
        /// </summary>
        public bool TurnOff() { Monitor(); return true; }

		public bool TurnOnOff(bool on)
		{
			if (on) return  TurnOn();
			return TurnOff();
		}

		/// <summary>
		/// What to do with the hardware device when this instance is Stopped.
		/// </summary>
        //[JsonProperty]
		public StopAction StopAction { get; set; } = StopAction.TurnOff;


		public void Monitor() => ChangeState(TargetStates.Monitor);
		public void StayActive() => ChangeState(TargetStates.StayActive);
        public void Standby() => ChangeState(TargetStates.Standby);


        /// <summary>
        /// Starts a fill cycle immediately, even if it's not needed. It will still turn off normally.
        /// </summary>
        public void ForceFill()
		{
			if (!LNSupplyValve.IsOpened)
				startLN();
		}

		/// <summary>
		/// Start a fill cycle.
		/// </summary>
		void startLN()
		{
            if (InhibitLN) return;
            WarmStart = LevelSensor.Temperature > -100;
			LNSupplyValve.OpenWait();
			sw.Restart();
		}

		/// <summary>
		/// Stop the flow of LN into the reservoir.
		/// </summary>
		void stopLN()
		{
			LNSupplyValve.CloseWait();
			sw.Reset();
		}

		void ManageState()
		{
			if (LNSupplyValve.IsOpened)
			{
				if (IsSlowToFill)
					SlowToFill?.Invoke();
                if (OverflowIsDetected || full || !needed)
                {
                    stopLN();
                    if (OverflowIsDetected)
                        OverflowDetected?.Invoke();
                }
            }
			else if (!OverflowIsDetected)
			{
				if (needed && !full)
					startLN();
			}
		}


        public LNManifold()
        {
            (this as IStateManager).ManageState = ManageState;
        }

        public override string ToString()
		{
			return $"{Name}: {State} " +
				$"{LevelSensor.Temperature} °C, {LNSupplyValve.IsOpened.ToString("(Filling)", "")}" +
				Utility.IndentLines(
					$"\r\n{LevelSensor}" +
					$"\r\n{LNSupplyValve}");
		}
	}
}
