using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Utilities;

namespace HACS.Components
{

	// TODO: make this a StateManager : IVacuumSystem and IVacuumSystem : IManometer
	/// <summary>
	/// A high-vacuum system with a turbomolecular pump and a low-vacuum 
	/// roughing pump that is also used as the backing pump for the turbo.
	/// </summary>
	public class VacuumSystem : HacsComponent, IVacuumSystem
	{
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			TurboPump = Find<IOnOff>(turboPumpName);
			RoughingPump = Find<IOnOff>(roughingPumpName);
			Manometer = Find<IManometer>(manometerName);
			ForelineManometer = Find<IManometer>(forelineManometerName);
			HighVacuumValve = Find<IValve>(highVacuumValveName);
			LowVacuumValve = Find<IValve>(lowVacuumValveName);
			BackingValve = Find<IValve>(backingValveName);
			RoughingValve = Find<IValve>(roughingValveName);
			VacuumManifold = Find<ISection>(vacuumManifoldName);
		}

		[HacsStart]
		protected virtual void Start()
		{
			stateThread = new Thread(stateLoop) { Name = $"{Name} ManageState", IsBackground = true };
			stateThread.Start();
			StateStopwatch.Restart();
		}

		[HacsStop]
		protected virtual void Stop()
		{
			if (AutoManometer)
				DisableManometer();
			TargetState = TargetStateCode.Stop;
            Stopping = true;
            stateSignal.Set();
            stoppedSignal.WaitOne();
        }

        ManualResetEvent stoppedSignal = new ManualResetEvent(true);

		// TODO this should be a protected override, no?
        public new bool Stopped => stoppedSignal.WaitOne(0);
        protected bool Stopping { get; set; }

		#endregion HacsComponent

		[JsonProperty("TurboPump")]
		string TurboPumpName { get => TurboPump?.Name; set => turboPumpName = value; }
		string turboPumpName;
		/// <summary>
		/// The current vacuum system pressure (Torr).
		/// </summary>
		public IOnOff TurboPump
		{
			get => turboPump;
			set => Ensure(ref turboPump, value, NotifyPropertyChanged);
		}
		IOnOff turboPump;
		bool turboPumpIsOn => !TurboPump?.IsOff ?? true;

		[JsonProperty("RoughingPump")]
		string RoughingPumpName { get => RoughingPump?.Name; set => roughingPumpName = value; }
		string roughingPumpName;
		/// <summary>
		/// The current vacuum system pressure (Torr).
		/// </summary>
		public IOnOff RoughingPump
		{
			get => roughingPump;
			set => Ensure(ref roughingPump, value, NotifyPropertyChanged);
		}
		IOnOff roughingPump;
		bool roughingPumpIsOn => !RoughingPump?.IsOff ?? true;

		[JsonProperty("Manometer")]
		string ManometerName { get => Manometer?.Name; set => manometerName = value; }
		string manometerName;
		/// <summary>
		/// The current vacuum system pressure (Torr).
		/// </summary>
		public IManometer Manometer
		{
			get => manometer;
			set => Ensure(ref manometer, value, NotifyPropertyChanged);
		}
		IManometer manometer;
		public double Pressure => Manometer.Pressure;

		[JsonProperty("ForelineManometer")]
		string ForelineManometerName { get => ForelineManometer?.Name; set => forelineManometerName = value; }
		string forelineManometerName;
		public IManometer ForelineManometer
		{
			get => forelineManometer;
			set => Ensure(ref forelineManometer, value, NotifyPropertyChanged);
		}
		IManometer forelineManometer;

		[JsonProperty("HighVacuumValve")]
		string HighVacuumValveName { get => HighVacuumValve?.Name; set => highVacuumValveName = value; }
		string highVacuumValveName;
		public IValve HighVacuumValve
		{
			get => highVacuumValve;
			set => Ensure(ref highVacuumValve, value, NotifyPropertyChanged);
		}
		IValve highVacuumValve;

