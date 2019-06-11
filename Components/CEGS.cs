using HACS.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class CEGS : ProcessManager
	{
		#region Component Implementation

		public static readonly new List<CEGS> List = new List<CEGS>();
		public static new CEGS Find(string name) { return List.Find(x => x?.Name == name); }

		protected virtual void PreConnect()
		{
			// HacsLog needs these early
			HacsLog.LogFolder = LogFolder;
			HacsLog.ArchiveFolder = ArchiveFolder;

			#region Data Logs
			VMPLog = HacsLog.Find("VMPLog");
			GRPLog = HacsLog.Find("GRPLog");
			FTCLog = HacsLog.Find("FTCLog");
			VLog = HacsLog.Find("VLog");
			TLog = HacsLog.Find("TLog");
			PLog = HacsLog.Find("PLog");
			AmbientLog = HacsLog.Find("AmbientLog");
			MCLog = HacsLog.Find("MCLog");
			VTTLog = HacsLog.Find("VTTLog");
			SampleLog = HacsLog.Find("SampleLog");
			EventLog = HacsLog.Find("EventLog");

			VMPLog.Update = logP_VMStatus;
			GRPLog.Update = logGRStatus;
			FTCLog.Update = logFTCStatus;
			TLog.Update = logTemperatureStatus;
			PLog.Update = logPressureStatus;
			AmbientLog.Update = logAmbientStatus;
			MCLog.Update = logMCStatus;
			VTTLog.Update = logVTTStatus;
			#endregion Data Logs
		}

		protected virtual void Connect()
		{
			#region LabJackDaqs
			// the list is used
			#endregion LabJackDaqs

			#region Chamber volumes
			mL_VP = Chamber.Find("VP").MilliLiters;
			mL_GM = Chamber.Find("GM").MilliLiters;
			mL_d13C = Chamber.Find("d13C").MilliLiters;
			mL_MC = Chamber.Find("MC").MilliLiters;
			mL_Split = Chamber.Find("Split").MilliLiters;
			mL_MCU = Chamber.Find("MCU").MilliLiters;
			mL_MCL = Chamber.Find("MCL").MilliLiters;
			mL_VTT = Chamber.Find("VTT").MilliLiters;
			mL_CuAg = Chamber.Find("CuAg").MilliLiters;

			// This average is used instead of the individual values, because the
			// variation is mostly due to loading.
			mL_GR =
				(Chamber.Find("GR1").MilliLiters +
				Chamber.Find("GR2").MilliLiters +
				Chamber.Find("GR3").MilliLiters +
				Chamber.Find("GR4").MilliLiters +
				Chamber.Find("GR5").MilliLiters +
				Chamber.Find("GR6").MilliLiters) / 6;
			#endregion Chamber volumes

			#region Meters
			m_p_MC = Meter.Find("m_p_MC");
			m_p_VTT = Meter.Find("m_p_VTT");
			m_p_IM = Meter.Find("m_p_IM");
			m_p_GM = Meter.Find("m_p_GM");
			m_v_LN_supply = Meter.Find("m_v_LN_supply");
			m_p_Ambient = Meter.Find("m_p_Ambient");
			m_V_5VPower = Meter.Find("m_V_5VPower");
			m_t_MC = Meter.Find("m_t_MC");
			m_t_Ambient = Meter.Find("m_t_Ambient");
			m_V_5VMainsDetect = Meter.Find("m_V_5VMainsDetect");
			#endregion Meters

			#region DigitalOutputs
			#endregion DigitalOutputs

			#region VSPressures
			#endregion VSPressures

			#region VacuumSystems
			VacuumSystem = VacuumSystem.Find("VacuumSystem");
			#endregion VacuumSystems

			#region Sections
			// VacuumManifold sections
			IM = Section.Find("IM");
			VTT = Section.Find("VTT");
			CuAg_d13C = Section.Find("CuAg_d13C");  // includes CuAg, MC, MCU, MCL, Split, GM, d13C Chambers

			CuAg = Section.Find("CuAg");
			MC = Section.Find("MC");
			Split = Section.Find("Split");
			GM = Section.Find("GM");
			d13C = Section.Find("d13C");

			IM_VTT = Section.Find("IM_VTT");

			VTT_CuAg = Section.Find("VTT_CuAg");
			VTT_MC = Section.Find("VTT_MC");

			CuAg_MC = Section.Find("CuAg_MC");

			MC_GM = Section.Find("MC_GM");
			GM_d13C = Section.Find("GM_d13C");

			#endregion Sections

			#region ActuatorControllers
			#endregion ActuatorControllers

			#region ThermalControllers
			#endregion ThermalControllers

			#region Valves
			v_MC_MCU = Valve.Find("v_MC_MCU");
			v_MC_MCL = Valve.Find("v_MC_MCL");
			v_MC_Split = Valve.Find("v_MC_Split");
			v_VTT_flow = RS232Valve.Find("v_VTT_flow");
			#endregion Valves

			#region Heaters
			h_CuAg = Heater.Find("h_CuAg");
			h_CC_Q = Heater.Find("h_CC_Q");
			h_CC_S = Heater.Find("h_CC_S");
			h_CC_S2 = Heater.Find("h_CC_S2");
			#endregion Heaters

			#region TempSensors
			ts_GM = TempSensor.Find("ts_GM");
			ts_tabletop = TempSensor.Find("ts_tabletop");
			#endregion TempSensors

			#region SwitchBanks
			#endregion SwitchBanks

			#region OnOffDevices
			fan_pump_HV = OnOffDevice.Find("fan_pump_HV");
			fan_IP = OnOffDevice.Find("fan_IP");
			#endregion OnOffDevices

			#region LnManifolds
			LnManifold = LnManifold.Find("LnManifold");
			#endregion LnManifolds

			#region FlowManagers
			VttFlowManager = FlowManager.Find("VttFlowManager");
			#endregion FlowManagers

			#region FTCs
			ftc_VTC = FTColdfinger.Find("ftc_VTC");
			ftc_CuAg = FTColdfinger.Find("ftc_CuAg");
			ftc_MC = FTColdfinger.Find("ftc_MC");
			ftc_GR = new FTColdfinger[6];
			ftc_GR[0] = FTColdfinger.Find("ftc_GR1");
			ftc_GR[1] = FTColdfinger.Find("ftc_GR2");
			ftc_GR[2] = FTColdfinger.Find("ftc_GR3");
			ftc_GR[3] = FTColdfinger.Find("ftc_GR4");
			ftc_GR[4] = FTColdfinger.Find("ftc_GR5");
			ftc_GR[5] = FTColdfinger.Find("ftc_GR6");
			ftc_VP = FTColdfinger.Find("ftc_VP");
			#endregion FTCs

			#region VTColdfingers
			VTC = VTColdfinger.Find("VTC");
			#endregion VTColdfingers

			#region GRs
			GR = new GraphiteReactor[6];
			GR[0] = GraphiteReactor.Find("GR1");
			GR[1] = GraphiteReactor.Find("GR2");
			GR[2] = GraphiteReactor.Find("GR3");
			GR[3] = GraphiteReactor.Find("GR4");
			GR[4] = GraphiteReactor.Find("GR5");
			GR[5] = GraphiteReactor.Find("GR6");
			#endregion GRs

			#region MFCs
			#endregion MFCs

			#region LinePorts
			IP = LinePort.Find("IP");
			VP = LinePort.Find("VP");
			#endregion LinePorts

			#region DynamicQuantities
			ugCinMC = DynamicQuantity.Find("ugCinMC");
			#endregion DynamicQuantities

			#region GasSupplies
			gs_O2_IM = GasSupply.Find("gs_O2_IM");
			gs_He_IM = GasSupply.Find("gs_He_IM");
			gs_He_VTT = GasSupply.Find("gs_He_VTT");
			gs_He_GM = GasSupply.Find("gs_He_GM");
			gs_He_MC = GasSupply.Find("gs_He_MC");
			gs_He_MC_GM = GasSupply.Find("gs_He_MC_GM");
			gs_He_IM_GM = GasSupply.Find("gs_He_IM_GM");
			gs_CO2_MC = GasSupply.Find("gs_CO2_MC");
			gs_H2_GM = GasSupply.Find("gs_H2_GM");
			gs_He_VTT_MC = GasSupply.Find("gs_He_VTT_MC");
			#endregion GasSupplies
		}

		protected virtual void PostConnect()
		{
			AlertManager.EventLog = EventLog;

			m_p_MC.StateChanged += updateSampleMeasurement;

			// Note: CEGS itself is not in ProcessManagers
			ProcessManagers?.ForEach(c =>
			{
				c.ShowProcessSequenceEditor = ShowProcessSequenceEditor;
				c.EventLog = EventLog;
				c.ProcessSubStep = ProcessSubStep;
			});

			VacuumSystem.ProcessStep = ProcessSubStep;

			GasSupplies?.ForEach(c =>
			{
				c.Alert = Alert;
				if (c.Destination.VacuumSystem == VacuumSystem)
					c.ProcessStep = ProcessSubStep;
			});

			VolumeCalibrations?.ForEach(c =>
			{
				c.ProcessStep = ProcessStep;
				c.ProcessSubStep = ProcessSubStep;
				c.OpenLine = openLine;
				c.pressure_ok = pressure_ok;
				c.Measurement = ugCinMC;
				c.Log = SampleLog;
			});

			calculateDerivedConstants();
		}

		protected virtual void PostStart()
		{
			SystemRunTime.Start();
			startThreads();
			Started = true;
			// For Debugging or switching settings file type
			//SaveSettings("settings.xml");
			//SaveSettings("settings.json");
			//SaveSettings("startup.xml");
			//SaveSettings("startup.json");
		}

		protected virtual void PreStop()
		{
			try
			{
				EventLog.Record("System shutting down");

				ShuttingDown = true;
				// TODO: make sure all the periodic CEGS activities are terminated 
				// before continuing (process, watchdogs, Update, etc.)
				// They should be triggered to quickly kill themselves on 
				// ShuttingDown == true
				while (lowPriorityThread != null && lowPriorityThread.IsAlive)
					Thread.Sleep(1);

				// TODO: replace updateTimer with thread WaitOne+timeout instead?
				updateTimer.Dispose();

				closeLNValves();

				// Note: controllers of multiple devices should shutdown in Stop()
				// The devices they control should have their shutdown states effected in PreStop()

				// TODO: this test is too specialized
				// add a property to the OnOffDevice: 
				//		("ShutdownState"?) <== could add to valves and heaters, too (see above for why)
				//		(public enum ShutdownStates { On, Off, DontChange })
				// and have the Component implement PreStop() to ensure the condition
				foreach (var d in OnOffDevices)
				{
					if (d != fan_pump_HV && d != fan_IP)
						d.TurnOff();
				}

			}
			catch (Exception e)
			{
				Notice.Send(e.ToString());
			}
		}

		protected virtual void PostStop()
		{
			SerialPortMonitor.Stop();
		}

		public CEGS()
		{
			List.Add(this);

			OnPreConnect += PreConnect;
			OnConnect += Connect;
			OnPostConnect += PostConnect;
			OnPostStart += PostStart;
			OnPreStop += PreStop;
			OnPostStop += PostStop;
		}

		#endregion Component Implementation

		#region System configuration

		#region Component lists
		[JsonProperty]public List<ActuatorController> ActuatorControllers { get; set; }
		[JsonProperty]public List<AnalogOutput> AnalogOutputs { get; set; }
		[JsonProperty]public List<Chamber> Chambers { get; set; }
		[JsonProperty]public List<DigitalOutput> DigitalOutputs { get; set; }
		[JsonProperty]public List<DynamicQuantity> DynamicQuantities { get; set; }
		[JsonProperty]public List<FlowManager> FlowManagers { get; set; }
		[JsonProperty]public List<FTColdfinger> FTColdfingers { get; set; }
		[JsonProperty]public List<GasSupply> GasSupplies { get; set; }
		[JsonProperty]public List<GraphiteReactor> GraphiteReactors { get; set; }
		[JsonProperty]public List<HacsLog> HacsLogs { get; set; }
		[JsonProperty]public List<Heater> Heaters { get; set; }
		[JsonProperty]public List<LabJackDaq> LabJackDaqs { get; set; }
		[JsonProperty]public List<LinePort> LinePorts { get; set; }
		[JsonProperty]public List<LnManifold> LnManifolds { get; set; }
		[JsonProperty]public List<MassFlowController> MassFlowControllers { get; set; }
		[JsonProperty]public List<Meter> Meters { get; set; }
		[JsonProperty]public List<OnOffDevice> OnOffDevices { get; set; }
		[JsonProperty]public List<Port> Ports { get; set; }
		[JsonProperty]public List<ProcessManager> ProcessManagers { get; set; }
		[JsonProperty]public List<Sample> Samples { get; set; }
		[JsonProperty]public List<SampleSource> SampleSources { get; set; }
		[JsonProperty]public List<Section> Sections { get; set; }
		[JsonProperty]public List<SwitchBank> SwitchBanks { get; set; }
		[JsonProperty]public List<TempSensor> TempSensors { get; set; }
		[JsonProperty]public List<ThermalController> ThermalControllers { get; set; }
		[JsonProperty]public List<TubeFurnace> TubeFurnaces { get; set; }
		[JsonProperty]public List<VacuumSystem> VacuumSystems { get; set; }
        [JsonProperty]public List<CpwValve> CpwValves { get; set; }
        [JsonProperty]public List<RS232Valve> RS232Valves { get; set; }
        [JsonProperty]public List<PneumaticValve> PneumaticValves { get; set; }
        [JsonProperty]public List<VolumeCalibration> VolumeCalibrations { get; set; }
		[JsonProperty]public List<VSPressure> VSPressures { get; set; }
		[JsonProperty]public List<VTColdfinger> VTColdfingers { get; set; }

		#endregion Component lists

		#region HacsComponents
		public string LastAlertMessage => AlertManager.LastAlertMessage;

        #region Data Logs
        [XmlIgnore] public HacsLog VMPLog;
        [XmlIgnore] public HacsLog GRPLog;
        [XmlIgnore] public HacsLog FTCLog;
		[XmlIgnore] public HacsLog VLog;
		[XmlIgnore] public HacsLog TLog;
		[XmlIgnore] public HacsLog PLog;
		[XmlIgnore] public HacsLog AmbientLog;
		[XmlIgnore] public HacsLog MCLog;
		[XmlIgnore] public HacsLog VTTLog;
		[XmlIgnore] public HacsLog SampleLog;
		#endregion Data Logs

		#region LabJack DAQ
		#endregion LabJack DAQ

		#region Meters
		[XmlIgnore] public Meter m_p_MC;
		[XmlIgnore] public Meter m_p_VTT;
		[XmlIgnore] public Meter m_p_IM;
		[XmlIgnore] public Meter m_p_GM;
		[XmlIgnore] public Meter m_v_LN_supply;
		[XmlIgnore] public Meter m_t_MC;
		[XmlIgnore] public Meter m_V_5VPower;
		[XmlIgnore] public Meter m_p_Ambient;
		[XmlIgnore] public Meter m_t_Ambient;
		[XmlIgnore] public Meter m_V_5VMainsDetect;
		#endregion Meters

		#region Digital IO
		#endregion Digital IO

		#region Serial devices
		#endregion Serial devices

		#region Valves
		[XmlIgnore] public RS232Valve v_VTT_flow;
		[XmlIgnore] public IValve v_MC_MCU;
		[XmlIgnore] public IValve v_MC_MCL;
		[XmlIgnore] public IValve v_MC_Split;
        #endregion Valves;

        #region VSPressures
        #endregion VSPressures

        #region VacuumSystems
        [XmlIgnore] public VacuumSystem VacuumSystem;
        #endregion VacuumSystems

        #region Sections
        [XmlIgnore] public Section IM;
		[XmlIgnore] public Section VTT;
		[XmlIgnore] public Section CuAg_d13C;

		[XmlIgnore] public Section CuAg;
		[XmlIgnore] public Section MC;
		[XmlIgnore] public Section Split;
		[XmlIgnore] public Section GM;
		[XmlIgnore] public Section d13C;

		[XmlIgnore] public Section IM_VTT;
		[XmlIgnore] public Section VTT_CuAg;
		[XmlIgnore] public Section VTT_MC;
		[XmlIgnore] public Section CuAg_MC;
		[XmlIgnore] public Section MC_GM;
		[XmlIgnore] public Section GM_d13C;
		#endregion Sections

		#region Heaters
		[XmlIgnore] public Heater h_CuAg;
		[XmlIgnore] public Heater h_CC_Q;
		[XmlIgnore] public Heater h_CC_S;
		[XmlIgnore] public Heater h_CC_S2;
		#endregion Heaters

		#region Temperature Sensors
		[XmlIgnore] public TempSensor ts_GM;
		[XmlIgnore] public TempSensor ts_tabletop;
		#endregion Temperature Sensors

		#region Graphite Reactors
		[XmlIgnore] public GraphiteReactor[] GR;
		#endregion Graphite Reactors

		#region SwitchBanks
		#endregion SwitchBanks

		#region OnOffDevices
		[XmlIgnore] public OnOffDevice fan_pump_HV;
		[XmlIgnore] public OnOffDevice fan_IP;
		#endregion OnOffDevices

		#region LnManifolds
		[XmlIgnore] public LnManifold LnManifold;
		#endregion LnManifolds

		#region FlowManagers
		[XmlIgnore] public FlowManager VttFlowManager;
		#endregion FlowManagers

		#region Freeze-Thaw Coldfingers
		[XmlIgnore] public FTColdfinger ftc_VTC;
		[XmlIgnore] public FTColdfinger ftc_CuAg;
		[XmlIgnore] public FTColdfinger ftc_MC;
		[XmlIgnore] public FTColdfinger[] ftc_GR;
		[XmlIgnore] public FTColdfinger ftc_VP;
		#endregion Freeze-Thaw Coldfingers

		#region Variable Temperature Coldfingers
		[XmlIgnore] public VTColdfinger VTC;
		#endregion Variable Temperature Coldfingers

		#region Mass Flow Controllers
		#endregion Mass Flow Controllers

		#region Line Ports
		[XmlIgnore] public LinePort IP;  // Inlet Port
		[XmlIgnore] public LinePort VP;  // Vial Port
		#endregion Line Ports

		#region Dynamic Quantitites
		[XmlIgnore] public DynamicQuantity ugCinMC;
		#endregion Dynamic Quantities

		#region Gas Supplies
		[XmlIgnore] public GasSupply gs_O2_IM;
		//[XmlIgnore] public GasSupply gs_O2_MC;    // to be used for volume calibrations in the future
		[XmlIgnore] public GasSupply gs_He_IM;
		[XmlIgnore] public GasSupply gs_He_VTT;
		[XmlIgnore] public GasSupply gs_He_GM;
		[XmlIgnore] public GasSupply gs_He_MC;
		[XmlIgnore] public GasSupply gs_He_MC_GM;
		[XmlIgnore] public GasSupply gs_He_IM_GM;
		[XmlIgnore] public GasSupply gs_CO2_MC;
		[XmlIgnore] public GasSupply gs_H2_GM;
		[XmlIgnore] public GasSupply gs_He_VTT_MC;
		#endregion Gas Supplies

		#region Chamber Volumes
		double mL_VP;
		double mL_GM;
		double mL_d13C;
		double mL_MC;
		double mL_Split;
		double mL_MCU;
		double mL_MCL;
		double mL_VTT;
		double mL_CuAg;
		double mL_GR;
        #endregion Chamber Volumes

        #endregion HacsComponents

        #region Globals

        #region UI Communications
        [XmlIgnore] public Func<bool, bool> VerifySampleInfo;
        public void PlaySound() => Notice.Send("PlaySound", Notice.Type.Tell);

		#endregion UI Communications

		#region Logging

		[JsonProperty] public string LogFolder { get; set; }
		[JsonProperty] public string ArchiveFolder { get; set; }

		#endregion Logging

		#region System state & operations

		[JsonProperty] public bool EnableWatchdogs { get; set; }
		[JsonProperty] public bool EnableAutozero { get; set; }
		[JsonProperty] public string Last_GR { get; set; }
		[JsonProperty] public int Next_GraphiteNumber { get; set; }

		[XmlIgnore] public bool PowerFailed { get; set; }

		#endregion System state & operations

		[JsonProperty] public int CurrentSample { get; set; } = 0;   // Future proofing. Stays constant for now.
		[XmlIgnore] public Sample Sample
        {
            get { return Samples[CurrentSample]; }
			set { Samples[CurrentSample] = value; }
		}

		#endregion Globals

		#region Constants
		// the only way to alter constants is to edit settings file
		// derived constants should be tagged [XmlIgnore]

		#region Pressure Constants

		[JsonProperty] public double pressure_over_atm { get; set; }
		[JsonProperty] public double pressure_ok { get; set; }              // clean enough to join sections for drying
		[JsonProperty] public double pressure_clean { get; set; }           // clean enough to start a new sample

		[JsonProperty] public double pressure_VP_He_Initial { get; set; }
		[JsonProperty] public double pressure_VP_Error { get; set; }        // abs(pVP - pressure_over_atm) < this value is nominal

		[JsonProperty] public double pressure_IM_O2 { get; set; }
		[JsonProperty] public double pressure_VTT_bleed_sample { get; set; }
		[JsonProperty] public double pressure_VTT_bleed_cleaning { get; set; }
		[JsonProperty] public double pressure_VTT_near_end_of_bleed { get; set; }
		[JsonProperty] public double pressure_VTT_flow_bypass { get; set; } = 5;

		[JsonProperty] public double pressure_Fe_prep_H2 { get; set; }

		[XmlIgnore] public double pressure_foreline_empty;
		[XmlIgnore] public double pressure_max_backing;

		#endregion Pressure Constants

		#region Rate of Change Constants

		[JsonProperty] public double roc_pVTT_falling_very_slowly { get; set; }
		[JsonProperty] public double roc_pVTT_falling_barely { get; set; }

		[JsonProperty] public double roc_pIM_plugged { get; set; }
		[JsonProperty] public double roc_pIM_loaded { get; set; }

		#endregion Rate of Change Constants

		#region Temperature Constants
		[JsonProperty] public int temperature_room { get; set; }        // "standard" room temperature
		[JsonProperty] public int temperature_warm { get; set; }
		[JsonProperty] public int temperature_CO2_evolution { get; set; }
		[JsonProperty] public int temperature_CO2_collection_min { get; set; }
		[JsonProperty] public int temperature_FTC_frozen { get; set; }
		[JsonProperty] public int temperature_FTC_raised { get; set; }
		[JsonProperty] public int temperature_VTT_cold { get; set; }
		[JsonProperty] public int temperature_VTT_cleanup { get; set; }
		[JsonProperty] public int temperature_trap_sulfur { get; set; }

		[JsonProperty] public int temperature_Fe_prep { get; set; }
		[JsonProperty] public int temperature_Fe_prep_max_error { get; set; }

		#endregion

		#region Time Constants
		[JsonProperty] public int minutes_Fe_prep { get; set; }
		[JsonProperty] public int minutes_CC_Q_Warmup { get; set; }
		[JsonProperty] public int minutes_trap_sulfur { get; set; }
		[JsonProperty] public int seconds_FTC_raised { get; set; }
		[JsonProperty] public int seconds_flow_supply_purge { get; set; }
		[JsonProperty] public int milliseconds_power_down_max { get; set; }
		[JsonProperty] public int milliseconds_UpdateLoop_interval { get; set; }
		#endregion

		#region Sample Measurement Constants

		// fundamental constants
		[JsonProperty] public double L { get; set; }                // Avogadro's number (particles/mol)
		[JsonProperty] public double kB { get; set; }               // Boltzmann constant (Pa * m^3 / K)
		[JsonProperty] public double Pa { get; set; }               // Pascals (1/atm)
		[JsonProperty] public double Torr { get; set; }         // (1/atm)
		[JsonProperty] public double mL { get; set; }               // milliliters per liter
		[JsonProperty] public double m3 { get; set; }               // cubic meters per liter

		[JsonProperty] public double ZeroDegreesC { get; set; } // kelvins
		[JsonProperty] public double ugC_mol { get; set; }         // mass of carbon per mole, in micrograms,
																   // assuming standard isotopic composition

		[JsonProperty] public double H2_CO2_stoich { get; set; }    // stoichiometric
		[JsonProperty] public double H2_CO2 { get; set; }           // target H2:CO2 ratio for graphitization

		[JsonProperty] public double densityAdjustment { get; set; }   // pressure reduction due to higher density of H2 in GR coldfinger

		[JsonProperty] public int mass_small_sample { get; set; }
		[JsonProperty] public int mass_diluted_sample { get; set; }
		[JsonProperty] public int ugC_sample_max { get; set; }

		// kB using Torr and milliliters instead of pascals and cubic meters
		[XmlIgnore] public double kB_Torr_mL;
		[XmlIgnore] public double nC_ug;			// average number of carbon atoms per microgram

		// Useful volume ratios
		[XmlIgnore] public double rAMS;		// remaining for AMS after d13C is taken
		[XmlIgnore] public double rMCU;
		[XmlIgnore] public double rMCL;

		[XmlIgnore] public int ugC_d13C_max;

		#endregion Sample Measurement Constants

		[JsonProperty] public int LN_supply_min { get; set; }
		[JsonProperty] public double V_5VMainsDetect_min { get; set; }

		[XmlIgnore] LookupTable CO2EqTable = new LookupTable(@"CO2 eq.dat");


		#endregion Constants

		#endregion System configuration

		#region System elements not saved/restored in Settings

		// for requesting user interface services (presently not used)
		[XmlIgnore] public EventHandler RequestService;
		
		protected Action DuringBleed;

		#region Threading

		protected Timer updateTimer;

		// logging
		protected Thread systemLogThread;
		protected AutoResetEvent systemLogSignal = new AutoResetEvent(false);

		// low priority activity
		protected Thread lowPriorityThread;
		protected AutoResetEvent lowPrioritySignal = new AutoResetEvent(false);

		#endregion Threading

		// system conditions
		[XmlIgnore] public Stopwatch SystemRunTime { get; set; } = new Stopwatch();
		[XmlIgnore] public bool ShuttingDown = false;

		// process management
		public bool SampleIsRunning => ProcessType == ProcessTypes.Sequence && !RunCompleted;

		[XmlIgnore] protected Stopwatch PowerDownTimer = new Stopwatch();

		#endregion System elements not saved in/restored from Settings

		#region Startup and ShutDown


		protected void calculateDerivedConstants()
		{
			#region Sample measurement constants
			kB_Torr_mL = kB * Torr / Pa * mL / m3;
			nC_ug = L / ugC_mol;		// number of atoms per microgram of carbon (standard isotopic distribution)

			rAMS = 1 - mL_d13C / (mL_MC + mL_Split + mL_GM + mL_d13C);

			rMCU = mL_MCU / mL_MC;
			rMCL = mL_MCL / mL_MC;

			ugC_d13C_max = (int)((1 - rAMS) * (double)ugC_sample_max);
			#endregion Sample measurement constants
		}

		protected void startThreads()
		{
			//Alert("System Alert", "System Started");
			EventLog.Record("System Started");

			systemLogThread = new Thread(logSystemStatus)
			{
				Name = $"{Name} logSystemStatus",
				IsBackground = true
			};
			systemLogThread.Start();

			lowPriorityThread = new Thread(lowPriorityActivities)
			{
                Name = $"{Name} lowPriorityActivities",
                IsBackground = true
			};
			lowPriorityThread.Start();
			
			updateTimer = new Timer(UpdateTimerCallback, null, 0, milliseconds_UpdateLoop_interval);
			
			//updateThread = new Thread(UpdateLoop);
			//updateThread.Name = "updateThread";
			//updateThread.IsBackground = true;
			//updateThread.Start();
		}

		#endregion Startup and ShutDown

		#region elementary utility functions

		/// <summary>
		/// From (pv = nkt): n = pv / kt
		/// </summary>
		/// <param name="pressure">Torr</param>
		/// <param name="volume">mL</param>
		/// <param name="temperature">°C</param>
		/// <returns></returns>
		double nParticles(double pressure, double volume, double temperature)
		{ return pressure * volume / kB_Torr_mL / (ZeroDegreesC + temperature); }

		/// <summary>
		/// From (pv = nkt): p = nkt / v
		/// </summary>
		/// <param name="pressure">Torr</param>
		/// <param name="volume">mL</param>
		/// <param name="temperature">°C</param>
		/// <returns></returns>
		double pressure(double nParticles, double volume, double temperature)
		{ return nParticles * kB_Torr_mL * (ZeroDegreesC + temperature) / volume; }

		double TorrPerKelvin(double nParticles, double volume)
		{ return nParticles * kB_Torr_mL / volume; }

		// what the pressure in MC would be for a given temperature and amount of C in CO2
		//double pMC(double ugC, double tMC)
		//{ return ugC * (ZeroDegreesC + tMC) / k_ugC_MC; }

		double ugC(double pressure, double volume, double temperature)
		{ return nParticles(pressure, volume, temperature) / nC_ug; }

		#endregion elementary utility functions

		#region Periodic system activities & maintenance

		#region Logging
		// To be replaced by a database system in the future

		[XmlIgnore] public double old_VSPressure;
		[XmlIgnore] public string VMrpt = "";
		protected virtual void logP_VMStatus()
		{ logPvmStatus(VMPLog, VacuumSystem.Pressure, ref old_VSPressure, ref VMrpt); }

        protected virtual void logPvmStatus(HacsLog log, VSPressure p, ref double pPrior, ref string rptPrior)
        {
            string rpt = $"{log.TimeStamp()}{p.Pressure:0.00e0}\t{p.IG.Pressure:0.00e0}\t{p.m_HP.Value:0.00e0}";
            if (p.SignificantChange(pPrior, p) || log.ElapsedMilliseconds > 30000)
            {
                pPrior = p;
                if (rptPrior != "") log.WriteLine(rptPrior);
                log.WriteLine(rpt);
                rptPrior = "";
            }
            else
            {
                rptPrior = rpt;    // most recent value observed but not recorded
            }
        }

        [XmlIgnore] public double[] old_pGR = new double[6];
		[XmlIgnore] public double GRmin = 0.5;
		protected virtual void logGRStatus()
		{
			if (
				Math.Abs(old_pGR[0] - GR[0].Pressure) > GRmin ||
				Math.Abs(old_pGR[1] - GR[1].Pressure) > GRmin ||
				Math.Abs(old_pGR[2] - GR[2].Pressure) > GRmin ||
				Math.Abs(old_pGR[3] - GR[3].Pressure) > GRmin ||
				Math.Abs(old_pGR[4] - GR[4].Pressure) > GRmin ||
				Math.Abs(old_pGR[5] - GR[5].Pressure) > GRmin)
			{
				old_pGR[0] = GR[0].Pressure;
				old_pGR[1] = GR[1].Pressure;
				old_pGR[2] = GR[2].Pressure;
				old_pGR[3] = GR[3].Pressure;
				old_pGR[4] = GR[4].Pressure;
				old_pGR[5] = GR[5].Pressure;
                GRPLog.Record($"{old_pGR[0]:0.00}\t{old_pGR[1]:0.00}\t{old_pGR[2]:0.00}\t{old_pGR[3]:0.00}\t{old_pGR[4]:0.00}\t{old_pGR[5]:0.00}");
			}
		}

		protected virtual void logVTTStatus()
		{
			if (
				Math.Abs(old_pVTT - m_p_VTT) > 0.003 ||
				Math.Abs(old_tVTT - VTC.Temperature) >= 0.4 ||
				VTTLog.ElapsedMilliseconds > 60000
				)
			{
				old_pVTT = m_p_VTT;
				old_tVTT = VTC.Temperature;
                VTTLog.Record(
					$"{old_pVTT:0.000}\t{old_tVTT:0.0}" +
					$"\t{VTC.Coldfinger.Temperature:0.0}" +
					$"\t{VTC.WireTempSensor.Temperature:0.0}" +
					$"\t{VTC.TopTempSensor.Temperature:0.0}");
			}
		}

		[XmlIgnore] public double old_ugCinMC;
		[XmlIgnore] public double old_p_MC;
		protected virtual void logMCStatus()
		{
			if (Math.Abs(ugCinMC - old_ugCinMC) > 0.3 ||
				MCLog.ElapsedMilliseconds > 30000
				)
			{
				old_ugCinMC = ugCinMC;
                MCLog.Record($"{m_p_MC.Value:0.000}\t{m_t_MC.Value:0.00}\t{ugCinMC.Value:0.0}\t{ftc_MC.Temperature:0.0}");
			}
		}

		[XmlIgnore] public double old_pIM, old_pGM, old_pVTT, old_pForeline;
		protected virtual void logPressureStatus()
		{
			if (Math.Abs(old_pIM - m_p_IM) > 0.9 ||
				Math.Abs(old_pGM - m_p_GM) > 0.9 ||
				Math.Abs(old_pForeline - VacuumSystem.pForeline) > 0.1 ||
				PLog.ElapsedMilliseconds > 30000
				)
			{
				old_pIM = m_p_IM;
				old_pGM = m_p_GM;
				old_pForeline = VacuumSystem.pForeline;
                PLog.Record($"{m_p_Ambient.Value:0.00}\t{old_pIM:0}\t{old_pGM:0}\t{m_p_VTT:0.0000}\t{VacuumSystem.pForeline.Value:0.000}\t{VacuumSystem.Pressure:0.0e0}");
			}
		}

		[XmlIgnore] public double old_tCC, old_tVTT;
		[XmlIgnore] public double[] old_tGR = new double[6];
		protected virtual void logTemperatureStatus()
		{
			if (Math.Abs(old_tCC - h_CC_S.Temperature) > 0.2 ||
				Math.Abs(old_tGR[0] - GR[0].FeTemperature) > 1 ||
				Math.Abs(old_tGR[1] - GR[1].FeTemperature) > 1 ||
				Math.Abs(old_tGR[2] - GR[2].FeTemperature) > 1 ||
				Math.Abs(old_tGR[3] - GR[3].FeTemperature) > 1 ||
				Math.Abs(old_tGR[4] - GR[4].FeTemperature) > 1 ||
				Math.Abs(old_tGR[5] - GR[5].FeTemperature) > 1 ||
				TLog.ElapsedMilliseconds > 300000
				)
			{
				old_tCC = h_CC_S.Temperature;
				old_tGR[0] = GR[0].FeTemperature;
				old_tGR[1] = GR[1].FeTemperature;
				old_tGR[2] = GR[2].FeTemperature;
				old_tGR[3] = GR[3].FeTemperature;
				old_tGR[4] = GR[4].FeTemperature;
				old_tGR[5] = GR[5].FeTemperature;
                TLog.Record($"{old_tCC:0.0}\t{old_tGR[0]:0.0}\t{old_tGR[1]:0.0}\t{old_tGR[2]:0.0}\t{old_tGR[3]:0.0}\t{old_tGR[4]:0.0}\t{old_tGR[5]:0.0}");
			}
		}

		[XmlIgnore] public double[] old_tFtcGR = new double[6];
		[XmlIgnore] public double old_tFtcVtc, old_tFtcCuAg, old_tFtcMC, old_tFtcVP;
		[XmlIgnore] public double old_tLnManifold;
		[XmlIgnore] public double FTCmin = 2.0;
		protected virtual void logFTCStatus()
		{
			if (Math.Abs(old_tFtcGR[0] - ftc_GR[0].Temperature) > FTCmin ||
				Math.Abs(old_tFtcGR[1] - ftc_GR[1].Temperature) > FTCmin ||
				Math.Abs(old_tFtcGR[2] - ftc_GR[2].Temperature) > FTCmin ||
				Math.Abs(old_tFtcGR[3] - ftc_GR[3].Temperature) > FTCmin ||
				Math.Abs(old_tFtcGR[4] - ftc_GR[4].Temperature) > FTCmin ||
				Math.Abs(old_tFtcGR[5] - ftc_GR[5].Temperature) > FTCmin ||
				Math.Abs(old_tFtcVtc - ftc_VTC.Temperature) > FTCmin ||
				Math.Abs(old_tFtcCuAg - ftc_CuAg.Temperature) > FTCmin ||
				Math.Abs(old_tFtcMC - ftc_MC.Temperature) > FTCmin ||
				Math.Abs(old_tFtcVP - ftc_VP.Temperature) > FTCmin ||
				Math.Abs(old_tLnManifold - LnManifold.LevelSensor.Temperature) > 2 ||
				FTCLog.ElapsedMilliseconds > 300000
				)
			{
				old_tFtcGR[0] = ftc_GR[0].Temperature;
				old_tFtcGR[1] = ftc_GR[1].Temperature;
				old_tFtcGR[2] = ftc_GR[2].Temperature;
				old_tFtcGR[3] = ftc_GR[3].Temperature;
				old_tFtcGR[4] = ftc_GR[4].Temperature;
				old_tFtcGR[5] = ftc_GR[5].Temperature;
				old_tFtcVtc = ftc_VTC.Temperature;
				old_tFtcCuAg = ftc_CuAg.Temperature;
				old_tFtcMC = ftc_MC.Temperature;
				old_tFtcVP = ftc_VP.Temperature;
				old_tLnManifold = LnManifold.LevelSensor.Temperature;

                FTCLog.Record($"{old_tFtcGR[0]:0}\t{old_tFtcGR[1]:0}\t{old_tFtcGR[2]:0}\t{old_tFtcGR[3]:0}\t{old_tFtcGR[4]:0}\t{old_tFtcGR[5]:0}\t{old_tFtcVtc:0}\t{old_tFtcCuAg:0}\t{old_tFtcMC:0}\t{old_tFtcVP:0}\t{old_tLnManifold:0}");
			}
		}

		[XmlIgnore] public double old_tAmbient;
		protected virtual void logAmbientStatus()
		{
			if (Math.Abs(old_tAmbient - m_t_Ambient) >= 0.05 ||
				AmbientLog.ElapsedMilliseconds > 300000
				)
			{
				old_tAmbient = m_t_Ambient;
				AmbientLog.Record(
					$"{old_tAmbient:0.00}\t" +
                    $"{ts_GM.Temperature:0.0}\t" +
                    $"{ts_tabletop.Temperature:0.0}\t" +
                    $"{m_t_MC.Value:0.00}\t" +
                    $"{ThermalControllers[0].CJ0Temperature:0.0}\t" +
                    $"{ThermalControllers[1].CJ0Temperature:0.0}\t" +
                    $"{ThermalControllers[1].CJ1Temperature:0.0}\t" +
                    $"{m_p_Ambient.Value:0.00}\t" +
                    $"{(m_t_Ambient.RoC * 60):0.00}\t" + // degC per min
                    $"{LnManifold.LevelSensor.Temperature:0.0}\t" +
                    $"{m_v_LN_supply.Value:0.0}"
				);
                //$"{ThermalControllers[0].CJ0Temperature:0.0}\t" +
                //$"{ThermalControllers[1].CJ0Temperature:0.0}\t" +
                //$"{ThermalControllers[2].CJ0Temperature:0.0}\t" +
                //$"{ThermalControllers[2].CJ1Temperature:0.0}\t" +

            }
        }

		protected void logSystemStatus()
		{
			try
			{
				while (true)
				{
                    if (ShuttingDown) break;
                    if (systemLogSignal.WaitOne(500))
                    {
                        if (!Started) continue;
                        try { HacsLog.UpdateAll(); ; }
                        catch (Exception e) { Notice.Send(e.ToString()); }
                    }
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		#endregion Logging

		protected void UpdateTimerCallback(object state)
		{
			try
			{
				if (!ShuttingDown)
					Update();
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		// Depending on device conditions and current operations,
		// execution time for this function normally varies from
		// 3 or 4 microseconds up to about 5 milliseconds max.
		[XmlIgnore] public int msUpdateLoop = 0;
		[XmlIgnore] public bool daqOk = false;
		protected void Update()
		{
			#region DAQs
			daqOk = true;
			foreach (LabJackDaq lj in LabJackDaqs)
			{
				if (!lj.IsUp)
				{
					daqOk = false;
					if (!lj.IsStreaming)
						EventLog.LogParsimoniously(lj.Name + " is not streaming");
					else if (!lj.DataAcquired)
						EventLog.LogParsimoniously(lj.Name + ": waiting for stream to start");
					else if (lj.Error != null)
					{
						EventLog.LogParsimoniously(lj.ErrorMessage(lj.Error));
						lj.ClearError();
					}
				}
			}
			#endregion DAQs

			#region Power failure watchdog
			if (daqOk) HandlePowerFailure();
			#endregion Power failure watchdog

			#region 100 ms
			if (daqOk && msUpdateLoop % 100 == 0)
			{
				if (EnableAutozero) ZeroPressureGauges();

				#region watchdogs
				#endregion watchdogs
			}
			#endregion 100 ms

			#region 200 ms
			if (daqOk && msUpdateLoop % 200 == 0)
			{
				systemLogSignal.Set();  // logSystemStatus();
			}
			#endregion 200 ms

			#region 500 ms
			if (msUpdateLoop % 500 == 0)
			{
				if (daqOk && Started)
				{

					#region manage graphite reactors
					foreach (var gr in GraphiteReactors)
					{
						gr.Update();
						if (gr.isBusy)
						{
							// graphitization is in progress
							if (gr.FurnaceUnresponsive)
								Alert("System Warning!",
									$"{gr.Name} furnace is unresponsive.");

							if (gr.ReactionNotStarting)
								Alert("System Warning!",
									$"{gr.Name} reaction hasn't started.\r\n" +
										"Are the furnaces up?");

							if (gr.ReactionNotFinishing)
							{
								Alert("System Warning!",
									$"{gr.Name} reaction hasn't finished.");
								gr.State = GraphiteReactor.States.WaitFalling;  // reset the timer
							}

							// GR.State is "Stop" for exactly one GR.Update() cycle.
							if (gr.State == GraphiteReactor.States.Stop)
							{
								SampleLog.Record(
									"Graphitization complete:\r\n" +
									$"\tGraphite {gr.Contents}");
								if (busyGRs() == 1 && !SampleIsRunning)  // the 1 is this GR; "Stop" is still 'busy'
								{
									string msg = "Last graphite reactor finished.";
									if (readyGRs() < 1)
										msg += "\r\nGraphite reactors need service.";
									Alert("Operator Needed", msg);
								}
							}
						}
						else if (gr.State == GraphiteReactor.States.WaitService)
						{
							if (gr.Aliquot != null)
							{
								Aliquot a = gr.Aliquot;
								if (!a.ResidualMeasured)
								{
									double ambientTemperature = ts_GM.Temperature;
									if (Math.Abs(ambientTemperature - gr.FeTemperature) < 3 &&
										Math.Abs(ambientTemperature - gr.CFTemperature) < 3)
									{
										// residual is P/T (Torr/kelvin)
										a.Residual = gr.Pressure / (273.15 + ambientTemperature);

										SampleLog.Record(
											"Residual measurement:\r\n" +
											$"\tGraphite {a.Name}\t{a.Residual:0.000}\tTorr/K"
											);
										a.ResidualMeasured = true;

										if (a.Residual > 2 * a.ResidualExpected)
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

					#endregion manage graphite reactors

					#region manage LnManifold & FTCs
					if (LnManifold.OverflowDetected)
						Alert("System Alert!", "LN Containment Failure");

					if (LnManifold.SlowToFill)
						Alert("System Warning!", "LN Manifold is slow to fill!");

					FTColdfingers.ForEach(x => x.Update());

					VTC.Update();

					bool whatItShouldBe = LnManifold.KeepActive || 
						(FTColdfingers?.Any(x => x.State >= FTColdfinger.States.Freeze) ?? false);
					if (LnManifold.IsActive != whatItShouldBe)
						LnManifold.IsActive = whatItShouldBe;
					LnManifold.Update();


					#endregion manage LnManifold & FTCs

					#region manage IP fan
					if (h_CC_Q.IsOn || h_CC_S.Temperature >= temperature_warm)
					{
						if (!fan_IP.IsOn) fan_IP.TurnOn();
					}
					else
					{
						if (fan_IP.IsOn) fan_IP.TurnOff();
					}
					#endregion manage IP fan
					lowPrioritySignal.Set();
				}
				//lowPrioritySignal.Set();
			}
			#endregion 500 ms

			#region 1 minute
			if (daqOk && msUpdateLoop % 60000 == 0)
			{
				VLog.LogParsimoniously(m_V_5VPower.Value.ToString("0.000"));
			}
			#endregion 1 minute

			if (msUpdateLoop % 3600000 == 0) msUpdateLoop = 0;
			msUpdateLoop += milliseconds_UpdateLoop_interval;
		}

		protected virtual void HandlePowerFailure()
		{
			if (EnableWatchdogs && Started && !PowerFailed)
			{
				if (m_V_5VMainsDetect < V_5VMainsDetect_min)
				{
					if (!PowerDownTimer.IsRunning)
					{
						PowerDownTimer.Restart();
						Alert("System Warning", "Mains Power is down");
					}
					else if (PowerDownTimer.ElapsedMilliseconds > milliseconds_power_down_max)
					{
						PowerFailed = true;
						Alert("System Failure", "Mains Power Failure");
                        Notice.Send("System Failure", "Mains Power Failure", Utilities.Notice.Type.Tell);
						AbortRunningProcess();
						VacuumSystem.Isolate();
						VacuumSystem.IsolateManifold();
					}
				}
				else if (PowerDownTimer.IsRunning)
				{
					Alert("System Message", "Mains Power restored (down " + PowerDownTimer.ElapsedMilliseconds.ToString() + " ms)");
					PowerDownTimer.Stop();
					PowerDownTimer.Reset();
				}
			}
		}

		protected void lowPriorityActivities()
		{
			try
			{
				while (true)
				{
					if (ShuttingDown) break;
					if (lowPrioritySignal.WaitOne(500))
						SaveSettings();
				}
			}
			catch (Exception e)
			{ Notice.Send(e.ToString()); }
		}

		protected void updateSampleMeasurement()
		{
			ugCinMC.Update(ugC(m_p_MC, mL_MC, m_t_MC));
		}

		// value > Km * sensitivity ==> meter needs zeroing
		protected void ZeroIfNeeded(Meter m, double Km)
		{
			if (Math.Abs(m) >= Km * m.Sensitivity)
				m.ZeroNow();
		}

		protected virtual void ZeroPressureGauges()
		{
			// ensure baseline VM pressure & steady state
			if (VacuumSystem.BaselineTimer.Elapsed.TotalSeconds < 10)
				return;

			//ZeroIfNeeded(m_p_Foreline, 20);	// calibrate this zero manually with Turbo Pump evacuating foreline
			//write VacuumSystem code to do this

			if (VTT.PathToVacuum.IsOpened)
				ZeroIfNeeded(m_p_VTT, 5);

			if (MC.PathToVacuum.IsOpened)
				ZeroIfNeeded(m_p_MC, 5);

			if (IM.PathToVacuum.IsOpened)
				ZeroIfNeeded(m_p_IM, 10);

			if (GM.PathToVacuum.IsOpened)
			{
				ZeroIfNeeded(m_p_GM, 10);
				foreach (var gr in GraphiteReactors)
					if (gr.IsOpened)
						ZeroIfNeeded(gr.PressureMeter, 5);
			}
		}

		#endregion Periodic system activities & maintenance

		#region Alerts

		public void Alert(string subject, string message) => AlertManager.Alert(subject, message);

        #endregion Alerts

        #region Process Management

        public override void AbortRunningProcess()
        {
            if (VttFlowManager != null && VttFlowManager.Busy)
                VttFlowManager.Stop();
            base.AbortRunningProcess();
        }

        #region parameterized processes
        protected override void Combust(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
		{
			if (admitO2)
			{
				ProcessStep.Start($"Combust at {temperature} °C, {min_string(minutes)}");
				admitIPO2();
			}
			else
				ProcessStep.Start($"Heat IP: {temperature} °C, {min_string(minutes)}");

			if (temperature > 700)
				h_CC_S2.TurnOn();
			else
				h_CC_S2.TurnOff();

			if (h_CC_S.IsOn)
				h_CC_S.SetSetpoint(temperature);
			else
				h_CC_S.TurnOn(temperature);

			if (openLine)
			{
				IM.Evacuate(pressure_ok);
				this.openLine();
			}

			if (waitForSetpoint)
			{
				ProcessStep.End();

				int closeEnough = temperature - 20;
				ProcessStep.Start($"Wait for CC_S to reach {closeEnough} °C");
				while (h_CC_S.Temperature < closeEnough) Wait();
				ProcessStep.End();

				ProcessStep.Start($"Combust at {temperature} °C for {min_string(minutes)}.");
			}

			WaitRemaining(minutes);

			ProcessStep.End();
		}
		#endregion parameterized processes

		protected void waitForOperator()
		{
			Alert("Operator Needed", "Operator needed");
			Notice.Send("Operator needed",
				"Waiting for Operator.\r\n" +
				"Press Ok to continue");
		}

        #region Valve operation

        protected virtual void exerciseAllValves()
        {
            ProcessStep.Start("Exercise all opened valves");
			CpwValves?.ForEach(v => { if (v.IsOpened) v.Exercise(); });
            PneumaticValves?.ForEach(v => { if (v.IsOpened) v.Exercise(); });
            ProcessStep.End();
        }

		protected virtual void exerciseLNValves()
		{
			ProcessStep.Start("Exercise all LN Manifold valves");
			FTColdfingers?.ForEach(ftc => ftc.LNValve.Exercise());
			ProcessStep.End();
		}

		protected virtual void closeLNValves()
		{
			FTColdfingers?.ForEach(ftc => ftc.LNValve.Close());
		}

		protected virtual void calibrateFlowValves()
		{
			RS232Valves.ForEach(v => v.Calibrate());
		}

		#endregion Valve operation

		#region Support and general purpose functions

		protected void waitForVSPressure(double pressure)
		{
            VacuumSystem.WaitForPressure(pressure);
		}

		protected void turnOffCCFurnaces()
		{
			h_CC_Q?.TurnOff();
			h_CC_S?.TurnOff();
			h_CC_S2?.TurnOff();
		}

		protected void heatQuartz(bool openLine)
		{
			ProcessStep.Start($"Heat CC Quartz ({minutes_CC_Q_Warmup} minutes)");
			h_CC_Q.TurnOn();
			if (IP.State == LinePort.States.Loaded)
				IP.State = LinePort.States.InProcess;
			if (openLine) this.openLine();
			WaitRemaining(minutes_CC_Q_Warmup);

			if (Sample.NotifyCC_S)
			{
				Alert("Operator Needed", "Sample ready for furnace.");
				Notice.Send("Operator needed",
					"Remove coolant from CC and raise furnace.\r\n" +
					"Press Ok to continue");
			}
			ProcessStep.End();
		}

		protected void heatQuartzOpenLine()
		{
			heatQuartz(true);
		}

		protected void admit(GasSupply gasSupply, Port port, double pressure)
		{
			gasSupply.Destination.ClosePorts();
			gasSupply.Admit(pressure);

			if (gasSupply.Value < pressure)
			{
				Notice.Send("Process Alert!", 
					$"Couldn't admit {pressure} {gasSupply.Value.UnitSymbol} of {gasSupply.GasName} into {gasSupply.Destination.Name}");
				// TODO: throw exception? Retry? WaitForOperator? return immediately?
			}

			port.Open();
			Wait(2000);
			port.Close();
			Wait(5000);
		}

		protected void admitIPO2()
		{
			admit(gs_O2_IM, IP, pressure_IM_O2);
		}

		protected void admitIPHe(double IM_pressure)
		{
			admit(gs_He_IM, IP, IM_pressure);
		}

		protected void He_flush_IP()
		{
			gs_He_IM.Destination.ClosePorts();
			gs_He_IM.Flush(pressure_over_atm, 0.1, 3, IP);
		}

		protected void discardIPGases()
		{
			ProcessStep.Start("Discard gases at inlet port (IP)");
			IM.Isolate();
			IP.Open();
			Wait(10000);				// give some time to record a measurement
			IM.Evacuate(pressure_ok);	// allow for high pressure due to water
			ProcessStep.End();
		}

		protected void discard_MC_gases()
		{
			ProcessStep.Start("Discard sample from MC");
			MC.Evacuate();
			ProcessStep.End();
		}

		protected void clean_IM()
		{
			IM.Evacuate(pressure_clean);  // Ports are not altered
		}

		protected void purge_IP()
		{
			IP.State = LinePort.States.InProcess;
			evacuateIP();
			He_flush_IP();

			// Residual He is undesirable only to the extent that it
			// displaces O2. An O2 concentration of 99.99% -- more than
			// adequate for perfect combustion -- equates to 0.01% He.
			// The admitted O2 pressure always exceeds 1000 Torr; 
			// 0.01% of 1000 is 0.1 Torr.
			waitForVSPressure(0.1);
			IP.Close();
		}

		protected void freeze_VTT()
		{
			ProcessStep.Start("Freeze VTT");

			if (VTC.State != VTColdfinger.States.Freeze && VTC.State != VTColdfinger.States.Raise)
				VTC.Freeze();

			if (!IM.IsOpened)
			{
				IM_VTT.Close();
				VTT.Open();			// but (re-)open any bypass valve
			}

			if (!CuAg_d13C.IsOpened)
				VTT_CuAg.Close();

			if (!VTT.IsOpened)
				VTT.Evacuate(pressure_clean);
			else
				VacuumSystem.WaitForPressure(pressure_clean);

			VTT.Isolate();

			ProcessStep.End();
		}

		protected void clean_VTT()
		{
            ProcessStep.Start("Pressurize VTT with He");
			VTT.Close();		// in case there's a bypass valve; does nothing if not

			ProcessSubStep.Start("Calibrate VTT flow valve");
            v_VTT_flow.CloseWait();
            v_VTT_flow.Calibrate();
            ProcessSubStep.End();

            ProcessSubStep.Start("Admit He into the VTT");
			gs_He_VTT.Admit(pressure_over_atm);
			gs_He_VTT.EvacuatePath();
			ProcessSubStep.End();

            ProcessStep.End();

			ProcessStep.Start("Bleed He through warm VTT");
			VTC.Regulate(temperature_VTT_cleanup);
			VTT.Close();        // in case there's a bypass valve; does nothing if not
			VTT.Evacuate();
			while (VTC.Temperature < -5)		// start the flow before too much water starts coming off
				Wait();

			VttBleed(pressure_VTT_bleed_cleaning);

			while (VTC.Temperature < temperature_VTT_cleanup)
				Wait();
			ProcessStep.End();

			VTC.Stop();
			waitForVSPressure(pressure_ok);
			VTT.Open();        // in case there's a bypass valve; does nothing if not

			VTC.Dirty = false;
		}

		bool VTT_MC_stable()
		{
			double delta = Math.Abs(m_p_VTT - m_p_MC);
			double div = Math.Max(Math.Min(m_p_VTT, m_p_MC), 0.060);
			double unbalance = delta / div;
			//ProcessSubStep.CurrentStep.Description = $"unb={unbalance:0.00} VTT={m_p_VTT.RoC.Value:0.000} MC={ugCinMC.RoC.Value:0.00}";
			return unbalance < 0.35 && m_p_VTT.IsStable && ugCinMC.IsStable;
		}

		protected void wait_VTT_MC_stable()
		{
			ProcessSubStep.Start("Wait for VTT..MC pressure to stabilize");
			Stopwatch sw = new Stopwatch();
			while (sw.Elapsed.TotalSeconds < 15)
			{
				if (VTT_MC_stable())
				{
					if (!sw.IsRunning) sw.Restart();
					Wait();
				}
				else
				{
					sw.Reset();
					while (!VTT_MC_stable()) Wait();
				}
			}
			ProcessSubStep.End();
		}

		protected void zero_VTT_MC()
		{
			ProcessSubStep.Start("Wait for foreline pressure stability");
			while (VacuumSystem.BaselineTimer.Elapsed.TotalSeconds < 10) Wait();
			ProcessSubStep.End();
			m_p_VTT.ZeroNow();
			m_p_MC.ZeroNow();
			while (m_p_VTT.Zeroing || m_p_MC.Zeroing) Wait();
		}

		protected void waitForMCStable() => waitForMCStable(5);

		protected void waitForMCStable(int seconds)
		{
			ProcessSubStep.Start($"Wait for μgC in MC to stabilize for {ToUnitsString(seconds, "second")}");
			ugCinMC.WaitForStable(seconds);
			ProcessSubStep.End();
		}

		protected void zero_MC()
		{
			waitForMCStable();
			ProcessSubStep.Start("Zero MC manometer");
			m_p_MC.ZeroNow();
			while (m_p_MC.Zeroing) Wait();
			ProcessSubStep.End();
		}

		#region FTC operation

		protected void freeze(FTColdfinger ftc)
		{
			ftc.Freeze();

			ProcessSubStep.Start($"Wait for {ftc.Name} < {temperature_CO2_collection_min} °C");
			while (ftc.Temperature > temperature_CO2_collection_min) Wait();
			ProcessSubStep.End();
		}

		protected void raise_LN(FTColdfinger ftc)
		{
			ftc.Raise();
			ProcessSubStep.Start($"Wait for {ftc.Name} < {temperature_FTC_raised} °C");
			while (ftc.Temperature > temperature_FTC_raised) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start($"Wait {seconds_FTC_raised} seconds with LN raised");
			Wait(seconds_FTC_raised * 1000);
			ProcessSubStep.End();
		}

		protected void waitFor_LN_peak(FTColdfinger ftc)
		{
			IValve lnv = ftc.LNValve;
			ProcessSubStep.Start($"Wait until {ftc.Name} LN level is at max");
			while (!lnv.IsOpened) Wait();
			while (ftc.Temperature > ftc.Target || !lnv.IsClosed) Wait();
			ProcessSubStep.End();
			ProcessSubStep.Start("Wait for 5 seconds for equilibrium");
			Wait(5000);	// wait for equilibrium
			ProcessSubStep.End();
		}

		#endregion FTC operation

		#region GR operation

		protected void closeAllGRs()
		{
			closeAllGRs(null);
		}

		protected void closeAllGRs(GraphiteReactor exceptGR)
		{
			foreach (var gr in GraphiteReactors)
				if (gr != exceptGR)
					gr.Close();
		}

		int busyGRs()
		{
			return GraphiteReactors.Count(gr => gr.isBusy);
		}

		protected void openReadyGRs()
		{
			foreach (var gr in GraphiteReactors)
				if (gr.isReady)
					gr.Open();
		}

		protected void closeReadyGRs()
		{
			foreach (var gr in GraphiteReactors)
				if (gr.isReady)
					gr.Close();
		}

		#endregion GR operation

		#endregion Support and general purpose functions

		#region GR service

		protected void pressurize_GRs_with_He(List<GraphiteReactor> grs)
		{
			GM.ClosePorts();
			GM.Isolate();

			gs_He_GM.Admit();
			while (m_p_GM < m_p_Ambient + 20)
				Wait();

			grs.ForEach(gr => gr.Open());

			Wait(3000);
			while (m_p_GM < m_p_Ambient + 20)
				Wait();

			gs_He_GM.ShutOff(true);

			closeAllGRs();
		}


		protected void prepare_GRs_for_service()
		{
			var grs = new List<GraphiteReactor>();
			foreach (var gr in GraphiteReactors)
			{
				if (gr.State == GraphiteReactor.States.WaitService)
					grs.Add(gr);
				else if (gr.State == GraphiteReactor.States.Ready && gr.Contents == "sulfur")
					gr.ServiceComplete();
			}

			if (grs.Count < 1)
			{
                Notice.Send("Nothing to do", "No reactors are awaiting service.", Utilities.Notice.Type.Tell);
				return;
			}

			Notice.Send("Operator needed",
				"Mark Fe/C tubes with graphite IDs.\r\n" +
				"Press Ok to continue");

			pressurize_GRs_with_He(grs);

			PlaySound();
			Notice.Send("Operator needed", "Ready to load new iron and desiccant.");

			grs.ForEach(gr => gr.ServiceComplete());
		}

		protected void He_flush_GM(int n)
		{
			gs_He_GM.Flush(pressure_over_atm, 0.1, n);
			gs_He_GM.v_flow.Close();
		}


		bool anyUnderTemp(List<GraphiteReactor> grs, int targetTemp)
		{ 
			foreach (var gr in grs)
				if (gr.FeTemperature < targetTemp)
					return true;
			return false;
		}

		protected void precondition_GRs()
		{
			var grs = GraphiteReactors.FindAll(gr => gr.State == GraphiteReactor.States.WaitPrep);
			if (grs.Count < 1)
			{
                Notice.Send("Nothing to do", "No reactors are awaiting preparation.", Utilities.Notice.Type.Tell);
				return;
			}

			ProcessStep.Start("Evacuate GRs, start heating Fe");
			GM_d13C.Close();
			foreach (var gr in (GraphiteReactors.Except(grs)))
				gr.Close();

			GM.Isolate();
			grs.ForEach(gr => { gr.Open(); gr.Furnace.TurnOn(temperature_Fe_prep); });
			GM.Evacuate(pressure_ok);
			ProcessStep.End();

			int targetTemp = temperature_Fe_prep - temperature_Fe_prep_max_error;
			ProcessStep.Start("Wait for GRs to reach " + targetTemp.ToString() + " °C.");
			while (anyUnderTemp(grs, targetTemp)) Wait();
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM(3);
			waitForVSPressure(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Admit H2 into GRs");
			GM.IsolateFromVacuum();
            gs_H2_GM.FlowPressurize(pressure_Fe_prep_H2);
			ProcessStep.End();

			ProcessStep.Start("Reduce iron for " + min_string(minutes_Fe_prep));
			grs.ForEach(gr => gr.Close());
			GM.Evacuate(pressure_ok);
			openLine();
			WaitRemaining(minutes_Fe_prep);
			ProcessStep.End();

			ProcessStep.Start("Evacuate GRs");
			GM_d13C.Close();
			closeAllGRs();
			isolateSections();
			VacuumSystem.Isolate();
			grs.ForEach(gr => { gr.Furnace.TurnOff(); gr.Open(); });
			GM.Evacuate(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM(3);
			ProcessStep.End();

			grs.ForEach(gr => gr.PreparationComplete());

			openLine();
			Alert("Operator Needed", "Graphite reactor preparation complete");
		}

		protected void change_sulfur_Fe()
		{
			var grs = GraphiteReactors.FindAll(gr =>
				isSulfurTrap(gr) && gr.State == GraphiteReactor.States.WaitService);

			if (grs.Count < 1)
			{
                Notice.Send("Nothing to do", "No sulfur traps are awaiting service.", Utilities.Notice.Type.Tell);
				return;
			}

			pressurize_GRs_with_He(grs);

			PlaySound();
			Notice.Send("Operator needed",
				"Replace iron in sulfur traps." + "\r\n" +
				"Press Ok to continue");

			// assume the Fe has been replaced

			ProcessStep.Start("Evacuate sulfur traps");
			GM_d13C.Close();
			isolateSections();

			grs.ForEach(gr => gr.Open());

			GM.Evacuate(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM(3);
			ProcessStep.End();

			grs.ForEach(gr => gr.PreparationComplete());

			openLine();
		}

		#endregion GR service

		#region Vacuum System

		protected void evacuate() => VacuumSystem.Evacuate();
		protected void evacuate(double pressure) => VacuumSystem.Evacuate(pressure);
		protected void evacuateIM() => IM.Evacuate();
		protected void evacuateVTT() => VTT.Evacuate();
		protected void evacuateSplit() => Split.Evacuate();
		protected void evacuateGM() => GM.Evacuate();
		protected void evacuateVTT_MC() => VTT_MC.OpenAndEvacuate(pressure_clean);
		protected void evacuateVTT_CuAg() => VTT_CuAg.OpenAndEvacuate(pressure_clean);
		protected void evacuateMC_GM(double pressure) => MC_GM.OpenAndEvacuate(pressure);

		protected void evacuateIP()
		{
			IM.Isolate();
			IM.ClosePorts();
			IP.Open();
			IM.OpenAndEvacuate(pressure_ok);
		}

		protected void evacuateIM_VTT()
		{
			IM_VTT.ClosePorts();
			IM_VTT.OpenAndEvacuate(pressure_ok);
		}

		#endregion Vacuum System

		#region Joining and isolating sections

		protected virtual void openLine()
		{
			ProcessStep.Start("Open line");

			if (VTC.Dirty) clean_VTT();

			ProcessSubStep.Start("Close gas supplies");
			foreach (GasSupply g in GasSupplies)
			{
				if (g.Destination.VacuumSystem == VacuumSystem)
					g.ShutOff();
			}

			// close gas flow valves after all shutoff valves are closed
			foreach (GasSupply g in GasSupplies)
			{
				if (g.Destination.VacuumSystem == VacuumSystem)
					g.v_flow?.CloseWait();
			}

			ProcessSubStep.End();

			bool vmOpened = VacuumSystem.State == VacuumSystem.States.HighVacuum;
			bool imOpened = IM.IsOpened;
			bool vttOpened = VTT.IsOpened;
			bool CuAg_d13COpened = CuAg_d13C.IsOpened &&
				readyGRsAreOpened() &&
				(VP.IsOpened || VPShouldBeClosed());

			if (vmOpened && imOpened && vttOpened && CuAg_d13COpened &&
				IM_VTT.IsOpened &&
				VTT_CuAg.IsOpened)
				return; // nothing to do; the line is already opened

			bool doD13C = GM_d13C.InternalValves.IsClosed;
			bool doVP = VP.IsClosed && !VPShouldBeClosed();
			bool doGRs = !readyGRsAreOpened();

			isolateSections();

			// make sure the VM is empty
			if (!vmOpened) evacuate(pressure_ok);

			if (!CuAg_d13COpened)
			{
				ProcessSubStep.Start("Evacuate CuAg..Split");
				VacuumSystem.IsolateExcept(CuAg_d13C);
				GM.IsolateFromVacuum();
				CuAg_MC.Open();
				v_MC_MCU.Open();
				v_MC_MCL.Open();
				VacuumSystem.Isolate();
				CuAg_MC.JoinToVacuum();
				evacuate(pressure_ok);
				CuAg_MC.IsolateFromVacuum();
				ProcessSubStep.End();
			}

			if (!vttOpened)
			{
				ProcessSubStep.Start("Evacuate VTT");
				VacuumSystem.IsolateExcept(VTT);
				v_VTT_flow.Open();
				VTT.JoinToVacuum();
				evacuate(pressure_ok);
				VTT.IsolateFromVacuum();
				ProcessSubStep.End();
			}

			if (!CuAg_d13COpened)
			{
				ProcessSubStep.Start("Evacuate GM");

				VacuumSystem.IsolateExcept(Split);
				MC.IsolateFromVacuum();
				GM.JoinToVacuum();
				evacuate(pressure_ok);

				if (doVP || doD13C)
				{
					VacuumSystem.Isolate();
					closeAllGRs();
					if (doVP) VP.Open();
					GM_d13C.Open();
					evacuate(pressure_ok);
					if (doVP && VP.State != LinePort.States.Prepared)
						He_flush_GM(3);
					if (VP.IsOpened) VP.State = LinePort.States.Prepared;
				}

				if (doGRs)
				{
					GM_d13C.Close();
					VacuumSystem.Isolate();
					openReadyGRs();
					evacuate(pressure_ok);
				}

				openReadyGRs();
				GM_d13C.Open();
				waitForVSPressure(pressure_ok);
				GM.IsolateFromVacuum();
				ProcessSubStep.End();
			}

			if (!imOpened)
			{
				ProcessSubStep.Start("Evacuate IM");
				VacuumSystem.IsolateExcept(IM);
				IM.JoinToVacuum();
				evacuate(pressure_ok);
				ProcessSubStep.End();
			}

			ProcessSubStep.Start("Join and evacuate all empty sections");
			//VacuumSystem.ManifoldSections.ForEach(s => s.JoinToVacuum());
			IM.JoinToVacuum();
			VTT.JoinToVacuum();
			MC.JoinToVacuum();
			GM.JoinToVacuum();

			IM_VTT.Open();
			VTT_CuAg.Open();
			ProcessSubStep.End();
			ProcessStep.End();
		}

		// TODO: this should be nearly obsolete; double-check usage
		protected virtual void isolateSections() => VacuumSystem.IsolateSections();

		/// <summary>
		/// </summary>
		/// <returns>True if VP state is InProcess or Completed</returns>
		protected bool VPShouldBeClosed()
		{
			return !(
				VP.State == LinePort.States.Loaded ||
				VP.State == LinePort.States.Prepared);
		}

		protected bool readyGRsAreOpened()
		{
			return !GraphiteReactors.Any(gr => gr.isReady && !gr.IsOpened);
		}

		#endregion Joining and isolating sections

		#region Sample loading and preparation

		protected virtual void admitDeadCO2()
		{
			// admit enough to supply the requested MC sample size plus the d13C
			double divisor = Sample.Take_d13C ? rAMS : 1;
			// and also enough for any additional aliquots
			if (Sample.nAliquots > 1) divisor += rMCU;
			if (Sample.nAliquots > 2) divisor += rMCL;
			admitDeadCO2(Sample.micrograms / divisor);
		}

		protected virtual void admitDeadCO2(double ugc_targetSize)
		{
			ProcessStep.Start("Join && evacuate MC..VM");
			VacuumSystem.IsolateManifold();
			MC.Isolate();
			if (Sample.nAliquots > 1)
				v_MC_MCU.OpenWait();
			if (Sample.nAliquots > 2)
				v_MC_MCL.OpenWait();
			MC.JoinToVacuum();
			VacuumSystem.Evacuate(pressure_clean);

			if (Sample.nAliquots < 2)
				v_MC_MCU.CloseWait();
			if (Sample.nAliquots < 3)
				v_MC_MCL.CloseWait();

			waitForVSPressure(pressure_clean);
			zero_MC();
			ProcessStep.End();

            ProcessStep.Start("Admit CO2 into the MC");
            gs_CO2_MC.Pressurize(ugc_targetSize);
            ProcessStep.End();
        }

        protected virtual void admitSealedCO2() { admitSealedCO2IP(); }

		protected void admitSealedCO2IP()
		{
			ProcessStep.Start("Evacuate and flush breakseal at IP");
			IM.ClosePorts();
			IM.Isolate();
			IP.Open();
			IM.Evacuate();
			He_flush_IP();
			waitForVSPressure(pressure_clean);
			ProcessStep.End();

			admitIPHe(pressure_over_atm);

			ProcessStep.Start("Release the sample");
			Alert("Operator Needed", "Release sealed sample at IP.");
			Notice.Send("Operator needed",
				"Release the sample by breaking the sealed CO2 tube.\r\n" +
				"Press Ok to continue");
			ProcessStep.End();
		}

		// prepare a carbonate sample for acidification
		protected void prepareCarbonateSample()
		{
			loadCarbonateSample();
			IP.Open();
			evacuateIP();
			He_flush_IP();
			ProcessStep.Start($"Wait for p_VM < {pressure_clean:0.0e0} Torr");
			waitForVSPressure(pressure_clean);
			ProcessStep.End();
			Alert("Operator Needed", "Carbonate sample is evacuated");
		}

		protected void loadCarbonateSample()
		{
			ProcessStep.Start("Provide positive He pressure at IP needle");
			IM.ClosePorts();
			IM.Isolate();
			gs_He_IM.Admit();
			gs_He_IM.WaitForPressure(pressure_over_atm);
			IP.Open();
			Wait(5000);
			gs_He_IM.WaitForPressure(pressure_over_atm);
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Remove previous sample or plug from IP needle");
			while (!m_p_IM.IsFalling && ProcessStep.Elapsed.TotalSeconds < 10)
				Wait(); // wait up to 10 seconds for p_IM clearly falling
			ProcessStep.End();

			ProcessStep.Start("Wait for stable He flow at IP needle");
			while (!m_p_IM.IsStable)
				Wait();
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Load next sample vial or plug at IP needle");
			while (m_p_IM.RoC < roc_pIM_plugged && ProcessStep.Elapsed.TotalSeconds < 20)
				Wait();
			if (m_p_IM.RoC > roc_pIM_loaded)
				IP.State = LinePort.States.Loaded;
			else
				IP.State = LinePort.States.Complete;
			ProcessStep.End();

			IP.Close();
			gs_He_IM.ShutOff();
		}

		protected void prepareNewVial()
		{
			if (!Sample.Take_d13C || VP.State == LinePort.States.Prepared) return;
			ProcessStep.Start("Prepare new vial");
			if (VP.State != LinePort.States.Loaded)
			{
				Alert("Sample Alert!", "d13C vial not available");
                Notice.Send("Error!",
					"Unable to prepare new vial.\r\n" +
					"Vial contains prior d13C sample!",
                    Utilities.Notice.Type.Tell);
				return;
			}
			GM.ClosePorts();
			GM.Isolate();
			isolateSections();
			VacuumSystem.Isolate();

			VP.Contents = "";
			VP.Open();
			GM_d13C.Open();
			GM.Evacuate(pressure_ok);
			He_flush_GM(3);

			VP.State = LinePort.States.Prepared;
			ProcessStep.End();
		}

		#endregion Sample loading and preparation

		#region Sample operation

		protected void editProcessSequences()
		{
			ShowProcessSequenceEditor(this);
		}

		protected void enterSampleData()
		{
			VerifySampleInfo(false);
		}

		int readyGRs()
		{
			return GraphiteReactors.Count(gr => gr.isReady);
		}

		public bool enoughGRs()
		{
			int needed = Sample.nAliquots;
			if (Sample.SulfurSuspected && !isSulfurTrap(nextSulfurTrap(Last_GR)))
				needed++;
			return readyGRs() >= needed;
		}

        public override void RunProcess(string processToRun)
        {
			ensureProcessStartConditions();
            if (processToRun == "Run sample")
                runSample();
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
		protected void ensureProcessStartConditions()
		{
			VacuumSystem.IonGaugeAuto = true;
			//GasSupplies.ForEach(gs => gs.ShutOff());
		}

		protected void runSample()
		{
			if (!VerifySampleInfo(true))
				return;

			Sample.ugDC = 0;

			if (m_v_LN_supply < LN_supply_min)
			{
				if (Notice.Send(
						"System Alert!",
						"There might not be enough LN!\r\n" +
							"Press OK to proceed anyway, or Cancel to abort.",
                        Utilities.Notice.Type.Warn).Text != "Ok")
					return;
			}

			if (!enoughGRs())
			{
                Notice.Send("Error!",
					"Unable to start process.\r\n" +
					"Not enough GRs ready!",
                    Utilities.Notice.Type.Tell);
				return;
			}

			if (ProcessSequence.Find(Sample.Process) == null)
				throw new Exception("No such Process Sequence: \"" + Sample.Process + "\"");

			SampleLog.WriteLine("");
			SampleLog.Record(
				$"Start Process:\t{Sample.Process}\r\n" +
				$"\t{Sample.ID}\t{Sample.milligrams:0.0000}\tmg\r\n" +
				$"\t{Sample.nAliquots}\taliquot{(Sample.nAliquots != 1 ? "s" : "")}");

            base.RunProcess(Sample.Process);
		}

		protected override void ProcessEnded()
		{
			string msg = (ProcessType == ProcessTypes.Sequence ? Sample.Process : ProcessToRun) + 
				$" process {(RunCompleted ? "complete" : "aborted")}";

			if (ProcessType == ProcessTypes.Sequence)
			SampleLog.Record(msg + "\r\n\t" + Sample.ID);

			Alert("System Status", msg);
			base.ProcessEnded();
		}

		#endregion Sample operation

		#region Sample extraction and measurement


		protected void VttBleed(double bleedPressure)
		{
			ProcessSubStep.Start($"Maintain VTT pressure near {bleedPressure:0.00} Torr");

			// disable ion gauge while low vacuum flow is expected
			var IGWasAuto = VacuumSystem.IonGaugeAuto;
			VacuumSystem.IonGaugeAuto = false;
			VacuumSystem.IGDisable();
			VacuumSystem.Evacuate();    // use low vacuum or high vacuum as needed

			VttFlowManager.Start(bleedPressure);

			// Does anything else need to be happening now?
			DuringBleed?.Invoke();

			while (VttFlowManager.Busy)
				Wait();

			VacuumSystem.IonGaugeAuto = IGWasAuto;

			ProcessSubStep.End();
		}

		protected virtual void bleed()
		{
			ProcessStep.Start("Bleed off incondensable gases and trap CO2");

			if (Sample.Source.LinePort == IP)
			{
				// Do not bleed to low pressure (< ~mTorr) while temperature is high (> ~450 C)
				// to avoid decomposing carbonates in organic samples.
				turnOffCCFurnaces();
			}

            ProcessSubStep.Start($"Wait for VTT temperature < {temperature_VTT_cold} °C");

            if (VTC.State != VTColdfinger.States.Freeze && VTC.State != VTColdfinger.States.Raise)
                VTC.Freeze();
            while (VTC.Coldfinger.Temperature > temperature_FTC_frozen) Wait();
            if (VTC.State != VTColdfinger.States.Raise)
                VTC.Raise();
            while (VTC.Temperature > temperature_VTT_cold) Wait();

            ProcessSubStep.End();

            ProcessSubStep.Start("Calibrate VTT flow valve");
			v_VTT_flow.Close();
			v_VTT_flow.Calibrate();
			ProcessSubStep.End();

			// Connect the gas from the sample source all the way up to, 
			// but not including, the VTT
			startBleed();

			ProcessSubStep.Start("Release incondensables");
			v_VTT_flow.Open();
			evacuateVTT();
			while (m_p_VTT.IsRising) Wait();
			ProcessSubStep.End();

			// Release the gas into the VTT
			v_VTT_flow.Close();
			VTT.Close();        // in case there's a bypass valve; does nothing if not
			Sample.Source.PathToVTT.Valves.Last().Open();
			VTC.Dirty = true;

			// Control flow valve to maintain constant downstream pressure until flow valve is fully opened.
			VttBleed(pressure_VTT_bleed_sample);

			// Open flow bypass when conditions allow it without producing an excessive
			// downstream pressure spike. Then wait for the spike to be evacuated.
			ProcessSubStep.Start("Wait for remaining pressure to bleed down");
			while (m_p_IM > pressure_VTT_flow_bypass || m_p_VTT.RoC < roc_pVTT_falling_very_slowly)
				Wait();
			ProcessSubStep.End();
			VTT.Open();        // in case there's a bypass valve; does nothing if not
			while (m_p_VTT.Value > pressure_VTT_near_end_of_bleed)
				Wait();

			// Process-specific override to ensure entire sample is trapped
			// (does nothing by default).
			finishBleed();

			// Close the Sample Source-to-VTT path
			Sample.Source.PathToVTT.Valves.ForEach(v =>
			{
				ProcessSubStep.Start($"Waiting to close {v.Name}");
				Wait(5000);
				while (m_p_VTT.RoC < roc_pVTT_falling_very_slowly)		// Torr/sec
					Wait();
				v.CloseWait();
				ProcessSubStep.End();
			});

			// Isolate the trap once the pressure has stabilized
			ProcessSubStep.Start($"Waiting to isolate VTT from vacuum");
			while (m_p_VTT.RoC < roc_pVTT_falling_barely)
				Wait();
			VTT.IsolateFromVacuum();
			ProcessSubStep.End();

			ProcessStep.End();

            Sample.Source.LinePort.State = LinePort.States.Complete;


		}

		// release the sample up to, but not including the VTT
		protected virtual void startBleed()
		{
			IM.ClosePorts();
			IM.Isolate();

			Sample.Source.LinePort.State = LinePort.States.InProcess;

			// open all but the last valve to the VTT
			var v_Last = Sample.Source.PathToVTT.Valves.Last();
			Sample.Source.PathToVTT.Valves.ForEach(v => 
			{
				if (v != v_Last)
					v.Open();
			});
		}

		protected virtual void finishBleed() { }

		#region Extract

		/// <summary>
		/// Pressurize VTT..MC with ~0.1 Torr He
		/// </summary>
		protected virtual void pressurizeVTT_MC()
		{
			ProcessStep.Start("Zero MC and VTT pressure gauges");
			v_VTT_flow.Open();
			evacuateVTT_MC();
			zero_VTT_MC();
			ProcessStep.End();

			ProcessStep.Start("Pressurize VTT..MC with minimal He");
			gs_He_VTT_MC.NormalizeFlow();
			gs_He_VTT_MC.ShutOff();
			wait_VTT_MC_stable();
			ProcessStep.End();

			double tgt = 0.1;
			double ccVTT = VTT.MilliLiters;
			double ccVTT_CuAg = VTT_CuAg.MilliLiters;
			double ccVTT_MC = VTT_MC.MilliLiters;
			double ccMC = MC.MilliLiters;
			double ccVTT_Split = ccVTT_MC + Split.MilliLiters;

			ProcessStep.Start($"Reduce pVTT_MC to ~{tgt} Torr He by discarding fractions");

			while (m_p_VTT * ccVTT / ccVTT_MC > tgt)
			{
				// discard CuAg_MC
				VTT.Isolate();
				CuAg_MC.OpenAndEvacuate(pressure_ok);
				VTT_MC.Isolate();
				VTT_MC.Open();
				wait_VTT_MC_stable();
			}

			while (m_p_VTT * ccVTT_CuAg / ccVTT_MC > tgt)
			{
				// discard MC
				VTT_CuAg.Isolate();
				MC.Evacuate(pressure_ok);
				VTT_MC.Isolate();
				VTT_MC.Open();
				wait_VTT_MC_stable();
			}

			while (m_p_VTT * ccVTT_MC / ccVTT_Split > tgt)
			{
				// discard Split
				Split.Isolate();
				v_MC_Split.OpenWait();
				Wait(5000);
				v_MC_Split.CloseWait();
				Split.Evacuate();
				wait_VTT_MC_stable();
			}
			ProcessStep.End();
		}

		protected void extractAt(int targetTemp)
		{
			ProcessStep.Start($"Extract at {targetTemp:0} °C");
			SampleLog.Record($"\tExtraction target temperature:\t{targetTemp:0}\t°C");

			VTC.Regulate(targetTemp);
			freeze(ftc_MC);

			targetTemp -= 1;			// continue at 1 deg under
			ProcessSubStep.Start($"Wait for VTT to reach {targetTemp:0} °C");
			while (VTC.Temperature < targetTemp) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait 15 seconds to ensure transfer is well underway");
			Wait(15000);
			ProcessSubStep.End();

			ProcessSubStep.Start($"Wait for VTT_MC pressure <= {0.15:0} Torr");
			// TODO: remove this magic number; need to know actual blanket pressure
			while (m_p_VTT > 0.230)		// more than He blanket pressure, due to temperature increase
				Wait();
			ProcessSubStep.End();

			ftc_MC.Raise();
			waitFor_LN_peak(ftc_MC);

			wait_VTT_MC_stable();		// assumes transfer is nearly finished

			SampleLog.Record("\tCO2 equilibrium temperature:" +
				$"\t{CO2EqTable.Interpolate(m_p_MC):0}\t°C");

			waitFor_LN_peak(ftc_MC);

			CuAg_MC.Close();
			ProcessStep.End();
		}

		double extractionPressure()
		{
			// Depends on which chambers are connected
			// During extraction, VTT..MC should be joined.
			var volVTT_MC = mL_VTT + mL_CuAg + mL_MC;
			double currentVolume = mL_VTT;
			if (VTT_CuAg.InternalValves.IsOpened)
			{
				currentVolume += mL_CuAg;
				if (CuAg_MC.InternalValves.IsOpened)
					currentVolume += mL_MC;
			}
			return m_p_VTT * currentVolume / volVTT_MC;
		}

		// Extracts gases from the VTT to the MC at a base pressure
		// provided by a small charge of He. The gas evolution
		// temperature is determined by adding the given offset,
		// dTCO2eq, to the CO2 equilibrium temperature for the base 
		// pressure.
		protected void pressurizedExtract(int dTCO2eq)
		{
			double extractionPressure = this.extractionPressure();
			SampleLog.Record("\tExtraction base pressure:" +
				$"\t{extractionPressure:0.000}\tTorr");

			int tCO2eq = (int)CO2EqTable.Interpolate(extractionPressure);
			SampleLog.Record($"\tExpected CO2 equilibrium temperature:\t{tCO2eq:0}\t°C");

			extractAt(tCO2eq + dTCO2eq);
		}

		protected void extract()
		{
			pressurizeVTT_MC();
			pressurizedExtract(3);		// targets CO2
			VTT_CuAg.Close();
			VTC.Stop();
		}

		#endregion Extract

		// returns the next available graphite reactor
		GraphiteReactor nextGR(string this_one)
		{
			bool passed_this_one = false;
			GraphiteReactor found_one = null;
			foreach (var gr in GraphiteReactors)
			{
				if (passed_this_one)
				{
					if (gr.isReady && gr.Aliquot == null) return gr;
				}
				else
				{
					if (found_one == null && gr.isReady && gr.Aliquot == null)
						found_one = gr;
					if (gr.Name == this_one)
						passed_this_one = true;
				}
			}
			return found_one;
		}

		bool isSulfurTrap(GraphiteReactor gr)
		{
			return gr.Aliquot != null && gr.Aliquot.Name == "sulfur";
		}

		GraphiteReactor nextSulfurTrap(string this_gr)
		{
			bool passed_this_one = false;
			GraphiteReactor found_one = null;
			foreach (var gr in GraphiteReactors)
			{
				if (passed_this_one)
				{
					if (isSulfurTrap(gr) && gr.State != GraphiteReactor.States.WaitService) return gr;
				}
				else
				{
					if (found_one == null && isSulfurTrap(gr) && gr.State != GraphiteReactor.States.WaitService)
						found_one = gr;
					if (gr.Name == this_gr)
						passed_this_one = true;
				}
			}
			if (found_one != null) return found_one;
			return nextGR(this_gr);
		}

		protected void openNextGRs()
		{
			string grName = Last_GR;
			for (int i = 0; i < Sample.nAliquots; ++i)
			{
				if (nextGR(grName) is GraphiteReactor gr)
				{
					gr.Open();
					grName = gr.Name;
				}
			}
		}

		protected void openNextGRsAndd13C()
		{
			// Requires/assumes low pressures or VacuumSystem.Isolated
			openNextGRs();
			if (Sample.Take_d13C && VP.State == LinePort.States.Prepared)
			{
				VP.Open();
				GM_d13C.Open();
			}
			GM.JoinToVacuum();
		}

		protected void takeMeasurement(bool first)
		{
			ProcessStep.Start("Take measurement");
			waitForMCStable();

			// this is the measurement
			double ugC = ugCinMC;

			if (first)
			{
				Sample.Aliquots.Clear();	// this line really shouldn't be needed...
				for (int i = 0; i < Sample.nAliquots; i++)
				{
					Aliquot aliquot = new Aliquot
					{
						Sample = Sample
					};
					Sample.Aliquots.Add(aliquot);
				}
			}

			Sample.Aliquots[0].ugC = Sample.Take_d13C ? ugC * rAMS : ugC;
			Sample.d13C_ugC = ugC - Sample.Aliquots[0].ugC;

			if (Sample.nAliquots > 1)
			{
				Sample.Aliquots[1].ugC = ugC * rMCU; // all of this aliquot will be graphitized
				if (Sample.nAliquots > 2)
					Sample.Aliquots[2].ugC = ugC * rMCL;   // all of this aliquot will be graphitized
			}

			if (first)
			{
				Sample.ugC = ugC;  // include the d13C
				for (int i = 1; i < Sample.nAliquots; i++)
					Sample.ugC += Sample.Aliquots[i].ugC;

				string yield = (Sample.ugDC > 0) ? "" :
					$"\tYield:\t{100 * Sample.ugC / Sample.micrograms:0.00}%";

				SampleLog.Record(
					"Sample measurement:\r\n" +
					$"\t{Sample.ID}\t{Sample.milligrams:0.0000}\tmg\r\n" +
					$"\tCarbon:\t{Sample.ugC:0.0}\tugC{yield}"
				);
			}
			else
			{
				SampleLog.Record(
					"Sample measurement (split discarded):\r\n" +
					$"\t{Sample.ID}\t{Sample.milligrams:0.0000}\tmg\r\n" +
					$"\tRemaining Carbon:\t{ugC:0.0}\tugC"
				);
			}

			ProcessStep.End();
		}

		protected void measure()
		{
			ProcessStep.Start("Prepare to measure MC contents");
			MC.Isolate();
			GM.Evacuate(pressure_clean);

			if (ftc_MC.State >= FTColdfinger.States.Freeze)
			{
				ProcessStep.Start("Release incondensables");

				raise_LN(ftc_MC);
				ProcessSubStep.Start($"Wait for MC coldfinger < {temperature_FTC_frozen} °C");
				while (ftc_MC.Temperature > temperature_FTC_frozen) Wait();
				ProcessSubStep.End();

				GM.IsolateFromVacuum();
				if (Sample.nAliquots > 1)
				{
					v_MC_MCU.Open();
					if (Sample.nAliquots > 2) v_MC_MCL.Open();
				}
				MC.JoinToVacuum();
				evacuate(pressure_clean);

				zero_MC();

				if (Sample.nAliquots < 3)
				{
					v_MC_MCL.CloseWait();
					if (Sample.nAliquots < 2) v_MC_MCU.CloseWait();
					Wait(5000);
				}
				ProcessStep.End();

				MC.IsolateFromVacuum();
				GM.JoinToVacuum();
			}

			openNextGRsAndd13C();

			if (!ftc_MC.isThawed())
			{
				ProcessSubStep.Start("Bring MC to uniform temperature");
				ftc_MC.Thaw();
				while (!ftc_MC.isThawed())
					Wait();
				ProcessSubStep.End();
			}

			ProcessStep.End();

			ProcessStep.Start("Measure Sample");
			takeMeasurement(true);
			ProcessStep.End();

			// exits with Split..VP joined and evacuating
		}

		protected void split()
		{
			ProcessStep.Start("Discard Excess sample");
			while (Sample.Aliquots[0].ugC > ugC_sample_max)
			{
				ProcessSubStep.Start("Evacuate Split");
				Split.Evacuate(0);
				ProcessSubStep.End();

				ProcessSubStep.Start("Split sample");
				Split.IsolateFromVacuum();
				v_MC_Split.OpenWait();
				Wait(5000);
				v_MC_Split.Close();
				ProcessSubStep.End();

				ProcessSubStep.Start("Discard split");
				Split.Evacuate(0);
				ProcessSubStep.End();

				takeMeasurement(false);
			}
			GM.JoinToVacuum();
			ProcessStep.End();
		}

		protected void dilute()
		{
			if (Sample.ugC > mass_small_sample) return;

			double ugCdg_needed = (double)mass_diluted_sample - Sample.ugC;

			ProcessStep.Start("Dilute sample");

			Alert("Sample Alert!", $"Small sample! ({Sample.ugC:0.0} ugC) Diluting...");

			// Should we pre-evacuate the CuAg via VTT? The release of incondensables 
			// later should remove any residuals now present in CuAg. And the only trash
			// in the CuAg would have come from the sample anyway, and should not contain 
			// condensables. It's probably better to avoid possible water from the VTT, 
			// although that could be prevented also by re-freezing it, or keeping it frozen
			// longer, at the expense of some time, in either case. 
			// We could clean the VTT as soon as the sample has been extracted into the MC;
			// then that PathToVacuum would be available, although the VTT might still have an 
			// elevated pressure at this point.
			// If the VTT were clean at this point, we could hold the sample there, and also
			// transfer the dilution gas there, and extract the mixture for a cleaner sample.

			VTT_CuAg.Close();
			ftc_CuAg.Freeze();

			ftc_MC.Thaw();
			CuAg_MC.Open();

			ProcessSubStep.Start("Wait for MC coldfinger to thaw.");
			while (ftc_MC.Temperature < m_t_MC - 5) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait for sample to freeze in the CuAg coldfinger.");
			while (ProcessSubStep.Elapsed.TotalMinutes < 1 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 4)
				Wait();
			Wait(30000);
			ProcessSubStep.End();

			ftc_CuAg.Raise();

			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			Wait(15000);
			CuAg_MC.Close();
			ProcessSubStep.End();

			// get the dilution gas into the MC
			admitDeadCO2(ugCdg_needed);

			// At this point, it would be useful to pass the dilution gas
			// through the VTT. But how to make that possible?

			// discard unused dilution gas, if necessary
			gs_CO2_MC?.Path?.JoinToVacuum();
			evacuate();

			ProcessSubStep.Start("Take measurement");
			waitForMCStable();
			Sample.ugDC = ugCinMC;
			SampleLog.Record($"Dilution gas measurement:\t{Sample.ugDC:0.0}\tugC");
			ProcessSubStep.End();

			ProcessSubStep.Start("Freeze dilution gas");
			freeze(ftc_MC);

			while (ProcessSubStep.Elapsed.TotalSeconds < 5 ||
				(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 1)
				Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Combine sample and dilution gas");
			ftc_CuAg.Thaw();
			CuAg_MC.Open();

			while (ProcessSubStep.Elapsed.TotalSeconds < 30 ||
					(ftc_CuAg.Temperature < 0 || ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 2)
				Wait();

			raise_LN(ftc_MC);

			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			Wait(15000);
			CuAg_MC.Close();
			ProcessSubStep.End();

			ftc_CuAg.Stop();
			ProcessSubStep.End();

			ProcessStep.End();

			// measure diluted sample
			measure();
		}

		protected void divideAliquots()
		{
			ProcessStep.Start("Divide aliquots");
			v_MC_MCL.Close();
			v_MC_MCU.CloseWait();
			ProcessStep.End();
		}

		protected void trapSulfur(GraphiteReactor gr)
		{
			var ftc = gr.Coldfinger;
			var h = gr.Furnace;

			ProcessStep.Start("Trap sulfur.");
			SampleLog.Record(
				$"Trap sulfur in {gr.Name} at {temperature_trap_sulfur} °C for {min_string(minutes_trap_sulfur)}");
			ftc.Thaw();
			h.TurnOn(temperature_trap_sulfur);
			ProcessSubStep.Start($"Wait for {gr.Name} to reach sulfur trapping temperature (~{temperature_trap_sulfur} °C).");
			while (ftc.Temperature < 0 || h.Temperature < temperature_trap_sulfur - 5)
				Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Hold for " + min_string(minutes_trap_sulfur));
			Wait(minutes_trap_sulfur * 60000);
			ProcessSubStep.End();

			h.TurnOff();
			ProcessStep.End();
		}

		protected void removeSulfur()
		{
			if (!Sample.SulfurSuspected) return;

			ProcessStep.Start("Remove sulfur.");

			GraphiteReactor gr = nextSulfurTrap(Last_GR);
			Last_GR = gr.Name;
			gr.Reserve("sulfur");
			gr.State = GraphiteReactor.States.InProcess;

			transferCO2FromMCToGR(gr, false);
			trapSulfur(gr);
			transferCO2FromGRToMC(gr, false);

			gr.Aliquot.ResidualMeasured = true;	// prevent graphitization retry
			gr.State = GraphiteReactor.States.WaitService;

			ProcessStep.End();
			measure();
		}

		protected void freeze(Aliquot aliquot)
		{
			aliquot.Name = Next_GraphiteNumber.ToString(); Next_GraphiteNumber++;
			GraphiteReactor gr = nextGR(Last_GR);
			if (gr == null)
				throw new Exception("Can't find a GR to freeze the aliquot into.");
			Last_GR = aliquot.GR = gr.Name;
			gr.Reserve(aliquot);

			if (aliquot == aliquot.Sample.Aliquots[0])
				transferCO2FromMCToGR(gr, Sample.Take_d13C);
			else if (aliquot == aliquot.Sample.Aliquots[1])
				transferCO2FromMCToGR(gr, v_MC_MCU);
			else if (aliquot == aliquot.Sample.Aliquots[2])
				transferCO2FromMCToGR(gr, v_MC_MCL);
		}

		protected double[] admitGasFromGMToColdfinger(GasSupply gs, double initialTargetPressure, FTColdfinger ftc, IValve chamberValve)
		{
			gs.Pressurize(initialTargetPressure);
			gs.IsolateFromVacuum();

			waitFor_LN_peak(ftc);
			double pInitial = m_p_GM;
			chamberValve.OpenWait();
			Wait(10000);
			chamberValve.CloseWait();
			Wait(15000);
			double pFinal = m_p_GM;
			return new double[] { pInitial, pFinal };
		}

		protected void add_GR_H2(Aliquot aliquot)
		{
			var gr = GraphiteReactor.Find(aliquot.GR);
			//double mL_GR = gr.MilliLitersVolume;	// use the average instead

			double nCO2 = aliquot.ugC * nC_ug;  // number of CO2 particles in the aliquot
			double nH2target = H2_CO2 * nCO2;   // ideal number of H2 particles for the reaction

			// The pressure of nH2target in the frozen GR, where it will be denser.
			aliquot.pH2Final = densityAdjustment * pressure(nH2target, mL_GR, ts_GM.Temperature);
			aliquot.pH2Initial = aliquot.pH2Final + pressure(nH2target, mL_GM, ts_GM.Temperature);

			// The GM pressure drifts a bit after the H2 is introduced, generally downward.
			// This value compensates for the consequent average error, which was about -4,
			// averaged over 14 samples in Feb-Mar 2018.
			// The compensation is bumped by a few more Torr to shift the variance in
			// target error toward the high side, as a slight excess of H2 is not 
			// deleterious, whereas a deficiency could be.
			double driftAndVarianceCompensation = 9;

			GM_d13C.Close();
			var p = admitGasFromGMToColdfinger(
				gs_H2_GM, 
				aliquot.pH2Initial + driftAndVarianceCompensation, 
				gr.Coldfinger, 
				gr.Valve);
			var pH2initial = p[0];
			var pH2final = p[1];

			// this is what we actually got
			var nH2 = nParticles(pH2initial - pH2final, mL_GM, ts_GM.Temperature);
			var pH2ratio = nH2 / nCO2;

			double nExpectedResidual;
			if (pH2ratio > H2_CO2_stoich)
				nExpectedResidual = nH2 - nCO2 * H2_CO2_stoich;
			else
				nExpectedResidual = nCO2 - nH2 / H2_CO2_stoich;
			aliquot.ResidualExpected = TorrPerKelvin(nExpectedResidual, mL_GR);

			SampleLog.Record(
				$"GR hydrogen measurement:\r\n\t{Sample.ID}\r\n\t" +
				$"Graphite {aliquot.Name}\t{aliquot.ugC:0.0}\tugC\t{aliquot.GR}\t" +
				$"pH2:CO2\t{pH2ratio:0.00}\t" +
				$"{pH2initial:0} => {pH2final:0} / {aliquot.pH2Initial:0} => {aliquot.pH2Final:0}\r\n\t" +
				$"expected residual:\t{aliquot.ResidualExpected:0.000}\tTorr/K"
				);

			if (pH2ratio < H2_CO2_stoich * 1.05)
			{
				Alert("Sample Alert!", "Not enough H2");
				Notice.Send("Error!",
					$"Not enough H2 in {aliquot.GR}\r\nProcess paused.");
			}
		}

		protected void graphitizeAliquots()
		{
			divideAliquots();
			foreach (Aliquot aliquot in Sample.Aliquots)
				freeze(aliquot);

			GM.IsolateFromVacuum();

			foreach (Aliquot aliquot in Sample.Aliquots)
			{
				ProcessStep.Start("Graphitize aliquot " + aliquot.Name);
				add_GR_H2(aliquot);
				GraphiteReactor.Find(aliquot.GR).Start();
				ProcessStep.End();
			}
			// exits with GM isolated and filled with H2
		}

		/// <summary>
		///  exits with MC..GM joined and evacuating via Split-VM
		/// </summary>
		protected void cleanCuAg()
		{
			if (h_CuAg?.IsOn ?? false)
			{
				ProcessStep.Start("Start cleaning CuAg");
				if (m_p_GM > 50)					// if there is pressure, assume it is H2
					gs_H2_GM.IsolateDestination();
				else
					gs_H2_GM.Pressurize(100);		// just enough to clean the CuAg

				CuAg_MC.Isolate();
				CuAg_MC.Open();
				MC_GM.Open();
				Wait(1000);
				CuAg.Isolate();
				MC_GM.OpenAndEvacuate(pressure_ok);
				ProcessStep.End();
			}
			else
			{
				MC_GM.OpenAndEvacuate(pressure_ok);
				CuAg_MC?.InternalValves?.Close();
				return;
			}
        }

		// normally enters with MC..GM joined and evacuating via Split-VM
		// (except when run as an independent process from the UI, perhaps)
		protected void add_d13C_He()
		{
			if (!Sample.Take_d13C) return;

			ProcessStep.Start("Add 1 atm He to vial");

			raise_LN(ftc_VP);

			ProcessSubStep.Start("Release incondensables");
			GM_d13C.Isolate();
			VP.Open();
			GM_d13C.OpenAndEvacuate(pressure_clean);
			VP.Close();
			ProcessSubStep.End();

			// desired final vial pressure, at normal room tempertaure
			var pTarget = pressure_over_atm;
			var nTarget = nParticles(pTarget, mL_VP, temperature_room);
			var n_CO2 = Sample.d13C_ugC * nC_ug;

			// how much the GM pressure needs to fall to produce pVial == pTarget
			var dropTarget = pressure(nTarget - n_CO2, mL_GM + mL_d13C, ts_GM.Temperature);

			// TODO: replace pressure_VP_He_Initial constant with a method that 
			// determines the initial GM gas pressure from dropTarget;

			var pa = admitGasFromGMToColdfinger(
				gs_He_GM,
				pressure_VP_He_Initial,
				ftc_VP,
				VP.Valve);
			var pHeInitial = pa[0];
			var pHeFinal = pa[1];

			var n_He = nParticles(pHeInitial - pHeFinal, mL_GM + mL_d13C, ts_GM.Temperature);
			var n = n_He + n_CO2;
			Sample.d13C_ppm = 1e6 * n_CO2 / n;

			// approximate standard-room-temperature vial pressure (neglects needle port volume)
			double pVP = pressure(n, mL_VP, temperature_room);

			SampleLog.Record(
				$"d13C measurement:\r\n\t{Sample.ID}\r\n" +
				$"\tGraphite {Sample.Aliquots[0].Name}" +
				$"\td13C:\t{Sample.d13C_ugC:0.0}\tugC" +
				$"\t{Sample.d13C_ppm:0}\tppm" +
				$"\tvial pressure:\t{pVP:0} / {pTarget:0}\tTorr"
			);

			double pVP_Error = pVP - pTarget;
			if (Math.Abs(pVP_Error) > pressure_VP_Error)
			{
				SampleLog.Record("Sample Alert! Vial pressure out of range");
				SampleLog.Record(
					$"\tpHeGM: ({pHeInitial:0} => {pHeFinal:0}) / " +
					$"({pressure_VP_He_Initial:0} => {pressure_VP_He_Initial-dropTarget})");
				Alert("Sample Alert!", $"Vial He pressure error: {pVP_Error:0}");
				if (pVP_Error > 3 * pressure_VP_Error || pVP_Error < -2 * pressure_VP_Error)
				{
					Notice.Send("Error!",
						"Vial He pressure out of range." +
						"\r\nProcess paused.");
					// anything to do here, after presumed remedial action?
				}
			}

			ftc_VP.Thaw();
			VP.State = LinePort.States.Complete;

			ProcessStep.End();
			// exits with GM..d13C filled with He
		}

		public void bleed_etc()
		{
			bleed();
			extract_etc();
		}

		public void extract_etc()
		{
			extract();
			measure_etc();
		}

		protected void measure_etc()
		{
			measure();
			split();
			removeSulfur();
			graphitize_etc();
		}

		protected void graphitize_etc()
		{
			try
			{
				dilute();
				graphitizeAliquots();
				cleanCuAg();		// exits with MC..GM joined and evacuating via Split-VM
				add_d13C_He();		// exits with GM..d13C filled with He
				openLine();
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		#endregion Sample extraction and measurement

		#region Transfer CO2 between chambers

		protected void transferCO2FromMCToGR(GraphiteReactor gr, IValve v_MCx, bool take_d13C)
		{
			FTColdfinger ftc = gr.Coldfinger;

			ProcessStep.Start("Evacuate graphite reactor" + (take_d13C ? " and VP" : ""));

			VacuumSystem.IsolateManifold();
			MC_GM.ClosePorts();
			MC_GM.Isolate();

			if (gr.IsClosed || take_d13C)
			{
				gr.Open();

				if (take_d13C)
				{
					if (VPShouldBeClosed())
						throw new Exception("Need to take d13C, but VP is not available.");
					VP.Open();
					Valve.Find("v_d13C_CF")?.Open();
					GM_d13C.Open();
				}
			}

			GM.PathToVacuum.Open();
			evacuate(pressure_clean);

			ProcessStep.End();

			ProcessStep.Start("Expand sample into GM");

			Valve.Find("v_d13C_CF")?.Close();

			VP.Close();

			if (take_d13C)
				gr.Close();
			else
				GM_d13C.Close();

			MC_GM.IsolateFromVacuum();
			v_MCx?.Open();                  // take it from from MCU or MCL
			MC_GM.Open();

			ProcessStep.End();

			if (take_d13C)
			{
				ProcessSubStep.Start("Take d13C");
				Wait(5000);
				d13C.Isolate();
				VP.Open();
				VP.State = LinePort.States.InProcess;
				VP.Contents = Sample.Aliquots[0].Name;
				ftc_VP.Freeze();
				gr.Open();
				ProcessSubStep.End();
			}

			ProcessStep.Start("Freeze to graphite reactor");
			freeze(ftc);

			ProcessSubStep.Start("Wait for CO2 to freeze into " + gr.Name);
			while (ProcessSubStep.Elapsed.TotalMinutes < 1 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 3.5)
				Wait();
			Wait(30000);
			raise_LN(ftc);
			Wait(15000);
			ProcessSubStep.End();


			ProcessSubStep.Start("Release incondensables");
			MC_GM.PathToVacuum.Open();
			Wait(5000);
			waitForVSPressure(0);
			ProcessSubStep.End();

			gr.Valve.CloseWait();
			v_MCx?.CloseWait();

			ProcessStep.End();
		}

		protected void transferCO2FromMCToGR(GraphiteReactor gr)
		{
			transferCO2FromMCToGR(gr, null, false);
		}

		protected void transferCO2FromMCToGR(GraphiteReactor gr, IValve v_MCx)
		{
			transferCO2FromMCToGR(gr, v_MCx, false);
		}

		protected void transferCO2FromMCToGR(GraphiteReactor gr, bool take_d13C)
		{
			transferCO2FromMCToGR(gr, null, take_d13C);
		}

		// TODO: wait after freezing for pGR to stabilize and close MCU and MCL
		protected void transferCO2FromGRToMC(GraphiteReactor gr, bool firstFreezeGR)
		{
			FTColdfinger grCF = gr.Coldfinger;

			ProcessStep.Start("Transfer CO2 from GR to MC.");

			if (firstFreezeGR)
				grCF.Freeze();

			evacuateMC_GM(pressure_clean);
			v_MC_MCU.Close();
			v_MC_MCL.Close();

			if (firstFreezeGR)
			{
				ProcessSubStep.Start($"Freeze CO2 in {gr.Name}.");
				freeze(grCF);
				raise_LN(grCF);

				ProcessSubStep.Start("Wait one minute.");
				Wait(60000);
				ProcessSubStep.End();

				ProcessSubStep.End();

				ProcessSubStep.Start("Evacuate incondensables.");
				gr.Close();
				MC_GM.OpenAndEvacuate(pressure_clean);
				gr.Open();
				waitForVSPressure(pressure_clean);
				MC_GM.IsolateFromVacuum();
				ProcessSubStep.End();
			}
			else
			{
				MC_GM.IsolateFromVacuum();
				gr.Open();
				MC_GM.Open();
			}

			if (grCF.Temperature < ts_GM.Temperature - 5) grCF.Thaw();
			freeze(ftc_MC);

			ProcessSubStep.Start("Wait for sample to freeze in the MC.");
			while (ProcessSubStep.Elapsed.TotalMinutes < 1 ||
					(ugCinMC > 1.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 2)
				Wait();
			Wait(30000);

			raise_LN(ftc_MC);
			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			Wait(15000);
			ProcessSubStep.End();

			MC_GM.Close();
			gr.Close();
			ProcessSubStep.End();

			ProcessStep.End();
		}

		protected void transferCO2FromGRToMC()
		{
			transferCO2FromGRToMC(GraphiteReactor.Find(Last_GR), true);
		}

		protected void transferCO2FromMCToVTT()
		{
			ProcessStep.Start("Transfer CO2 from MC to VTT");
			evacuateVTT_CuAg();
			v_VTT_flow.Close();
			VTT_CuAg.IsolateFromVacuum();
			ftc_MC.Thaw();
			VTT_MC.Open();

			VTC.Freeze();
			ProcessSubStep.Start($"Freeze VTC (wait for LN sensor <= {temperature_FTC_frozen} °C)");
			while (VTC.Coldfinger.Temperature >= temperature_FTC_frozen) Wait();
			ProcessSubStep.End();

			VTC.Raise();
			ProcessSubStep.Start($"Wait for VTC temperature <= {temperature_VTT_cold} °C");
			while (VTC.Temperature > temperature_VTT_cold) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Make sure the CO2 has started evolving.");
			while (ftc_MC.Temperature < CO2EqTable.Interpolate(0.07)) Wait();
			ProcessSubStep.End();

			wait_VTT_MC_stable();
			// ? v_VTT_flow.Open();
			ProcessStep.End();
		}

		protected void transferCO2FromMCToIP()
		{
			ProcessStep.Start("Evacuate and join IM..Split via VM");
			evacuateIP();
			IM.IsolateFromVacuum();
			evacuateSplit();
			IM.JoinToVacuum();
			waitForVSPressure(0);
			ProcessStep.End();

			ProcessStep.Start("Transfer CO2 from MC to IP");
			Alert("Operator Needed", "Put LN on inlet port.");
			Notice.Send("Operator needed", "Almost ready for LN on inlet port.\r\n" +
				"Press Ok to continue, then raise LN onto inlet port tube");

			VacuumSystem.Isolate();
			MC.JoinToVacuum();		// connects to VM; VacuumSystem state is not changed

			ProcessSubStep.Start("Wait for CO2 to freeze in the IP");
			while (ProcessSubStep.Elapsed.TotalMinutes < 1 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMinutes < 4)
				Wait();
			ProcessSubStep.End();

			Alert("Operator Needed", "Raise inlet port LN.");
			Notice.Send("Operator needed", "Raise inlet port LN one inch.\r\n" +
				"Press Ok to continue.");

			ProcessSubStep.Start("Wait 30 seconds");
			Wait(30000);
			ProcessSubStep.End();

			IP.Close();
			ProcessStep.End();
		}

		#endregion Transfer CO2 between chambers

		#endregion Process Management

		#region Chamber volume calibration routines

		/// <summary>
		/// Install the CalibratedKnownVolume chamber in place of the MCU.
		/// Sets the value of MC.MilliLiters.
		/// </summary>
		protected void calibrateVolumeMC()
		{ VolumeCalibration.Find("MC")?.Calibrate(); }

		/// <summary>
		/// Make sure the MCU is installed (and not the CalibratedKnownVolume).
		/// </summary>
        protected void calibrateAllVolumesFromMC()
		{
			VolumeCalibration.List.ForEach(vol =>
			{ if (!vol.ExpansionVolumeIsKnown) vol.Calibrate(); });
		}

		#endregion Chamber volume calibration routines

		#region Other calibrations
		protected void calibrate_d13C_He()
		{
			if (VPShouldBeClosed())
			{
				Alert("Calibration error", "calibrate_d13C_He: the VP is not available");
				return;
			}

			SampleLog.WriteLine();
			SampleLog.Record("d13C He Calibration");
			SampleLog.Record($"pInitial\tpFinal\tpTarget\tpVP\terror");

			openLine();
			closeAllGRs();

			raise_LN(ftc_VP);

			bool adjusted = false;
			do
			{
				VacuumSystem.WaitForPressure(pressure_ok);
				VP.Close();
				GM_d13C.Open();
				var pa = admitGasFromGMToColdfinger(
					gs_He_GM,
					pressure_VP_He_Initial,
					ftc_VP,
					VP.Valve);
				VP.Open();
				GM.Evacuate();

				var pTarget = pressure_over_atm;
				var nTarget = nParticles(pTarget, mL_VP, temperature_room);
				var dropTarget = pressure(nTarget, mL_GM + mL_d13C, ts_GM.Temperature);
				var n = nParticles(pa[0] - pa[1], mL_GM + mL_d13C, ts_GM.Temperature);
				var p = pressure(n, mL_VP, temperature_room);

				var error = pTarget - p;
				if (Math.Abs(error) / pressure_VP_Error > 0.4)
				{
					var multiplier = pTarget / p;       // no divide-by-zero check
					pressure_VP_He_Initial *= multiplier;
					adjusted = true;
				}
				SampleLog.Record($"{pa[0]:0}\t{pa[1]:0}\t{pTarget:0}\t{p:0}\t{error:0.0}");

			} while (adjusted);

			ftc_VP.Thaw();
			openLine();
		}

		protected void calibrate_GR_H2()
		{
			if (readyGRs() != GraphiteReactors.Count)
			{
				Alert("Calibration error", "calibrate_GR_H2 requires all GRs to be Ready");
				return;
			}

			openLine();
			VP.Close();
			GM_d13C.Close();

			SampleLog.WriteLine();
			SampleLog.Record("densityRatio tests");
			SampleLog.Record("GR\tpInitial\tpFinal\tpNormalized\tpRatio");

			foreach (var gr in GraphiteReactors)
			{
				raise_LN(gr.Coldfinger);

				for (int repeat = 0; repeat < 2; repeat++)
				{
					VacuumSystem.WaitForPressure(pressure_ok);
					closeAllGRs();

					var pa = admitGasFromGMToColdfinger(
						gs_H2_GM,
						850 - 250 * repeat,
						gr.Coldfinger,
						gr.Valve);

					gr.Open();
					GM.Evacuate();

					// p[1] is the pressure in the cold GR with n particles of H2,
					// whereas p would be the pressure if the GR were at the GM temperature.
					// densityAdjustment = p[1] / p
					var n = nParticles(pa[0] - pa[1], mL_GM, ts_GM.Temperature);
					var p = pressure(n, gr.Chamber.MilliLiters, ts_GM.Temperature);
					// The above uses the measured, GR-specific volume. To avoid errors,
					// this procedure should only be performed if the Fe and perchlorate 
					// tubes have never been altered since the GR volumes were measured.

					SampleLog.Record($"{gr.Name}\t{pa[0]:0}\t{pa[1]:0}\t{p:0}\t{pa[1] / p:0.000}");
				}

				gr.Coldfinger.Thaw();
			}

			openLine();
		}
		#endregion Other calibrations

		#region Test functions

		/// <summary>
		/// Admit some O2 into the IP.
		/// The ultimate IP pressure depends on gs_O2_IM pressure and the IM/(IM+VM) volume ratio.
		/// </summary>
		protected void admitIPO2_300()
		{
			IM.Evacuate(pressure_ok);
			IM.ClosePorts();

			gs_O2_IM.Admit(1000);       // dunno, 1000-1500 Torr?
            VacuumSystem.Isolate();

            IM.JoinToVacuum();		// one cycle might keep ~10% in the IM
			IM.Isolate();

			IP.Open();
			Wait(2000);
			IP.Close();
			Wait(5000);

			IM.Evacuate();
		}


		/// <summary>
		/// TODO: This procedure depends on section volume ratios. The amount
		/// of O2 carrier gas will vary based on that.
		/// </summary>
		protected void CO2_MC_IP_MC_loop()
		{
			freeze_VTT();
			transferCO2FromMCToIP();

			admitIPO2_300();        // carrier gas

			Alert("Operator Needed", "Thaw inlet port.");
			Notice.Send("Operator needed", 
				"Remove LN from inlet port and thaw the coldfinger.\r\n" +
				"Press Ok to continue");

            Sample.SampleSourceRef.Name = SampleSource.List.Find(x => x.LinePort == IP).Name;
            bleed();
			extract();
			measure();
			closeAllGRs();				// ? is this needed still ?
		}
		

		protected virtual void cleanupCO2inMC()
		{
			transferCO2FromMCToVTT();
			extract();
			measure();
		}


		protected virtual void measureIPExtractEfficiency()
		{
			measureProcessEfficiency(CO2_MC_IP_MC_loop);
		}


		protected virtual void measureExtractEfficiency()
		{
			measureProcessEfficiency(cleanupCO2inMC);
		}



		/// <summary>
		/// Set the Sample ID to the desired number of loops
		/// Set the Sample mass to the desired starting quantity
		/// If there is at least 80% of the desired starting quantity
		/// already in the measurement chamber, that will be used
		/// instead of admitting fresh gas.
		/// </summary>
		/// <param name="transferLoop">method to move sample from MC to somewhere else and back</param>
		protected virtual void measureProcessEfficiency(Action transferLoop)
		{
			SampleLog.WriteLine("\r\n");
			SampleLog.Record("Process Efficiency test");

			ProcessStep.Start("Measure transfer efficiency");
			if (ugCinMC < Sample.micrograms * 0.8)
			{
				VTC.Dirty = false;  // keep cold
				openLine();
				waitForVSPressure(pressure_clean);
				admitDeadCO2(Sample.micrograms);
			}
			cleanupCO2inMC();

			int n; try { n = int.Parse(Sample.ID); } catch { n = 1; }
			for (int repeats = 0; repeats < n; repeats++)
			{
				Sample.micrograms = Sample.ugC;
				transferLoop();
			}
			ProcessStep.End();
		}


		// Discards the MC contents soon after they reach the 
		// temperature at which they were extracted.
		protected void discardExtractedGases()
		{
			ProcessStep.Start("Discard extracted gases");
			ftc_MC.Thaw();
			ProcessSubStep.Start("Wait for MC coldfinger to thaw enough.");
			while (ftc_MC.Temperature <= VTC.RegulatedSetpoint + 10) Wait();
			ProcessSubStep.End();
			ftc_MC.Stop();	// stop thawing to save time

			// record pressure
			SampleLog.Record($"\tPressure of pre-CO2 discarded gases:\t{m_p_MC.Value:0.000}\tTorr");

			VTT_MC.OpenAndEvacuate(pressure_ok);
			VTT_MC.IsolateFromVacuum();
			ProcessStep.End();
		}

		protected void stepExtract()
		{
			pressurizeVTT_MC();
			// The equilibrium temperature of HCl at pressures from ~(1e-5..1e1)
			// is about 14 degC or more colder than CO2 at the same pressure.
			pressurizedExtract(-13);		// targets HCl
			discardExtractedGases();
			pressurizedExtract(1);		// targets CO2
		}

		protected void stepExtractionYieldTest()
		{
			Sample.ID = "Step Extraction Yield Test";
			//admitDeadCO2(1000);
			measure();

			transferCO2FromMCToVTT();
			extract();
			measure();

			//transfer_CO2_MC_VTT();
			//step_extract();
			//VTT.Stop();
			//measure();
		}

		protected virtual void test() { }

		#endregion Test functions
	}
}
