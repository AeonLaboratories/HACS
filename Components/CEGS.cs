using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Utilities;
using static HACS.Components.CegsPreferences;
using static Utilities.Utility;

namespace HACS.Components
{
    public class CEGS : ProcessManager, ICegs
	{
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			InletPort = Find<InletPort>(inletPortName);
		}

		/// <summary>
		/// Notify the operator if the required object is missing.
		/// </summary>
		private void CegsNeeds(object obj, string objName)
		{
			if (obj == null)
				Warn("Configuration Error", 
					$"Can't find {obj.GetType()} {objName}. CEGS needs one Connected.");
		}

		[HacsPostConnect]
		protected virtual void PostConnect()
		{
			// check that the essentials are found
			CegsNeeds(Power, nameof(Power));
			CegsNeeds(Ambient, nameof(Ambient));
			CegsNeeds(VacuumSystem, nameof(VacuumSystem));
			CegsNeeds(IM, nameof(IM));
			CegsNeeds(VTT, nameof(VTT));
			CegsNeeds(MC, nameof(MC));
			CegsNeeds(Split, nameof(Split));
			CegsNeeds(GM, nameof(GM));
			CegsNeeds(VTT_MC, nameof(VTT_MC));
			CegsNeeds(MC_Split, nameof(MC_Split));
			CegsNeeds(ugCinMC, nameof(ugCinMC));

			foreach (var cf in Coldfingers.Values)
				cf.SlowToFreeze += OnSlowToFreeze;

			Power.MainsDown += OnMainsDown;
			Power.MainsFailed += OnMainsFailed;
			Power.MainsRestored += OnMainsRestored;

			foreach (var x in LNManifolds.Values)
			{
				x.OverflowDetected += OnOverflowDetected;
				x.SlowToFill += OnSlowToFill;
			}

			//ugCinMC depends on both of these, but they are both updated
			//every daq cycle so only one needs to trigger the update
			//MC.Manometer.PropertyChanged += UpdateSampleMeasurement;
			MC.Thermometer.PropertyChanged += UpdateSampleMeasurement;

			foreach (var c in VolumeCalibrations.Values)
			{
				c.ProcessStep = ProcessStep;
				c.ProcessSubStep = ProcessSubStep;
				c.OpenLine = OpenLine;
				c.OkPressure = OkPressure;
				c.Log = SampleLog;
			}

			CalculateDerivedConstants();
		}

		[HacsPostStart]
		protected virtual void PostStart()
		{
			Stopping = false;
			SystemUpTime.Start();
			StartThreads();
			Started = true;
			SaveSettingsToFile("startup.json");
		}