		[JsonProperty("LowVacuumValve")]
		string LowVacuumValveName { get => LowVacuumValve?.Name; set => lowVacuumValveName = value; }
		string lowVacuumValveName;
		public IValve LowVacuumValve
		{
			get => lowVacuumValve;
			set => Ensure(ref lowVacuumValve, value, NotifyPropertyChanged);
		}
		IValve lowVacuumValve;

		[JsonProperty("BackingValve")]
		string BackingValveName { get => BackingValve?.Name; set => backingValveName = value; }
		string backingValveName;
		public IValve BackingValve
		{
			get => backingValve;
			set => Ensure(ref backingValve, value, NotifyPropertyChanged);
		}
		IValve backingValve;

		[JsonProperty("RoughingValve")]
		string RoughingValveName { get => RoughingValve?.Name; set => roughingValveName = value; }
		string roughingValveName;
		public IValve RoughingValve
		{
			get => roughingValve;
			set => Ensure(ref roughingValve, value, NotifyPropertyChanged);
		}
		IValve roughingValve;

		[JsonProperty("VacuumManifold")]
		string VacuumManifoldName { get => VacuumManifold?.Name ; set => vacuumManifoldName = value; }
		string vacuumManifoldName;
		public ISection VacuumManifold
		{
			get => vacuumManifold;
			set => Ensure(ref vacuumManifold, value);
		}
		ISection vacuumManifold;

		protected enum TargetStateCode { Standby, Isolate, Rough, Evacuate, Stop }
		[JsonProperty("State")]
		protected TargetStateCode TargetState
		{
			get => targetState;
			set => Ensure(ref targetState, value, NotifyConfigChanged);
		}
		TargetStateCode targetState;

		// pTarget can be altered dynamically while WaitForPressure is active; that's its intended use.
		[JsonProperty]
		public double TargetPressure
		{
			get => targetPressure;
			set => Ensure(ref targetPressure, value, NotifyConfigChanged);
		}
		double targetPressure;

		[JsonProperty]
		public double BaselinePressure
		{
			get => baselinePressure;
			set => Ensure(ref baselinePressure, value, NotifyConfigChanged);
		}
		double baselinePressure;       // typical maximum pressure for auto-zeroing pressure gauges

		[JsonProperty]
		public double GoodBackingPressure
		{
			get => goodBackingPressure;
			set => Ensure(ref goodBackingPressure, value, NotifyConfigChanged);
		}
		double goodBackingPressure;  // open or close the turbopump backing valve when pForeline is less than this

		[JsonProperty]
		public double HighVacuumPreferredPressure
		{
			get => highVacuumPreferredPressure;
			set => Ensure(ref highVacuumPreferredPressure, value, NotifyConfigChanged);
		}
		double highVacuumPreferredPressure;   // HV is preferred below this pressure

		[JsonProperty]
		public double HighVacuumRequiredPressure
		{
			get => highVacuumRequiredPressure;
			set => Ensure(ref highVacuumRequiredPressure, value, NotifyConfigChanged);
		}
		double highVacuumRequiredPressure;    // do not use LV below this pressure

		[JsonProperty]
		public double LowVacuumRequiredPressure
		{
			get => lowVacuumRequiredPressure;
			set => Ensure(ref lowVacuumRequiredPressure, value, NotifyConfigChanged);
		}
		double lowVacuumRequiredPressure;    // do not use HV above this pressure

		/// <summary>
		/// A StepTracker to receive ongoing process state messages.
		/// </summary>
		public StepTracker ProcessStep
		{
			get => processStep ?? StepTracker.Default;
			set => Ensure(ref processStep, value);
		}
		StepTracker processStep;

		public enum StateCode { Unknown, Isolated, Roughing, RoughingForeline, HighVacuum, Stopped }

