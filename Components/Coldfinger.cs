using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	public class Coldfinger : StateManager<Coldfinger.TargetStates, Coldfinger.States>, IColdfinger
    {
		#region static
		static List<Coldfinger> List { get; set; }

		/// <summary>
		/// One or more of the FTCs supplied by the given LNManifold currently need it.
		/// </summary>
		public static bool AnyNeed(LNManifold lnManifold)
		{
			if (List == null) List = CachedList<Coldfinger>();
			return List.FirstOrDefault(ftc =>
				ftc.LNManifold == lnManifold &&
				ftc.IsActivelyCooling) != null;
		}

        #endregion static

        #region HacsComponent
        [HacsConnect]
        protected virtual void Connect()
        {
            LevelSensor = Find<IThermometer>(levelSensorName);
            LNValve = Find<IValve>(lnValveName);
			AirValve = Find <IValve>(airValveName);
            LNManifold = Find<ILNManifold>(lnManifoldName);
            AirThermometer = Find<HacsComponent>(airThermometerName);
        }

        [HacsInitialize]
        protected virtual void Initialize()
        {
            EnsureState(TargetState);
        }

        #endregion HacsComponent

        [JsonProperty("LevelSensor")]
        string LevelSensorName { get => LevelSensor?.Name; set => levelSensorName = value; }
        string levelSensorName;
        /// <summary>
        /// The Thermometer (thermocouple) used by this device to detect the level of 
        /// liquid nitrogen in its reservoir.
        /// </summary>
        public IThermometer LevelSensor
		{ 
			get => levelSensor;
			set => Ensure(ref levelSensor, value, NotifyPropertyChanged);
		}
		IThermometer levelSensor;

		[JsonProperty("LNValve")]
        string LNValveName { get => LNValve?.Name; set => lnValveName = value; }
        string lnValveName;
        /// <summary>
        /// The valve that provides liquid nitrogen to this device.
        /// </summary>
		public IValve LNValve
		{
			get => lnValve;
			set => Ensure(ref lnValve, value, NotifyPropertyChanged);
		}
		IValve lnValve;

		/// <summary>
		/// The name of the LN valve operation to use for trickle flow.
		/// </summary>
		[JsonProperty("Trickle")]
        public string Trickle
		{
			get => trickle;
			set => Ensure(ref trickle, value, NotifyPropertyChanged);
		}
		string trickle;


		[JsonProperty("AirValve")]
		string AirValveName { get => AirValve?.Name; set => airValveName = value; }
		string airValveName;
        /// <summary>
        /// The valve that provides forced air to this device.
        /// </summary>
		public IValve AirValve
		{
			get => airValve;
			set => Ensure(ref airValve, value, NotifyPropertyChanged);
		}
		IValve airValve;

		[JsonProperty("LNManifold")]
		string LNManifoldName { get => LNManifold?.Name; set => lnManifoldName = value; }
		string lnManifoldName;
        /// <summary>
        /// The LNManifold where this device's LN valve is located.
        /// </summary>
		public ILNManifold LNManifold { get; set; }      // TODO make private?

        [JsonProperty("AirThermometer")]
		string AirThermometerName { get => AirThermometer?.Name; set => airThermometerName = value; }
		string airThermometerName;
        /// <summary>
        /// The device used to detect the air temperature around the FTC.
        /// </summary>
        public IHacsComponent AirThermometer
		{
			get => airThermometer;
			set => Ensure(ref airThermometer, value, NotifyPropertyChanged);
		}
		IHacsComponent airThermometer;

		/// <summary>
		/// The temperature from the level sensor that the FTC uses to conclude
		/// that its liquid nitrogen reservoir is full; usually a few degrees warmer
		/// than -195.8 °C.
		/// </summary>
		[JsonProperty, DefaultValue(-192)]
		public int FrozenTemperature
		{
			get => frozenTemperature;
			set => Ensure(ref frozenTemperature, value);
		}
		int frozenTemperature = -192;

        /// <summary>
        /// In Freeze mode, the FTC will request liquid nitrogen
        /// if its Temperature is this much warmer than FrozenTemperature.
        /// </summary>
		[JsonProperty, DefaultValue(5)]
		public int FreezeTrigger
		{
			get => freezeTrigger;
			set => Ensure(ref freezeTrigger, value);
		}
		int freezeTrigger = 5;


		/// <summary>
		/// In Raise mode, if the LNValve doesn't have a Trickle operation, this device 
		/// will request liquid nitrogen if its Temperature is this much warmer than 
		/// FrozenTemperature.
		/// </summary>
		[JsonProperty, DefaultValue(2)]
		public int RaiseTrigger
		{
			get => raiseTrigger;
			set => Ensure(ref raiseTrigger, value);
		}
		int raiseTrigger = 2;


		/// <summary>
		/// Whenever liquid nitrogen is flowing, the FTC moves the 
		/// LNValve (close-open cycle) every this many seconds, to prevent 
		/// the valve from sticking open.
		/// </summary>
		[JsonProperty, DefaultValue(60)]
		public int MaximumSecondsLNFlowing
		{
			get => maximumSecondsLNFlowing;
			set => Ensure(ref maximumSecondsLNFlowing, value);
		}
		int maximumSecondsLNFlowing = 60;

		/// <summary>
		/// How many seconds to wait for temperature equilibrium after the Raise state 
		/// is reached.
		/// </summary>
		[JsonProperty, DefaultValue(15)]
		public int SecondsToWaitAfterRaised
		{
			get => secondsToWaitAfterRaised;
			set => Ensure(ref secondsToWaitAfterRaised, value);
		}
		int secondsToWaitAfterRaised = 15;


		/// <summary>
		/// The FTC is "near" air temperature if it is within this 
		/// many degrees of AirTemperature.
		/// </summary>
		[JsonProperty, DefaultValue(7.0)]
		public double NearAirTemperature
		{
			get => nearAirTemperature;
			set => Ensure(ref nearAirTemperature, value);
		}
		double nearAirTemperature = 7.0;

		/// <summary>
		/// The available target states for an FTColdfinger. The FTC
		/// is controlled by setting TargetState to one of these values.
		/// </summary>
		public enum TargetStates
		{
			/// <summary>
			/// Turn off active warming and cooling.
			/// </summary>
			Standby,
			/// <summary>
			/// Warm coldfinger until thawed, then switch to Standby.
			/// </summary>
			Thaw,
			/// <summary>
			/// Immerse the coldfinger in LN, and maintain a minimal level of liquid there.
			/// </summary>
			Freeze,
			/// <summary>
			/// Freeze if needed and raise the LN, to the level of a trickling overflow if possible.
			/// </summary>
			Raise
		}

        /// <summary>
        /// The possible states of an FTColdfinger. The FTC is always
        /// in one of these states.
        /// </summary>
		public enum States
		{
			/// <summary>
			/// Coldfinger temperature is not being actively controlled.
			/// </summary>
			Standby,
			/// <summary>
			/// Warming coldfinger to ambient temperature.
			/// </summary>
			Thawing,
			/// <summary>
			/// Cooling the coldfinger using liquid nitrogen.
			/// </summary>
			Freezing,
			/// <summary>
			/// Maintaining a minimal level of liquid nitrogen on the coldfinger.
			/// </summary>
			Frozen,
			/// <summary>
			/// Raising the LN level on the coldfinger.
			/// </summary>
			Raising,
			/// <summary>
			/// Maintaining a maximum level of liquid nitrogen, with a trickling overflow if possible.
			/// </summary>
			Raised
		}

        /// <summary>
        /// The FTC is currently warming the coldfinger with forced air.
        /// </summary>
		public bool Thawing => State == States.Thawing;

		/// <summary>
		/// The FTC is at least as cold as FrozenTemperature and
		/// the TargetState is such as to maintain that condition.
		/// </summary>
		public bool Frozen =>
			Temperature <= FrozenTemperature &&
			State != States.Standby &&
			State != States.Thawing &&
			State != States.Freezing;

        /// <summary>
        /// The FTC is currently maintaining a maximum level of liquid
        /// nitrogen, with a trickling overflow if possible.
        /// </summary>
		public bool Raised => State == States.Raised;

		/// <summary>
		/// The coldfinger temperature is within a specified range of air temperature.
		/// </summary>
		public bool IsNearAirTemperature =>
			Math.Abs(Temperature - AirTemperature) <= Math.Abs(NearAirTemperature);

        /// <summary>
        /// The FTC is actively working to cool the coldfinger.
        /// </summary>
		public bool IsActivelyCooling =>
			TargetState == TargetStates.Freeze ||
			TargetState == TargetStates.Raise;

		/// <summary>
		/// Whether the coldfinger temperature is warmer than a specified amount (NearAirTemperature)
		/// below air temperature.
		/// </summary>
		public bool Thawed =>
			Temperature > AirTemperature - NearAirTemperature;

        /// <summary>
        /// The temperature (°C) reported by the level sensor.
        /// </summary>
		public double Temperature => LevelSensor.Temperature;

        /// <summary>
        /// The temperature (°C) of the air around the FTC.
        /// </summary>
		public double AirTemperature
		{
			get
			{
				if (AirThermometer is ITemperature t)
					return t.Temperature;
				if (AirThermometer is IThermometer th)
					return th.Temperature;
				if (AirThermometer is Meter m)
					return m;
				if (Find<Chamber>("Ambient") is Chamber c)
					return c.Temperature;
				return 22;      // room temperature?
			}
		}



		/// <summary>
		/// Whether the LN valve has a Trickle operation;
		/// </summary>
		bool trickleSupported => LNValve.Operations.Contains(Trickle);
		Stopwatch valveOpenStopwatch = new Stopwatch();
		double coldestLNSensorTemperature;
		public double Target { get; protected set; }

        /// <summary>
        /// The present state of the FTC.
        /// </summary>
		public override States State
		{
			get
			{
				if (TargetState == TargetStates.Standby)
					return States.Standby;
				if (TargetState == TargetStates.Thaw)
					return States.Thawing;

				//else TargetState is Freeze or Raise

				if (Temperature >= FrozenTemperature + FreezeTrigger)
					return States.Freezing;

				if (TargetState == TargetStates.Freeze)
					return States.Frozen;

				// Target state is Raise
				if (Temperature <= FrozenTemperature + RaiseTrigger)
					return States.Raised;
				else
					return States.Raising;
			}
		}

        /// <summary>
        /// Changes the TargetState (operating mode) of the FTC.
        /// Does nothing if the state parameter matches the present TargetState.
        /// </summary>
        /// <param name="state"></param>
		public override void ChangeState(TargetStates state)
		{
			if (TargetState != state)
			{
				EnsureState(state);
				StateStopwatch.Restart();
			}
		}

        /// <summary>
        /// Puts the FTC in Standby (turn off active cooling and warming).
        /// </summary>
		public void Standby()
		{
			LNOff();
			AirOff();
			ChangeState(TargetStates.Standby);
		}

        /// <summary>
        /// Fill and maintain a minimal level of liquid nitrogen in the reservoir.
        /// </summary>
		public void Freeze() => ChangeState(TargetStates.Freeze);

        /// <summary>
        /// Reach and maintain a maximum level of liquid nitrogen, 
        /// with a trickling overflow if possible.
        /// </summary>
        public void Raise() => ChangeState(TargetStates.Raise);

        /// <summary>
        /// Warm the coldfinger with forced air.
        /// </summary>
		public void Thaw() => ChangeState(TargetStates.Thaw);

        /// <summary>
        /// Ensures the desired TargetState is in effect.
        /// </summary>
        /// <param name="state">the desired TargetState</param>
		public void EnsureState(TargetStates state)
		{
			switch (state)
			{
				case TargetStates.Standby:
					break;
				case TargetStates.Thaw:
					LNOff();
					break;
				case TargetStates.Freeze:
				case TargetStates.Raise:
					Target = FrozenTemperature;
					coldestLNSensorTemperature = Target + 3;
					break;
				default:
					break;
			}

			base.ChangeState(state);
		}


		// whether overflow trickling is presently preferred
		bool trickling =>
			trickleSupported &&
			TargetState == TargetStates.Raise;

		int trigger =>
			TargetState == TargetStates.Freeze ? FreezeTrigger : RaiseTrigger;

        /// <summary>
        /// Controls the LNValve as needed to maintain the desired LN level in the reservoir.
        /// </summary>
		void manageLNLevel()
		{
			if (valveOpenStopwatch.IsRunning)
			{
				// Track the coldest temperature observed since the valve was opened
				if (Temperature < coldestLNSensorTemperature)
					coldestLNSensorTemperature = Temperature;

				if (valveOpenStopwatch.Elapsed.TotalSeconds > MaximumSecondsLNFlowing)
				{
					LNOff();			// cycle the valve periodically to avoid sticking
					Target = FrozenTemperature;	// reset Target to default on timeout
				}
				else if (Temperature <= Target && !trickling)
				{
					LNOff();
				}
			}
			else
			{
				if (Temperature > Target + trigger || trickling)
				{
					// adjust Target to 2 degrees warmer than coldest observed temperature
					Target = coldestLNSensorTemperature + 2;
					// but no warmer than 3 degrees over the default
					if (Target > FrozenTemperature + 3) Target = FrozenTemperature + 3;
					LNOn();
				}
			}
		}

        /// <summary>
        /// Starts the flow of liquid nitrogen to the reservoir.
        /// </summary>
		void LNOn()
		{
			coldestLNSensorTemperature = Temperature;       // reset tracking
			if (!LNManifold.IsCold)
				return;
			if (trickling && Temperature < Target + RaiseTrigger)
				LNValve.DoOperation(Trickle);
			else
				LNValve.Open();
			valveOpenStopwatch.Restart();
		}

        /// <summary>
        /// Stop the liquid nitrogen flow.
        /// </summary>
		void LNOff()
		{
			LNValve.WaitForIdle();
			if (!LNValve.IsClosed) LNValve.CloseWait();
			valveOpenStopwatch.Reset();
		}

        /// <summary>
        /// Blow air through the reservoir, to eject liquid 
		/// nitrogen and warm the chamber.
        /// </summary>
		void AirOn()
		{
			if (!AirValve.IsOpened)
                AirValve.OpenWait();
            else if (Temperature > Target)
				AirValve.CloseWait();
		}

        /// <summary>
        /// Stop the air flow.
        /// </summary>
		void AirOff()
		{
			AirValve.CloseWait();
		}



		void ManageState()
		{
			if (!Connected || Hacs.Stopping) return;
			switch (TargetState)
			{
				case TargetStates.Standby:
					Target = AirTemperature;
					break;
				case TargetStates.Thaw:
					Target = AirTemperature - NearAirTemperature;
					AirOn();
					if (!AirValve.IsOpened) Standby();
					break;
				case TargetStates.Freeze:
				case TargetStates.Raise:
					AirOff();
					manageLNLevel();
					break;
				default:
					break;
			}

			if (State == States.Freezing && StateTimer.Elapsed.TotalMinutes > 10) // MaximumFreezeMinutes)
			{
				SlowToFreeze?.Invoke();
				StateTimer.Restart();
			}
        }
		Stopwatch StateTimer = new Stopwatch();
		public Action SlowToFreeze { get; set; }

		/// <summary>
		/// Freezes the coldfinger and waits for it to reach the Frozen state.
		/// </summary>
		public void FreezeWait()
		{
			var st = StepTracker.Default;
			Freeze();
			st?.Start($"Wait for {Name} < {FrozenTemperature + FreezeTrigger} °C");
			while (State != States.Frozen) Wait();
			st?.End();
		}


		/// <summary>
		/// Raises the LN level, and once it has reached it, waits a few seconds for equilibrium.
		/// </summary>
		public void RaiseLN()
		{
			var step = StepTracker.Default;
			Raise();
			step?.Start($"Wait for {Name} LN level to raise");
			while (State != States.Raised) Wait();
			step?.End();

			step?.Start($"Wait {SecondsToWaitAfterRaised} seconds with LN raised");
			Thread.Sleep(SecondsToWaitAfterRaised * 1000);
			step?.End();
		}


		/// <summary>
		/// Raises the LN level, and once it has reached it, waits a few seconds for equilibrium.
		/// This process is shortcut if the FTC supports an overflow-trickle mode.
		/// </summary>
		public void WaitForLNpeak()
		{
			var st = StepTracker.Default;
			Raise();

			// Trickle-capable FTC's maintain the LN peak condition
			if (trickling && Temperature < Target + RaiseTrigger)
			{
				while (!LNValve.IsOpened) Wait();
				return;
			}

			st?.Start($"Wait until {Name} LN level is at max");
			while (!LNValve.IsOpened) Wait();
			while (State != States.Raised || !LNValve.IsClosed) Wait();
			st?.End();
			st?.Start("Wait for 5 seconds for equilibrium");
			Thread.Sleep(5000);     // TODO Could this cause a ShuttingDown delay?
			st?.End();
		}


		public Coldfinger()
		{
			(this as IStateManager).ManageState = ManageState;
		}

		public override string ToString()
		{
            StringBuilder sb = new StringBuilder($"{Name}: {State}, {Temperature:0.###} °C");
			if (State != States.Standby)
				sb.Append($", Target = {Target:0.###} °C");
            StringBuilder sb2 = new StringBuilder();
            sb2.Append($"\r\n{LevelSensor}");
            sb2.Append($"\r\n{LNValve}");
            sb2.Append($"\r\n{AirValve}");
            sb.Append(Utility.IndentLines(sb2.ToString()));
            return sb.ToString();
		}
	}
}
