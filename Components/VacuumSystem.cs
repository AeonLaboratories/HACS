using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using System.Xml.Serialization;
using HACS.Core;
using System.Linq;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
	// derive from VSPressure ?
	public class VacuumSystem : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<VacuumSystem> List = new List<VacuumSystem>();
		public static new VacuumSystem Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
		{
			if (Pressure != null) Pressure.StateChanged += deviceStateChanged;
		}

		protected void Start()
		{
			stateThread = new Thread(ManageState) { Name = $"{Name} ManageState", IsBackground = true };
			stateThread.Start();
			StateStopwatch.Restart();
		}

		protected void Stop()
		{
			IGDisable();
			TargetState = TargetStates.Stop;
			stateSignal.Set();
			while (State != States.Stopped)
				Wait();
		}

		public VacuumSystem()
		{
			List.Add(this);
			OnConnect += Connect;
			OnStart += Start;
			OnStop += Stop;
		}

		#endregion Component Implementation
		
		[JsonProperty]
		public HacsComponent<VSPressure> VSPressureRef { get; set; }
        /// <summary>
        /// The current vacuum system pressure (Torr).
        /// </summary>
        public VSPressure Pressure => VSPressureRef?.Component;

		[JsonProperty]
		public HacsComponent<Meter> ForelinePressureMeterRef { get; set; }
		public Meter pForeline => ForelinePressureMeterRef?.Component;

		[JsonProperty]
		public HacsComponent<HacsComponent> v_HighVacuumRef { get; set; }
        public IValve v_HighVacuum => v_HighVacuumRef?.Component as IValve;

		[JsonProperty]
		public HacsComponent<HacsComponent> v_LowVacuumRef { get; set; }
        public IValve v_LowVacuum => v_LowVacuumRef?.Component as IValve;

		[JsonProperty]
		public HacsComponent<HacsComponent> v_BackingRef { get; set; }
        public IValve v_Backing => v_BackingRef?.Component as IValve;

		[JsonProperty]
		public HacsComponent<HacsComponent> v_RoughingRef { get; set; }
        public IValve v_Roughing => v_RoughingRef?.Component as IValve;

		// Note: The last Section.Isolation.Valve always isolates the Section from the
		// Vacuum Manifold.
		[XmlArray("ManifoldSections")]
		[XmlArrayItem("SectionRef")]
		[JsonProperty("ManifoldSections")]
		public List<HacsComponent<Section>> SectionRefs { get; set; }
        [XmlIgnore] public List<Section> ManifoldSections => SectionRefs?.Select(sr => sr.Component).ToList();

		// ion gauge operating mode
		[JsonProperty]//, DefaultValue(true)]
		public bool IonGaugeAuto { get; set; } = true;

		[XmlType(AnonymousType = true)]
		public enum TargetStates { Standby, Isolate, Rough, Evacuate, Stop }
		[JsonProperty]
		public TargetStates TargetState { get; set; }

		// pTarget can be altered dynamically while WaitForPressure is active; that's its intended use.
		[JsonProperty]
		public double pTarget { get; set; }

		[JsonProperty]
		public double pressure_baseline { get; set; }       // typical maximum pressure for auto-zeroing pressure gauges
		[JsonProperty]
		public double pressure_good_backing { get; set; }   // open or close the turbopump backing valve when pForeline is less than this
		[JsonProperty]
		public double pressure_HV_preferred { get; set; }   // HV is preferred below this pressure
		[JsonProperty]
		public double pressure_HV_required { get; set; }    // do not use LV below this pressure
		[JsonProperty]
		public double pressure_LV_required { get; set; }    // do not use HV above this pressure

		[XmlIgnore] public StepTracker ProcessStep;

		[XmlType(AnonymousType = true)]
		public enum States { Unknown, Isolated, Roughing, RoughingForeline, HighVacuum, Stopped }

		public States State
		{
			get
			{
				if (stateThread == null || !stateThread.IsAlive)
					return States.Stopped;
				if (v_HighVacuum.IsClosed && v_LowVacuum.IsClosed && v_Backing.IsOpened && (v_Roughing?.IsOpened ?? true))
					return States.Isolated;     // and backing
				if (v_HighVacuum.IsClosed && v_LowVacuum.IsOpened && v_Backing.IsClosed && (v_Roughing?.IsOpened ?? true))
					return States.Roughing;
				if (v_HighVacuum.IsClosed && v_LowVacuum.IsClosed && v_Backing.IsClosed && (v_Roughing?.IsOpened ?? true))
					return States.RoughingForeline;
				if (v_HighVacuum.IsOpened && v_LowVacuum.IsClosed && v_Backing.IsOpened && (v_Roughing?.IsOpened ?? true))
					return States.HighVacuum;
				return States.Unknown;
			}
		}

		[JsonProperty]
		public Stopwatch StateStopwatch { get; set; } = new Stopwatch();
		public long MillisecondsInState => StateStopwatch.ElapsedMilliseconds;

		[XmlIgnore] public Stopwatch BaselineTimer = new Stopwatch();

		public override string ToString()
		{
			return $"{Name}: {State}  P={Pressure.Pressure:0.0e0} {(Pressure.IG.IsOn ? "(ion)" : "")}";
		}

		#region Class Interface Methods -- Control the device using these functions

		/// <summary>
		/// Close the last valve in each ManifoldSection and Isolate the VacuumSystem.
		/// The last valve is supposed to be the one that connects the Section to 
		/// the Vacuum Manifold.
		/// </summary>
		public void IsolateManifold()
		{
			ManifoldSections.ForEach(s => { s.Isolation.CloseLast(); });
			Isolate();
		}

		/// <summary>
		/// IsolateManifold() but skip "ExceptThis" if it is supplied.
		/// </summary>
		/// <param name="ExceptThis">Skip this section</param>
		public void IsolateExcept(Section ExceptThis)
		{
			ManifoldSections.ForEach(s => { if (s != ExceptThis) s.Isolation.CloseLast(); });
			Isolate();
		}

		/// <summary>
		/// IsolateManifold() but skip "ExceptThis" if it is supplied.
		/// </summary>
		/// <param name="ExceptThis">Skip this valve</param>
		public void IsolateExcept(IValve ExceptThis)
		{
			ManifoldSections.ForEach(s => { if (s.Isolation.Last != ExceptThis) s.Isolation.CloseLast(); });
			Isolate();
		}

		/// <summary>
		/// Isolate each of the main sections connected to the VacuumSystem manifold.
		/// </summary>
		public void IsolateSections()
		{
			ManifoldSections.ForEach(s => s.Isolate());
		}


		/// <summary>
		///  Disables all automatic control of VacuumSystem.
		/// </summary>
		public void Standby()
		{
			if (State == States.Stopped) Start();
			TargetState = TargetStates.Standby;
			targetStateChanged();
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
            if (State == States.Stopped) Start();
            TargetState = TargetStates.Isolate;
			targetStateChanged();
			while (State != States.Isolated && waitForState)
				Wait();
		}

		/// <summary>
		/// Requests Evacuation mode. Initiates pumping on the vacuum manifold and attempts to bring it to high vacuum.
		/// </summary>
		public void Evacuate()
		{
            if (State == States.Stopped) Start();
            TargetState = TargetStates.Evacuate;
			targetStateChanged();
		}

		/// <summary>
		/// Requests Evacuate mode. Returns when the target pressure is reached.
		/// </summary>
		/// <param name="pressure">Target pressure</param>
		public void Evacuate(double pressure)
		{
            Evacuate();
			while (State < States.Roughing)
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
            if (pressure == 0) pressure = pressure_baseline;
            pTarget = pressure;

            ProcessStep?.Start($"Wait for p_VM < {pTarget:0.0e0} Torr");
            while (Pressure > pTarget)
            {
                if (pTarget != pressure)
                {
                    pressure = pTarget;
                    if (ProcessStep != null)
                        ProcessStep.CurrentStep.Description = $"Wait for p_VM < {pTarget:0.0e0} Torr";
                }
                Thread.Sleep(35);
            }
            ProcessStep?.End();
        }

        /// <summary>
        /// Request to evacuate vacuum manifold using low-vacuum pump only.
        /// Vacuum Manifold will be roughed and isolated alternately
        /// to maintain VM pressure between pressure_HV_required
        /// and pressure_LV_required
        /// </summary>
        public void Rough()
		{
            if (State == States.Stopped) Start();
            TargetState = TargetStates.Rough;
			targetStateChanged();
		}

        public void SetTargetState(TargetStates targetState)
        {
            switch (targetState)
            {
                case TargetStates.Standby:
                    Standby();
                    break;
                case TargetStates.Isolate:
                    Isolate(false);
                    break;
                case TargetStates.Rough:
                    Rough();
                    break;
                case TargetStates.Evacuate:
                    Evacuate();
                    break;
                case TargetStates.Stop:
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

		void targetStateChanged()
		{
			stateSignal.Set();
		}

		void deviceStateChanged()
		{
			stateSignal.Set();
		}

        bool valvesReady => v_HighVacuum.Ready && v_LowVacuum.Ready && v_Backing.Ready;

		// TODO: Need to add a timer to monitor how long Backing has been closed, and 
		// to periodically empty the turbo pump exhaust.
		void ManageState()
		{
			try
			{
				while (true)
				{
					if (TargetState == TargetStates.Stop)
						break;

                    int timeout = 50;              // TODO: consider moving this (StateManagerIdleLoopMilliseconds-ugh) into settings.xml
                    if (valvesReady)
                    {
                        switch (TargetState)
                        {
                            case TargetStates.Isolate:
                                if (State != States.Isolated)
                                {
                                    IGDisable();
                                    v_HighVacuum.Close();
                                    v_LowVacuum.Close();
                                    v_Roughing?.Open();
                                    // TODO need a timeout here with alert/failure;
                                    // the turbo pump could fail if the roughing pump is down
                                    while (pForeline > pressure_good_backing)
                                        Wait();
                                    v_Backing.OpenWait();
                                    StateStopwatch.Restart();
                                    StateChanged?.Invoke();
                                }
                                break;
                            case TargetStates.Rough:
                                if (RoughAsNeeded())
                                {
                                    StateStopwatch.Restart();
                                    StateChanged?.Invoke();
                                    timeout = 0;        // keep working until no action taken
                                }
                                break;
                            case TargetStates.Evacuate:
                                if (EvacuateOrRoughAsNeeded())
                                {
                                    StateStopwatch.Restart();
                                    StateChanged?.Invoke();
                                    timeout = 0;        // keep working until no action taken
                                }
                                break;
                            case TargetStates.Standby:
                                break;
                            default:
                                break;
                        }
                    }

					ManageIonGauge();
					MonitorBaseline();

					stateSignal.WaitOne(timeout);
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		public void IGDisable()
		{
			Pressure.IG.Disable();
		}

		public void IGEnable()
		{
			Pressure.IG.Enable();
		}

		protected void ManageIonGauge()
		{
			if (IonGaugeAuto)
			{
				if (!(v_HighVacuum.IsOpened && v_Backing.IsOpened))
					IGDisable();
				else if (Pressure.IG.MillisecondsInState > Pressure.IG.milliseconds_min_off)
				{
					bool pressureHigh = Pressure.m_HP > Pressure.pressure_VM_switchpoint;
					if (pressureHigh && Pressure.IG.IsOn)
						IGDisable();
					else if (!pressureHigh && !Pressure.IG.IsOn)
						IGEnable();
				}
			}
		}

		protected void MonitorBaseline()
		{
			// monitor time at "baseline" (minimal pressure and steady foreline pressure)
			if (State == States.HighVacuum &&
				Pressure <= pressure_baseline &&
				Math.Abs(pForeline.RoC) < 20 * pForeline.Sensitivity
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
            if (State == States.HighVacuum && Pressure < pressure_LV_required ||
				State == States.Roughing && Pressure > pressure_HV_required)
                return false; // no action taken

			v_Roughing?.OpenWait();

            if (Pressure < pressure_HV_preferred)               // ok to go to HV
            {
                if (v_LowVacuum.IsOpened)
                    v_LowVacuum.CloseWait();
                if (pForeline < pressure_good_backing)
                    v_Backing.OpenWait();
                if (v_Backing.IsOpened)
                    v_HighVacuum.OpenWait();
            }
            else            // need LV
            {
                if (v_HighVacuum.IsOpened)
                    v_HighVacuum.CloseWait();
                if (pForeline < pressure_good_backing)
                    v_Backing.CloseWait();
                if (v_Backing.IsClosed)
                    v_LowVacuum.OpenWait();
            }
            return true;    // even though no valve may have moved; might be waiting on pForeline for backing
        }

        protected bool RoughAsNeeded()
		{
			bool stateChanged = false;
			if (!v_HighVacuum.IsClosed)
				v_HighVacuum.CloseWait();

			if (v_LowVacuum.IsClosed)	// isolated
			{
				if (Pressure < pressure_LV_required)
				{
					// back TurboPump whenever possible
					if (v_Backing.IsClosed && pForeline < pressure_good_backing)
					{
						v_Backing.OpenWait();
						stateChanged = true;	// backing
					}
				}
				else    // need to rough
				{
					if (v_Backing.IsOpened)
						v_Backing.Close();
					v_LowVacuum.OpenWait();
					stateChanged = true;    // roughing
				}
			}
			else	// currently roughing
			{
				if (Pressure <= pressure_HV_required)
				{
					v_LowVacuum.CloseWait();
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
	}
}