		public StateCode State
		{
			get
			{
				if (Stopped)
					return StateCode.Stopped;
				if (HighVacuumValve.IsClosed && 
						LowVacuumValve.IsClosed && 
						(turboPumpIsOn ? BackingValve.IsOpened : BackingValve.IsClosed) && 
						(roughingPumpIsOn ? (RoughingValve?.IsOpened ?? true) : (RoughingValve?.IsClosed ?? true)))
					return StateCode.Isolated;     // and backing
				if (HighVacuumValve.IsClosed && LowVacuumValve.IsOpened && BackingValve.IsClosed && (RoughingValve?.IsOpened ?? true))
					return StateCode.Roughing;
				if (HighVacuumValve.IsClosed && LowVacuumValve.IsClosed && BackingValve.IsClosed && (RoughingValve?.IsOpened ?? true))
					return StateCode.RoughingForeline;
				if (HighVacuumValve.IsOpened && LowVacuumValve.IsClosed && BackingValve.IsOpened && (RoughingValve?.IsOpened ?? true))
					return StateCode.HighVacuum;
				return StateCode.Unknown;
			}
		}

		[JsonProperty]
		protected Stopwatch StateStopwatch { get; private set; } = new Stopwatch();
		public long MillisecondsInState => StateStopwatch.ElapsedMilliseconds;

		protected Stopwatch BaselineTimer { get; private set; } = new Stopwatch();
		public TimeSpan TimeAtBaseline => BaselineTimer.Elapsed;


		public override string ToString()
		{
			return $"{Name} ({TargetState}): {State}" + Utility.IndentLines($"\r\n{Manometer}");
		}

		#region Class Interface Methods -- Control the device using these functions

		/// <summary>
		/// Isolate the VacuumManifold.
		/// </summary>
		public void IsolateManifold() => VacuumManifold?.Isolate();

		/// <summary>
		/// IsolateManifold() but skip the specified valves.
		/// </summary>
		/// <param name="section">Skip these valves</param>
		public void IsolateExcept(IEnumerable<IValve> valves) =>
			VacuumManifold?.Isolation?.CloseExcept(valves);

		/// <summary>
		///  Disables all automatic control of VacuumSystem.
		/// </summary>
		public void Standby()
		{
			if (State == StateCode.Stopped) Start();
			TargetState = TargetStateCode.Standby;
		}

		/// <summary>
		/// Isolates the pumps from the vacuum manifold.
		/// Returns only after isolation is complete.
		/// </summary>
		public void Isolate() => Isolate(true);

		/// <summary>
		/// Isolates the pumps from the vacuum manifold.
		/// </summary>
		/// <param name="waitForState">If true, returns only after isolation is complete.</param>
		public void Isolate(bool waitForState)
		{
            if (State == StateCode.Stopped) Start();
            TargetState = TargetStateCode.Isolate;
			while (State != StateCode.Isolated && waitForState)
				Wait();
		}

		/// <summary>
		/// Requests Evacuation mode. Initiates pumping on the vacuum manifold and attempts to bring it to high vacuum.
		/// </summary>
		public void Evacuate()
		{
            if (State == StateCode.Stopped) Start();
            TargetState = TargetStateCode.Evacuate;
		}

		/// <summary>
		/// Requests Evacuate mode. Returns when the target pressure is reached.
		/// </summary>
		/// <param name="pressure">Target pressure</param>
		public void Evacuate(double pressure)
		{
            Evacuate();
			while (State < StateCode.Roughing)
				Wait();
            WaitForPressure(pressure);
		}

        /// <summary>
        /// Waits 3 seconds, then until the given pressure is reached.
        /// Use 0 to wait for baseline, &lt;0 to just wait 3 seconds.
        /// </summary>
        /// <param name="pressure">Use 0 to wait for baseline, &lt;0 to just wait 3 seconds.</param>
        public void WaitForPressure(double pressure)
        {

            Thread.Sleep(3000);                 // always wait at least 3 seconds
            if (pressure < 0) return;   // don't wait for a specific pressure
            if (pressure == 0) pressure = BaselinePressure;
            TargetPressure = pressure;

            ProcessStep?.Start($"Wait for p_VM < {TargetPressure:0.0e0} Torr");
            while (Pressure > TargetPressure)
            {
                if (TargetPressure != pressure)
                {
                    pressure = TargetPressure;
                    if (ProcessStep != null)
                        ProcessStep.CurrentStep.Description = $"Wait for p_VM < {TargetPressure:0.0e0} Torr";
                }
                Thread.Sleep(35);
            }
            ProcessStep?.End();
        }