		[HacsPreStop]
		protected virtual void PreStop()
		{
			try
			{
				EventLog.Record("System shutting down");
				Stopping = true;

				UpdateTimer.Dispose();
				SystemLogSignal.Set();
				lowPrioritySignal.Set();
				stoppedSignal1.WaitOne();
				stoppedSignal2.WaitOne();

				// Note: controllers of multiple devices should shutdown in Stop()
				// The devices they control should have their shutdown states
				// effected in PreStop()

			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		/// <summary>
		/// system status logs stopped
		/// </summary>
		ManualResetEvent stoppedSignal1 = new ManualResetEvent(true);

		/// <summary>
		/// low priority activities stopped
		/// </summary>
		ManualResetEvent stoppedSignal2 = new ManualResetEvent(true);
		public new bool Stopped => stoppedSignal1.WaitOne(0) && stoppedSignal2.WaitOne(0);
		protected bool Stopping { get; set; }

		[HacsPostStop]
		protected virtual void PostStop()
		{
			SerialPortMonitor.Stop();
		}

		#endregion HacsComponent

		#region System configuration

		#region Component lists
		[JsonProperty] public Dictionary<string, IDeviceManager> DeviceManagers { get; set; }
		[JsonProperty] public Dictionary<string, IManagedDevice> ManagedDevices { get; set; }
		[JsonProperty] public Dictionary<string, IMeter> Meters { get; set; }
		[JsonProperty] public Dictionary<string, IValve> Valves { get; set; }
		[JsonProperty] public Dictionary<string, ISwitch> Switches { get; set; }
		[JsonProperty] public Dictionary<string, IHeater> Heaters { get; set; }
		[JsonProperty] public Dictionary<string, IPidSetup> PidSetups { get; set; }

		[JsonProperty] public Dictionary<string, ILNManifold> LNManifolds { get; set; }
		[JsonProperty] public Dictionary<string, IColdfinger> Coldfingers { get; set; }
		[JsonProperty] public Dictionary<string, IVTColdfinger> VTColdfingers { get; set; }

		[JsonProperty] public Dictionary<string, IVacuumSystem> VacuumSystems { get; set; }
		[JsonProperty] public Dictionary<string, IChamber> Chambers { get; set; }
		[JsonProperty] public Dictionary<string, ISection> Sections { get; set; }
		[JsonProperty] public Dictionary<string, IGasSupply> GasSupplies { get; set; }
		[JsonProperty] public Dictionary<string, IFlowManager> FlowManagers { get; set; }

		[JsonProperty] public Dictionary<string, IVolumeCalibration> VolumeCalibrations { get; set; }
		[JsonProperty] public Dictionary<string, IHacsLog> Logs { get; set; }

		// TODO: make this an interface
		// The purpose of the FindAll().ToDictionary is to automate deletions
		// from the settings file (i.e., to avoid needing a backing variable and
		// Samples.Remove())
		[JsonProperty]
		public Dictionary<string, Sample> Samples
		{
			get => FindAll<Sample>().ToDictionary(s => s.Name, s => s);
			set { } 
		}

		#endregion Component lists

		#region HacsComponents
		[JsonProperty] public virtual Power Power { get; set; }

		#region Data Logs
		public virtual DataLog AmbientLog { get; set; }
		public virtual DataLog VMPressureLog { get; set; }
		public virtual HacsLog SampleLog { get; set; }
		#endregion Data Logs

		public virtual IChamber Ambient { get; set; }
		public virtual IVacuumSystem VacuumSystem { get; set; }

		public virtual ISection IM { get; set; }
		public virtual ISection CT { get; set; }
		public virtual ISection VTT { get; set; }
		public virtual ISection MC { get; set; }
		public virtual ISection Split { get; set; }
		public virtual ISection d13C { get; set; }
		public virtual ISection d13C_14C { get; set; }
		public virtual ISection GM { get; set; }

		public virtual ISection d13CM { get; set; }
		public virtual ISection VTT_MC { get; set; }
		public virtual ISection MC_Split { get; set; }

		// insist on an actual Meter, to enable implicit double
		public virtual Meter ugCinMC { get; set; }

		protected virtual ISection FirstTrap => CT ?? VTT;

		#endregion HacsComponents

		#region Constants

		[JsonProperty]
		public virtual CegsPreferences Preferences { get; set; }

		#region Globals

		#region UI Communications

		public virtual Func<bool, List<IInletPort>> SelectSamples { get; set; }

		public virtual void PlaySound() => Notice.Send("PlaySound", Notice.Type.Tell);

		#endregion UI Communications

		public virtual string PriorAlertMessage => AlertManager.PriorAlertMessage;

		public virtual List<IInletPort> InletPorts { get; set; }
		public virtual List<IGraphiteReactor> GraphiteReactors { get; set; }
		public virtual List<Id13CPort> d13CPorts { get; set; }


		[JsonProperty("InletPort")]
		string InletPortName { get => InletPort?.Name; set => inletPortName = value; }
		string inletPortName;
		public virtual IInletPort InletPort
		{
			get => inletPort;
			set => Ensure(ref inletPort, value, OnPropertyChanged);
		}
		IInletPort inletPort;

		protected void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
			if (sender == InletPort && InletPort.Sample is ISample sample)
				Sample = sample;
        }

		public virtual ISample Sample
		{
			get => sample;
			set => Ensure(ref sample, value);
		}
		ISample sample;

		protected virtual Id13CPort d13CPort
		{
			get => _d13CPort ?? Guess_d13CPort();
			set => _d13CPort = value;
		}
		Id13CPort _d13CPort;

		protected virtual Id13CPort Guess_d13CPort() =>
			Guess_d13CPort(Sample);

		protected virtual Id13CPort Guess_d13CPort(ISample sample) =>
			Guess_d13CPort(sample?.InletPort);

		protected virtual Id13CPort Guess_d13CPort(IInletPort inletPort)
		{
			if (inletPort?.Name is string ipName &&
				ipName.StartsWith("IP") &&
				ipName.Length > 2 &&
				FirstOrDefault<Id13CPort>(p => p.Name.EndsWith(ipName[2..])) is Id13CPort p)
				return p;
			return FirstOrDefault<Id13CPort>();
		}

		#endregion Globals

		#region Sample Measurement Constants

		/// <summary>
		///  Boltzmann constant (Torr * mL / K)
		/// </summary>
		protected double BoltzmannConstantTorr_mL;
		/// <summary>
		/// average number of carbon atoms per microgram
		/// </summary>
		protected double CarbonAtomsPerMicrogram;

		#endregion Sample Measurement Constants

		/// <summary>
		/// Reference data for CO2 phase equilibrium temperature as a function of pressure
		/// </summary>
		protected LookupTable CO2EqTable = new LookupTable(@"CO2 eq.dat");

		#endregion Constants

		#endregion System configuration

		#region System elements not saved/restored in Settings

		public new virtual bool Started { get; protected set; }

		protected Action DuringBleed;

		#region Threading

		public Timer UpdateTimer { get; set; }

		// logging
		Thread systemLogThread;
		protected AutoResetEvent SystemLogSignal { get; private set; } = new AutoResetEvent(false);

		// low priority activity
		Thread lowPriorityThread;
		protected AutoResetEvent lowPrioritySignal { get; private set; } = new AutoResetEvent(false);

		#endregion Threading

		// system conditions
		protected Stopwatch SystemUpTime { get; private set; } = new Stopwatch();
		public virtual TimeSpan Uptime => SystemUpTime.Elapsed;

		// process management
		public virtual bool SampleIsRunning => ProcessSequenceIsRunning;

		#endregion System elements not saved in/restored from Settings

		#region Startup and ShutDown

		protected virtual void CalculateDerivedConstants()
		{
			#region Sample measurement constants
			BoltzmannConstantTorr_mL = BoltzmannConstant * Torr / Pascal * MilliLiter / CubicMeter;
			CarbonAtomsPerMicrogram = AvogadrosNumber / MicrogramsCarbonPerMole;        // number of atoms per microgram of carbon, assuming standard isotopic composition
			#endregion Sample measurement constants

			MaximumAliquotsPerSample = 1 + MC?.Ports?.Count ?? 0;
		}

		protected virtual void StartThreads()
		{
			EventLog.Record("System Started");

			systemLogThread = new Thread(LogSystemStatus)
			{
				Name = $"{Name} logSystemStatus",
				IsBackground = true
			};
			systemLogThread.Start();

			lowPriorityThread = new Thread(LowPriorityActivities)
			{
				Name = $"{Name} lowPriorityActivities",
				IsBackground = true
			};
			lowPriorityThread.Start();

			UpdateTimer = new Timer(UpdateTimerCallback, null, 0, UpdateIntervalMilliseconds);
		}

		#endregion Startup and ShutDown

		#region elementary utility functions

		/// <summary>
		/// From (pv = nkt): n = pv / kt
		/// </summary>
		/// <param name="pressure">Torr</param>
		/// <param name="volume">milliliters</param>
		/// <param name="temperature">°C</param>
		/// <returns>number of particles</returns>
		protected virtual double Particles(double pressure, double volume, double temperature) =>
			pressure * volume / BoltzmannConstantTorr_mL / (ZeroDegreesC + temperature);

		/// <summary>
		/// Temperature-dependent pressure for number of particles in a fixed volume (in milliliters).
		/// </summary>
		/// <param name="particles">number of particles</param>
		/// <param name="volume">milliliters</param>
		/// <returns></returns>
		protected virtual double TorrPerKelvin(double particles, double volume) =>
			particles * BoltzmannConstantTorr_mL / volume;

		/// <summary>
		/// From (pv = nkt): p = nkt / v.
		/// Units are Torr, K, milliliters
		/// </summary>
		/// <param name="particles">number of particles</param>
		/// <param name="volume">milliliters</param>
		/// <param name="temperature">°C</param>
		/// <returns>pressure in Torr</returns>
		protected virtual double Pressure(double particles, double volume, double temperature) =>
			(ZeroDegreesC + temperature) * TorrPerKelvin(particles, volume);

		/// <summary>
		/// The mass of carbon in a quantity of CO2 gas, given its pressure, volume and temperature.
		/// </summary>
		/// <param name="pressure">Torr</param>
		/// <param name="volume">mL</param>
		/// <param name="temperature">°C</param>
		/// <returns></returns>
		protected virtual double MicrogramsCarbon(double pressure, double volume, double temperature) =>
			Particles(pressure, volume, temperature) / CarbonAtomsPerMicrogram;
			
		/// <summary>
		/// The mass of carbon in the chamber, assuming it contains only gaseous CO2.
		/// </summary>
		/// <param name="ch"></param>
		/// <returns></returns>
		protected virtual double MicrogramsCarbon(IChamber ch) => 
			MicrogramsCarbon(ch.Pressure, ch.MilliLiters, ch.Temperature);

		/// <summary>
		/// The mass of carbon in the section, assuming it contains only gaseous CO2.
		/// </summary>
		/// <param name="section"></param>
		/// <returns></returns>
		protected virtual double MicrogramsCarbon(ISection section) =>
			MicrogramsCarbon(section.Pressure, section.CurrentVolume(true), section.Temperature);

		#endregion elementary utility functions

		#region Periodic system activities & maintenance

		#region Logging
		protected virtual void LogSystemStatus()
		{
			stoppedSignal1.Reset();
			try
			{
				while (!Stopping)
				{
					if (!Started) continue;
					try { HacsLog.UpdateAll(); }
					catch (Exception e) { Notice.Send(e.ToString()); }
					SystemLogSignal.WaitOne(500);
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
			stoppedSignal1.Set();
		}

		#endregion Logging

		protected virtual void UpdateTimerCallback(object state)
		{
			try
			{
				if (!Stopping)
					Update();
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		int msUpdateLoop = 0;
		bool allDaqsOk = false;
		List<IDaq> daqs = CachedList<IDaq>();
		protected virtual void Update()
		{
			#region DAQs
			var daqsOk = true;
			foreach (var daq in daqs)
			{
				if (!daq.IsUp)
				{
					daqsOk = false;
					if (!daq.IsStreaming)
						EventLog.LogParsimoniously(daq.Name + " is not streaming");
					else if (!daq.DataAcquired)
						EventLog.LogParsimoniously(daq.Name + ": waiting for stream to start");
					else
					{
						var error = daq.Error;
						if (error != default)
							EventLog.LogParsimoniously(error);
						daq.ClearError();
					}
				}
			}
			if (!allDaqsOk && daqsOk)
			{
				var ugcFilter = ugCinMC?.Filter as ButterworthFilter;
				var pMCFilter = MC?.Manometer?.Filter as ButterworthFilter;
				if (ugcFilter != null && pMCFilter != null)
					ugcFilter.SamplingFrequency = pMCFilter.SamplingFrequency;
			}
			allDaqsOk = daqsOk;
			#endregion DAQs

			#region Power failure watchdog
			if (Started && EnableWatchdogs)
				Power?.Update();
			#endregion Power failure watchdog

			#region 200 ms
			if (daqsOk && msUpdateLoop % 200 == 0)
			{
				SystemLogSignal.Set();
			}
			#endregion 200 ms

			#region 500 ms
			if (daqsOk && Started && msUpdateLoop % 500 == 0)
			{
				lowPrioritySignal.Set();
			}
			#endregion 500 ms

			if (msUpdateLoop % 3600000 == 0) msUpdateLoop = 0;
			msUpdateLoop += UpdateIntervalMilliseconds;
		}

		protected virtual void PostUpdateGR(IGraphiteReactor gr)
		{
			if (gr.Busy)
			{
				// GR.State is "Stop" for exactly one GR.Update() cycle.
				if (gr.State == GraphiteReactor.States.Stop)
				{
					SampleLog.Record(
						"Graphitization complete:\r\n" +
						$"\tGraphite {gr.Contents}");
					if (BusyGRCount() == 1 && !SampleIsRunning)  // the 1 is this GR; "Stop" is still 'Busy'
					{
						string msg = "Last graphite reactor finished.";
						if (PreparedGRs() < 1)
							msg += "\r\nGraphite reactors need service.";
						Alert("Operator Needed", msg);
					}
				}
			}
			else if (gr.State == GraphiteReactor.States.WaitService)
			{
				if (gr.Aliquot != null)
				{
					IAliquot a = gr.Aliquot;
					if (!a.ResidualMeasured)
					{
						double ambientTemperature = Manifold(gr).Temperature;
						if (Math.Abs(ambientTemperature - gr.SampleTemperature) < 10 &&		// TODO: magic number
							Math.Abs(ambientTemperature - gr.ColdfingerTemperature) < 10)
						{
							// residual is P/T (Torr/kelvin)
							a.ResidualPressure = gr.Pressure / (ZeroDegreesC + ambientTemperature);

							SampleLog.Record(
								"Residual measurement:\r\n" +
								$"\tGraphite {a.Name}\t{a.ResidualPressure:0.000}\tTorr/K"
								);
							a.ResidualMeasured = true;

							if (a.ResidualPressure > 2 * a.ExpectedResidualPressure)
							{
								if (a.Tries > 1)
								{
									SampleLog.Record(
										"Excessive residual pressure. Graphitization failed.\r\n" +
										$"\tGraphite {a.Name}"
										);
								}
								else
								{
									SampleLog.Record(
										"Excessive residual pressure. Trying again.\r\n" +
										$"\tGraphite {a.Name}"
										);
									gr.Start();  // try again
									a.ResidualMeasured = false;
								}
							}
						}
					}
				}
			}
		}

		#region device event handlers

		protected virtual void OnMainsDown() =>
			Warn("System Warning", "Mains Power is down");

		protected virtual void OnMainsRestored() =>
			Warn("System Message", $"Mains Power restored (down {Power.MainsDownTimer.ElapsedMilliseconds} ms)");

		protected virtual void OnMainsFailed()
		{
			EventLog.Record("System Failure: Mains Power Failure");
			Alert("System Failure", "Mains Power Failure");
			Notice.Send("System Failure", "Mains Power Failure", Notice.Type.Tell);
			AbortRunningProcess();
			VacuumSystem.Isolate();
			VacuumSystem.IsolateManifold();
		}

		protected virtual void OnOverflowDetected() =>
			Warn("System Alert!", "LN Containment Failure");

		protected virtual void OnSlowToFill() =>
			Alert("System Warning!", "LN Manifold is slow to fill!");

		protected virtual void OnSlowToFreeze()
		{
			var coldfingers = FindAll<Coldfinger>();
			var on = coldfingers.FindAll (cf => cf.State == Coldfinger.States.Freezing);
			coldfingers.ForEach(cf => cf.Standby());
			string list = "";
			on.ForEach(cf => list += cf.Name + "? ");
			Warn("System Alert!", $"A coldfinger is slow to freeze. {list} System paused for operator.");
		}
		#endregion device event handlers

		protected virtual void LowPriorityActivities()
		{
			stoppedSignal2.Reset();
			try
			{
				while (!Stopping)
				{
					if (EnableAutozero) ZeroPressureGauges();

					GraphiteReactors?.ForEach(gr => { gr.Update(); PostUpdateGR(gr); });
					InletPorts?.ForEach(ip => ip.Update());

					SaveSettings();
					lowPrioritySignal.WaitOne(500);
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
			stoppedSignal2.Set();
		}

		/// <summary>
		/// Event handler for MC temperature and pressure changes
		/// </summary>
		protected virtual void UpdateSampleMeasurement(object sender = null, PropertyChangedEventArgs e = null)
		{
			if (e?.PropertyName == nameof(IValue.Value))
				ugCinMC.Update(MicrogramsCarbon(MC));
		}


		// value > Km * sensitivity ==> meter needs zeroing
		protected virtual void ZeroIfNeeded(IMeter m, double Km)
		{
			if (m != null && Math.Abs(m.Value) >= Km * m.Sensitivity)
				m.ZeroNow();
		}

		protected virtual void ZeroPressureGauges() { }

		#endregion Periodic system activities & maintenance

		#region Process Management

		/// <summary>
		/// This method must be provided by the derived class.
		/// </summary>
		protected virtual void OverrideNeeded([CallerMemberName] string caller = default) =>
			Warn("Program Error", $"{Name} needs an override for {caller}().");


		protected virtual void SampleRecord(ISample sample) { }
		protected virtual void SampleRecord(IAliquot aliquot) {}


		/// <summary>
		/// The gas supply that delivers the specified gas to the destination.
		/// </summary>
		protected virtual GasSupply GasSupply(string gas, ISection destination)
		{
			var gasSupplyName = gas + "." + destination.Name;
			if (Find<GasSupply>(gasSupplyName) is GasSupply gasSupply)
				return gasSupply;

			Warn("Process Alert!",
				$"Cannot admit {gas} into {destination.Name}. There is no GasSupply named {gasSupplyName}.");
			return null;
		}

		/// <summary>
		/// The Section's He supply, if there is one; otherwise, its Ar supply;
		/// null if neither is found.
		/// </summary>
		protected virtual GasSupply InertGasSupply(ISection section)
		{
			var gasSupply = Find<GasSupply>("He." + section.Name);
			if (gasSupply == null)
				gasSupply = Find<GasSupply>("Ar." + section.Name);
			return gasSupply;
		}

		/// <summary>
		/// The first Section that contains the given port.
		/// </summary>
		protected virtual Section Manifold(IPort port)
		{
			return FirstOrDefault<Section>(s => s.Ports?.Contains(port) ?? false);
		}

		/// <summary>
		/// Gets InletPort's manifold.
		/// </summary>
		/// <returns>false if InletPort is null or the manifold is not found.</returns>
		protected virtual bool IpIm(out ISection im)
		{
			im = Manifold(InletPort);
			if (InletPort == null)
			{
				Warn("Process Error", "No InletPort is selected.");
				return false;
			}
			if (im == null)
			{
				Warn("Process Error", $"Can't find manifold Section for {InletPort.Name}.");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Gets InletPort's manifold and O2 gas supply.
		/// </summary>
		/// <returns>false if InletPort is null, or if the manifold or gas supply is not found.</returns>
		protected virtual bool IpImO2(out ISection im, out IGasSupply gs)
		{
			gs = null;
			if (IpIm(out im))
				gs = GasSupply("O2", im);
			return gs != null;
		}

		/// <summary>
		/// Gets InletPort's manifold and inert gas supply.
		/// </summary>
		/// <returns>false if InletPort is null, or if the manifold or gas supply is not found.</returns>
		protected virtual bool IpImInertGas(out ISection im, out IGasSupply gs)
		{
			gs = null;
			if (!IpIm(out im)) return false;
			gs = InertGasSupply(im);
			if (gs != null) return true;
			Warn("Configuration Error", $"Section {im.Name} has no inert GasSupply.");
			return false;
		}

		/// <summary>
		/// Gets the GraphiteReactor's manifold.
		/// </summary>
		/// <returns>false if gr is null or the manifold is not found.</returns>
		protected virtual bool GrGm(IGraphiteReactor gr, out ISection gm)
		{
			gm = Manifold(gr);
			if (gr == null)
			{
				Warn("Process Error", "gr cannot be null.");
				return false;
			}
			if (gm == null)
			{
				Warn("Process Error", $"Can't find manifold Section for {gr.Name}.");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Gets the GraphiteReactor's manifold and H2 gas supply.
		/// </summary>
		/// <returns>false if gr is null, or if the manifold or gas supply is not found.</returns>
		protected virtual bool GrGmH2(IGraphiteReactor gr, out ISection gm, out IGasSupply gs)
		{
			gs = null;
			if (GrGm(gr, out gm))
				gs = GasSupply("H2", gm);
			return gs != null;
		}

		/// <summary>
		/// Gets the GraphiteReactor's manifold and inert gas supply.
		/// </summary>
		/// <returns>false if gr is null, or if the manifold or gas supply is not found.</returns>
		protected virtual bool GrGmInertGas(IGraphiteReactor gr, out ISection gm, out IGasSupply gs)
		{
			gs = null;
			if (!GrGm(gr, out gm)) return false;
			gs = InertGasSupply(gm);
			if (gs != null) return true;
			Warn("Configuration Error", $"Section {gm.Name} has no inert GasSupply.");
			return false;
		}


		#region parameterized processes
		protected override void Combust(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
		{
			if (!IpImO2(out ISection im, out IGasSupply O2)) return;

			if (admitO2)
			{
				ProcessStep.Start($"Combust at {temperature} °C, {MinutesString(minutes)}");
				AdmitIPO2();
			}
			else
				ProcessStep.Start($"Heat IP: {temperature} °C, {MinutesString(minutes)}");

			if (InletPort.SampleFurnace.IsOn)
				InletPort.SampleFurnace.Setpoint = temperature;
			else
				InletPort.SampleFurnace.TurnOn(temperature);

			if (openLine)
			{
				im.Evacuate(OkPressure);
				OpenLine();
			}

			if (waitForSetpoint)
			{
				ProcessStep.End();

				int closeEnough = temperature - 20;
				ProcessStep.Start($"Wait for {InletPort.SampleFurnace.Name} to reach {closeEnough} °C");
				while (InletPort.SampleFurnace.Temperature < closeEnough) Wait();
				ProcessStep.End();

				ProcessStep.Start($"Combust at {temperature} °C for {MinutesString(minutes)}.");
			}

			WaitRemaining(minutes);

			ProcessStep.End();
		}
		#endregion parameterized processes

		protected virtual void WaitForOperator()
		{
			Alert("Operator Needed", "Waiting for Operator.");
			Pause("Operator Needed", "Waiting for Operator.");
		}

		#region Valve operations

		protected virtual void ExerciseAllValves()
		{
			ProcessStep.Start("Exercise all opened valves");
			foreach (var v in Valves?.Values) 
				if ((v is CpwValve || v is PneumaticValve) && v.IsOpened)
					v.Exercise();
			ProcessStep.End();
		}

		protected virtual void ExerciseLNValves()
		{
			ProcessStep.Start("Exercise all LN Manifold valves");
			foreach (var ftc in Coldfingers?.Values) ftc.LNValve.Exercise();
			ProcessStep.End();
		}

		protected virtual void CloseLNValves()
		{
			foreach (var ftc in Coldfingers?.Values) ftc.LNValve.Close();
		}

		protected virtual void CalibrateRS232Valves()
		{
			foreach (var v in Valves.Values)
				if (v is RS232Valve rv)
					rv.Calibrate();
		}

		#endregion Valve operations

		#region Support and general purpose functions

		protected virtual void WaitForPressure(double pressure) => 
			VacuumSystem?.WaitForPressure(pressure);

		protected virtual void WaitForStablePressure(double pressure, int seconds = 5)
		{
			var sw = new Stopwatch();
			ProcessSubStep.Start($"Wait for stable pressure below {pressure} {VacuumSystem.Manometer.UnitSymbol}");
			while (sw.Elapsed.TotalSeconds < seconds)
			{
				Wait(100);
				if (VacuumSystem.Pressure <= pressure && VacuumSystem.ForelineManometer.IsStable)
				{
					if (!sw.IsRunning)
						sw.Restart();
				}
				else
					sw.Reset();
			}
			ProcessSubStep.End();
		}

		protected virtual void TurnOffCCFurnaces() => InletPort?.TurnOffFurnaces();

		protected virtual void HeatQuartz(bool openLine)
		{
			if (InletPort == null) return;

			ProcessStep.Start($"Heat Combustion Chamber Quartz ({QuartzFurnaceWarmupMinutes} minutes)");
			InletPort.QuartzFurnace.TurnOn();
			if (InletPort.State == LinePort.States.Loaded ||
				InletPort.State == LinePort.States.Prepared)
				InletPort.State = LinePort.States.InProcess;
			if (openLine) OpenLine();
			WaitRemaining(QuartzFurnaceWarmupMinutes);

			if (InletPort.NotifySampleFurnaceNeeded)
			{
				Alert("Operator Needed", $"{Sample?.LabId} is ready for sample furnace.");
				Notice.Send("Operator needed",
					$"Remove any coolant from combustion tube and \r\n" +
					$"raise the sample furnace at {InletPort.Name}.\r\n" +
					"Press Ok to continue");
			}
			ProcessStep.End();
		}

		/// <summary>
		/// Heat the InletPort's quartz bed, while evacuating the rest
		/// of the line.
		/// </summary>
		[Description("Heat the InletPort's quartz bed, while evacuating the rest of the line.")]
		protected virtual void HeatQuartzOpenLine() => HeatQuartz(true);

		protected virtual void Admit(string gas, ISection destination, IPort port, double pressure)
		{
			if (!(GasSupply(gas, destination) is GasSupply gasSupply))
				return;
			gasSupply.Destination.ClosePorts();
			gasSupply.Admit(pressure);

			if (gasSupply.Meter.Value < pressure)
			{
				Warn("Process Alert!",
					$"Couldn't admit {pressure} {gasSupply.Meter.UnitSymbol} of {gasSupply.GasName} into {gasSupply.Destination.Name}");
			}

			if (port != null)
			{
				ProcessSubStep.Start($"Admit {gasSupply.GasName} into {port.Name}");
				port.Open();
				Wait(2000);
				port.Close();
				ProcessSubStep.End();
				WaitSeconds(5);
			}
		}

		/// <summary>
		/// Admit O2 into the InletPort
		/// </summary>
		protected virtual void AdmitIPO2()
		{
			if (!IpIm(out ISection im)) return;
			Admit("O2", im, InletPort, IMO2Pressure);
		}

		protected virtual void AdmitIPInertGas(double pressure)
		{
			if (!IpImInertGas(out ISection im, out IGasSupply gs)) return;
			Admit(gs.GasName, im, InletPort, pressure);
		}

		protected virtual void DiscardIPGases()
		{
			if (!IpIm(out ISection im)) return;
			ProcessStep.Start($"Discard gases at ({InletPort.Name})");
			im.Isolate();
			InletPort.Open();
			WaitSeconds(10);                // give some time to record a measurement
			im.Evacuate(OkPressure);	// allow for high pressure due to water
			ProcessStep.End();
		}

		protected virtual void DiscardMCGases()
		{
			ProcessStep.Start("Discard sample from MC");
			SampleRecord(Sample);
			MC?.Evacuate();
			ProcessStep.End();
		}

		protected virtual void Flush(ISection section, int n, IPort port = null)
		{
			if (InertGasSupply(section) is GasSupply gs)
				gs.Flush(PressureOverAtm, 0.1, n, port);
			else
				Warn("Configuration Error", $"Section {section.Name} has no inert GasSupply.");
		}

		protected virtual void FlushIP()
		{
			if (!IpIm(out ISection im)) return;

			InletPort.State = LinePort.States.InProcess;
			EvacuateIP();
			Flush(im, 3, InletPort);

			// Residual inert gas is undesirable only to the extent that it
			// displaces O2. An O2 concentration of 99.99% -- more than
			// adequate for perfect combustion -- equates to 0.01% inert gas.
			// The admitted O2 pressure always exceeds 1000 Torr; 
			// 0.01% of 1000 is 0.1 Torr.
			WaitForPressure(0.1);
			InletPort.Close();
		}

		/// <summary>
		/// The d13C port is neither Loaded nor Prepared.
		/// </summary>
		protected virtual bool ShouldBeClosed(Id13CPort port) =>
			port.State != LinePort.States.Loaded &&
			port.State != LinePort.States.Prepared;


		#region GR operations
		protected virtual IGraphiteReactor NextGR(string thisOne, GraphiteReactor.Sizes size = GraphiteReactor.Sizes.Standard)
		{
			bool passedThisOne = false;
			IGraphiteReactor foundOne = null;
			foreach (var gr in GraphiteReactors)
			{
				if (passedThisOne)
				{
					if (gr.Prepared && gr.Aliquot == null && gr.Size == size) return gr;
				}
				else
				{
					if (foundOne == null && gr.Prepared && gr.Aliquot == null && gr.Size == size)
						foundOne = gr;
					if (gr.Name == thisOne)
						passedThisOne = true;
				}
			}
			return foundOne;
		}

		protected virtual bool IsSulfurTrap(IGraphiteReactor gr) =>
			gr?.Aliquot?.Name == "sulfur";

		protected virtual IGraphiteReactor NextSulfurTrap(string thisGr)
		{
			bool passedThisOne = false;
			IGraphiteReactor foundOne = null;
			foreach (var gr in GraphiteReactors)
			{
				if (passedThisOne)
				{
					if (IsSulfurTrap(gr) && gr.State != GraphiteReactor.States.WaitService) return gr;
				}
				else
				{
					if (foundOne == null && IsSulfurTrap(gr) && gr.State != GraphiteReactor.States.WaitService)
						foundOne = gr;
					if (gr.Name == thisGr)
						passedThisOne = true;
				}
			}
			if (foundOne != null) return foundOne;
			return NextGR(thisGr);
		}

		//protected virtual void OpenNextGRs()
		//{

		//	string grName = PriorGR;
		//	for (int i = 0; i < Sample.AliquotsCount; ++i)
		//	{
		//		if (NextGR(grName) is IGraphiteReactor gr)
		//		{
		//			gr.Open();
		//			grName = gr.Name;
		//		}
		//	}
		//}

		//protected virtual void OpenNextGRsAndd13C()
		//{
		//	if (!GrGm(NextGR(PriorGR), out ISection gm)) return;
		//	VacuumSystem.Isolate();
		//	OpenNextGRs();
		//	gm.JoinToVacuum();

		//	if (Sample.Take_d13C)
		//	{
		//		var port = d13CPort;
		//		if (port == null)
		//		{
		//			Warn("Sample Alert",
		//				$"Can't find d13C port for Sample {Sample.LabId} from {InletPort?.Name}");
		//		}
		//		else if (port.State == LinePort.States.Prepared)
		//		{
		//			var manifold = Manifold(port);
		//			if (manifold == null)
		//			{
		//				Warn("Configuration Error", $"Can't find manifold Section for d13C port {port.Name}.");
		//			}
		//			else
		//			{
		//				manifold.ClosePorts();
		//				manifold.Isolate();
		//				port.Open();
		//				manifold.JoinToVacuum();
		//			}
		//		}
		//	}
		//	Evacuate();
		//}

		protected virtual void CloseAllGRs() => CloseAllGRs(null);

		protected virtual void CloseAllGRs(IGraphiteReactor exceptGR)
		{
			foreach (var gr in GraphiteReactors)
				if (gr != exceptGR)
					gr.Close();
		}

		protected virtual int BusyGRCount() => GraphiteReactors.Count(gr => gr.Busy);

		protected virtual int PreparedGRs() =>
			GraphiteReactors.Count(gr => gr.Prepared);

		protected virtual bool EnoughGRs()
		{
			int needed = Sample?.AliquotsCount ?? 1;
			if ((Sample?.SulfurSuspected ?? false) && !IsSulfurTrap(NextSulfurTrap(PriorGR)))
				needed++;
			return PreparedGRs() >= needed;
		}

		protected virtual void OpenPreparedGRs()
		{
			foreach (var gr in GraphiteReactors)
				if (gr.Prepared)
					gr.Open();
		}

		protected virtual bool PreparedGRsAreOpened() => 
			!GraphiteReactors.Any(gr => gr.Prepared && !gr.IsOpened);

		protected virtual void ClosePreparedGRs()
		{
			foreach (var gr in GraphiteReactors)
				if (gr.Prepared)
					gr.Close();
		}

		#region GR service

		protected virtual void PressurizeGRsWithInertGas(List<IGraphiteReactor> grs)
		{
			ProcessStep.Start("Backfill the graphite reactors with inert gas");
			var gasSupply = InertGasSupply(GM);
			if (gasSupply == null)
			{
				Warn("Configuration Error", $"Section {GM.Name} has no inert GasSupply.");
				return;
			}

			var pressure = Ambient.Pressure + 20;

			ProcessSubStep.Start($"Admit {pressure:0} Torr {gasSupply.GasName} into {GM.Name}");
			GM.ClosePorts();
			gasSupply.Admit();
			while (GM.Pressure < pressure)
				Wait();
			ProcessSubStep.End();


			ProcessSubStep.Start($"Open graphite reactors that are awaiting service.");
			grs.ForEach(gr => gr.Open());
			ProcessSubStep.End();

			ProcessSubStep.Start($"Ensure {GM.Name} pressure is ≥ {pressure:0} Torr");
			Wait(3000);
			while (GM.Pressure < pressure)
				Wait();
			gasSupply.ShutOff(true);
			ProcessSubStep.End();

			ProcessSubStep.Start("Isolate the graphite reactors");
			CloseAllGRs();
			ProcessSubStep.End();

			ProcessStep.End();
		}

		protected virtual void PrepareGRsForService()
		{
			var grs = new List<IGraphiteReactor>();
			foreach (var gr in GraphiteReactors)
			{
				if (gr.State == GraphiteReactor.States.WaitService)
					grs.Add(gr);
				else if (gr.State == GraphiteReactor.States.Prepared && gr.Contents == "sulfur")
					gr.ServiceComplete();
			}

			if (grs.Count < 1)
			{
				Notice.Send("Nothing to do", "No reactors are awaiting service.", Notice.Type.Tell);
				return;
			}

			grs.ForEach(gr => SampleRecord(gr.Aliquot));

			Notice.Send("Operator needed",
				"Mark Fe/C tubes with graphite IDs.\r\n" +
				"Press Ok to continue");

			PressurizeGRsWithInertGas(grs);

			PlaySound();
			Notice.Send("Operator needed", "Ready to load new iron and desiccant.");

			List<IAliquot> toDelete = new List<IAliquot>();
			grs.ForEach(gr =>
			{
				toDelete.Add(gr.Aliquot);
				gr.ServiceComplete();
			});

			toDelete.ForEach(a =>
			{
				if (a?.Sample is ISample s)
				{
					s.Aliquots.Remove(a);
					a.Name = null;          // remove the aliquot from the NamedObject Dictionary.
					if (s.AliquotsCount < 1)
						s.Name = null;      // remove the sample from the NamedObject Dictionary.
				}
			});
		}

		protected virtual bool AnyUnderTemp(List<IGraphiteReactor> grs, int targetTemp)
		{
			foreach (var gr in grs)
				if (gr.SampleTemperature < targetTemp)
					return true;
			return false;
		}

		protected virtual void PreconditionGRs()
		{
			var grs = GraphiteReactors.FindAll(gr => gr.State == GraphiteReactor.States.WaitPrep);
			if (grs.Count < 1)
			{
				Notice.Send("Nothing to do", "No reactors are awaiting preparation.", Notice.Type.Tell);
				return;
			}

			// close grs that aren't awaiting prep
			foreach (var gr in GraphiteReactors.Except(grs))
				gr.Close();

			var count = grs.Count;
			ProcessStep.Start($"Calibrate GR {"manometer".Plurality(count)} and {"volume".Plurality(count)}");

			// on the first flush, get the sizes
			ProcessSubStep.Start("Evacuate graphite reactors");
			GM.Isolate();
			grs.ForEach(gr => gr.Open());
			GM.Evacuate(OkPressure);
			WaitForStablePressure(OkPressure);        // this might be excessive
			ProcessSubStep.End();
			grs.ForEach(gr =>
			{
				ProcessSubStep.Start($"Zero {gr.Manometer.Name}");
				gr.Manometer.ZeroNow();
				while (gr.Manometer.Zeroing) Wait();
				gr.Close();
				ProcessSubStep.End();
			});

			var gs = InertGasSupply(GM);
			gs.Admit(PressureOverAtm);
			GM.Isolate();
			WaitSeconds(30);
			foreach (var gr in grs)
			{
				ProcessStep.Start($"Measure {gr.Name} volume");
				WaitSeconds(10);
				var p0 = GM.Manometer.WaitForAverage(30);
				var gmMilliLiters = GM.CurrentVolume(true);
				gr.Open();
				WaitSeconds(10);
				gr.Close();
				WaitSeconds(10);
				var p1 = GM.Manometer.WaitForAverage(30);
				gr.Open();

				ProcessSubStep.Start($"Calibrate {gr.Manometer.Name}");
				// TODO: make this safe and move it into AIVoltmeter
				var offset = gr.Manometer.Conversion.Operations[0];
				var v = offset.Execute((gr.Manometer as AIVoltmeter).Voltage);
				var gain = gr.Manometer.Conversion.Operations[1] as Arithmetic;
				gain.Operand = p1 / v;
				ProcessSubStep.End();

				gr.MilliLiters = gmMilliLiters * (p0 / p1 - 1);
				gr.Size = gr.MilliLiters < 2.0 ? GraphiteReactor.Sizes.Small : GraphiteReactor.Sizes.Standard;
				ProcessStep.End();
			}
			GM.Evacuate(OkPressure);
			ProcessStep.End();

			ProcessStep.Start("Evacuate & Flush GRs with inert gas");
			Flush(GM, 2);
			WaitForPressure(OkPressure);
			ProcessStep.End();

			ProcessStep.Start("Start Heating Fe");
			grs.ForEach(gr =>
			{
				gr.Open();
				gr.TurnOn(IronPreconditioningTemperature);
			});
			ProcessStep.End();

			int targetTemp = IronPreconditioningTemperature - IronPreconditioningTemperatureCushion;
			ProcessStep.Start("Wait for GRs to reach " + targetTemp.ToString() + " °C.");
			while (AnyUnderTemp(grs, targetTemp)) Wait();
			ProcessStep.End();

			if (IronPreconditionH2Pressure > 0)
			{
				ProcessStep.Start("Admit H2 into GRs");
				GM.IsolateFromVacuum();
				GasSupply("H2", GM).FlowPressurize(IronPreconditionH2Pressure);
				grs.ForEach(gr => gr.Close());
				ProcessStep.End();
			}

			ProcessStep.Start("Precondition iron for " + MinutesString(IronPreconditioningMinutes));
			if (IronPreconditionH2Pressure > 0)
			{
				GM.Evacuate(OkPressure);
				OpenLine();
			}
			WaitRemaining(IronPreconditioningMinutes);
			ProcessStep.End();

			if (IronPreconditionH2Pressure > 0)
			{
				ProcessStep.Start("Evacuate GRs");
				GM.Isolate();
				CloseAllGRs();
				grs.ForEach(gr => { gr.Heater.TurnOff(); gr.Open(); });
				GM.Evacuate(OkPressure);
				ProcessStep.End();

				ProcessStep.Start("Flush GRs with inert gas");
				Flush(GM, 3);
				ProcessStep.End();
			}
			else
			{
				grs.ForEach(gr => { gr.Heater.TurnOff(); gr.Open(); });
			}

			grs.ForEach(gr => gr.PreparationComplete());

			OpenLine();
			Alert("Operator Needed", "Graphite reactor preparation complete");
		}

		protected virtual void PrepareIPsForCollection() => 
			PrepareIPsForCollection(null);

		protected virtual void PrepareIPsForCollection(List<IInletPort> ips = null)
        {
			if (ips == null)
				ips = InletPorts.FindAll(ip => ip.State == LinePort.States.Loaded);
			else
				ips = ips.FindAll(ip => ip.State == LinePort.States.Loaded);

			if (ips.Count < 1) return;

			// close ips that aren't awaiting prep
			foreach (var ip in InletPorts.Except(ips))
				ip.Close();

			ProcessStep.Start("Evacuate & Flush IPs with inert gas");

			IM.Isolate();
			ips.ForEach(ip => ip.Open());
			IM.Evacuate(OkPressure);
			WaitForStablePressure(OkPressure);        // this might be excessive

			Flush(IM, 3);
			WaitForPressure(CleanPressure);

			ProcessStep.End();

			ips.ForEach(ip => ip.Close());

			ProcessStep.Start("Release the samples");
			var msg = "Release the samples at the following ports:";
			ips.ForEach(ip => msg += $"\r\n\t{ip?.Sample?.LabId} at {ip.Name}");

			Alert("Operator needed", "Release the prepared samples");
			Notice.Send("Operator needed", msg + "\r\n" +
				"Press Ok to continue", Notice.Type.Request);
			ProcessStep.End();

			ips.ForEach(ip => ip.State = LinePort.States.Prepared);

			//OpenLine();
		}


		protected virtual void ChangeSulfurFe()
		{
			var grs = GraphiteReactors.FindAll(gr =>
				IsSulfurTrap(gr) && gr.State == GraphiteReactor.States.WaitService);

			if (grs.Count < 1)
			{
				Notice.Send("Nothing to do", "No sulfur traps are awaiting service.", Notice.Type.Tell);
				return;
			}

			PressurizeGRsWithInertGas(grs);

			PlaySound();
			Notice.Send("Operator needed",
				"Replace iron in sulfur traps." + "\r\n" +
				"Press Ok to continue");

			// assume the Fe has been replaced

			ProcessStep.Start("Evacuate sulfur traps");
			GM.Isolate();
			grs.ForEach(gr => gr.Open());
			GM.Evacuate(OkPressure);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			Flush(GM, 3);
			ProcessStep.End();

			grs.ForEach(gr => gr.PreparationComplete());

			OpenLine();
		}

		#endregion GR service

		#endregion GR operations

		#endregion Support and general purpose functions

		#region Vacuum System

		protected virtual void Evacuate() => VacuumSystem.Evacuate();
		protected virtual void Evacuate(double pressure) => VacuumSystem.Evacuate(pressure);

		protected virtual void EvacuateIP()
		{
			if (!IpIm(out ISection im)) return;
			im.OpenAndEvacuate(OkPressure, InletPort);
		}

		#endregion Vacuum System

		#region Joining and isolating sections

		protected virtual void OpenLine() => OverrideNeeded();

		#endregion Joining and isolating sections

		#region Running samples

		public override void RunProcess(string processToRun)
		{
			EnsureProcessStartConditions();
			if (processToRun == "Run samples")
				RunSamples();
			//else if (processToRun == "Run all samples")
			//	RunAllSamples();
			else
				base.RunProcess(processToRun);
		}

		/// <summary>
		/// These conditions are assumed at the start of a process. 
		/// Sometimes they are changed during a process, which should always
		/// restore them to the "normal" state before or on completion. However,
		/// if the process is abnormally interrupted (e.g. aborted) they can 
		/// be left in the incorrect state.
		/// </summary>
		protected virtual void EnsureProcessStartConditions()
		{
			VacuumSystem.AutoManometer = true;
			//GasSupplies.Values.ToList().ForEach(gs => gs.ShutOff());
		}

		protected virtual void RunSample()
		{
			if (Sample == null)
			{
				if (InletPort != null)
					Notice.Send("Process Error",
					   $"{InletPort.Name} does not contain a sample.",
					   Notice.Type.Tell);
				else
					Notice.Send("Process Error",
					   $"No sample to run.",
					   Notice.Type.Tell);
				return;
			}

			if (InletPort != null && InletPort.State > LinePort.States.Prepared)
			{
				Notice.Send("Process Error",
					$"{InletPort.Name} is not ready to run.",
					Notice.Type.Tell);
				return;
			}

			if (LNManifolds.Values.All(x => x.SupplyEmpty))
			{
				double liters = LNManifolds.Values.Sum(x => x.Liters.Value);
				if (Notice.Send(
						"System Alert!",
						$"There might not be enough LN! ({liters:0.0} L)\r\n" +
							"Press OK to proceed anyway, or Cancel to abort.",
						Notice.Type.Warn).Text != "Ok")
					return;
			}

			if (!EnoughGRs())
			{
				Notice.Send("Process Error",
					"Unable to start process.\r\n" +
					"Not enough GRs are prepared!",
					Notice.Type.Tell);
				return;
			}

			if (!(ProcessSequences[Sample.Process] is ProcessSequence ps))
			{
				Notice.Send("Process Error",
					$"No such Process Sequence: '{Sample.Process}' ({Sample.Name} at {InletPort.Name} needs it.)",
					Notice.Type.Tell);
				return;
			}

			SampleLog.WriteLine("");
			SampleLog.Record(
				$"Start Process:\t{Sample.Process}\r\n" +
				$"\t{Sample.LabId}\t{Sample.Milligrams:0.0000}\tmg\r\n" +
				$"\t{Sample.AliquotsCount}\t{"aliquot".Plurality(Sample.AliquotsCount)}");

			base.RunProcess(Sample.Process);
		}

		//TODO revisit implementing this to run a sample from clicking on the InletPort?
		//protected virtual void RunSelectedSample() =>
		//	RunSamples(new List<IInletPort>() { SelectedInletPort });

		protected virtual void RunSamples(bool all)
		{
			if (!(SelectSamples?.Invoke(all) is List<IInletPort> ips))
				ips = new List<IInletPort>();

			RunSamples(ips);
		}

		protected virtual void RunSamples() =>
			RunSamples(false);

		protected virtual void RunAllSamples() =>
			RunSamples(true);

		protected virtual void RunSamples(List<IInletPort> ips)
		{
			if (ips.Count < 1)
			{
				//Notice.Send("Process Error",
				//	"No InletPorts selected, or contain Samples that are ready to run.",
				//	Notice.Type.Tell);
				return;
			}

			ips.ForEach(ip =>
			{
				InletPort = ip;
				RunSample();
				while (base.Busy) Wait(1000);
			});
		}

		protected override void ProcessEnded()
		{
			string msg = (ProcessType == ProcessTypeCode.Sequence ? Sample.Process : ProcessToRun) + 
				$" process {(RunCompleted ? "complete" : "aborted")}";

			if (ProcessType == ProcessTypeCode.Sequence)
			SampleLog.Record(msg + "\r\n\t" + Sample.LabId);

			Alert("System Status", msg);
			base.ProcessEnded();
		}

		#endregion Running samples

		#region Sample loading and preparation

		protected virtual void AdmitDeadCO2(double ugc_targetSize)
		{
			var CO2 = GasSupply("CO2", MC);
			if (CO2 == null) return;

			ProcessStep.Start("Join && evacuate MC..VM");
			MC.Isolate();
			var aliquots = Sample?.AliquotsCount ?? 1;
			if (aliquots > 1)
				MC.Ports[0].Open();
			if (aliquots > 2)
				MC.Ports[1].Open();
			MC.Evacuate(CleanPressure);

			if (aliquots < 2)
				MC.Ports[0].Close();
			if (aliquots < 3)
				MC.Ports[1].Close();

			WaitForPressure(CleanPressure);
			ZeroMC();
			ProcessStep.End();

			ProcessStep.Start("Admit CO2 into the MC");
			CO2.Pressurize(ugc_targetSize);
			ProcessStep.End();

			ProcessSubStep.Start("Take measurement");
			WaitForMCStable();
			SampleLog.Record($"Admitted CO2:\t{ugCinMC:0.0}\tµgC\t={ugCinMC/GramsCarbonPerMole:0.00}\tµmolC\t(target was {ugc_targetSize}\tµgC)");
			ProcessSubStep.End();
		}

		protected virtual void AdmitDeadCO2()
			=> AdmitDeadCO2(Sample.Micrograms);

		protected virtual void AdmitSealedCO2IP()
		{
			if (!IpIm(out ISection im)) return;

			ProcessStep.Start($"Evacuate and flush {InletPort.Name}");
			im.ClosePortsExcept(InletPort);
			im.Isolate();
			InletPort.Open();
			im.Evacuate(OkPressure);
			WaitForStablePressure(OkPressure);
			Flush(im, 3);
			WaitForPressure(CleanPressure);
			ProcessStep.End();

			InletPort.Close();

			ProcessStep.Start("Release the sample");
			Alert("Operator Needed", $"Release sealed sample '{Sample.LabId}' at {InletPort.Name}.");
			Notice.Send("Operator needed",
				$"Release the sealed sample '{Sample.LabId}' at {InletPort.Name}.\r\n" +
				"Press Ok to continue");
			ProcessStep.End();
			InletPort.State = LinePort.States.Prepared;
		}

		/// <summary>
		/// prepare a carbonate sample for acidification
		/// </summary>
		protected virtual void PrepareCarbonateSample()
		{
			if (!IpIm(out ISection im)) return;

			LoadCarbonateSample();
			EvacuateIP();
			Flush(im, 3, InletPort);

			ProcessStep.Start($"Wait for pVM < {CleanPressure:0.0e0} Torr");
			WaitForPressure(CleanPressure);
			ProcessStep.End();
			Alert("Operator Needed", "Carbonate sample is evacuated");
		}

		protected virtual void LoadCarbonateSample()
		{
			if (!IpImInertGas(out ISection im, out IGasSupply gs)) return;

			ProcessStep.Start($"Provide positive He pressure at {InletPort.Name} needle");
			IM.ClosePorts();
			IM.Isolate();
			gs.Admit();
			gs.WaitForPressure(PressureOverAtm);
			InletPort.Open();
			Wait(5000);
			gs.WaitForPressure(PressureOverAtm);
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Remove previous sample or plug from IP needle");
			while (!IM.Manometer.IsFalling && ProcessStep.Elapsed.TotalSeconds < 10)
				Wait(); // wait up to 10 seconds for pIM clearly falling
			ProcessStep.End();

			ProcessStep.Start("Wait for stable He flow at IP needle");
			while (!IM.Manometer.IsStable)
				Wait();
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Load next sample vial or plug at IP needle");
			while (IM.Manometer.RateOfChange < IMPluggedPressureRateOfChange && ProcessStep.Elapsed.TotalSeconds < 20)
				Wait();
			if (IM.Manometer.RateOfChange > IMLoadedPressureRateOfChange)
				InletPort.State = LinePort.States.Loaded;
			else
				InletPort.State = LinePort.States.Complete;
			ProcessStep.End();

			InletPort.Close();
			gs.ShutOff();
		}

		protected virtual void Evacuate_d13CPort()
		{
			if (!Sample.Take_d13C) return;
			var port = d13CPort;
			if (port == null)
			{
				Warn("Sample Alert", 
					$"Can't find d13C port for Sample {Sample.LabId} from {InletPort?.Name}");
				return;
			}
			if (port.State == LinePort.States.Prepared) return;
			if (port.State != LinePort.States.Loaded)
			{
				Alert("Sample Alert", $"d13C port {port.Name} is not available.");
				Notice.Send("Sample Alert",
					$"d13C port {port.Name} is not available.\r\n" +
					"It may contain a prior d13C sample.",
					Notice.Type.Tell);
				return;
			}
			var manifold = Manifold(port);
			if (manifold == null)
			{
				Warn("Configuration Error", $"Can't find manifold Section for d13C port {port.Name}.");
				return;
			}
			ProcessStep.Start($"Prepare d13C port {port.Name}");
			manifold.ClosePorts();
			port.Open();
			manifold.OpenAndEvacuate(OkPressure);

			if (InertGasSupply(manifold) is GasSupply gs)
				Flush(manifold, 3, port);
			else
				Announce("Process Alert",
					$"Unable to flush {port.Name}. There is no inert gas supply for {manifold.Name}");

			manifold.Evacuate(OkPressure);
			port.State = LinePort.States.Prepared;
			ProcessStep.End();
		}

		#endregion Sample loading and preparation

		#region Sample collection, extraction and measurement

		#region Collect

		protected virtual void EmptyAndFreeze(ISection trap)
		{
			trap.OpenAndEvacuate(CleanPressure);
			WaitForStablePressure(CleanPressure);
			trap.Isolate();
			if (trap.VTColdfinger != null)
				trap.VTColdfinger.Freeze();
			else if(trap.Coldfinger != null)
				trap.Coldfinger.Freeze();
			else
				Pause("Operator needed", $"Put LN on {trap.Name}.");
		}

		protected virtual void FreezeVtt() => EmptyAndFreeze(VTT);

		protected virtual void IpFreezeToTrap(ISection trap)
		{
			if (trap == null)
			{
				Warn("Configuration error", $"Can't find Section {trap.Name}");
				return;
			}

			if (!IpIm(out ISection im)) return;
			var im_trap = FirstOrDefault<Section>(s => 
				s.Chambers?.First() == im.Chambers?.First() && 
				s.Chambers?.Last() == trap.Chambers?.Last());

			if (im_trap == null)
			{
				Warn("Configuration error", $"Can't find Section linking {im.Name} and {trap.Name}");
				return;
			}

			ProcessStep.Start($"Evacuate {im.Name}");
			im.Evacuate(CleanPressure);
			WaitForStablePressure(CleanPressure);
			ProcessStep.End();

			ProcessStep.Start($"Evacuate and Freeze {trap.Name}");
			EmptyAndFreeze(trap);

			ProcessSubStep.Start($"Wait for {trap.Name} temperature < {VttColdTemperature} °C");
			var vtc = trap.VTColdfinger;
			var ftc = trap.Coldfinger;
			while ((vtc?.Temperature ?? ftc.Temperature) > VttColdTemperature) Wait();
			ProcessSubStep.End();

			ProcessStep.End();

			ProcessStep.Start($"Freeze the CO2 from {InletPort.Name} in the {trap.Name}");
			im_trap.Close();
			InletPort.State = LinePort.States.InProcess;
			InletPort.Open();
			trap.Dirty = true;
			im_trap.Open();
			WaitMinutes(CollectionMinutes);
			InletPort.Close();
			InletPort.State = LinePort.States.Complete;
			ProcessStep.End();

			ProcessStep.Start($"Evacuate incondensable gases from {trap.Name}");
			trap.Evacuate(CleanPressure);
			trap.Isolate();
			ProcessStep.End();
		}


		protected virtual void Collect()
		{
			var FirstTrap = CT ?? VTT;
			if (FirstTrap.FlowValve == null || FirstTrap.Manometer == null)
				IpFreezeToTrap(FirstTrap);
			else
				FrozenBleed();
		}

		protected virtual void Bleed(ISection trap, double bleedPressure)
		{
			if (trap == null)
			{
				Warn("Process Error", $"Bleed trap cannot be null.");
				return;
			}
			if (trap.FlowManager == null)
			{
				Warn("Configuration Error", $"Bleed operation requires {trap.Name} to have a FlowManager.");
				return;
			}

			ProcessSubStep.Start($"Maintain {trap.Name} pressure near {bleedPressure:0.00} Torr");

			// disable ion gauge while low vacuum flow is expected
			var pVSWasAuto = VacuumSystem.AutoManometer;
			VacuumSystem.AutoManometer = false;
			VacuumSystem.DisableManometer();
			VacuumSystem.Evacuate();    // use low vacuum or high vacuum as needed

			trap.FlowManager.Start(bleedPressure);

			// Does anything else need to be happening now?
			DuringBleed?.Invoke();

			while (trap.FlowManager.Busy)
				Wait();

			VacuumSystem.AutoManometer = pVSWasAuto;

			ProcessSubStep.End();
		}

		// release the sample up to, but not including the first trap
		protected virtual void StartBleed()
		{
			if (!IpIm(out ISection im)) return;
			im.ClosePorts();
			im.Isolate();

			InletPort.State = LinePort.States.InProcess;

			// open all but the last valve to the first trap
			var vLast = InletPort.PathToFirstTrap.Last();
			InletPort.PathToFirstTrap.ForEach(v =>
			{
				if (v != vLast)
					v.OpenWait();
			});
			Wait(1000);
			SampleLog.Record($"\tBleed initial IM pressure:\t{im.Manometer:0}\tTorr");
		}

		protected virtual void FinishBleed() { }

		protected virtual void FrozenBleed()
		{
			if (FirstTrap == null)
			{
				Warn("Process Error", $"FrozenBleed(): FirstTrap cannot be null.");
				return;
			}
			var vtc = FirstTrap.VTColdfinger;
			var ftc = FirstTrap.Coldfinger;
			if (vtc == null && ftc == null)
			{
				Warn("Process Error", $"FrozenBleed() requires a Coldfinger or VTColdfinger.");
				return;
			}

			ProcessStep.Start("Bleed off incondensable gases and trap CO2");

			if (InletPort.PortType == HACS.Components.InletPort.Type.Combustion)
			{
				// Do not bleed to low pressure (< ~mTorr) while temperature is high (> ~450 C)
				// to avoid decomposing carbonates in organic samples.
				TurnOffCCFurnaces();
			}

			ProcessSubStep.Start($"Wait for {FirstTrap.Name} temperature < {VttColdTemperature} °C");
			if (vtc == null)
				ftc.Freeze();
			else
				vtc.Freeze();
			while ((vtc?.Temperature ?? ftc.Temperature) > VttColdTemperature) Wait();
			ProcessSubStep.End();

			// Connect the gas from the sample source all the way up to, 
			// but not including, the VTT
			StartBleed();

			ProcessSubStep.Start("Release incondensables");
			FirstTrap.FlowValve.Open();
			FirstTrap.Evacuate();
			while (FirstTrap.Manometer.IsRising) Wait();
			ProcessSubStep.End();

			// Release the gas into the VTT
			FirstTrap.FlowValve.Close();
			FirstTrap.Close();
			InletPort.PathToFirstTrap.Last().Open();
			FirstTrap.Dirty = true;

			// Control flow valve to maintain constant downstream pressure until flow valve is fully opened.
			SampleLog.Record($"Bleed target: {VttSampleBleedPressure} Torr");
			Bleed(FirstTrap, VttSampleBleedPressure);

			// Open flow bypass when conditions allow it without producing an excessive
			// downstream pressure spike. Then wait for the spike to be evacuated.
			ProcessSubStep.Start("Wait for remaining pressure to bleed down");
			while (IM.Pressure > VttFlowBypassPressure || FirstTrap.Manometer.RateOfChange < VttPressureFallingVerySlowlyRateOfChange)
				Wait();
			ProcessSubStep.End();
			FirstTrap.Open();
			while (FirstTrap.Pressure > VttNearEndOfBleedPressure)
				Wait();

			// Process-specific override to ensure entire sample is trapped
			// (does nothing by default).
			FinishBleed();

			// Close the Sample Source-to-VTT path
			InletPort.PathToFirstTrap.ForEach((v =>
			{
				ProcessSubStep.Start($"Waiting to close {v.Name}");
				Wait(5000);
				while (this.FirstTrap.Manometer.RateOfChange < VttPressureFallingVerySlowlyRateOfChange)     // Torr/sec
					Wait();
				v.CloseWait();
				ProcessSubStep.End();
			}));

			// Isolate the trap once the pressure has stabilized
			ProcessSubStep.Start($"Waiting to isolate {FirstTrap.Name} from vacuum");
			while (FirstTrap.Manometer.RateOfChange < VttPressureBarelyFallingRateOfChange)
				Wait();
			FirstTrap.IsolateFromVacuum();
			ProcessSubStep.End();

			ProcessStep.End();

			InletPort.State = LinePort.States.Complete;
		}

		#endregion Collect

		#region Extract

		protected virtual bool VTT_MCStable()
		{
			var delta = Math.Abs(Math.Max(VTT.Pressure, VTT.Manometer.Sensitivity) - Math.Max(MC.Pressure, MC.Manometer.Sensitivity));
			var tooMuch = 10 * (VTT.Manometer.Resolution + MC.Manometer.Resolution);
			var stable = delta < tooMuch && VTT.Manometer.IsStable && ugCinMC.IsStable;

			// TODO: Comment out this debugging message for release
			if (!stable)
				ProcessSubStep.CurrentStep.Description = $"d={delta:0.00}({tooMuch:0.00}) VTT={VTT.Manometer.RateOfChange.Value:0.000} MC={ugCinMC.RateOfChange.Value:0.00}";
			else
				ProcessSubStep.CurrentStep.Description = "Wait for VTT..MC pressure to stabilize";

			return stable;
		}

		protected virtual void WaitForVTT_MCStable()
		{
			ProcessSubStep.Start("Wait for stable VTT..MC pressure");
			Stopwatch sw = new Stopwatch();
			while (sw.Elapsed.TotalSeconds < 10)
			{
				if (VTT_MCStable())
				{
					if (!sw.IsRunning) sw.Restart();
					Wait();
				}
				else
				{
					sw.Reset();
					while (!VTT_MCStable()) Wait();
				}
			}
			ProcessSubStep.End();
		}

		protected virtual void ZeroVTT_MC()
		{
			ProcessSubStep.Start("Zero VTT and MC manometers");
			WaitForStablePressure(CleanPressure);
			while (!VTT_MCStable()) Wait();
			VTT.Manometer.ZeroNow();
			MC.Manometer.ZeroNow();
			while (VTT.Manometer.Zeroing || MC.Manometer.Zeroing) Wait();
			ProcessSubStep.End();
		}


		/// <summary>
		/// Pressurize VTT..MC with ~0.1 Torr inert gas
		/// </summary>
		protected virtual void PressurizeVTT_MC()
		{
			var gs = InertGasSupply(VTT_MC);
			if (gs == null) return;

			ProcessStep.Start("Zero MC and VTT pressure gauges");
			VTT.FlowValve?.Open();
			MC.OpenPorts();
			VTT_MC.OpenAndEvacuate(CleanPressure);
			MC.ClosePorts();
			ZeroVTT_MC();
			ProcessStep.End();

			ProcessStep.Start("Pressurize VTT..MC with minimal inert gas");
			gs.NormalizeFlow();
			gs.ShutOff();
			WaitForVTT_MCStable();
			ProcessStep.End();

			double tgt = 0.1;
			double ccVTT = VTT.MilliLiters;
			double ccVTT_MC = VTT_MC.MilliLiters;
			double ccMC = MC.MilliLiters;
			double ccVTT_Split = ccVTT_MC + Split.MilliLiters;

			ProcessStep.Start($"Reduce pVTT_MC to ~{tgt} Torr by discarding fractions");

			while (VTT.Pressure * ccVTT / ccVTT_MC > tgt)
			{
				// discard MC
				VTT.Isolate();
				MC.Evacuate(OkPressure);
				VTT_MC.Isolate();
				VTT_MC.Open();
				WaitForVTT_MCStable();
			}

			while (VTT.Pressure * ccVTT_MC / ccVTT_Split > tgt)
			{
				// discard Split
				Split.Isolate();
				MC_Split.Open();
				Wait(5000);
				MC_Split.Close();
				Split.Evacuate();
				WaitForVTT_MCStable();
			}
			ProcessStep.End();
		}

		protected virtual void ExtractAt(int targetTemp)
		{
			ProcessStep.Start($"Extract at {targetTemp:0} °C");
			SampleLog.Record($"\tExtraction target temperature:\t{targetTemp:0}\t°C");

			VTT_MC.Isolate();
			VTT_MC.Open();

			var vtc = VTT.VTColdfinger;
			var ftcMC = MC.Coldfinger;
			vtc.Regulate(targetTemp);
			ftcMC.FreezeWait();

			targetTemp -= 1;			// continue at 1 deg under
			ProcessSubStep.Start($"Wait for {VTT.Name} to reach {targetTemp:0} °C");
			while (vtc.Temperature < targetTemp) Wait();
			ProcessSubStep.End();

			// TODO: remove this magic number; need to know actual blanket pressure.
			// Must be higher than blanket pressure, due to temperature increase.
			// When relying on time instead of pressure to establish the end
			// of the transfer, the blanket is not needed.
			var tgt = 0.23; // Torr;
			ProcessSubStep.Start("Wait for sample extraction complete");
			while (VTT.Pressure > tgt || ProcessSubStep.Elapsed.TotalMinutes < ExtractionMinutes)
				Wait();
			ProcessSubStep.End();

			WaitForVTT_MCStable();      // assumes transfer is finished, or nearly so
			ftcMC.WaitForLNpeak();
			MC.Isolate();
			vtc.Standby();

			SampleLog.Record("\tCO2 equilibrium temperature:" +
				$"\t{CO2EqTable.Interpolate(MC.Pressure):0}\t°C");

			ProcessStep.End();
		}

		protected virtual double ExtractionPressure()
		{
			// Depends on which chambers are connected
			// During extraction, VTT..MC should be joined.
			var volVTT_MC = VTT.MilliLiters + MC.MilliLiters;
			double currentVolume = VTT.MilliLiters;
			if (VTT_MC.InternalValves.IsOpened())
				currentVolume += MC.MilliLiters;
			return VTT.Pressure * currentVolume / volVTT_MC;
		}

		// Extracts gases from the VTT to the MC at a base pressure
		// provided by a small charge of inert gas. The gas evolution
		// temperature is determined by adding the given offset,
		// dTCO2eq, to the CO2 equilibrium temperature for the base 
		// pressure.
		protected virtual void PressurizedExtract(int dTCO2eq)
		{
			double extractionPressure = ExtractionPressure();
			SampleLog.Record("\tExtraction base pressure:" +
				$"\t{extractionPressure:0.000}\tTorr");

			int tCO2eq = (int)CO2EqTable.Interpolate(extractionPressure);
			SampleLog.Record($"\tExpected CO2 equilibrium temperature:\t{tCO2eq:0}\t°C");

			ExtractAt(-140);
		}

		protected virtual void Extract()
		{
			MC.OpenAndEvacuateAll(CleanPressure);
			ZeroMC();
			MC.ClosePorts();
			MC.Isolate();

			PressurizeVTT_MC();
			PressurizedExtract(3);		// targets CO2
		}

		#endregion Extract

		#region Measure

		protected virtual void WaitForMCStable(int seconds)
		{
			ProcessSubStep.Start($"Wait for μgC in MC to stabilize for {SecondsString(seconds)}");
			ugCinMC.WaitForStable(seconds);
			ProcessSubStep.End();
		}

		protected virtual void WaitForMCStable() => WaitForMCStable(5);

		protected virtual void ZeroMC()
		{
			WaitForMCStable();
			ProcessSubStep.Start($"Zero {MC.Manometer.Name}");
			MC.Manometer.ZeroNow();
			while (MC.Manometer.Zeroing) Wait();
			ProcessSubStep.End();
		}

		/// <summary>
		/// Apportion the currently SelectedMicrogramsCarbon into aliquots
		/// based on the MC chamber and port volumes.
		/// </summary>
		/// <param name="sample"></param>
		/// <returns></returns>
		protected virtual void ApportionAliquots()
		{
			var ugC = Sample.SelectedMicrogramsCarbon;
			// if no aliquots were specified, create one
			if (Sample.AliquotsCount < 1) Sample.AliquotsCount = 1;
			var n = Sample.AliquotsCount;
			var v0 = MC.Chambers[0].MilliLiters;
			var vTotal = v0;
			if (n > 1)
			{
				var v1 = MC.Ports[0].MilliLiters;
				vTotal += v1;
				if (n > 2)
				{
					var v2 = MC.Ports[1].MilliLiters;
					vTotal += v2;
					Sample.Aliquots[2].MicrogramsCarbon = ugC * v2 / vTotal;
				}
				Sample.Aliquots[1].MicrogramsCarbon = ugC * v1 / vTotal;
			}
			Sample.Aliquots[0].MicrogramsCarbon = ugC * v0 / vTotal;
		}

		protected virtual void TakeMeasurement()
		{
			ProcessStep.Start("Take measurement");
			if (MC.Manometer.Pressure > 100)
			{
				ProcessSubStep.Start("Expand sample into MC+MCU");
				MC.Ports[0].Open();
				WaitSeconds(15);
				ProcessSubStep.End();
				if (MC.Manometer.Pressure > 100)
                {
					ProcessSubStep.Start("Expand sample into MC+MCU+MCL");
					MC.Ports[1].Open();
					WaitSeconds(15);
					ProcessSubStep.End();
				}
			}

			WaitForMCStable();

			// this is the measurement
			double ugC = ugCinMC.WaitForAverage(30);
			Sample.SelectedMicrogramsCarbon = ugC;
			ApportionAliquots();

			string yield = "";

			if (Sample.TotalMicrogramsCarbon == 0)	// first measurement
			{
				Sample.TotalMicrogramsCarbon = ugC;
				yield = $"\tYield:\t{100 * Sample.TotalMicrogramsCarbon / Sample.Micrograms:0.00}%";
			}

			SampleLog.Record(
				"Sample measurement:\r\n" +
				$"\t{Sample.LabId}\t{Sample.Milligrams:0.0000}\tmg\r\n" +
				$"\tCarbon:\t{ugC:0.0}\tµgC\t={ugC / GramsCarbonPerMole:0.00}\tµmolC{yield}"
			);

			ProcessStep.End();
		}


		protected virtual void Measure()
		{
			var ftcMC = MC.Coldfinger;

			ProcessStep.Start("Prepare to measure MC contents");
			MC.Isolate();

			if (ftcMC.IsActivelyCooling)
			{
				ProcessStep.Start("Release incondensables");

				MC.OpenPorts();
				ftcMC.RaiseLN();
				ftcMC.WaitForLNpeak();
				MC.JoinToVacuum();
				Evacuate(CleanPressure);

				ZeroMC();
				if (Sample.AliquotsCount < 3)
				{
					MC.Ports[1].Close();
					if (Sample.AliquotsCount < 2) MC.Ports[0].Close();
					Wait(5000);
				}
				ProcessStep.End();

				MC.Isolate();
			}

			if (!ftcMC.Thawed)
			{
				ProcessSubStep.Start("Bring MC to uniform temperature");
				ftcMC.Thaw();
				while (!ftcMC.Thawed)
					Wait();
				ProcessSubStep.End();
			}

			ProcessStep.End();

			ProcessStep.Start("Measure Sample");
			TakeMeasurement();
			ProcessStep.End();
		}


		protected virtual void DiscardSplit()
		{
			ProcessStep.Start("Discard Excess sample");
			while (Sample.Aliquots[0].MicrogramsCarbon > MaximumSampleMicrogramsCarbon)
			{
				ProcessSubStep.Start("Evacuate Split");
				Split.Evacuate(0);
				ProcessSubStep.End();

				ProcessSubStep.Start("Split sample");
				Split.IsolateFromVacuum();
				MC_Split.Open();
				Wait(5000);
				MC_Split.Close();
				ProcessSubStep.End();

				ProcessSubStep.Start("Discard split");
				Split.Evacuate(0);
				ProcessSubStep.End();

				SampleLog.Record(
					"Split discarded:\r\n" +
					$"\t{Sample.LabId}\t{Sample.Milligrams:0.0000}\tmg"
				);
				TakeMeasurement();
			}
			ProcessStep.End();
		}

		#endregion Measure

		#region Graphitize

		protected virtual void DivideAliquots() => MC.ClosePorts();

		// TODO: move this routine to the graphite reactor class
		protected virtual void TrapSulfur(IGraphiteReactor gr)
		{
			var ftc = gr.Coldfinger;
			var h = gr.Heater;

			ProcessStep.Start("Trap sulfur.");
			SampleLog.Record(
				$"Trap sulfur in {gr.Name} at {SulfurTrapTemperature} °C for {MinutesString(SulfurTrapMinutes)}");
			ftc.Thaw();
			gr.TurnOn(SulfurTrapTemperature);
			ProcessSubStep.Start($"Wait for {gr.Name} to reach sulfur trapping temperature (~{SulfurTrapTemperature} °C).");
			while (ftc.Temperature < 0 || gr.SampleTemperature < SulfurTrapTemperature - 5)
				Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Hold for " + MinutesString(SulfurTrapMinutes));
			Wait(SulfurTrapMinutes * 60000);
			ProcessSubStep.End();

			h.TurnOff();
			ProcessStep.End();
		}

		protected virtual void RemoveSulfur()
		{
			if (!Sample.SulfurSuspected) return;

			ProcessStep.Start("Remove sulfur.");

			IGraphiteReactor gr = NextSulfurTrap(PriorGR);
			PriorGR = gr.Name;
			gr.Reserve("sulfur");
			gr.State = GraphiteReactor.States.InProcess;

			TransferCO2FromMCToGR(gr, 0, true);
			TrapSulfur(gr);
			TransferCO2FromGRToMC(gr, false);

			gr.Aliquot.ResidualMeasured = true;	// prevent graphitization retry
			gr.State = GraphiteReactor.States.WaitService;

			ProcessStep.End();
			Measure();
		}


		protected virtual void Freeze(Aliquot aliquot)
		{
			if (aliquot == null) return;
			if (aliquot.Name.IsBlank())
			{
				aliquot.Name = NextGraphiteNumber.ToString();
				NextGraphiteNumber++;
			}

			var size = aliquot.MicrogramsCarbon <= SmallSampleMicrogramsCarbon ? 
				GraphiteReactor.Sizes.Small : 
				GraphiteReactor.Sizes.Standard;
			IGraphiteReactor gr = NextGR(PriorGR, size);
			if (gr == null)
			{
				Pause("Process exception!", 
					$"Can't find a suitable graphite reactor for this {aliquot.MicrogramsCarbon:0.0} µgC ({aliquot.MicromolesCarbon:0.00} µmol) aliquot.");
				return;
			}

			TransferCO2FromMCToGR(gr, aliquot.Sample.AliquotIndex(aliquot));
		}

		protected virtual double[] AdmitGasToPort(IGasSupply gs, double initialTargetPressure, IPort port)
		{
			if (!(gs?.FlowManager?.Meter is IMeter meter))
			{
				Warn("Process Error", $"AdmitGasToPort: {gs?.Name}.FlowManager.Meter is invalid.");
				return new double[] { double.NaN, double.NaN };
			}

			gs.Pressurize(initialTargetPressure);
			gs.IsolateFromVacuum();

			double pInitial = meter.WaitForAverage(60);
			if (port.Coldfinger?.IsActivelyCooling ?? false)
				port.Coldfinger.WaitForLNpeak();

			ProcessSubStep.Start($"Admit {gs.GasName} into {port.Name}");
			port.Open();
			Wait(10000);
			port.Close();
			ProcessSubStep.End();
			WaitSeconds(15);
			double pFinal = meter.WaitForAverage(60);
			return new double[] { pInitial, pFinal };
		}

		protected virtual void AddH2ToGR(IAliquot aliquot)
		{
			var gr = Find<IGraphiteReactor>(aliquot.GraphiteReactor);
			if (!GrGmH2(gr, out ISection gm, out IGasSupply H2)) return;

			double mL_GR = gr.MilliLiters;

			double nCO2 = aliquot.MicrogramsCarbon * CarbonAtomsPerMicrogram;  // number of CO2 particles in the aliquot
			double nH2target = H2_CO2GraphitizationRatio * nCO2;   // ideal number of H2 particles for the reaction

			// The pressure of nH2target in the frozen GR, where it will be denser.
			var targetFinalH2Pressure = Pressure(nH2target, mL_GR, gm.Temperature);
			// the small reactors don't seem to require the density adjustment.
			if (gr.Size == GraphiteReactor.Sizes.Standard)
				targetFinalH2Pressure *= H2DensityAdjustment;

			double nH2 = 0;
			double pH2ratio = 0;
			for (int i = 0; i < 3; ++i)     // up to three tries
			{
				var targetInitialH2Pressure = targetFinalH2Pressure + 
					Pressure(nH2target, gm.MilliLiters, gm.Temperature);

				// The GM pressure drifts a bit after the H2 is introduced, generally downward.
				// This value compensates for the consequent average error, which was about -4,
				// averaged over 14 samples in Feb-Mar 2018.
				// The compensation is bumped by a few more Torr to shift the variance in
				// target error toward the high side, as a slight excess of H2 is not 
				// deleterious, whereas a deficiency could be.
				double driftAndVarianceCompensation = 9;	// TODO this should be a setting

				gm.Isolate();
				var p = AdmitGasToPort(
					H2,
					targetInitialH2Pressure + driftAndVarianceCompensation, 
					gr);
				var pH2initial = p[0];
				var pH2final = p[1];

				// this is what we actually got
				nH2 += Particles(pH2initial - pH2final, gm.MilliLiters, gm.Temperature);
				aliquot.H2CO2PressureRatio = pH2ratio = nH2 / nCO2;

				double nExpectedResidual;
				if (pH2ratio > H2_CO2StoichiometricRatio)
					nExpectedResidual = nH2 - nCO2 * H2_CO2StoichiometricRatio;
				else
					nExpectedResidual = nCO2 - nH2 / H2_CO2StoichiometricRatio;

				aliquot.InitialGmH2Pressure = pH2initial;
				aliquot.FinalGmH2Pressure = pH2final;
				aliquot.ExpectedResidualPressure = TorrPerKelvin(nExpectedResidual, mL_GR);

				SampleLog.Record(
					$"GR hydrogen measurement:\r\n\t{Sample.LabId}\r\n\t" +
					$"Graphite {aliquot.Name}\t{aliquot.MicrogramsCarbon:0.0}\tµgC\t={aliquot.MicromolesCarbon:0.00}\tµmolC\t{aliquot.GraphiteReactor}\t" +
					$"pH2:CO2\t{pH2ratio:0.00}\t" +
					$"{aliquot.InitialGmH2Pressure:0} => {aliquot.FinalGmH2Pressure:0}\r\n\t" +
					$"expected residual:\t{aliquot.ExpectedResidualPressure:0.000}\tTorr/K"
					);

				if (pH2ratio >= H2_CO2StoichiometricRatio * 1.05)
					break;

				// try to add more H2
				targetFinalH2Pressure *= nH2target / nH2;
			}

			if (pH2ratio < H2_CO2StoichiometricRatio * 1.05)
			{
				Warn("Sample Alert", 
					$"Not enough H2 in {aliquot.GraphiteReactor}\r\nProcess paused.");
			}
		}

		protected virtual void GraphitizeAliquots()
		{
			DivideAliquots();
			foreach (Aliquot aliquot in Sample.Aliquots)
				Freeze(aliquot);

			GM.IsolateFromVacuum();

			foreach (Aliquot aliquot in Sample.Aliquots)
			{
				ProcessStep.Start("Graphitize aliquot " + aliquot.Name);
				AddH2ToGR(aliquot);
				Find<IGraphiteReactor>(aliquot.GraphiteReactor).Start();
				ProcessStep.End();
			}
			GM.OpenAndEvacuate();
		}


		#endregion Graphitize

		protected virtual void Clean(ISection section)
		{
			if (!section.Dirty) return;

			ProcessStep.Start($"Clean {section.Name}");
			var vtc = section.VTColdfinger;
			var gs = InertGasSupply(section);
			var flowClean = gs != null && section.FlowValve != null;

			if (flowClean)
			{
				ProcessSubStep.Start($"Pressurize {section.Name} with {gs.GasName}");
				gs.Admit(PressureOverAtm);
				section.Close();   // close any bypass valve
				gs.EvacuatePath();
				ProcessSubStep.End();
			}

			if (vtc != null)
			{
				ProcessSubStep.Start($"Warm {section.Name} to {vtc.CleanupTemperature} °C");
				vtc.Regulate(vtc.CleanupTemperature+2);
			}

			if (!flowClean) section.Open();
			section.Evacuate();

			if (vtc != null)
			{
				// start flow before too much water starts coming off
				while (vtc.Temperature < -5)
					Wait();
			}

			if (flowClean)
			{
				Bleed(section, VttCleanupPressure);
				section.Open();    // open any bypass valve
			}

			if (vtc != null)
			{
				while (vtc.Temperature < vtc.CleanupTemperature)
					Wait();
				vtc.Standby();
				ProcessSubStep.End();
			}

			WaitForPressure(OkPressure);
			section.Dirty = false;
			ProcessStep.End();
		}

		protected virtual void CollectEtc()
		{
			Collect();
			ExtractEtc();
		}

		protected virtual void BleedEtc()
		{
			FrozenBleed();
			ExtractEtc();
		}

		protected virtual void ExtractEtc()
		{
			Extract();
			MeasureEtc();
		}

		protected virtual void MeasureEtc()
		{
			Measure();
			DiscardSplit();
			RemoveSulfur();
			GraphitizeEtc();
		}

		protected virtual void GraphitizeEtc()
		{
			try
			{
				GraphitizeAliquots();
				OpenLine();
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		#endregion Sample extraction and measurement

		#region Transfer CO2 between chambers

		// No foolproofing. All sections and coldfingers must be defined,
		// and the combined section must be named as expected.
		// If fromSection doesn't have a Coldfinger or VTColdfinger, this method
		// assumes fromSection is thawed (i.e., if there is an LN dewar on
		// fromSection, it must be removed before calling this method).
		protected virtual void TransferCO2(ISection fromSection, ISection toSection)
		{
			var combinedSection = Find<Section>(fromSection.Name + "_" + toSection.Name)
				?? Find<Section>(toSection.Name + "_" + fromSection.Name);
			if (combinedSection == null)
				return;

			ProcessStep.Start($"Transfer CO2 from {fromSection.Name} to {toSection.Name}");

			ProcessSubStep.Start($"Empty and Freeze {toSection.Name}");
			EmptyAndFreeze(toSection);
			ProcessSubStep.End();

			if (fromSection.VTColdfinger != null)
				fromSection.VTColdfinger.Thaw();
 			else if (fromSection.Coldfinger != null)
				fromSection.Coldfinger.Thaw();

			ProcessSubStep.Start($"Join {toSection.Name} to {fromSection.Name}");
			combinedSection.Isolate();
			combinedSection.Open();
			ProcessSubStep.End();

			if (toSection.VTColdfinger != null)
			{
				ProcessSubStep.Start($"Wait for {toSection.Name} to freeze");
				while (!toSection.VTColdfinger.Frozen) Wait();
				ProcessSubStep.End();
				ProcessSubStep.Start($"Wait for {toSection.Name} temperature <= {VttColdTemperature} °C");
				while (toSection.VTColdfinger.Temperature > VttColdTemperature) Wait();
				ProcessSubStep.End();
			}			
			else if (toSection.Coldfinger != null)
			{
				ProcessSubStep.Start($"Wait for {toSection.Name} to freeze");
				while (!toSection.Coldfinger.Frozen) Wait();
				ProcessSubStep.End();
			}

			ProcessSubStep.Start("Wait for CO2 to start evolving.");
			while (fromSection.Coldfinger.Temperature < CO2EqTable.Interpolate(0.07)) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait for transfer complete.");
			WaitMinutes(CO2TransferMinutes);
			ProcessSubStep.End();

			if (toSection.VTColdfinger == null && toSection.Coldfinger == null)
			{
				Pause("Operator Needed", $"Raise {toSection.Name} LN one inch.\r\n" +
					"Press Ok to continue.");
				WaitSeconds(30);
			}

			ProcessSubStep.Start($"Isolate {toSection.Name}");
			toSection.Isolate();
			ProcessSubStep.End();

			ProcessStep.End();
		}

		protected virtual void TransferCO2FromMCToVTT() =>
			TransferCO2(MC, VTT);

		protected virtual void TransferCO2FromMCToStandardGR() =>
			TransferCO2FromMCToGR(NextGR(PriorGR), 0);

		protected virtual void TransferCO2FromMCToGR() =>
			TransferCO2FromMCToGR(NextGR(PriorGR, 
				ugCinMC <= SmallSampleMicrogramsCarbon ? 
					GraphiteReactor.Sizes.Small : 
					GraphiteReactor.Sizes.Standard), 0);

		protected virtual void TransferCO2FromMCToGR(IGraphiteReactor gr, int aliquotIndex = 0, bool skip_d13C = false)
		{
			if (gr == null) return;
			if (!GrGm(gr, out ISection gm)) return;
			var pathName = MC.Name + "_" + gm.Name;
			var mc_gm = Find<Section>(pathName);
			if (mc_gm == null)
			{
				Warn("Configuration error", $"Can't find Section {pathName}");
				return;
			}

			PriorGR = gr.Name;

			IAliquot aliquot = null;
			if (Sample != null && Sample.AliquotsCount > aliquotIndex)
			{
				aliquot = Sample.Aliquots[aliquotIndex];
				gr.Reserve(aliquot);
				aliquot.GraphiteReactor = gr.Name;
			}

			var take_d13C = !skip_d13C && (Sample?.Take_d13C ?? false) && aliquotIndex == 0;
			if (take_d13C && gr.Aliquot != null && gr.Aliquot.MicrogramsCarbon < MinimumUgCThatPermits_d13CSplit)
			{
				Warn("Process exception",
					$"d13C was requested but the sample ({gr.Aliquot.MicromolesCarbon} µmol) is too small.");
				take_d13C = false;
			}

			Id13CPort d13CPort = take_d13C ? Guess_d13CPort(Sample) : null;

			ProcessStep.Start("Evacuate paths to sample destinations");
			MC.Isolate();
			gm.ClosePortsExcept(gr);
			if (gr.IsOpened)
				gm.IsolateExcept(gm.PathToVacuum);
			else
			{
				gm.Isolate();
				gr.Open();
			}

			var toBeOpened = gm.PathToVacuum.SafeUnion(Split.PathToVacuum);
			if (d13CPort != null)
			{
				if (ShouldBeClosed(d13CPort))
				{
					Warn("Process Error",
						$"Need to take d13C, but {d13CPort.Name} is not available.");
				}
				else
				{
					toBeOpened = toBeOpened.SafeUnion(d13C.PathToVacuum);
					if (d13CM != null)
					{
						toBeOpened = toBeOpened.SafeUnion(d13CM.PathToVacuum);
						if (d13CPort.IsOpened)
							d13CM.IsolateExcept(toBeOpened);
						else
							d13CM.Isolate();
						d13CM.ClosePortsExcept(d13CPort);
					}
					else
					{
						if (d13CPort.IsOpened)
							d13C.IsolateExcept(toBeOpened);
						else
							d13C.Isolate();
					}
					d13CPort.Open();
				}
			}
			VacuumSystem.IsolateExcept(toBeOpened);
			toBeOpened.Open();
			Evacuate(CleanPressure);
			ProcessStep.End();

			ProcessStep.Start("Expand the sample");

			if (d13CPort != null)
			{
				if (d13CM == null)
					d13CPort.Close();
				else
					d13CM.Isolate();

				gr.Close();
			}

			mc_gm.IsolateFromVacuum();

			// release the sample
			var mcPort = aliquotIndex > 0 ? MC.Ports[aliquotIndex - 1] : null;
			mcPort?.Open();      // take it from from an MC port

			mc_gm.Open();

			ProcessStep.End();


			if (d13CPort != null)
			{
				ProcessStep.Start("Take d13C split");
				WaitSeconds(30);    // really doesn't take so long, but the digital filters can make it look like it does
				d13C.IsolateFrom(mc_gm);
				Sample.Micrograms_d13C = aliquot.MicrogramsCarbon * d13C.MilliLiters / (d13C.MilliLiters + mc_gm.MilliLiters);
				aliquot.MicrogramsCarbon -= Sample.Micrograms_d13C;
				d13C.JoinTo(d13CM); // does nothing if there is no d13CM
				d13CPort.Open();
				d13CPort.State = LinePort.States.InProcess;
				d13CPort.Aliquot = aliquot;
				d13CPort.Coldfinger.Freeze();
				ProcessStep.End();

				ProcessStep.Start($"Freeze sample into {gr.Name} and {d13CPort.Name}");
				gr.Open();
			}
			else
				ProcessStep.Start($"Freeze sample into {gr.Name}");

			var grCF = gr.Coldfinger;
			grCF.Freeze();

			var grDone = false;
			var d13CDone = d13CPort == null;
			ProcessSubStep.Start($"Wait for coldfinger{(d13CPort == null ? "" : "s")} to freeze");
			while (!grDone || !d13CDone)
			{
				Wait();
				if (!grDone) grDone = grCF.Frozen;
				if (!d13CDone) d13CDone = d13CPort.Coldfinger.Frozen;
			}
			ProcessSubStep.End();

			WaitMinutes(CO2TransferMinutes);

			grDone = false;
			d13CDone = d13CPort == null;
			ProcessSubStep.Start("Raise LN");
			grCF.Raise();
			if (d13CPort != null) d13CPort.Coldfinger.Raise();
			while (!grDone || !d13CDone)
			{
				Wait();
				if (!grDone) grDone = grCF.State == Coldfinger.States.Raised;
				if (!d13CDone) d13CDone = d13CPort.Coldfinger.State == Coldfinger.States.Raised;
			}
			ProcessSubStep.End();

			if (d13CPort != null)
            {
				var cf = d13CPort.Coldfinger;
				ProcessSubStep.Start($"Wait for {cf.Name} LN level to peak");
				while (!cf.LNValve.IsOpened) Wait();
				while (!cf.LNValve.IsClosed) Wait();
				WaitSeconds(5);
				d13CPort.Close();
				cf.Standby();
				d13CPort.State = LinePort.States.Complete;
				ProcessSubStep.End();
			}

			ProcessSubStep.Start($"Release incondensables from {gr.Name}");
			while (!grCF.LNValve.IsOpened) Wait();
			while (!grCF.LNValve.IsClosed) Wait();
			WaitSeconds(5);
			mc_gm.JoinToVacuum();
			WaitForPressure(0);
			gr.Close();
			mcPort?.Close();
			ProcessSubStep.End();

			ProcessStep.End();
		}




		protected virtual void TransferCO2FromGRToMC() =>
			TransferCO2FromGRToMC(Find<IGraphiteReactor>(PriorGR), true);

		protected virtual void TransferCO2FromGRToMC(IGraphiteReactor gr, bool firstFreezeGR)
		{
			if (!GrGm(gr, out ISection gm)) return;
			var pathName = MC.Name + "_" + gm.Name;
			var mc_gm = Find<Section>(pathName);
			if (mc_gm == null)
			{
				Warn("Configuration error", $"Can't find Section {pathName}");
				return;
			}

			var grCF = gr.Coldfinger;

			ProcessStep.Start($"Transfer CO2 from {gr.Name} to {MC.Name}.");

			if (firstFreezeGR)
			{
				gr.Close();		// it should be closed already
				grCF.Freeze();
			}

			mc_gm.OpenAndEvacuate(CleanPressure);
			MC.ClosePorts();

			if (firstFreezeGR)
			{
				grCF.RaiseLN();

				WaitMinutes(1);

				grCF.WaitForLNpeak();

				ProcessSubStep.Start("Evacuate incondensables.");
				mc_gm.OpenAndEvacuate(CleanPressure);
				gr.Open();
				WaitForPressure(CleanPressure);
				mc_gm.IsolateFromVacuum();
				ProcessSubStep.End();
			}
			else
			{
				mc_gm.IsolateFromVacuum();
				ProcessSubStep.Start($"Open the path from {gr.Name} to {MC.Name}");
				gr.Open();
				mc_gm.Open();
				ProcessSubStep.End();
			}

			if (grCF.Temperature < grCF.NearAirTemperature) grCF.Thaw();
			var mcCF = MC.Coldfinger;
			mcCF.FreezeWait();

			ProcessSubStep.Start($"Wait for sample to freeze into {MC.Name}");
			while (ProcessSubStep.Elapsed.TotalMinutes < CO2TransferMinutes)
				Wait();
			mcCF.RaiseLN();

			mc_gm.Close();
			gr.Close();
			ProcessSubStep.End();

			ProcessStep.End();
		}




		/// <summary>
		/// Transfer CO2 from the MC to the IP.
		/// </summary>
		protected virtual void TransferCO2FromMCToIP()
		{
			if (!IpIm(out ISection im)) return;

			var mc = MC.Coldfinger;

			// TODO:
			// Construct a section from im to whatever chamber is just before MC
			// This code assumes:
			//	FirstTrap is adjacent to im, 
			//	there are no chambers between FirstTrap and VTT,
			//	a single-chamber Section of the same name exists for every chamber involved
			//var chambers = im.Chambers;
			//chambers = chambers.Union(FirstTrap.Chambers).ToList();
			//chambers = chambers.Union(VTT_MC.Chambers).ToList();
			//chambers.Remove(Find<Chamber>("MC"));
			//Section section;
			//if (chambers.Count < 2)
			//	section = im as Section;
			//else
			//{
			//	section = Find<Section>(chambers[0].Name).Clone();
			//	chambers.RemoveAt(0);
			//	foreach (var c in chambers)
			//		section = Section.Combine(section, Find<Section>(c.Name));
			//}
			ISection section = im;	// works on WHOI-CEGS; replace with above, when working

			ProcessStep.Start($"Evacuate and join {InletPort.Name}_{VTT.Name}");
			im.ClosePortsExcept(InletPort);
			InletPort.Open();

			section.OpenAndEvacuate(CleanPressure);
			WaitForStablePressure(CleanPressure);
			ProcessStep.End();

			ProcessStep.Start($"Transfer CO2 from MC to {InletPort.Name}");
			Alert("Operator Needed", "Put LN on inlet port.");
			Notice.Send("Operator needed", "Almost ready for LN on inlet port.\r\n" +
				"Press Ok to continue, then raise LN onto inlet port tube");

			section.IsolateFromVacuum();
			VTT_MC.Open();
			if (!mc.Thawed)
				mc.Thaw();

			ProcessSubStep.Start($"Wait for {MC.Name} to warm up a bit.");
			while (mc.Temperature < -85)
				Wait();
			ProcessSubStep.End();

			WaitMinutes(CO2TransferMinutes);

			Alert("Operator Needed", "Raise inlet port LN.");
			Notice.Send("Operator needed", $"Raise {InletPort.Name} LN one inch.\r\n" +
				"Press Ok to continue.");

			WaitSeconds(30);

			InletPort.Close();
			ProcessStep.End();
		}



		/// <summary>
		/// Transfer CO2 from the MC to the IP via the VM.
		/// Useful when IM_MC contains a flow restriction.
		/// </summary>
		protected virtual void TransferCO2FromMCToIPViaVM()
		{
			ProcessStep.Start("Evacuate and join IM..Split via VM");
			EvacuateIP();
			IM.IsolateFromVacuum();
			Split.Evacuate();
			IM.JoinToVacuum();
			WaitForPressure(0);
			ProcessStep.End();

			ProcessStep.Start($"Transfer CO2 from MC to {InletPort.Name}");
			Alert("Operator Needed", "Put LN on inlet port.");
			Notice.Send("Operator needed", "Almost ready for LN on inlet port.\r\n" +
				"Press Ok to continue, then raise LN onto inlet port tube");

			VacuumSystem.Isolate();
			MC.JoinToVacuum();		// connects to VM; VacuumSystem state is not changed

			ProcessSubStep.Start($"Wait for CO2 to freeze into {InletPort.Name}");
			while (ProcessSubStep.Elapsed.TotalMinutes < 1 ||
					(ugCinMC > 0.5 || ugCinMC.RateOfChange < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 4)
				Wait();
			ProcessSubStep.End();

			Alert("Operator Needed", "Raise inlet port LN.");
			Notice.Send("Operator needed", $"Raise {InletPort.Name} LN one inch.\r\n" +
				"Press Ok to continue.");

			WaitSeconds(30);

			InletPort.Close();
			ProcessStep.End();
		}

		#endregion Transfer CO2 between chambers

		#endregion Process Management

		#region Chamber volume calibration routines

		/// <summary>
		/// Install the CalibratedKnownVolume chamber in place of the port used
		/// in the VolumeCalibration named 'MC'.
		/// Sets the value of MC.MilliLiters.
		/// </summary>
		protected virtual void CalibrateVolumeMC()
		{
			FindAll<VolumeCalibration>().FirstOrDefault(vol => vol.ExpansionVolumeIsKnown)?.Calibrate();
		}

		/// <summary>
		/// Make sure the normal MC ports are installed (and not the CalibratedKnownVolume).
		/// TODO: make sure the settings calibration order is preserved 
		/// (add SequenceIndex property to VolumeCalibration?)
		/// </summary>
		protected virtual void CalibrateAllVolumesFromMC()
		{
			VolumeCalibrations.Values.ToList().ForEach(vol =>
			{ if (!vol.ExpansionVolumeIsKnown) vol.Calibrate(); });
		}

		/// <summary>
		/// Measures a valve's headspace and "OpenedVolumeDelta" which
		/// is the volume added to two chambers joined by the valve when
		/// the valve is opened, as compared to the combined volumes
		/// of the two chambers when the valve is closed.
		/// </summary>
		protected virtual void MeasureValveVolumes()
		{
			SampleLog.Record($"MC\tMC+vH+vB\tMC+vH");
			for (int i = 1; i <= 5; ++i)
			{
				ProcessStep.Start($"Measure valve volumes (pass {i})");
				ProcessSubStep.Start($"Evacuate, admit gas");
				MC.Isolate();
				MC.OpenPorts();
				MC.Evacuate(0);
				MC.ClosePorts();
				var GasSupply = InertGasSupply(MC);
				GasSupply.Pressurize(95);
				ProcessSubStep.End();

				ProcessSubStep.Start($"get p0");
				WaitSeconds(15);
				MC.Manometer.WaitForStable(5);
				var p0 = MC.Manometer.WaitForAverage(30) / (MC.Temperature + ZeroDegreesC);
				ProcessSubStep.End();

				// When compared to p2, p1 reveals the volume
				// difference between the valve's Opened and Closed
				// positions.
				ProcessSubStep.Start($"get p1");
				MC.Ports[1].Open();
				WaitSeconds(15);
				MC.Manometer.WaitForStable(5);
				var p1 = MC.Manometer.WaitForAverage(30) / (MC.Temperature + ZeroDegreesC);
				ProcessSubStep.End();

				// When compared to p0, p2 reveals the valve's
				// downstream headspace, if the downstream port
				// is plugged flush with the fitting shoulder.
				ProcessSubStep.Start($"get p2");
				MC.Ports[1].Close();
				WaitSeconds(15);
				MC.Manometer.WaitForStable(5);
				var p2 = MC.Manometer.WaitForAverage(30) / (MC.Temperature + ZeroDegreesC);
				SampleLog.Record($"{p0:0.00000}\t{p1:0.00000}\t{p2:0.00000}");
				ProcessSubStep.End();
				ProcessStep.End();
			}
		}

		#endregion Chamber volume calibration routines

		#region Other calibrations

		// all of the listed grs need must be on the same manifold
		protected virtual void CalibrateGRH2(List<IGraphiteReactor> grs)
		{
			grs.ForEach(gr => 
			{
				if (!gr.Prepared) 
				{
					Pause("Error", "CalibrateGRH2() requires all of the listed grs to be Prepared");
					return;
				}
			});

			ProcessStep.Start("Freeze graphite reactors");
			grs.ForEach(gr => gr.Coldfinger.Raise());
			while (grs.Any(gr => !gr.Coldfinger.Frozen))
				Wait();
			ProcessStep.End();

			SampleLog.WriteLine();
			SampleLog.Record("H2DensityRatio test");
			SampleLog.Record("GR\tpInitial\tpFinal\tpNormalized\tpRatio");

			GrGmH2(grs[0], out ISection gm, out IGasSupply gs);

			gm.ClosePorts();
			gm.Isolate();
			for (int pH2 = 850; pH2 > 10; pH2 /= 2)
			{
				grs.ForEach(gr => gr.Open());
				gm.Evacuate(CleanPressure);
				gm.ClosePorts();

				gs.Pressurize(pH2);
				gs.IsolateFromVacuum();
				grs.ForEach(gr =>
                {
					var pInitial = gm.Manometer.WaitForAverage(60);
					gr.Coldfinger.WaitForLNpeak();
					gr.Open();
					WaitSeconds(10);
					gr.Close();
					WaitSeconds(15);
					var pFinal = gm.Manometer.WaitForAverage(60);

					// pFinal is the pressure in the cold GR with n particles of H2,
					// whereas p would be the pressure if the GR were at the GM temperature.
					// densityAdjustment = pFinal / p
					var n = Particles(pInitial - pFinal, gm.MilliLiters, gm.Temperature);
					var p = Pressure(n, gr.MilliLiters, gm.Temperature);
					// The above uses the measured, GR-specific volume. To avoid errors,
					// this procedure should only be performed if the Fe and perchlorate 
					// tubes have never been altered since the GR volumes were measured.
					SampleLog.Record($"{gr.Name}\t{pInitial:0.00}\t{pFinal:0.00}\t{p:0.00}\t{pFinal / p:0.000}");
				});
			}
			grs.ForEach(gr => gr.Coldfinger.Standby());
			OpenLine();
		}

		#endregion Other calibrations

		#region Test functions

		/// <summary>
		/// Transfer CO2 from MC to IP, 
		/// optionally add some carrier gas, 
		/// then Collect(), Extract() and Measure()
		/// </summary>
		protected virtual void CO2LoopMC_IP_MC()
		{
			FreezeVtt();
			TransferCO2FromMCToIP();
			admitCarrier?.Invoke();

			Alert("Operator Needed", "Thaw inlet port.");
			Notice.Send("Operator needed", 
				"Remove LN from inlet port and thaw the coldfinger.\r\n" +
				"Press Ok to continue");

			VacuumSystem.Evacuate();
			Collect();
			Extract();
			Measure();
		}
		

		/// <summary>
		/// Transfers CO2 from MC to VTT, then performs extract() and measure()
		/// </summary>
		protected virtual void CleanupCO2InMC()
		{
			TransferCO2FromMCToVTT();
			Extract();
			Measure();
		}

		/// <summary>
		/// Admit some carrier gas into the IM and join to the IP.
		/// </summary>
		protected virtual void AdmitIPPuffs()
		{
			IpIm(out ISection im);
			im.Evacuate(OkPressure);
			im.ClosePorts();

			var gs = Find<GasSupply>("O2." + im.Name);
			if (gs == null) gs = InertGasSupply(im);

			for (int i = 0; i < amountOfCarrier; ++i)
			{
				gs.Admit();       // dunno, 1000-1500 Torr?
				Wait(1000);
				gs.ShutOff();
				VacuumSystem.Isolate();
				im.JoinToVacuum();      // one cycle might keep ~10% in the IM
				Wait(2000);
				im.Isolate();
			}

			InletPort.Open();
		}


		protected virtual void AdmitIPO2EvacuateIM()
		{
			AdmitIPO2();
			IM.Evacuate();
		}

		protected virtual void MeasureExtractEfficiency()
		{
			SampleLog.WriteLine("\r\n");
			SampleLog.Record("Measure VTT extract efficiency");
			SampleLog.Record($"Bleed target: {VttSampleBleedPressure} Torr");
			MeasureProcessEfficiency(CleanupCO2InMC);
		}


		protected Action admitCarrier;
		protected int amountOfCarrier;
		protected virtual void MeasureIpCollectionEfficiency()
		{
			SampleLog.WriteLine("\r\n");
			SampleLog.Record("IP collection efficiency test");
			if (FirstTrap.FlowManager == null)
				admitCarrier = null;
			else
			{ 
				admitCarrier = AdmitIPPuffs;
				amountOfCarrier = 3;    // puffs
			}
			MeasureProcessEfficiency(CO2LoopMC_IP_MC);
		}



		/// <summary>
		/// Simulates an organic extraction
		/// </summary>
		protected virtual void MeasureOrganicExtractEfficiency()
		{
			SampleLog.WriteLine("\r\n");
			SampleLog.Record("Organic bleed yield test");
			SampleLog.Record($"Bleed target: {VttSampleBleedPressure} Torr");
			admitCarrier = AdmitIPO2EvacuateIM;
			MeasureProcessEfficiency(CO2LoopMC_IP_MC);
		}




		/// <summary>
		/// Set the Sample LabId to the desired number of loops
		/// Set the Sample mass to the desired starting quantity
		/// If there is at least 80% of the desired starting quantity
		/// already in the measurement chamber, that will be used
		/// instead of admitting fresh gas.
		/// </summary>
		/// <param name="transferLoop">method to move sample from MC to somewhere else and back</param>
		protected virtual void MeasureProcessEfficiency(Action transferLoop)
		{
			ProcessStep.Start("Measure transfer efficiency");
			if (ugCinMC < Sample.Micrograms * 0.8)
			{
				Find<Chamber>("VTT").Dirty = false;  // keep cold
				OpenLine();
				WaitForPressure(CleanPressure);
				AdmitDeadCO2(Sample.Micrograms);
			}
			CleanupCO2InMC();

			int n; try { n = int.Parse(Sample.LabId); } catch { n = 1; }
			for (int repeats = 0; repeats < n; repeats++)
			{
				Sample.Micrograms = Sample.TotalMicrogramsCarbon;
				transferLoop();
			}
			ProcessStep.End();
		}


		// Discards the MC contents soon after they reach the 
		// temperature at which they were extracted.
		protected virtual void DiscardExtractedGases()
		{
			ProcessStep.Start("Discard extracted gases");
			var mcCF = MC.Coldfinger;
			mcCF.Thaw();
			ProcessSubStep.Start("Wait for MC coldfinger to thaw enough.");
			while (mcCF.Temperature <= VTT.VTColdfinger.Setpoint + 10) Wait();
			ProcessSubStep.End();
			mcCF.Standby();	// stop thawing to save time

			// record pressure
			SampleLog.Record($"\tPressure of pre-CO2 discarded gases:\t{MC.Pressure:0.000}\tTorr");

			VTT_MC.OpenAndEvacuate(OkPressure);
			VTT_MC.IsolateFromVacuum();
			ProcessStep.End();
		}

		protected virtual void StepExtract()
		{
			PressurizeVTT_MC();
			// The equilibrium temperature of HCl at pressures from ~(1e-5..1e1)
			// is about 14 degC or more colder than CO2 at the same pressure.
			PressurizedExtract(-13);		// targets HCl
			DiscardExtractedGases();
			PressurizedExtract(1);		// targets CO2
		}

		protected virtual void StepExtractionYieldTest()
		{
			Sample.LabId = "Step Extraction Yield Test";
			//admitDeadCO2(1000);
			Measure();

			TransferCO2FromMCToVTT();
			Extract();
			Measure();

			//transfer_CO2_MC_VTT();
			//step_extract();
			//VTT.Stop();
			//measure();
		}

		protected virtual void Test() { }

		#endregion Test functions
	}
}