		/// <summary>
		/// Wait until the TimeAtBaseline timer reaches at least 10 seconds
		/// </summary>
		public virtual void WaitForStableBaselinePressure()
        {
			while (TimeAtBaseline.TotalSeconds < 10) 
				Wait();
		}

		/// <summary>
		/// Request to evacuate vacuum manifold using low-vacuum pump only.
		/// Vacuum Manifold will be roughed and isolated alternately
		/// to maintain VM pressure between pressure_HV_required
		/// and pressure_LV_required
		/// </summary>
		public void Rough()
		{
            if (State == StateCode.Stopped) Start();
            TargetState = TargetStateCode.Rough;
		}

        protected void SetTargetState(TargetStateCode targetState)
        {
            switch (targetState)
            {
                case TargetStateCode.Standby:
                    Standby();
                    break;
                case TargetStateCode.Isolate:
                    Isolate(false);
                    break;
                case TargetStateCode.Rough:
                    Rough();
                    break;
                case TargetStateCode.Evacuate:
                    Evacuate();
                    break;
                case TargetStateCode.Stop:
                    Stop();
                    break;
                default:
                    break;
            }
        }

		#endregion

		#region State Manager

		Thread stateThread;
		AutoResetEvent stateSignal = new AutoResetEvent(false);

		/// <summary>
		/// Maximum time (milliseconds) for idle state manager to wait before doing something.
		/// </summary>
		[JsonProperty, DefaultValue(50)]
		protected int IdleTimeout { get; set; } = 50;

        bool valvesReady => 
			(HighVacuumValve?.Ready ?? true) && 
			(LowVacuumValve?.Ready ?? true) && 
			(BackingValve?.Ready ?? true) &&
			(RoughingValve?.Ready ?? true);

		// TODO: Need to monitor how long Backing has been closed, and 
		// to periodically empty the turbo pump exhaust, or shut down the
		// turbo pump with an alert.
		void stateLoop()
		{
            stoppedSignal.Reset();
			try
			{
				while (!Stopping)
				{
					var idleTimeout = IdleTimeout;

                    if (valvesReady)
                    {
                        switch (TargetState)
                        {
                            case TargetStateCode.Isolate:
                                if (State != StateCode.Isolated)
                                {
									if (AutoManometer)
										DisableManometer();
                                    HighVacuumValve.Close();
                                    LowVacuumValve.CloseWait();

									// TODO need a timeout check somewhere in here with alert/failure.
									// The turbo pump can be damaged if it is runs without a backing
									// pump for too long.

									if (!roughingPumpIsOn)
										RoughingValve?.CloseWait();
									else
									{
										RoughingValve?.Open();

										if (TurboPump?.IsOn ?? true)
										{
											while (ForelineManometer.Pressure > GoodBackingPressure)
												Wait();
											BackingValve.OpenWait();
										}
										else
											BackingValve.CloseWait();
									}
                                    StateStopwatch.Restart();
									NotifyPropertyChanged("State");
                                }
                                break;
                            case TargetStateCode.Rough:
                                if (RoughAsNeeded())
                                {
                                    StateStopwatch.Restart();
									NotifyPropertyChanged("State");
									idleTimeout = 0;        // keep working until no action taken
                                }
                                break;
                            case TargetStateCode.Evacuate:
                                if (EvacuateOrRoughAsNeeded())
                                {
                                    StateStopwatch.Restart();
									NotifyPropertyChanged("State");
									idleTimeout = 0;        // keep working until no action taken
                                }
                                break;
                            case TargetStateCode.Standby:
                                break;
                            default:
                                break;
                        }
                    }

					ManageIonGauge();
					MonitorBaseline();

					stateSignal.WaitOne(idleTimeout);
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
            stoppedSignal.Set();
		}

		/// <summary>
		/// The Vacuum system controls whether Manometer is on or off.
		/// </summary>
		public bool AutoManometer
		{
			get => autoManometer;
			set
			{
				if (Ensure(ref autoManometer, value))
				{
					if (Manometer is IManualMode m && m.ManualMode == value)
						m.ManualMode = !value;

					ManageIonGauge();
				}
			}
		}
		bool autoManometer = true;

		public void DisableManometer()
		{
			if (Manometer is ISwitchedManometer m && !m.IsOff)
				m.TurnOff();
		}

		public void EnableManometer()
		{
			if (Manometer is IDualManometer m)
			{
				if (m.ManualMode)
					m.ManualMode = false;
			}
			else if (Manometer is ISwitchedManometer m2)
			{
				if (!m2.IsOn)
					m2.TurnOn();
			}
		}

		protected void ManageIonGauge()
		{
			if (!AutoManometer) return;

			var ionOk = HighVacuumValve.IsOpened && BackingValve.IsOpened;
			if (!ionOk)
				DisableManometer();
			else
				EnableManometer();
		}

		protected void MonitorBaseline()
		{
			// monitor time at "baseline" (minimal pressure and steady foreline pressure)
			if (State == StateCode.HighVacuum &&
				Pressure <= BaselinePressure &&
				ForelineManometer.IsStable
			)
			{
				if (!BaselineTimer.IsRunning)
					BaselineTimer.Restart();
			}
			else if (BaselineTimer.IsRunning)
				BaselineTimer.Reset();
		}

		protected bool EvacuateOrRoughAsNeeded()
		{
            if (State == StateCode.HighVacuum && Pressure < LowVacuumRequiredPressure ||
				State == StateCode.Roughing && Pressure > HighVacuumRequiredPressure)
                return false; // no action taken

			RoughingValve?.OpenWait();

            if (Pressure < HighVacuumPreferredPressure)               // ok to go to HV
            {
                if (LowVacuumValve.IsOpened)
                    LowVacuumValve.CloseWait();
                if (ForelineManometer.Pressure < GoodBackingPressure)
                    BackingValve.OpenWait();
                if (BackingValve.IsOpened)
                    HighVacuumValve.OpenWait();
            }
            else            // need LV
            {
                if (HighVacuumValve.IsOpened)
                    HighVacuumValve.CloseWait();
                if (ForelineManometer.Pressure < GoodBackingPressure)
                    BackingValve.CloseWait();
                if (BackingValve.IsClosed)
                    LowVacuumValve.OpenWait();
            }
            return true;    // even though no valve may have moved; might be waiting on pForeline for backing
        }

        protected bool RoughAsNeeded()
		{
			bool stateChanged = false;
			if (!HighVacuumValve.IsClosed)
				HighVacuumValve.CloseWait();

			if (LowVacuumValve.IsClosed)	// isolated
			{
				if (Pressure < LowVacuumRequiredPressure)
				{
					// back TurboPump whenever possible
					if (BackingValve.IsClosed && ForelineManometer.Pressure < GoodBackingPressure)
					{
						BackingValve.OpenWait();
						stateChanged = true;	// backing
					}
				}
				else    // need to rough
				{
					if (BackingValve.IsOpened)
						BackingValve.Close();
					LowVacuumValve.OpenWait();
					stateChanged = true;    // roughing
				}
			}
			else	// currently roughing
			{
				if (Pressure <= HighVacuumRequiredPressure)
				{
					LowVacuumValve.CloseWait();
					stateChanged = true;    // isolated
				}
			}
			return stateChanged;
		}

		/// <summary>
		/// sleep for the given number of milliseconds
		/// </summary>
		/// <param name="milliseconds"></param>
		protected void Wait(int milliseconds) { Thread.Sleep(milliseconds); }

		/// <summary>
		/// wait for 35 milliseconds
		/// </summary>
		protected void Wait() { Wait(35); }

		#endregion


		/// <summary>
		/// These event handlers are invoked whenever the desired device
		/// configuration changes. EventArgs.PropertyName is usually the 
		/// name of an updated configuration property, but it may be null,
		/// or a generalized indication of the reason the event was raised, 
		/// such as &quot;{Init}&quot;.
		/// </summary>
		public virtual PropertyChangedEventHandler ConfigChanged { get; set; }

		/// <summary>
		/// Raises the ConfigChanged event.
		/// </summary>
		protected virtual void NotifyConfigChanged(object sender, PropertyChangedEventArgs e)
		{
			stateSignal.Set();
			ConfigChanged?.Invoke(sender, e);
		}
	}
}


