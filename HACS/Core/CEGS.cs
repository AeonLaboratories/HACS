using System;
using System.Collections.Generic;
using System.Linq;
using HACS.Components;
using System.Xml.Serialization;
using System.Threading;
using System.IO;
using System.Net.Mail;
using Utilities;

namespace HACS.Core
{
	public enum ProcessStates { Ready, Busy, Finished }
	public enum sections { FC, IM, VTT, split, GM }
	public enum Gases { He, H2, CO2, O2, Ar, Air }

	public class CEGS
	{
		static readonly bool On = true, Off = false;

		#region System configuration

		#region Globals

		public string SystemName { get; set; } = "CEGS";
		public string LastAlertMessage { get; set; }
		public ContactInfo ContactInfo { get; set; }
		public SmtpInfo SmtpInfo { get; set; }

		#region UI Communications

		[XmlIgnore] public MessageSender MessageHandler = new MessageSender();		
		[XmlIgnore] public Func<bool, bool> VerifySampleInfo;
		[XmlIgnore] public Action ShowProcessSequenceEditor;
		[XmlIgnore] public Action PlaySound;

		#endregion UI Communications

		#region Logging
		
		public string LogFolder { get; set; }
		public string ArchiveFolder { get; set; }

		protected LogFile VMPLog;
		protected LogFile GRPLog;
		protected LogFile FTCLog;
		protected LogFile VLog;
		protected LogFile TLog;
		protected LogFile PLog;
		protected LogFile AmbientLog;
		protected LogFile MCLog;
		protected LogFile VTTLog;
		protected LogFile SampleLog;
		[XmlIgnore] public LogFile EventLog;

		#endregion Logging

		#region System state & operations
		
		public bool EnableWatchdogs { get; set; }
		public bool EnableAutozero { get; set; }
		public bool IonGaugeAuto { get; set; }
		public string Last_GR { get; set; }
		public int Next_GraphiteNumber { get; set; }

		[XmlIgnore] public double p_VM { get; set; }
		[XmlIgnore] public bool PowerFailed { get; set; }

		#endregion System state & operations

		public int CurrentSample = 0;   // Future proofing. Stays constant for now.

		[XmlIgnore] public Sample Sample
		{
			get
			{
				return SystemComponents.Samples[CurrentSample];
			}
			set
			{
				SystemComponents.Samples[CurrentSample] = value;
			}
		}

		#endregion Globals

		#region SystemComponents

		public SystemComponents SystemComponents;

		#region LabJack DAQ
		[XmlIgnore] public int[] LabJack_LocalID;   // list of LabJack Local IDs
		#endregion LabJack DAQ

		#region Analog IO (meters)
		[XmlIgnore] public Meter m_p_MC;
		[XmlIgnore] public Meter m_p_Foreline;
		[XmlIgnore] public Meter m_p_VTT;
		[XmlIgnore] public Meter m_p_IM;
		[XmlIgnore] public Meter m_p_GM;
		[XmlIgnore] public Meter m_v_LN_supply;
		[XmlIgnore] public Meter m_p_Ambient;
		[XmlIgnore] public Meter m_V_5VPower;
		[XmlIgnore] public Meter m_p_VM_IG;
		[XmlIgnore] public Meter m_p_VM_HP;
		[XmlIgnore] public Meter m_t_MC;
		[XmlIgnore] public Meter m_t_muxAIN13;
		[XmlIgnore] public Meter[] m_p_GR;
		#endregion Analog IO (meters)

		#region Digital IO
		[XmlIgnore] public DigitalOutput IonGaugeEnable;
		#endregion Digital IO

		#region Serial devices
		[XmlIgnore] public ServoController ActuatorController;
		[XmlIgnore] public List<ThermalController> ThermalControllers;
		#endregion Serial devices

		#region Valves
		[XmlIgnore] public Valve v_HV;
		[XmlIgnore] public Valve v_LV;
		[XmlIgnore] public Valve v_B;

		[XmlIgnore] public Valve v_IM_VM;
		[XmlIgnore] public Valve v_VTTR_VM;
		[XmlIgnore] public Valve v_split_VM;
		[XmlIgnore] public Valve v_GM_VM;

		[XmlIgnore] public Valve v_He_IM;
		[XmlIgnore] public Valve v_O2_IM;
		[XmlIgnore] public Valve v_IP_IM;
		[XmlIgnore] public Valve v_IM_VTTL;
		[XmlIgnore] public Valve v_He_VTTL;
		[XmlIgnore] public Valve v_VTT_flow;
		[XmlIgnore] public Valve v_VTTL_VTTR;
		[XmlIgnore] public Valve v_VTTR_CuAg;
		[XmlIgnore] public Valve v_CuAg_MC;
		[XmlIgnore] public Valve v_MC_MCU;
		[XmlIgnore] public Valve v_MC_MCL;
		[XmlIgnore] public Valve v_MC_split;
		[XmlIgnore] public Valve v_split_GM;
		[XmlIgnore] public Valve v_He_GM;
		[XmlIgnore] public Valve v_He_GM_flow;
		[XmlIgnore] public Valve v_H2_GM;
		[XmlIgnore] public Valve v_H2_GM_flow;
		[XmlIgnore] public Valve v_CO2_GM;
		[XmlIgnore] public Valve v_CO2_GM_flow;
		[XmlIgnore] public Valve[] v_GR_GM;
		[XmlIgnore] public Valve v_d13C_GM;
		[XmlIgnore] public Valve v_VP_d13C;

		[XmlIgnore] public Valve v_LN_VTT;
		[XmlIgnore] public Valve v_LN_CuAg;
		[XmlIgnore] public Valve v_LN_MC;
		[XmlIgnore] public Valve[] v_LN_GR;
		[XmlIgnore] public Valve v_LN_VP;
		#endregion Valves;

		#region Heaters
		[XmlIgnore] public Heater h_VTT;
		[XmlIgnore] public Heater h_CuAg;
		[XmlIgnore] public Heater[] h_GR;
		[XmlIgnore] public Heater h_CC_Q;
		[XmlIgnore] public Heater h_CC_S;
		[XmlIgnore] public Heater h_CC_S2;
		[XmlIgnore] public Heater h_FTC_air;
		#endregion Heaters

		#region Temperature Sensors
		[XmlIgnore] public TempSensor ts_LN_Tank;

		[XmlIgnore] public TempSensor ts_LN_VTT;
		[XmlIgnore] public TempSensor ts_VTT_wire;
		[XmlIgnore] public TempSensor ts_VTT_top;
		[XmlIgnore] public TempSensor ts_LN_CuAg;
		[XmlIgnore] public TempSensor ts_LN_MC;
		[XmlIgnore] public TempSensor ts_GM;
		[XmlIgnore] public TempSensor[] ts_LN_GR;
		[XmlIgnore] public TempSensor ts_LN_VP;

		[XmlIgnore] public TempSensor ts_tabletop;
		#endregion Temperature Sensors

		#region Graphite Reactors
		[XmlIgnore] public GraphiteReactor[] GR;
		#endregion Graphite Reactors

		#region SwitchBanks
		[XmlIgnore] public SwitchBank SB0;
		#endregion SwitchBanks

		#region OnOffDevices
		[XmlIgnore] public OnOffDevice air_VTT_FTC;
		[XmlIgnore] public OnOffDevice air_CuAg_FTC;
		[XmlIgnore] public OnOffDevice air_MC_FTC;
		[XmlIgnore] public OnOffDevice[] air_GR_FTC;
		[XmlIgnore] public OnOffDevice air_VP_FTC;

		[XmlIgnore] public OnOffDevice LN_Tank_LN;

		[XmlIgnore] public OnOffDevice pump_HV;

		[XmlIgnore] public OnOffDevice fan_IP;
		#endregion OnOffDevices

		#region Tanks
		[XmlIgnore] public Tank LN_Tank;
		#endregion Tanks

		#region Freeze-Thaw Coldfingers
		[XmlIgnore] public FTColdfinger ftc_VTT;
		[XmlIgnore] public FTColdfinger ftc_CuAg;
		[XmlIgnore] public FTColdfinger ftc_MC;
		[XmlIgnore] public FTColdfinger[] ftc_GR;
		[XmlIgnore] public FTColdfinger ftc_VP;
		#endregion Freeze-Thaw Coldfingers

		#region Variable Temperature Traps
		[XmlIgnore] public VTT VTT;
		#endregion Variable Temperature Traps

		#region Mass Flow Controllers
		#endregion Mass Flow Controllers

		#region Line Ports
		[XmlIgnore] public LinePort IP;  // Inlet Port
		[XmlIgnore] public LinePort VP;  // Vial Port
		#endregion Line Ports

		#region Dynamic Quantitites
		//[XmlIgnore] public DynamicQuantity p_VMdq;
		[XmlIgnore] public DynamicQuantity ugCinMC;
		#endregion Dynamic Quantities

		#endregion SystemComponents

		#region Constants
		// the only way to alter constants is to edit settings.xml

		#region Pressure Constants

		public double pressure_over_atm;
		public double pressure_ok;                // clean enough to join sections for drying
		public double pressure_clean;              // clean enough to start a new sample
		public double pressure_baseline;
		public double pressure_backstreaming_safe;  // min roughing VM pressure
		public double pressure_backstreaming_limit; // min foreline pressure for LV open
		public double pressure_close_LV;
		public double pressure_open_HV;
		public double pressure_switch_LV_to_HV;
		public double pressure_switch_HV_to_LV;
		public double pressure_max_HV;
		public double pressure_max_gas_purge;
		public double pressure_VM_max_IG;           // max pressure to read exclusively from ion gauge
		public double pressure_VM_min_HP;           // min pressure to read exclusively from HP gauge
		public double pressure_VM_switchpoint;      // ion gauge on/off switchpoint pressure
		public double pressure_VM_measureable;    // anything below this is definitely not overrange


		// When He is admitted from GM+d13C into VP, the nominal pGM drop is:
		//	  pHeInitial - pHeFinal = pressure_over_atm * vVP / (vGM + vd13C)
		// Works out to be very close to 187 Torr.
		// Empirically, 
		//	  495 drops ~196
		public double pressure_VP_He_Initial;
		public double pressure_VP_He_Drop;
		public double pressure_VP_Error;                // abs(pVP - pressure_over_atm) < this value is nominal

		public double pressure_IM_O2;
		public double pressure_VTT_bleed_sample;
		public double pressure_VTT_bleed_cleaning;
		public double pressure_VTT_near_end_of_bleed;

		public double pressure_Fe_prep_H2;

		public double pressure_foreline_empty;
		public double pressure_max_backing;
		public double pressure_VP_He_Final;

		public double pressure_calibration;      // Torr of He

		#endregion Pressure Constants

		#region Rate of Change Constants

		public double roc_pVTT_falling_rapidly;
		public double roc_pVTT_near_end_of_bleed;
		public double roc_pVTT_stable;
		public double roc_pVTT_falling;
		public double roc_pVTT_falling_very_slowly;
		public double roc_pVTT_falling_barely;
		public double roc_pVTT_rising;

		public double roc_pForeline_rising;
		public double roc_ugc_stable;
		public double roc_ugc_rising;
		public double roc_pGM_rising;

		public double roc_pIM_rising;
		public double roc_pIM_falling;
		public double roc_pIM_stable;
		public double roc_pIM_plugged;
		public double roc_pIM_loaded;

		#endregion Rate of Change Constants

		#region Temperature Constants
		public int temperature_room;        // "standard" room temperature
		public int temperature_warm;
		public int temperature_CO2_evolution;
		public int temperature_CO2_collection_min;
		public int temperature_FTC_frozen;
		public int temperature_FTC_raised;
		public int temperature_VTT_cold;
		public int temperature_VTT_cleanup;
		public int temperature_trap_sulfur;

		public int temperature_Fe_prep;
		public int temperature_Fe_prep_max_error;

		#endregion

		#region Time Constants
		public int minutes_Fe_prep;
		public int minutes_CC_Q_Warmup;
		public int minutes_trap_sulfur;
		public int seconds_FTC_raised;
		public int seconds_flow_shutoff_purge;
		public double seconds_GM_stability_delay;   // effective seconds to stability after shutoff valve closed
		public int milliseconds_power_down_max;
		public int milliseconds_UpdateLoop_interval;
		public int milliseconds_calibration;        // milliseconds of settling time
		public int milliseconds_IG_stabilize;
		public int milliseconds_IG_min_off;
		#endregion

		#region Sample Measurement Constants

		// fundamental constants
		public double L;                // Avogadro's number (particles/mol)
		public double kB;              // Boltzmann constant (Pa * m^3 / K)
		public double Pa;              // Pascals (1/atm)
		public double Torr;          // (1/atm)
		public double mL;              // milliliters per liter
		public double m3;              // cubic meters per liter

		public double ZeroDegreesC;  // kelvins
		public double ugC_mol;        // mass of carbon per mole, in micrograms,
									  // assuming standard isotopic composition

		public MC_GM MC_GM = MC_GM.Default;
			
		// chamber volumes (mL)
		public double mL_KV;            // known volume
		public double mL_VM;            // vacuum manifold
		public double mL_IM;            // intake manifold
		public double mL_IP;            // inlet port
		public double mL_VTT;          // VTT
		public double mL_CuAg;        // copper/silver trap
		public double mL_MC;            // measurement chamber
		public double mL_MCU;          // upper aliquot
		public double mL_MCL;          // lower aliquot
		public double mL_split;      // split chamber
		public double mL_GM;            // graphite manifold
		public double mL_GR1;          // graphite reactor 1
		public double mL_GR2;
		public double mL_GR3;
		public double mL_GR4;
		public double mL_GR5;
		public double mL_GR6;
		public double mL_d13C;        // d13C aliquant
		public double mL_VP;            // vial port (with vial)

		public double H2_CO2_stoich;    // stoichiometric
		public double H2_CO2;          // target H2:CO2 ratio for graphitization

		// The value below is an average of pressure ratios observed for a quantity of H2 
		// in the GRs. The denominator pressures are observed with the GR at room 
		// temperature (same as GM). The numerator pressures are several minimums observed 
		// with the GRs cooled by FTCs to the "raise" state.
		public double densityAdjustment;   // pressure reduction due to higher density of H2 in GR coldfinger

		public int mass_small_sample;
		public int mass_diluted_sample;
		public int ugC_sample_max;

		public double roc_ugc_rising_rapidly;
		public double roc_pGM_rising_rapidly;

		// kB using Torr and milliliters instead of pascals and cubic meters
		public double kB_Torr_mL;
		public double nC_ug;            // average number of carbon atoms per microgram

		// Useful volume ratios
		public double rAMS;          // remaining for AMS after d13C is taken
		public double rMCU;
		public double rMCL;

		public int ugC_d13C_max;

		#endregion Sample Measurement Constants

		public int LN_supply_min;
		public double V_5VMainsDetect_min;

		[XmlIgnore] LookupTable CO2EqTable = new LookupTable(@"CO2 eq.dat");

		#endregion Constants
		
		#endregion System configuration

		#region System elements not saved/restored in Settings

		[XmlIgnore] public string SettingsFilename = @"settings.xml";

		// for requesting user interface services
		[XmlIgnore] public EventHandler RequestService;

		protected XmlSerializer XmlSerializer;

		#region Threading

		protected Thread updateThread;
		protected ManualResetEvent updateSignal = new ManualResetEvent(false);
		protected Thread serverThread;

		// alert system
		protected Queue<AlertMessage> QAlertMessage = new Queue<AlertMessage>();
		protected Thread alertThread;
		protected ManualResetEvent alertSignal = new ManualResetEvent(false);
		protected Stopwatch AlertTimer = new Stopwatch();

		// logging
		protected Thread systemLogThread;
		protected ManualResetEvent systemLogSignal = new ManualResetEvent(false);

		// low priority activity
		protected Thread lowPriorityThread;
		protected ManualResetEvent lowPrioritySignal = new ManualResetEvent(false);

		#endregion Threading

		// system conditions
		[XmlIgnore] public Stopwatch SystemRunTime { get; set; } = new Stopwatch();
		[XmlIgnore] public bool Initialized = false;
		[XmlIgnore] public bool ShuttingDown = false;

		// process management
		[XmlIgnore] public ProcessStates ProcessState = ProcessStates.Ready;
		[XmlIgnore] public Thread ProcessThread = null;
		[XmlIgnore]	public Stopwatch ProcessTime { get; set; } = new Stopwatch();

		[XmlIgnore]
		public StepTracker ProcessStep { get; set; } = new StepTracker("ProcessStep");

		[XmlIgnore]
		public StepTracker ProcessSubStep { get; set; } = new StepTracker("ProcessSubStep");

		[XmlIgnore] public Dictionary<string, ThreadStart> ProcessDictionary = new Dictionary<string, ThreadStart>();
		[XmlIgnore] public string ProcessToRun;
		[XmlIgnore] bool runStarted = false;
		[XmlIgnore] public bool SampleIsRunning { get { return runStarted; } }

		[XmlIgnore] protected Stopwatch BaselinePressureTimer = new Stopwatch();
		[XmlIgnore] protected Stopwatch PowerDownTimer = new Stopwatch();

		#endregion System elements not saved in/restored from Settings

		#region Startup and ShutDown

		public void Start()
		{
			startLogs();
			calculateDerivedConstants();
			getSystemComponents();
			connectSystemComponents();
			buildProcessDictionary();
		}

		public void StartInitializing()
		{
			initializeSystemComponents();
			SystemRunTime.Start();
			initializeThreads();
		}

		public void ShutDown()
		{
			try
			{
				ShuttingDown = true;
				EventLog.Record("System shutting down");

				closeLNValves();

				saveSettings(SettingsFilename);

				LN_Tank.IsActive = false;
				h_VTT.TurnOff();

				foreach (OnOffDevice d in OnOffDevice.List)
				{
					// TODO: this test is too specialized
					// we should either:
					//		1. not turnOff any devices, or
					//		2. maintain a list of devices that should be (or shouldn't be)
					//			forced to a given state on shutdown
					if (d != pump_HV && d != fan_IP)
						d.TurnOff();
				}

				DisableIonGauge();
				foreach (LabJackDaq lj in LabJackDaq.List)
				{
					if (lj.IsUp)
						while (lj.PendingAO + lj.PendingDO > 0)
							wait(1);
					lj.Stop();
				}

				foreach (ServoController c in ServoController.List)
				{
					// should this be something like c.waitForIdle() ?
					waitForActuatorController();
					c.Close();
				}

				foreach (ThermalController c in ThermalController.List)
					c.Stop();

				foreach (EurothermFurnace c in EurothermFurnace.List)
					c.Close();

				stopLogs();
			}
			catch (Exception e)
			{
				MessageHandler.Send(e.ToString());
			}
		}

		#region Logs

		protected virtual void startLogs()
		{
			VMPLog = CEGSLog(@"VM Pressure data.txt");
			GRPLog = CEGSLog(@"GR data.txt");
			FTCLog = CEGSLog(@"FTC data.txt");
			VLog = CEGSLog(@"Voltage data.txt");
			TLog = CEGSLog(@"Temperature data.txt");
			PLog = CEGSLog(@"Pressure data.txt");
			AmbientLog = CEGSLog(@"Ambient data.txt");
			MCLog = CEGSLog(@"MC data.txt");
			VTTLog = CEGSLog(@"VTT data.txt");
			SampleLog = CEGSLog(@"Sample data.txt", false);
			EventLog = CEGSLog(@"Event log.txt", false);
		}

		protected virtual LogFile CEGSLog(string fileName)
		{
			return CEGSLog(fileName, true);
		}

		protected virtual LogFile CEGSLog(string fileName, bool archiveDaily)
		{
			LogFile log = new LogFile(fileName, archiveDaily);
			log.LogFolder = LogFolder;
			log.ArchiveFolder = ArchiveFolder;
			return log;
		}

		protected virtual void stopLogs()
		{
			VMPLog.Close();
			GRPLog.Close();
			FTCLog.Close();
			VLog.Close();
			TLog.Close();
			PLog.Close();
			AmbientLog.Close();
			MCLog.Close();
			VTTLog.Close();
			SampleLog.Close();

			EventLog.Close();
		}

		#endregion Logs

		#region SystemComponents

		protected virtual void getSystemComponents()
		{
			#region DAQs
			#endregion DAQs

			#region Meters
			m_p_MC = Meter.Find("m_p_MC");
			m_p_VM_HP = Meter.Find("m_p_VM_HP");
			m_p_VM_IG = Meter.Find("m_p_VM_IG");
			m_p_Foreline = Meter.Find("m_p_Foreline");
			m_p_VTT = Meter.Find("m_p_VTT");
			m_p_IM = Meter.Find("m_p_IM");
			m_p_GM = Meter.Find("m_p_GM");
			m_v_LN_supply = Meter.Find("m_v_LN_supply");
			m_p_Ambient = Meter.Find("m_p_Ambient");
			m_V_5VPower = Meter.Find("m_V_5VPower");
			m_t_MC = Meter.Find("m_t_MC");
			m_t_muxAIN13 = Meter.Find("m_t_muxAIN13");
			m_p_GR = new Meter[6];
			m_p_GR[0] = Meter.Find("m_p_GR1");
			m_p_GR[1] = Meter.Find("m_p_GR2");
			m_p_GR[2] = Meter.Find("m_p_GR3");
			m_p_GR[3] = Meter.Find("m_p_GR4");
			m_p_GR[4] = Meter.Find("m_p_GR5");
			m_p_GR[5] = Meter.Find("m_p_GR6");
			#endregion Meters

			#region DigitalOutputs
			IonGaugeEnable = DigitalOutput.Find("IonGaugeEnable");
			#endregion DigitalOutputs

			#region ActuatorControllers
			ActuatorController = ServoController.Find("ActuatorController");
			#endregion ActuatorControllers

			#region ThermalControllers
			ThermalControllers = ThermalController.List;
			#endregion ThermalControllers

			#region EurothermFurnaces
			#endregion EurothermFurnaces

			#region Valves
			v_HV = Valve.Find("v_HV");
			v_LV = Valve.Find("v_LV");
			v_B = Valve.Find("v_B");

			v_IM_VM = Valve.Find("v_IM_VM");
			v_VTTR_VM = Valve.Find("v_VTTR_VM");
			v_split_VM = Valve.Find("v_split_VM");
			v_GM_VM = Valve.Find("v_GM_VM");

			v_He_IM = Valve.Find("v_He_IM");
			v_O2_IM = Valve.Find("v_O2_IM");
			v_IP_IM = Valve.Find("v_IP_IM");

			v_IM_VTTL = Valve.Find("v_IM_VTTL");
			v_He_VTTL = Valve.Find("v_He_VTTL");
			v_VTTL_VTTR = Valve.Find("v_VTTL_VTTR");
			v_VTTR_CuAg = Valve.Find("v_VTTR_CuAg");
			v_CuAg_MC = Valve.Find("v_CuAg_MC");

			v_MC_MCU = Valve.Find("v_MC_MCU");
			v_MC_MCL = Valve.Find("v_MC_MCL");
			v_MC_split = Valve.Find("v_MC_split");
			v_split_GM = Valve.Find("v_split_GM");

			v_He_GM = Valve.Find("v_He_GM");
			v_H2_GM = Valve.Find("v_H2_GM");
			v_CO2_GM = Valve.Find("v_CO2_GM");

			v_GR_GM = new Valve[6];
			v_GR_GM[0] = Valve.Find("v_GR1_GM");
			v_GR_GM[1] = Valve.Find("v_GR2_GM");
			v_GR_GM[2] = Valve.Find("v_GR3_GM");
			v_GR_GM[3] = Valve.Find("v_GR4_GM");
			v_GR_GM[4] = Valve.Find("v_GR5_GM");
			v_GR_GM[5] = Valve.Find("v_GR6_GM");
			v_d13C_GM = Valve.Find("v_d13C_GM");

			v_VP_d13C = Valve.Find("v_VP_d13C");

			v_VTT_flow = Valve.Find("v_VTT_flow");
			v_He_GM_flow = Valve.Find("v_He_GM_flow");
			v_H2_GM_flow = Valve.Find("v_H2_GM_flow");
			v_CO2_GM_flow = Valve.Find("v_CO2_GM_flow");

			v_LN_VTT = Valve.Find("v_LN_VTT");
			v_LN_CuAg = Valve.Find("v_LN_CuAg");
			v_LN_MC = Valve.Find("v_LN_MC");
			v_LN_GR = new Valve[6];
			v_LN_GR[0] = Valve.Find("v_LN_GR1");
			v_LN_GR[1] = Valve.Find("v_LN_GR2");
			v_LN_GR[2] = Valve.Find("v_LN_GR3");
			v_LN_GR[3] = Valve.Find("v_LN_GR4");
			v_LN_GR[4] = Valve.Find("v_LN_GR5");
			v_LN_GR[5] = Valve.Find("v_LN_GR6");
			v_LN_VP = Valve.Find("v_LN_VP");
			#endregion Valves

			#region Heaters
			h_GR = new Heater[6];
			h_GR[0] = Heater.Find("h_GR1");
			h_GR[1] = Heater.Find("h_GR2");
			h_GR[2] = Heater.Find("h_GR3");
			h_GR[3] = Heater.Find("h_GR4");
			h_GR[4] = Heater.Find("h_GR5");
			h_GR[5] = Heater.Find("h_GR6");
			h_CC_Q = Heater.Find("h_CC_Q");
			h_CC_S = Heater.Find("h_CC_S");
			h_CC_S2 = Heater.Find("h_CC_S2");
			h_VTT = Heater.Find("h_VTT");
			h_CuAg = Heater.Find("h_CuAg");
			h_FTC_air = Heater.Find("h_FTC_air");
			#endregion Heaters

			#region TempSensors
			ts_LN_Tank = TempSensor.Find("ts_LN_Tank");
			ts_LN_VTT = TempSensor.Find("ts_LN_VTT");
			ts_VTT_wire = TempSensor.Find("ts_VTT_wire");
			ts_VTT_top = TempSensor.Find("ts_VTT_top");
			ts_LN_CuAg = TempSensor.Find("ts_LN_CuAg");
			ts_LN_MC = TempSensor.Find("ts_LN_MC");
			ts_GM = TempSensor.Find("ts_GM");
			ts_LN_GR = new TempSensor[6];
			ts_LN_GR[0] = TempSensor.Find("ts_LN_GR1");
			ts_LN_GR[1] = TempSensor.Find("ts_LN_GR2");
			ts_LN_GR[2] = TempSensor.Find("ts_LN_GR3");
			ts_LN_GR[3] = TempSensor.Find("ts_LN_GR4");
			ts_LN_GR[4] = TempSensor.Find("ts_LN_GR5");
			ts_LN_GR[5] = TempSensor.Find("ts_LN_GR6");
			ts_LN_VP = TempSensor.Find("ts_LN_VP");
			ts_tabletop = TempSensor.Find("ts_tabletop");
			#endregion TempSensors

			#region SwitchBanks
			SB0 = SwitchBank.Find("SB0");
			#endregion SwitchBanks

			#region OnOffDevices
			air_VTT_FTC = OnOffDevice.Find("air_VTT_FTC");
			air_CuAg_FTC = OnOffDevice.Find("air_CuAg_FTC");
			air_MC_FTC = OnOffDevice.Find("air_MC_FTC");
			air_GR_FTC = new OnOffDevice[6];
			air_GR_FTC[0] = OnOffDevice.Find("air_GR_FTC1");
			air_GR_FTC[1] = OnOffDevice.Find("air_GR_FTC2");
			air_GR_FTC[2] = OnOffDevice.Find("air_GR_FTC3");
			air_GR_FTC[3] = OnOffDevice.Find("air_GR_FTC4");
			air_GR_FTC[4] = OnOffDevice.Find("air_GR_FTC5");
			air_GR_FTC[5] = OnOffDevice.Find("air_GR_FTC6");
			air_VP_FTC = OnOffDevice.Find("air_VP_FTC");
			LN_Tank_LN = OnOffDevice.Find("LN_Tank_LN");
			pump_HV = OnOffDevice.Find("pump_HV");
			fan_IP = OnOffDevice.Find("fan_IP");
			#endregion OnOffDevices

			#region Tanks
			LN_Tank = Tank.Find("LN_Tank");
			#endregion Tanks

			#region FTCs
			ftc_VTT = FTColdfinger.Find("ftc_VTT");
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

			#region VTTs
			VTT = HACS.Components.VTT.Find("VTT");
			#endregion VTTs

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
		}

		protected virtual void connectSystemComponents()
		{
			foreach (Meter x in Meter.List) x.Connect();
			foreach (DigitalOutput x in DigitalOutput.List) x.Connect();
			foreach (Valve x in Valve.List) x.Connect(ActuatorController);		// TODO: name and add to settings.xml
			foreach (Heater x in Heater.List) x.Connect();
			foreach (TempSensor x in TempSensor.List) x.Connect();
			foreach (GraphiteReactor x in GraphiteReactor.List) x.Connect();
			foreach (SwitchBank x in SwitchBank.List) x.Connect();
			foreach (OnOffDevice x in OnOffDevice.List) x.Connect();
			foreach (Tank x in Tank.List) x.Connect();
			foreach (FTColdfinger x in FTColdfinger.List) x.Connect();
			foreach (VTT x in VTT.List) x.Connect();
			foreach (MassFlowController x in MassFlowController.List) x.Connect();
			foreach (var x in DynamicQuantity.List) x.Connect();

			ftc_MC.AirTemperatureSensor = m_t_MC;           // TODO: name and add to settings.xml
		}

		protected virtual void initializeSystemComponents()
		{
			m_p_MC.StateChanged += updateSampleMeasurement;
			m_p_VM_IG.StateChanged += update_p_VM;

			GR[0].MilliLitersVolume = mL_GR1;           // TODO: add ChamberVolumes list to SystemComponents and add/find these names in settings.xml
			GR[1].MilliLitersVolume = mL_GR2;
			GR[2].MilliLitersVolume = mL_GR3;
			GR[3].MilliLitersVolume = mL_GR4;
			GR[4].MilliLitersVolume = mL_GR5;
			GR[5].MilliLitersVolume = mL_GR6;

			foreach (LabJackDaq x in LabJackDaq.List) x.Initialize();
			foreach (Meter x in Meter.List) x.Initialize();
			foreach (DigitalOutput x in DigitalOutput.List) x.Initialize();
			foreach (ServoController x in ServoController.List) x.Initialize();
			foreach (ThermalController x in ThermalController.List) x.Initialize();
			foreach (EurothermFurnace x in EurothermFurnace.List) x.Initialize();
			foreach (Valve x in Valve.List) x.Initialize();
			foreach (Heater x in Heater.List) x.Initialize();
			foreach (TempSensor x in TempSensor.List) x.Initialize();
			foreach (SwitchBank x in SwitchBank.List) x.Initialize();
			foreach (OnOffDevice x in OnOffDevice.List) x.Initialize();
			foreach (Tank x in Tank.List) x.Initialize();
			foreach (FTColdfinger x in FTColdfinger.List) x.Initialize();
			foreach (VTT x in HACS.Components.VTT.List) x.Initialize();
			foreach (GraphiteReactor x in GraphiteReactor.List) x.Initialize();
			foreach (MassFlowController x in MassFlowController.List) x.Initialize();
			foreach (LinePort x in LinePort.List) x.Initialize();
			foreach (Sample x in SystemComponents.Samples)
				foreach (Aliquot a in x.Aliquots) a.Sample = x;
			foreach (DynamicQuantity d in DynamicQuantity.List) d.Initialize();
		}

		#endregion SystemComponents

		protected void calculateDerivedConstants()
		{
			pressure_foreline_empty = pressure_backstreaming_limit;
			pressure_max_backing = pressure_open_HV;
			pressure_VP_He_Final = pressure_VP_He_Initial - pressure_VP_He_Drop;
			roc_ugc_rising_rapidly = 2 * roc_ugc_rising;
			roc_pGM_rising_rapidly = 2 * roc_pGM_rising;

			#region Sample measurement constants
			kB_Torr_mL = kB * Torr / Pa * mL / m3;
			nC_ug = L / ugC_mol;        // number of atoms per microgram of carbon (standard isotopic distribution)

			rAMS = 1 - mL_d13C / (mL_MC + mL_split + mL_GM + mL_d13C);

			rMCU = mL_MCU / mL_MC;
			rMCL = mL_MCL / mL_MC;

			ugC_d13C_max = (int)((1 - rAMS) * (double)ugC_sample_max);
			#endregion Sample measurement constants
		}

		// Even though there is a lot of commonality between systems for the
		// process dictionary, order is important so they are handled separately
		protected virtual void buildProcessDictionary() { }

		protected void initializeThreads()
		{
			alertThread = new Thread(AlertHandler);
			alertThread.Name = "alertThread";
			alertThread.IsBackground = true;
			alertThread.Start();

			//Alert("System Alert", "System Started");
			EventLog.Record("System Started");

			systemLogThread = new Thread(logSystemStatus);
			systemLogThread.Name = "system logging thread";
			systemLogThread.IsBackground = true;
			systemLogThread.Start();

			lowPriorityThread = new Thread(lowPriorityActivities);
			lowPriorityThread.Name = "low priority thread";
			lowPriorityThread.IsBackground = true;
			lowPriorityThread.Start();

			updateThread = new Thread(UpdateLoop);
			updateThread.Name = "updateThread";
			updateThread.IsBackground = true;
			updateThread.Start();
					}

		protected void saveSettings(string filename)
		{
			if (!Initialized) return;
			try
			{
				try
				{
					string backup = filename.Replace(".xml", ".backup.xml");
					System.IO.File.Copy(filename, backup, true);
				}
				catch { }
				TextWriter writer = new StreamWriter(filename, false);
				XmlSerializer.Serialize(writer, this);
				writer.Close();
			}
			catch (Exception e)
			{
				EventLog.Record("Exception saving settings\r\n" + e.ToString());
			}
		}

		#endregion Startup and ShutDown

		#region Periodic system activities & maintenance

		protected void EnableIonGauge()
		{
			if (!IonGaugeEnable.IsOn)
				IonGaugeEnable.SetOutput(On);
		}

		protected void DisableIonGauge()
		{
			if (IonGaugeEnable.IsOn)
				IonGaugeEnable.SetOutput(Off);
		}

		protected virtual void update_p_VM()
		{
			update_p_VM(m_p_VM_IG, m_p_VM_HP);
		}

		// m_IG is low pressure meter (ion gauge)
		// m_HP is high pressure (> high vacuum)
		protected void update_p_VM(Meter m_IG, Meter m_HP)
		{
			double pIG = m_IG;
			double pHP = Math.Max(m_HP, m_HP.Sensitivity);

			if (pHP > pressure_VM_min_HP || IonGaugeEnable.MillisecondsOn < milliseconds_IG_stabilize)
				p_VM = pHP;
			else if (pIG < pressure_VM_max_IG)
				p_VM = pIG;
			else if (pIG > pHP)
				p_VM = pHP;
			else      // pressure_VM_max_IG <= pIG <= pHP <= pressure_VM_min_HP
			{
				// high pressure reading weight coefficient
				double whp = (pHP - pressure_VM_max_IG) / (pressure_VM_min_HP - pressure_VM_max_IG);
				p_VM = whp * pHP + (1 - whp) * pIG;
			}


			if (IonGaugeAuto == true)
			{
				bool enableIonGauge = v_HV.isOpened && v_B.isOpened && m_p_Foreline < pressure_foreline_empty;

				if (!enableIonGauge)
					DisableIonGauge();
				else if (IonGaugeEnable.MillisecondsInState > milliseconds_IG_min_off)
				{
					bool pressureHigh = pHP > pressure_VM_switchpoint;
					if (pressureHigh == IonGaugeEnable.IsOn)
						IonGaugeEnable.SetOutput(!pressureHigh);
				}
			}

			// monitor time at "baseline" (minimal pressure and steady foreline pressure)
			if
			(
				v_HV.isOpened &&
				v_B.isOpened &&
				v_LV.isClosed &&
				p_VM <= pressure_baseline &&
				Math.Abs(m_p_Foreline.RoC) < 20 * m_p_Foreline.Sensitivity
			)
			{
				if (!BaselinePressureTimer.IsRunning)
					BaselinePressureTimer.Restart();
			}
			else if (BaselinePressureTimer.IsRunning)
				BaselinePressureTimer.Reset();
		}

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

		protected void updateSampleMeasurement()
		{
			ugCinMC.Value = DigitalFilter.WeightedUpdate(
				ugC(m_p_MC, mL_MC, m_t_MC), ugCinMC, 0.8);
		}

		#region Logging
		// To be replaced by a database system in the future

		protected virtual void setLogHeaders()
		{
			VMPLog.Header = "pVM\tpVM_IG\tpVM_HP";
			GRPLog.Header = "pGR1\tpGR2\tpGR3\tpGR4\tpGR5\tpGR6";
			VTTLog.Header = "pVTT\ttVTT\ttVTT_CF\ttVTT_wire\ttVTT_top";
			MCLog.Header = "pMC\ttMC\tugCinMC\ttFtcMC";
			PLog.Header = "pAmbient\tpIM\tpGM\tpForeline\tpVM";
			TLog.Header = "tCC\ttGR1\ttGR2\ttGR3\ttGR4\ttGR5\ttGR6";
			FTCLog.Header = "tFtcGR1\ttFtcGR2\ttFtcGR3\ttFtcGR4\ttFtcGR5\ttFtcGR6" +
				"\ttFtcVTT\ttFtcCuAg\ttFtcMC\ttFtcVP\ttLNTank";
			AmbientLog.Header = "tAmbient\ttGM\ttTabletop\ttMC\ttTC0CJ0\ttTC1CJ0\ttTC2CJ0\ttTC2CJ1\tpAmbient\ttAmbientRoC\tlitersLNSupply";
		}

		[XmlIgnore] public double VMincreased = 0, VMdecreased = 0;
		[XmlIgnore] public string VMrpt = "";
		protected virtual void logP_VMStatus()
		{
			string rpt = VMPLog.TimeStamp() +
				p_VM.ToString("0.00e0") + "\t" +
				m_p_VM_IG.Value.ToString("0.00e0") + "\t" +
				m_p_VM_HP.Value.ToString("0.00e0"); ;
			if (p_VM < VMdecreased || p_VM > VMincreased || VMPLog.ElapsedMilliseconds > 300000)
			{
				if (VMrpt != "") VMPLog.WriteLine(VMrpt);
				VMPLog.WriteLine(rpt);
				double dp, dn = 0.95;       // positive, negative significant changes
				if (p_VM < 0.001)
					dp = 1.2;
				else
				{
					if (p_VM > 200) dn = 0.995;
					else if (p_VM > 1) dn = 0.99;
					else if (p_VM > 0.005) dn = 0.98;
					dp = 2 - dn;
				}

				VMincreased = dp * p_VM;    // min higher pressure to trigger a report
				VMdecreased = dn * p_VM;    // max lower pressure to trigger a report
				VMrpt = "";
			}
			else
			{
				VMrpt = rpt;    // last unrecorded value observed
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
				GRPLog.WriteLine(
					GRPLog.TimeStamp() +
					old_pGR[0].ToString("0.00") + "\t" +
					old_pGR[1].ToString("0.00") + "\t" +
					old_pGR[2].ToString("0.00") + "\t" +
					old_pGR[3].ToString("0.00") + "\t" +
					old_pGR[4].ToString("0.00") + "\t" +
					old_pGR[5].ToString("0.00")
				);
			}
		}

		protected virtual void logVTTStatus()
		{
			if (
				Math.Abs(old_pVTT - m_p_VTT) > 0.001 ||
				Math.Abs(old_tVTT - VTT.Temperature) >= 0.4 ||
				VTTLog.ElapsedMilliseconds > 60000
				)
			{
				old_pVTT = m_p_VTT;
				old_tVTT = VTT.Temperature;
				VTTLog.Record(
					old_pVTT.ToString("0.000") + "\t" +
					old_tVTT.ToString("0.0") + "\t" +
					VTT.Coldfinger.Temperature.ToString("0.0") + "\t" +
					VTT.WireTempSensor.Temperature.ToString("0.0") + "\t" +
					ts_VTT_top.Temperature.ToString("0.0")
					);
			}
		}

		[XmlIgnore] public double old_ugCinMC;
		[XmlIgnore] public double old_p_MC;
		protected virtual void logMCStatus()
		{
			if (Math.Abs(ugCinMC - old_ugCinMC) >= 0.1 ||
				MCLog.ElapsedMilliseconds > 30000
				)
			{
				old_ugCinMC = ugCinMC;
				MCLog.Record(
					m_p_MC.Value.ToString("0.000") + "\t" +
					m_t_MC.Value.ToString("0.00") + "\t" +
					ugCinMC.Value.ToString("0.0") + "\t" +
					ftc_MC.Temperature.ToString("0.0")
				);
			}
		}

		[XmlIgnore] public double old_pIM, old_pGM, old_pVTT;
		protected virtual void logPressureStatus()
		{
			if (Math.Abs(old_pIM - m_p_IM) > 2 ||
				Math.Abs(old_pGM - m_p_GM) > 2 ||
				PLog.ElapsedMilliseconds > 30000
				)
			{
				old_pIM = m_p_IM;
				old_pGM = m_p_GM;
				old_pVTT = m_p_VTT;
				PLog.Record(
					m_p_Ambient.Value.ToString("0.00") + "\t" +
					old_pIM.ToString("0") + "\t" +
					old_pGM.ToString("0") + "\t" +
					m_p_Foreline.Value.ToString("0.000") + "\t" +
					p_VM.ToString("0.0e0")
				);
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
				TLog.Record(
					old_tCC.ToString("0.0") + "\t" +
					old_tGR[0].ToString("0.0") + "\t" +
					old_tGR[1].ToString("0.0") + "\t" +
					old_tGR[2].ToString("0.0") + "\t" +
					old_tGR[3].ToString("0.0") + "\t" +
					old_tGR[4].ToString("0.0") + "\t" +
					old_tGR[5].ToString("0.0")
				);
			}
		}

		[XmlIgnore] public double[] old_tGRFTC = new double[6];
		[XmlIgnore] public double old_tVTTFTC, old_tCuAgFTC, old_tMCFTC, old_tVPFTC;
		[XmlIgnore] public double old_tLNTank;
		[XmlIgnore] public double FTCmin = 2.0;
		protected virtual void logFTCStatus()
		{
			if (Math.Abs(old_tGRFTC[0] - ftc_GR[0].Temperature) > FTCmin ||
				Math.Abs(old_tGRFTC[1] - ftc_GR[1].Temperature) > FTCmin ||
				Math.Abs(old_tGRFTC[2] - ftc_GR[2].Temperature) > FTCmin ||
				Math.Abs(old_tGRFTC[3] - ftc_GR[3].Temperature) > FTCmin ||
				Math.Abs(old_tGRFTC[4] - ftc_GR[4].Temperature) > FTCmin ||
				Math.Abs(old_tGRFTC[5] - ftc_GR[5].Temperature) > FTCmin ||
				Math.Abs(old_tVTTFTC - ftc_VTT.Temperature) > FTCmin ||
				Math.Abs(old_tCuAgFTC - ftc_CuAg.Temperature) > FTCmin ||
				Math.Abs(old_tMCFTC - ftc_MC.Temperature) > FTCmin ||
				Math.Abs(old_tVPFTC - ftc_VP.Temperature) > FTCmin ||
				Math.Abs(old_tLNTank - LN_Tank.LevelSensor.Temperature) > 2 ||
				FTCLog.ElapsedMilliseconds > 300000
				)
			{
				old_tGRFTC[0] = ftc_GR[0].Temperature;
				old_tGRFTC[1] = ftc_GR[1].Temperature;
				old_tGRFTC[2] = ftc_GR[2].Temperature;
				old_tGRFTC[3] = ftc_GR[3].Temperature;
				old_tGRFTC[4] = ftc_GR[4].Temperature;
				old_tGRFTC[5] = ftc_GR[5].Temperature;
				old_tVTTFTC = ftc_VTT.Temperature;
				old_tCuAgFTC = ftc_CuAg.Temperature;
				old_tMCFTC = ftc_MC.Temperature;
				old_tVPFTC = ftc_VP.Temperature;
				old_tLNTank = LN_Tank.LevelSensor.Temperature;

				FTCLog.Record(
					old_tGRFTC[0].ToString("0") + "\t" +
					old_tGRFTC[1].ToString("0") + "\t" +
					old_tGRFTC[2].ToString("0") + "\t" +
					old_tGRFTC[3].ToString("0") + "\t" +
					old_tGRFTC[4].ToString("0") + "\t" +
					old_tGRFTC[5].ToString("0") + "\t" +
					old_tVTTFTC.ToString("0") + "\t" +
					old_tCuAgFTC.ToString("0") + "\t" +
					old_tMCFTC.ToString("0") + "\t" +
					old_tVPFTC.ToString("0") + "\t" +
					old_tLNTank.ToString("0")
				);
			}
		}

		[XmlIgnore] public double old_tAmbient;
		protected virtual void logAmbientStatus()
		{
			if (Math.Abs(old_tAmbient - m_t_muxAIN13) >= 0.05 ||
				AmbientLog.ElapsedMilliseconds > 300000
				)
			{
				old_tAmbient = m_t_muxAIN13;
				AmbientLog.Record(
					old_tAmbient.ToString("0.00") + "\t" +
					ts_GM.Temperature.ToString("0.0") + "\t" +
					ts_tabletop.Temperature.ToString("0.0") + "\t" +
					m_t_MC.Value.ToString("0.00") + "\t" +
					ThermalControllers[0].CJ0Temperature.ToString("0.0") + "\t" +
					ThermalControllers[1].CJ0Temperature.ToString("0.0") + "\t" +
					ThermalControllers[2].CJ0Temperature.ToString("0.0") + "\t" +
					ThermalControllers[2].CJ1Temperature.ToString("0.0") + "\t" +
					m_p_Ambient.Value.ToString("0.00") + "\t" +
					(m_t_muxAIN13.RoC * 60).ToString("0.00") + "\t" + // degC per min
					m_v_LN_supply.Value.ToString("0.0")
				);
			}
		}

		protected virtual void logAllStatus()
		{
			logP_VMStatus();
			logGRStatus();
			logVTTStatus();
			logMCStatus();
			logPressureStatus();
			logTemperatureStatus();
			logFTCStatus();
			logAmbientStatus();
		}

		protected void logSystemStatus()
		{
			try
			{
				setLogHeaders();

				while (true)
				{
					systemLogSignal.Reset();
					systemLogSignal.WaitOne();
					if (!Initialized) continue;
					if (ShuttingDown) break;

					try { logAllStatus(); }
					catch (Exception e) { MessageHandler.Send(e.ToString()); }
				}
			}
			catch (Exception e) { MessageHandler.Send(e.ToString()); }
		}

		#endregion Logging

		#region Pump management

		protected virtual bool Backstreaming()
		{
			// roughing pump oil backstreaming check
			return
				p_VM < pressure_backstreaming_safe &&
				m_p_Foreline < pressure_backstreaming_limit &&
				v_LV.isOpened;
		}

		// a bit of a kludge
		// If we can't see the VM pressure at the scale
		// that flooding occurs, we infer it from the Foreline 
		// pressure, because the HV should always be closed when
		// foreline pressure is high, as that gas presumably
		// gets to the foreline via the VM.
		// NB: Although we switch from roughing to backing only
		// after the VM has been roughed to below pressure_LV_HV_switch Torr and
		// the foreline has been further roughed to below 
		// pressure_min_LV Torr with the backing valve still closed, the 
		// turbo pump's pumping speed is great enough to cause a 
		// significant bump in Foreline pressure (perhaps to over 
		// 500 Torr) when the HV valve is first opened.
		// This could cause a spurious HVPumpFlooded alarm.
		//protected virtual bool HVPumpFlooded()
		//{
		//	return
		//		m_p_Foreline > pressure_max_HV &&
		//		v_HV.isOpened /* || v_B.isOpened */ ;
		//}

		protected virtual bool HVPumpFlooded()
		{
			return p_VM > pressure_max_HV && v_HV.isOpened;
		}



		[XmlIgnore] public Stopwatch roughingTimer = new Stopwatch();
		bool RoughingFailure()
		{
			if (m_p_Foreline.RoC > roc_pForeline_rising)
			{
				if (!roughingTimer.IsRunning)
					roughingTimer.Restart();
				else if (roughingTimer.ElapsedMilliseconds > 120000)
					return true;
			}
			else if (m_p_Foreline.RoC <= 0 && roughingTimer.IsRunning)
				roughingTimer.Reset();

			return false;
		}

		#endregion Pump management

		// value > Km * sensitivity ==> meter needs zeroing
		protected void ZeroIfNeeded(Meter m, double Km)
		{
			if (Math.Abs(m) >= Km * m.Sensitivity)
				m.ZeroNow();
		}

		protected void ZeroPressureGauges()
		{
			// ensure baseline VM pressure & steady state
			if (BaselinePressureTimer.Elapsed.TotalSeconds < 10)
				return;

			ZeroIfNeeded(m_p_Foreline, 20);

			if ((v_VTTR_VM.isOpened || v_IM_VM.isOpened && v_IM_VTTL.isOpened && v_VTTL_VTTR.isOpened))
				ZeroIfNeeded(m_p_VTT, 5);

			if (v_split_VM.isOpened && v_MC_split.isOpened)
				ZeroIfNeeded(m_p_MC, 5);

			if (v_IM_VM.isOpened)
				ZeroIfNeeded(m_p_IM, 10);

			if (v_GM_VM.isOpened || v_split_VM.isOpened && v_split_GM.isOpened)
			{
				ZeroIfNeeded(m_p_GM, 10);
				foreach (GraphiteReactor gr in GraphiteReactor.List)
					if (gr.GMValve.isOpened)
					{
						//if (gr.Name == "GR")
						//MessageBox.Show(gr.Name)
						ZeroIfNeeded(gr.PressureMeter, 5);
					}
			}
		}

		protected void UpdateLoop()
		{
			try
			{
				while (!ShuttingDown)
				{
					Update();
					updateSignal.Set();
					wait(milliseconds_UpdateLoop_interval);
				}
			}
			catch (Exception e) { MessageHandler.Send(e.ToString()); }
		}

		// Depending on device conditions and current operations,
		// execution time for this function normally varies from
		// 3 or 4 microseconds up to about 5 milliseconds max.
		[XmlIgnore] public int msUpdateLoop = 0;
		[XmlIgnore] public bool daqOk = false;


		protected void Update()
		{
			#region DAQ maintenance
			daqOk = true;
			foreach (LabJackDaq lj in LabJackDaq.List)
			{
				if (!lj.IsUp)
				{
					daqOk = false;
					if (!lj.IsStreaming)
						EventLog.Record(lj.Name + " is not streaming");
					else if (!lj.DataAcquired)
						EventLog.Record(lj.Name + ": waiting for stream to start");
					else if (lj.Error != null)
					{
						EventLog.Record(lj.ErrorMessage(lj.Error));
						lj.ClearError();
					}
				}
			}
			#endregion DAQ maintenance

			#region Power failure watchdog
			if (daqOk) HandlePowerFailure();
			#endregion Power failure watchdog

			#region 100 ms
			if (daqOk && msUpdateLoop % 100 == 0)
			{
				if (EnableAutozero) ZeroPressureGauges();

				#region watchdogs
				if (EnableWatchdogs)
				{
					if (Backstreaming())
					{
						if (v_LV.LastMotion != Valve.States.Closing)
						{
							Alert("System Alert!", "Backstreaming Prevented");
							v_LV.Close();
						}
					}
					if (HVPumpFlooded())
					{
						if (v_HV.LastMotion != Valve.States.Closing)
						{
							Alert("System Alert!", "High-Vacuum Pump Flooded");
							v_HV.Close();
							v_LV.Close();   // let backstreaming watchdog finish the save
						}
					}
					if (RoughingFailure())
					{
						if (v_HV.LastMotion != Valve.States.Closing ||
							v_B.LastMotion != Valve.States.Closing ||
							v_LV.LastMotion != Valve.States.Closing)
						{
							Alert("System Alert!", "Low-Vacuum Pump Failure");
							v_HV.Close();
							v_B.Close();
							v_LV.Close();
						}
					}
					if (ts_tabletop.Temperature < 0 && (LN_Tank_LN.IsOn))
					{
						Alert("System Alert!", "LN Containment Failure");
						LN_Tank_LN.TurnOff();
					}
				}
				#endregion watchdogs
			}
			#endregion 100 ms

			#region 200 ms
			if (daqOk && msUpdateLoop % 200 == 0)
			{
				ManageProcess();
				systemLogSignal.Set();  // logSystemStatus();
			}
			#endregion 200 ms

			#region 500 ms
			if (daqOk && Initialized && msUpdateLoop % 500 == 0)
			{
				#region manage graphite reactors
				foreach (GraphiteReactor gr in GraphiteReactor.List)
				{
					gr.Update();
					if (gr.isBusy)
					{
						// graphitization is in progress
						if (gr.FurnaceUnresponsive)
							Alert("System Warning!",
								gr.Name + " furnace is unresponsive.");

						if (gr.ReactionNotStarting)
							Alert("System Warning!",
								gr.Name + " reaction hasn't started.\r\n" +
									"Are the furnaces up?");

						if (gr.ReactionNotFinishing)
						{
							Alert("System Warning!",
								gr.Name + " reaction hasn't finished.");
							gr.State = GraphiteReactor.States.WaitFalling;  // reset the timer
						}

						// GR.State is "Stop" for exactly one GR.Update() cycle.
						if (gr.State == GraphiteReactor.States.Stop)
						{
							SampleLog.Record(
								"Graphitization complete:\r\n\t" +
								"Graphite " + gr.Contents);
							if (busy_GRs() == 1 && !runStarted)  // the 1 is this GR; "Stop" is still 'busy'
							{
								string msg = "Last graphite reactor finished.";
								if (ready_GRs() < 1)
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
										"Residual measurement:\r\n\t" +
										"Graphite " + a.Name +
										"\t" + a.Residual.ToString("0.000") + "\tTorr/K"
										);
									a.ResidualMeasured = true;

									if (a.Residual > 2 * a.ResidualExpected)
									{
										if (a.Tries > 1)
										{
											SampleLog.Record(
												"Excessive residual pressure. Graphitization failed.\r\n\t" +
												"Graphite " + a.Name
												);
										}
										else
										{
											SampleLog.Record(
												"Excessive residual pressure. Trying again.\r\n\t" +
												"Graphite " + a.Name
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

				#region manage Tanks & FTCs
				if (ts_tabletop.Temperature < 0)
				{
					// shut down anything that uses LN
					// alert operator
					VTT.Stop();
					foreach (FTColdfinger ftc in FTColdfinger.List)
						ftc.Stop();
					LN_Tank.IsActive = false;
					Alert("System Warning!", "LN leak.");
				}
				else
				{
					foreach (FTColdfinger ftc in FTColdfinger.List)
						ftc.Update();
					bool whatItShouldBe = LN_Tank.KeepActive;
					if (!LN_Tank.KeepActive)
					{
						foreach (FTColdfinger ftc in FTColdfinger.List)
							if (ftc.State >= FTColdfinger.States.Freeze)
							{
								whatItShouldBe = true;
								break;
							}
					}
					if (LN_Tank.IsActive != whatItShouldBe)
						LN_Tank.IsActive = whatItShouldBe;
				}

				#region manage FTC air heat
				bool airHeatNeeded = false;
				foreach (FTColdfinger ftc in FTColdfinger.List)
				{
					if (ftc.AirSupply.IsOn)
					{
						airHeatNeeded = true;
						break;
					}
				}
				if (airHeatNeeded)
				{
					if (!h_FTC_air.IsOn)
						h_FTC_air.TurnOn();
				}
				else
				{
					if (h_FTC_air.IsOn)
						h_FTC_air.TurnOff();
				}
				#endregion manage FTC air heat

				VTT.Update();

				LN_Tank.Update();
				if (LN_Tank.SlowToFill)
				{
					Alert("System Warning!", "LN tank is slow to fill!");
				}
				#endregion manage Tanks & FTCs

				#region manage IP fan
				if (h_CC_Q.IsOn || h_CC_S.Temperature >= temperature_warm)
				{
					if (!fan_IP.IsOn) fan_IP.TurnOn();
				}
				else if (fan_IP.IsOn)
					fan_IP.TurnOff();
				#endregion manage IP fan

				lowPrioritySignal.Set();
			}
			#endregion 1000 ms

			#region 1 minute
			if (daqOk && msUpdateLoop % 60000 == 0)
			{
				VLog.LogParsimoniously(m_V_5VPower.Value.ToString("0.000"));
			}
			#endregion 1 minute

			if (msUpdateLoop % 3600000 == 0) msUpdateLoop = 0;
			msUpdateLoop += milliseconds_UpdateLoop_interval;
		}

		protected virtual void HandlePowerFailure() { }

		protected void lowPriorityActivities()
		{
			try
			{
				while (true)
				{
					lowPrioritySignal.Reset();
					lowPrioritySignal.WaitOne();
					saveSettings(SettingsFilename);
				}
			}
			catch (Exception e)
			{ MessageHandler.Send(e.ToString()); }
		}

		#endregion Periodic system activities & maintenance

		#region Alerts

		public struct AlertMessage
		{
			public string Subject;
			public string Message;
			public AlertMessage(string subject, string message)
			{ Subject = subject; Message = message; }
		}

		public void Alert(string subject, string message)
		{
			if (AlertTimer.IsRunning && AlertTimer.ElapsedMilliseconds < 1800000 && message == LastAlertMessage)
				return;

			string date = "(" + DateTime.Now.ToString("MMMM dd, H:mm:ss") + ") ";
			EventLog.Record(subject + ": " + message);
			AlertMessage alert = new AlertMessage(date + subject, message);
			lock (QAlertMessage) QAlertMessage.Enqueue(alert);
			alertSignal.Set();

			PlaySound();
			LastAlertMessage = message;
			AlertTimer.Restart();
		}

		protected void AlertHandler()
		{
			try
			{
				AlertMessage alert;
				while (true)
				{
					alertSignal.Reset();
					alertSignal.WaitOne();
					while (QAlertMessage.Count > 0)
					{
						lock (QAlertMessage) alert = QAlertMessage.Dequeue();
						SendMail(alert.Subject, alert.Message);
					}
				}
			}
			catch (Exception e)
			{ MessageHandler.Send(e.ToString()); }
		}

		public void clearLastAlertMessage()
		{ LastAlertMessage = ""; AlertTimer.Stop(); }

		string getEmailAddress(string s)
		{
			int comment = s.IndexOf("//");
			return (comment < 0) ? s : s.Substring(0, comment).Trim();
		}

		protected void SendMail(string subject, string message)
		{
			try
			{
				MailMessage mail = new MailMessage();
				mail.From = new MailAddress(SmtpInfo.Username, SystemName);
				foreach (string r in ContactInfo.PermanentAlertRecipients)
				{
					string a = getEmailAddress(r);
					if (a.Length > 0) mail.To.Add(new MailAddress(a));
				}
				foreach (string line in ContactInfo.AlertRecipients)
				{
					string a = getEmailAddress(line);
					if (a.Length > 0) mail.To.Add(new MailAddress(a));
				}
				mail.Subject = subject;
				mail.Body = message +
					"\r\n\r\n" + ContactInfo.SiteName + "\r\n" +
					ContactInfo.PhoneNumber;

				// System.Net.Mail can't do explicit SSL (port 465)
				SmtpClient SmtpServer = new SmtpClient(SmtpInfo.Host, SmtpInfo.Port);
				SmtpServer.EnableSsl = true;
				SmtpServer.Credentials = new System.Net.NetworkCredential(SmtpInfo.Username, SmtpInfo.Password);
				SmtpServer.Send(mail);
			}
			catch { }
		}

		#endregion Alerts

		#region Process control

		#region Process system mangement

		// A Process runs in its own thread.
		// Only one Process can be executing at a time.
		protected void ManageProcess()
		{
			switch (ProcessState)
			{
				case ProcessStates.Ready:
					if (!string.IsNullOrEmpty(ProcessToRun))
					{
						ProcessState = ProcessStates.Busy;
						ProcessThread = new Thread((ProcessDictionary[ProcessToRun]));
						ProcessThread.Name = "Process Thread";
						ProcessThread.IsBackground = true;
						ProcessTime.Restart();
						ProcessThread.Start();
						EventLog.Record("Process started: " + ProcessToRun);
					}
					break;
				case ProcessStates.Busy:
					if (!ProcessThread.IsAlive)
					{
						ProcessState = ProcessStates.Finished;
						if (runStarted)
						{
							SampleLog.Record("Sample aborted:\r\n\t" + Sample.ID);
							runStarted = false;
						}
					}
					break;
				case ProcessStates.Finished:
					ProcessStep.Clear();
					ProcessSubStep.Clear();
					ProcessTime.Stop();
					EventLog.Record("Process ended: " + ProcessToRun);
					ProcessThread = null;
					ProcessToRun = null;
					ProcessState = ProcessStates.Ready;
					break;
				default:
					break;
			}
		}

		protected void wait_for_operator()
		{
			Alert("Operator Needed", "Operator needed");
			MessageHandler.Send("Operator needed",
				"Waiting for Operator.\r\n" +
				"Press Ok to continue");
		}

		/// <summary>
		/// sleep for the given number of milliseconds
		/// </summary>
		/// <param name="milliseconds"></param>
		protected void wait(int milliseconds) { Thread.Sleep(milliseconds); }

		/// <summary>
		/// wait for 35 milliseconds
		/// </summary>
		protected void wait() { wait(35); }

		protected void waitMilliseconds(string description, int milliseconds)
		{
			ProcessSubStep.Start(description);
			int elapsed = (int)ProcessSubStep.Elapsed.TotalMilliseconds;
			while (milliseconds > elapsed)
			{
				wait((int)Math.Min(milliseconds - elapsed, 35));
				elapsed = (int)ProcessSubStep.Elapsed.TotalMilliseconds;
			}
			ProcessSubStep.End();
		}

		protected void waitRemaining(int minutes)
		{
			int milliseconds = minutes * 60000 - (int)ProcessStep.Elapsed.TotalMilliseconds;
			if (milliseconds > 0)
				waitMilliseconds("Wait for remainder of " + min_string(minutes) + ".", milliseconds);
		}

		protected void waitMinutes(int minutes)
		{
			waitMilliseconds("Wait " + min_string(minutes) + ".", minutes * 60000);
		}

		// considers y always a consonant
		protected bool IsVowel(char c) { return "aeiou".Contains(c); }

		// Tries to guess the plural of a singular word.
		// Fails for words like deer, mouse, and ox.
		protected string Plural(string singular)
		{
			if (string.IsNullOrEmpty(singular)) return string.Empty;
			singular.TrimEnd();
			if (string.IsNullOrEmpty(singular)) return string.Empty;

			int slen = singular.Length;
			char ultimate = singular[slen - 1];
			if (slen == 1)
			{
				if (char.IsUpper(ultimate)) return singular + "s";
				return singular + "'s";
			}
			ultimate = char.ToLower(ultimate);
			char penultimate = char.ToLower(singular[slen - 2]);

			if (ultimate == 'y')
			{
				if (IsVowel(penultimate)) return singular + "s";
				return singular.Substring(0, slen - 1) + "ies";
			}
			if (ultimate == 'f')
				return singular.Substring(0, slen - 1) + "ves";
			if (penultimate == 'f' && ultimate == 'e')
				return singular.Substring(0, slen - 2) + "ves";
			if ((penultimate == 'c' && ultimate == 'h') ||
				(penultimate == 's' && ultimate == 'h') ||
				(penultimate == 's' && ultimate == 's') ||
				(ultimate == 'x') ||
				(ultimate == 'o' && !IsVowel(penultimate)))
				return singular + "es";
			return singular + "s";
		}

		/// <summary>
		/// Returns a string like "5.2 minutes" or "1 second".
		/// </summary>
		/// <param name="howmany"></param>
		/// <param name="singularUnit"></param>
		/// <returns></returns>
		protected string ToUnitsString(double howmany, string singularUnit)
		{ return howmany.ToString() + " " + ((howmany == 1) ? singularUnit : Plural(singularUnit)); }

		protected string min_string(int minutes)
		{ return ToUnitsString(minutes, "minute"); }

		#endregion Process system mangement

		#region System initialization

		protected void initializeSystem()
		{
			try
			{
				ProcessStep.Start("Initialize System");
				initializeValvePositions();

				ProcessStep.Start("Wait for initial thermal device states");
				while (true)
				{
					foreach (Heater h in Heater.List)
					{
						if (h.ReportsReceived < 1) continue;
					}
					foreach (TempSensor ts in TempSensor.List)
					{
						if (ts.ReportsReceived < 1) continue;
					}
					wait();
					break;
				}
				ProcessStep.End();

				Initialized = true;
				ProcessStep.End();
			}
			catch (Exception e)
			{ MessageHandler.Send(e.ToString()); }
		}

		protected virtual void initializeValvePositions()
		{
			ProcessStep.Start("Initialize Valve Positions");
			bool hv_was_opened = v_HV.isOpened;
			if (EnableWatchdogs)
				safePumpState();

			if (m_p_Foreline < pressure_max_backing)
			{
				ProcessSubStep.Start("Wait up to 3 seconds for VM pressure indication");
				while (p_VM < pressure_open_HV &&
						ProcessSubStep.Elapsed.TotalMilliseconds < 3000)
					wait();
				ProcessSubStep.End();

				if (v_B.isOpened && p_VM < pressure_close_LV && hv_was_opened)
					v_HV.Open();
			}
		}

		protected virtual void startHVPump() { }

		#endregion System initialization

		#region Valve operation

		protected virtual void exerciseAllValves() { }

		protected virtual void exerciseValve(Valve v)
		{
			if (v.isOpened)
			{
				waitForActuatorController();

				ProcessSubStep.Start("Exercising " + v.Name);

				EventLog.Record("Exercising " + v.Name + " on channel " + v.Channel.ToString());
				v.Close();
				waitForActuatorController();
				v.Open();
				waitForActuatorController();

				ProcessSubStep.End();
			}
		}

		protected virtual void exerciseLNValves()
		{
			ProcessStep.Start("Exercise all LN tank valves");
			exerciseLNValve(v_LN_VTT);
			exerciseLNValve(v_LN_CuAg);
			exerciseLNValve(v_LN_MC);
			foreach (Valve v in v_LN_GR)
				exerciseLNValve(v);
			exerciseLNValve(v_LN_VP);
			ProcessStep.End();
		}

		protected virtual void exerciseLNValve(Valve v)
		{
			v.Open();
			waitForActuatorController();
			v.Close();
		}

		protected virtual void closeLNValves()
		{
			foreach (FTColdfinger f in FTColdfinger.List)
				f.LNValve.Close();
		}

		protected void cycleFlowValve(Valve v_flow)
		{
			// open for one second
			v_flow.DoAction(Valve.OpenOneSecond);
			waitForActuatorController();
			v_flow.Close();
		}

		protected void waitForActuatorController()
		{ while (ActuatorController.Busy()) wait(); }

		protected virtual void close_v_QB_IM() { }

		protected virtual void close_v_BP_GM() { }

		protected virtual void open_v_d13C_CF() { }

		protected virtual void close_v_d13C_CF() { }

		#endregion Valve operation

		#region Support and general purpose functions

		protected void safePumpState()
		{
			ProcessSubStep.Start("close LV && HV");
			v_LV.Close();
			v_HV.Close();
			waitForActuatorController();
			ProcessSubStep.End();

			ProcessSubStep.Start("wait for p_Foreline <= " + pressure_foreline_empty.ToString("0.000"));
			while (m_p_Foreline > pressure_foreline_empty && ProcessSubStep.Elapsed.TotalMilliseconds < 20000) wait();
			ProcessSubStep.End();

			if (m_p_Foreline > pressure_foreline_empty || m_p_Foreline < 0)
			{
				m_p_Foreline.ZeroNow();
				while (m_p_Foreline > pressure_foreline_empty || m_p_Foreline < 0)
					wait();
			}

			if (!pump_HV.IsOn) startHVPump();

			v_B.Open();
			waitForActuatorController();
		}

		public double pVM_target;
		/// <summary>
		/// Waits 3 seconds, then until the given pressure is reached.
		/// Use 0 to wait for baseline, &lt;0 to just wait 3 seconds.
		/// </summary>
		/// <param name="pressure">Use 0 to wait for baseline, &lt;0 to just wait 3 seconds.</param>
		protected void waitFor_p_VM(double pressure)
		{
			waitForActuatorController();    // make sure there are no pending valve motions
			wait(3000);                 // always wait at least 3 seconds
			if (pressure < 0) return;      // don't wait for a specific pressure
			if (pressure == 0) pressure = pressure_baseline;
			pVM_target = pressure;

			ProcessSubStep.Start("Wait for p_VM < " + pVM_target.ToString("0.0e0") + " Torr");
			while (p_VM > pVM_target)
			{
				if (pVM_target != pressure)
				{
					pressure = pVM_target;
					ProcessSubStep.CurrentStep.Description = "Wait for p_VM < " + pVM_target.ToString("0.0e0") + " Torr";
				}
				wait();
			}
			ProcessSubStep.End();
		}

		protected virtual void turnOnFixedHeaters() { }

		protected virtual void turnOffFixedHeaters() { }

		protected void turn_off_CC_furnaces()
		{
			h_CC_Q.TurnOff();
			h_CC_S.TurnOff();
			h_CC_S2.TurnOff();
		}

		protected void heat_quartz(bool openLine)
		{
			ProcessStep.Start("Heat CC Quartz (" + minutes_CC_Q_Warmup.ToString() + " minutes)");
			h_CC_Q.TurnOn();
			if (IP.State == LinePort.States.Loaded)
				IP.State = LinePort.States.InProcess;
			if (openLine) open_line();
			waitRemaining(minutes_CC_Q_Warmup);

			if (Sample.NotifyCC_S)
			{
				Alert("Operator Needed", "Sample ready for furnace.");
				MessageHandler.Send("Operator needed",
					"Remove coolant from CC and raise furnace.\r\n" +
					"Press Ok to continue");
			}

			ProcessStep.End();
		}

		protected void heat_quartz_open_line()
		{
			heat_quartz(true);
		}

		protected void admit_IP_O2()
		{
			close_v_QB_IM();
			v_IM_VTTL.Close();
			v_IP_IM.Close();
			v_IM_VM.Close();
			v_O2_IM.Open();
			waitForActuatorController();
			wait(5000);
			v_O2_IM.Close();
			waitForActuatorController();

			if (m_p_IM < pressure_IM_O2)
			{
				Alert("Sample Alert!", "Not enough O2");
				MessageHandler.Send("Sample Alert!", "Not enough O2 in IM");
			}

			v_IP_IM.Open();
			waitForActuatorController();
			wait(2000);
			v_IP_IM.Close();
			waitForActuatorController();
			wait(5000);
		}

		protected void admit_IP_He(double IM_pressure)
		{
			ProcessStep.Start("Admit He into the IP");
			close_v_QB_IM();
			v_IM_VTTL.Close();
			v_IP_IM.Close();
			v_IM_VM.Close();
			v_He_IM.Open();
			waitForActuatorController();
			ProcessSubStep.Start("Wait for pIM ~" + IM_pressure.ToString("0"));
			while (m_p_IM < IM_pressure) wait();
			ProcessSubStep.End();
			v_He_IM.Close();
			v_IP_IM.Open();
			waitForActuatorController();
			wait(2000);
			v_IP_IM.Close();
			waitForActuatorController();
			ProcessStep.End();
		}

		protected void discard_IP_gases()
		{
			ProcessStep.Start("Discard gases at inlet port (IP)");
			isolate_sections();
			v_IP_IM.Open();
			wait(10000);                   // give some time to record a measurement
			evacuate_IM(pressure_ok);      // allow for high pressure due to water
			ProcessStep.End();
		}

		protected void discard_MC_gases()
		{
			ProcessStep.Start("Discard sample from MC");
			v_split_GM.Close();
			v_split_VM.Close();
			v_MC_split.Open();
			evacuate_split(0);
			ProcessStep.End();
		}

		protected void clean_IM()
		{
			evacuate_IM(pressure_clean);  // v_IP_IM is not moved
		}

		protected void He_flush_IP()
		{
			He_flush_IP(3);
		}

		protected void He_flush_IP(int flushes)
		{
			v_IM_VTTL.Close();
			for (int i = 1; i <= flushes; i++)
			{
				ProcessStep.Start("Flush IP with He (" + i.ToString() + " of " + flushes.ToString() + ")");
				v_IP_IM.Close();
				v_IM_VM.Close();
				v_He_IM.Open();
				waitForActuatorController();
				v_He_IM.Close();
				v_IP_IM.Open();
				waitForActuatorController();
				evacuate_IM();
				ProcessStep.End();
			}
		}

		protected void purge_IP()
		{
			IP.State = LinePort.States.InProcess;
			evacuate_IP();
			He_flush_IP();

			// Residual He is undesirable only to the extent that it
			// displaces O2. An O2 concentration of 99.99% -- more than
			// adequate for perfect combustion -- equates to 0.01% He.
			// The admitted O2 pressure always exceeds 1000 Torr; 
			// 0.01% of 1000 is 0.1 Torr. The highest pressure that can
			// be measured on the VM is 0.1 Torr. The following lesser
			// value is used instead, simply to ensure that a 
			// measureable pressure has indeed been attained.
			waitFor_p_VM(pressure_VM_measureable);
			v_IP_IM.Close();
		}

		protected void freeze_VTT()
		{
			ProcessStep.Start("Freeze VTT");

			VTT.Freeze();

			if (!section_is_open(sections.IM))
				v_IM_VTTL.Close();

			if (!section_is_open(sections.split))
				v_VTTR_CuAg.Close();

			waitForActuatorController();

			if (!section_is_open(sections.VTT))
				evacuate_VTT(pressure_clean);
			else
				waitFor_p_VM(pressure_clean);

            v_VTTR_VM.Close();

			ProcessStep.End();
		}

		protected void clean_VTT()
		{
			ProcessStep.Start("Pressurize VTTL with He");
			v_HV.Close();
			v_LV.Close();
			v_IM_VTTL.Close();
			v_VTTR_CuAg.Close();
			v_VTTL_VTTR.Close();
			v_VTT_flow.DoAction(Valve.OpenOneSecond);
			waitForActuatorController();
			v_VTT_flow.Close();

			v_He_VTTL.Open();
			waitForActuatorController();
			wait(1000);
			v_He_VTTL.Close();
			waitForActuatorController();
			ProcessStep.End();

			ProcessStep.Start("Bleed He through warm VTT");
			VTT.Regulate(temperature_VTT_cleanup);
			evacuate_VTT();
			VTT_bleed(pressure_VTT_bleed_cleaning);
			ProcessStep.End();

			VTT.Stop();

			ProcessStep.Start("Evacuate VTT");
			v_VTTL_VTTR.Open();
			v_VTT_flow.Open();
			waitForActuatorController();
			wait(3000);
			waitFor_p_VM(pressure_ok);
			ProcessStep.End();

			VTT.Dirty = false;
		}

		bool VTT_MC_stable()
		{
			double delta = Math.Abs(m_p_VTT - m_p_MC);
			double div = Math.Max(Math.Min(m_p_VTT, m_p_MC), 0.060);
			double unbalance = delta / div;
			return unbalance < 0.35 &&
				Math.Abs(m_p_VTT.RoC) <= roc_pVTT_stable &&
				Math.Abs(ugCinMC.RoC) <= roc_ugc_stable;
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
					wait();
				}
				else
				{
					sw.Reset();
					while (!VTT_MC_stable()) wait();
				}
			}
			ProcessSubStep.End();
		}

		protected void zero_VTT_MC()
		{
			ProcessSubStep.Start("Wait for foreline pressure stability");
			while (BaselinePressureTimer.Elapsed.TotalSeconds < 10) wait();
			ProcessSubStep.End();
			m_p_VTT.ZeroNow();
			m_p_MC.ZeroNow();
			while (m_p_VTT.Zeroing || m_p_MC.Zeroing) wait();
		}

		protected void waitForMCStable(int seconds)
		{
			ProcessSubStep.Start("Wait for μgC in MC to stabilize for " + ToUnitsString(seconds, "second"));
			while (Math.Abs(ugCinMC.RoC) > roc_ugc_stable) wait();
			Stopwatch sw = new Stopwatch();
			sw.Restart();
			while (sw.ElapsedMilliseconds < seconds * 1000)
			{
				wait();
				if (Math.Abs(ugCinMC.RoC) > roc_ugc_stable)
					sw.Restart();
			}
			ProcessSubStep.End();
		}

		bool waitForEquilibrium = false;
		protected void stabilize_MC()
		{
			ProcessSubStep.Start("Wait for μgC in MC to stabilize");
			if (waitForEquilibrium)
			{
				int minutes = 5;
				ProcessSubStep.Start("Wait for equilibrium (" + min_string(minutes) + ")");
				wait(minutes * 60000);
				ProcessSubStep.End();
			}
			waitForMCStable(5);
			ProcessSubStep.End();
		}

		protected void zero_MC()
		{
			stabilize_MC();
			ProcessSubStep.Start("Zero MC manometer");
			m_p_MC.ZeroNow();
			while (m_p_MC.Zeroing) wait();
			ProcessSubStep.End();
		}

		#region FTC operation

		protected void freeze(FTColdfinger ftc)
		{
			ftc.Freeze();

			ProcessSubStep.Start("Wait for " + ftc.Name + " < " + temperature_CO2_collection_min.ToString() + " °C");
			while (ftc.Temperature > temperature_CO2_collection_min) wait();
			ProcessSubStep.End();
		}

		protected void raise_LN(FTColdfinger ftc)
		{
			ftc.Raise();
			ProcessSubStep.Start("Wait for " + ftc.Name + " < " + temperature_FTC_raised.ToString() + " °C");
			while (ftc.Temperature > temperature_FTC_raised) wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait " + seconds_FTC_raised.ToString() + " seconds with LN raised");
			wait(seconds_FTC_raised * 1000);
			ProcessSubStep.End();
		}

		protected void waitFor_LN_peak(FTColdfinger ftc)
		{
			Valve v = ftc.LNValve;
			ProcessSubStep.Start("Wait until " + ftc.Name + " LN level is at max");
			while (!v.isOpened) wait();
			while (ftc.Temperature > ftc.Target || !v.isClosed) wait();
			ProcessSubStep.End();
			ProcessSubStep.Start("Wait for 5 seconds for equilibrium");
			wait(5000);     // wait for equilibrium
			ProcessSubStep.End();
		}

		#endregion FTC operation

		#region GR operation

		protected void close_all_GRs()
		{
			close_all_GRs(null);
		}

		protected void close_all_GRs(GraphiteReactor exceptGR)
		{
			foreach (GraphiteReactor gr in GraphiteReactor.List)
				if (gr != exceptGR) gr.GMValve.Close();
			waitForActuatorController();
		}

		int busy_GRs()
		{
			return GraphiteReactor.List.Count(gr => gr.isBusy);
		}

		protected void open_ready_GRs()
		{
			foreach (GraphiteReactor gr in GraphiteReactor.List)
				if (gr.isReady) gr.GMValve.Open();
			waitForActuatorController();
		}

		protected void close_ready_GRs()
		{
			foreach (GraphiteReactor gr in GraphiteReactor.List)
				if (gr.isReady) gr.GMValve.Close();
			waitForActuatorController();
		}

		#endregion GR operation

		#endregion Support and general purpose functions

		#region GR service

		protected void pressurize_GRs_with_He(List<GraphiteReactor> grs)
		{
			close_v_BP_GM();
			v_d13C_GM.Close();
			close_all_GRs();
			isolate_sections();

			// clean_pressurize_GM("He", pressure_over_atm);
			// fast pressurize GM to > 1 atm He
			v_He_GM.Open();
			v_He_GM_flow.Open();
			waitForActuatorController();
			while (m_p_GM < m_p_Ambient + 20)
				wait();

			foreach (GraphiteReactor gr in grs)
				gr.GMValve.Open();
			waitForActuatorController();

			wait(3000);
			while (m_p_GM < m_p_Ambient + 20)
				wait();

			v_He_GM.Close();
			close_all_GRs();
			v_He_GM_flow.Close();
		}


		protected void prepare_GRs_for_service()
		{
			var grs = new List<GraphiteReactor>();
			foreach (GraphiteReactor gr in GraphiteReactor.List)
			{
				if (gr.State == GraphiteReactor.States.WaitService)
					grs.Add(gr);
				else if (gr.State == GraphiteReactor.States.Ready && gr.Contents == "sulfur")
					gr.ServiceComplete();
			}
			if (grs.Count < 1)
			{
				MessageHandler.Send("Nothing to do", "No reactors are awaiting service.", Message.Type.Tell);
				return;
			}

			MessageHandler.Send("Operator needed",
				"Mark Fe/C tubes with graphite IDs.\r\n" +
				"Press Ok to continue");

			pressurize_GRs_with_He(grs);

			PlaySound();
			MessageHandler.Send("Operator needed", "Ready to load new iron and desiccant.");

			foreach (GraphiteReactor gr in grs)
				gr.ServiceComplete();
		}

		protected void He_flush_GM()
		{
			ProcessSubStep.Start("Flush GM with He");
			v_GM_VM.Close();

			// open for one second
			v_He_GM_flow.DoAction(Valve.OpenOneSecond);
			waitForActuatorController();
			v_He_GM_flow.Close();
			v_He_GM.Open();
			waitForActuatorController();
			v_He_GM.Close();
			waitForActuatorController();
			evacuate_GM();
			ProcessSubStep.End();
		}

		protected void normalize_GM_gas_flow(Gases gas)
		{
			Valve v_flow, v_shutoff;
			if (gas == Gases.H2)
			{
				v_flow = v_H2_GM_flow;
				v_shutoff = v_H2_GM;
			}
			else if (gas == Gases.He)
			{
				v_flow = v_He_GM_flow;
				v_shutoff = v_He_GM;
			}
			else if (gas == Gases.CO2)
			{
				v_flow = v_CO2_GM_flow;
				v_shutoff = v_CO2_GM;
			}
			else
				return;

			ProcessStep.Start("Normalize " + gas + " flow conditions");

			cycleFlowValve(v_flow);

			v_HV.Close();
			v_shutoff.Open();
			v_GM_VM.Open();
			waitForActuatorController();
			wait(3000);

			//a rough 'rough()'
			if (p_VM > pressure_backstreaming_safe)
			{
				v_B.Close();
				v_LV.Open();
				waitForActuatorController();

				ProcessSubStep.Start("Wait up to 2 seconds to detect foreline pressure");
				while (m_p_Foreline < pressure_max_gas_purge &&
						ProcessSubStep.Elapsed.TotalMilliseconds < 2000)
					wait();
				ProcessSubStep.End();
			}

			ProcessSubStep.Start("Purge flow-shutoff volume");
			while (m_p_Foreline > pressure_max_gas_purge &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 1000 * seconds_flow_shutoff_purge)
				wait();
			if (m_p_Foreline > pressure_max_gas_purge)
				Alert("Sample Alert!", gas + "-GM flow valve isn't closed");
			ProcessSubStep.End();

			v_GM_VM.Close();
			v_LV.Close();
			while (m_p_Foreline > pressure_max_backing) wait();
			v_B.Open();
			waitForActuatorController();
			ProcessStep.End();
		}

		double pgmValue(Gases gas)
		{
			if (gas == Gases.CO2) return ugCinMC;
			else return m_p_GM;
		}

		protected void pressurize_GM(Gases gas, double targetValue)
		{
			#region configuration

			Valve v_flow, v_shutoff;
			RateOfChange roc = m_p_GM.RoC;
			double rising = roc_pGM_rising;
			int cycleTime = 2500;           // milliseconds
			double highRate = 20;           // units of pgmValue/sec
			double delay;   // ~seconds to final value after v_shutoff is commanded to close

			if (gas == Gases.CO2)
			{
				v_flow = v_CO2_GM_flow;
				v_shutoff = v_CO2_GM;
				roc = ugCinMC.RoC;
				rising = roc_ugc_rising;
				delay = 1.2;            // increased delay required by heavier ugCinMC filtering
			}
			else
			{
				if (gas == Gases.H2)
				{
					v_flow = v_H2_GM_flow;
					v_shutoff = v_H2_GM;
				}
				else // if (gas == Gases.He)
				{
					v_flow = v_He_GM_flow;
					v_shutoff = v_He_GM;
				}

				delay = v_shutoff.FindAction(Valve.CloseValve).TimeLimit;
			}

			double cushion = delay + 8;     // (seconds): when to "Wait for pressure" instead of managing flow
			double lowRate = highRate / cushion * 2;
			double tInfinite = 10 * cushion;

			#endregion configuration

			if (gas == Gases.CO2)
				ProcessStep.Start("Admit " + targetValue.ToString("0 µgC into the MC"));
			else
				ProcessStep.Start("Pressurize GM to " + targetValue.ToString("0 Torr with ") + gas.ToString());

			v_flow.Close();
			v_shutoff.Open();
			waitForActuatorController();

			ProcessSubStep.Start("Wait out any inital surge");
			// wait as much as cycleTime to detect an increasing pressure rate of change
			while (ProcessSubStep.Elapsed.TotalMilliseconds < cycleTime &&
				(roc < rising || roc.RoC < 0))
				wait();
			// then wait for roc to peak (i.e, is no longer increasing)
			while (roc.RoC > 0) wait();
			// give RoC time to be updated after the peak
			wait(3 * (int)roc.RoC.SamplingIntervalMilliseconds);
			// finally, wait until roc is effectively steady
			while (roc.RoC < -rising) wait();
			ProcessSubStep.End();

			// crack open the flow valve
			v_flow.DoAction(Valve.OpenPulse);
			waitForActuatorController();
			wait(500);      // let flow valve coast to a stop
			v_flow.DoAction(Valve.CloseABit);
			waitForActuatorController();
			wait(cycleTime);

			double toDo = targetValue - pgmValue(gas);
			double tExpected = (roc > 0) ? toDo / roc : tInfinite;

			while (toDo > 0 && tExpected > cushion)
			{
				double tTarget = Math.Max(cushion, toDo / ((toDo > cushion * highRate) ? highRate : lowRate));
				ProcessSubStep.Start("[tExpected:tTarget] = " + tExpected.ToString("[0:") + tTarget.ToString("0]"));

				if (tExpected < tTarget)
					v_flow.DoAction(Valve.CloseABit);
				else if (tExpected > tTarget + 2 * cushion)
					v_flow.DoAction(Valve.OpenABit);
				waitForActuatorController();
				// TODO: would ideally be waiting for the specific valve action above
				// to complete (and not for some random LN valve, e.g.)

				wait(cycleTime);
				toDo = targetValue - pgmValue(gas);
				tExpected = (roc > 0) ? toDo / roc : tInfinite;

				ProcessSubStep.End();
			}

			if (gas == Gases.CO2)
				ProcessSubStep.Start("Wait for " + targetValue.ToString("0 µgC"));
			else
				ProcessSubStep.Start("Wait for " + targetValue.ToString("0 Torr"));

			v_flow.DoAction(Valve.CloseABit);
			wait(cycleTime);

			while (roc > rising &&
				pgmValue(gas) + delay * roc < targetValue)
				wait();
			ProcessSubStep.End();

			v_shutoff.Close();
			v_flow.Close();

			ProcessSubStep.Start("Wait 20 seconds for stability");
			wait(20000);
			ProcessSubStep.End();

			ProcessStep.End();
		}

		protected void clean_pressurize_GM(Gases gas, double pressure)
		{
			normalize_GM_gas_flow(gas);
			pressurize_GM(gas, pressure);
		}

		bool anyUnderTemp(List<GraphiteReactor> grs, int targetTemp)
		{
			foreach (GraphiteReactor gr in grs)
				if (gr.FeTemperature < targetTemp)
					return true;
			return false;
		}

		protected void precondition_GRs()
		{
			var grs = new List<GraphiteReactor>();

			foreach (GraphiteReactor gr in GraphiteReactor.List)
			{
				if (gr.State == GraphiteReactor.States.WaitPrep)
					grs.Add(gr);
			}
			if (grs.Count < 1)
			{
				MessageHandler.Send("Nothing to do", "No reactors are awaiting preparation.", Message.Type.Tell);
				return;
			}

			ProcessStep.Start("Evacuate GRs, start heating Fe");
			close_v_BP_GM();
			v_d13C_GM.Close();
			isolate_sections();

			foreach (GraphiteReactor gr in grs)
			{
				gr.GMValve.Open();
				gr.Furnace.TurnOn(temperature_Fe_prep);
			}
			waitForActuatorController();
			evacuate_GM(pressure_ok);
			ProcessStep.End();

			int targetTemp = temperature_Fe_prep - temperature_Fe_prep_max_error;
			ProcessStep.Start("Wait for GRs to reach " + targetTemp.ToString() + " °C.");
			while (anyUnderTemp(grs, targetTemp)) wait();
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM();
			waitFor_p_VM(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Admit H2 into GRs");
			v_GM_VM.Close();
			waitForActuatorController();
			pressurize_GM(Gases.H2, pressure_Fe_prep_H2);
			ProcessStep.End();

			ProcessStep.Start("Reduce iron for " + min_string(minutes_Fe_prep));
			foreach (GraphiteReactor gr in grs)
				gr.GMValve.Close();
			evacuate_GM(pressure_ok);
			open_line();
			waitRemaining(minutes_Fe_prep);
			ProcessStep.End();

			ProcessStep.Start("Evacuate GRs");
			close_v_BP_GM();
			v_d13C_GM.Close();
			close_all_GRs();
			isolate_sections();
			v_HV.Close();
			foreach (GraphiteReactor gr in grs)
			{
				gr.Furnace.TurnOff();
				gr.GMValve.Open();
			}
			evacuate_GM(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM();
			ProcessStep.End();

			foreach (GraphiteReactor gr in grs)
				gr.PreparationComplete();

			open_line();
			Alert("Operator Needed", "Graphite reactor preparation complete");
		}

		protected void change_sulfur_Fe()
		{
			var grs = new List<GraphiteReactor>();

			foreach (GraphiteReactor gr in GraphiteReactor.List)
			{
				if (isSulfurTrap(gr) && gr.State == GraphiteReactor.States.WaitService)
					grs.Add(gr);
			}
			if (grs.Count < 1)
			{
				MessageHandler.Send("Nothing to do", "No sulfur traps are awaiting service.", Message.Type.Tell);
				return;
			}

			pressurize_GRs_with_He(grs);

			PlaySound();
			MessageHandler.Send("Operator needed",
				"Replace iron in sulfur traps." + "\r\n" +
				"Press Ok to continue");

			// assume the Fe has been replaced

			ProcessStep.Start("Evacuate sulfur traps");
			close_v_BP_GM();
			v_d13C_GM.Close();
			isolate_sections();

			foreach (GraphiteReactor gr in grs)
				gr.GMValve.Open();
			waitForActuatorController();
			evacuate_GM(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM();
			ProcessStep.End();

			foreach (GraphiteReactor gr in grs)
				gr.PreparationComplete();

			open_line();
		}

		#endregion GR service

		#region Roughing and evacuating

		protected void start_roughing()
		{
			waitForActuatorController();
			v_HV.Close();   // will normally be closed already
			v_LV.Close();   // will normally be closed already
			waitForActuatorController();

			// give the meter up to 3 seconds to register a rough-able pressure
			ProcessSubStep.Start("Wait up to 3 seconds for VM pressure indication");
			while (p_VM < pressure_close_LV &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 3000)
				wait();
			ProcessSubStep.End();

			if (p_VM > pressure_close_LV)
			{
				// make sure the foreline is empty before roughing
				while (m_p_Foreline > pressure_foreline_empty) wait();

				// make sure the HV pump is empty, too
				v_B.Open();
				wait(1000);
				while (m_p_Foreline > pressure_foreline_empty) wait();
				v_B.Close();

				// start roughing
				v_LV.Open();
				waitForActuatorController();
			}
		}

		protected void rough()
		{
			ProcessSubStep.Start("Rough");
			start_roughing();
			if (v_LV.isOpened)
			{
				ProcessSubStep.Start("Wait up to 2 seconds for system to detect foreline pressure");
				while (p_VM > pressure_open_HV && m_p_Foreline < pressure_close_LV &&
						ProcessSubStep.Elapsed.TotalMilliseconds < 2000)
					wait();
				ProcessSubStep.End();

				ProcessSubStep.Start("Close LV when foreline pressure < " + pressure_close_LV.ToString("0.000") + " Torr");
				while (m_p_Foreline > pressure_close_LV) wait();
				v_LV.Close();
				waitForActuatorController();
				ProcessSubStep.End();

				ProcessSubStep.Start("Open B when foreline pressure < " + pressure_max_backing.ToString("0.000") + " Torr");
				while (m_p_Foreline > pressure_max_backing) wait();
				v_B.Open();
				waitForActuatorController();
				ProcessSubStep.End();
			}
			ProcessSubStep.End();
		}

		protected void evacuate()
		{
			// Without a higher pressure gauge on the VM, this routine must
			// assume that the p_VM < HV pump backing pressure. Ensuring that
			// condition is the responsibility of the code leading up to the
			// call to this function.
			ProcessSubStep.Start("Evacuate");
			v_LV.Close();
			waitForActuatorController();

			while (m_p_Foreline > pressure_max_backing) wait();
			v_B.Open();
			v_HV.Open();
			waitForActuatorController();

			ProcessSubStep.End();
		}

		protected void roughAndEvacuate()
		{
			roughAndEvacuate(-1);
		}

		protected void roughAndEvacuate(double pressure)
		{
			rough();
			evacuate();
			waitFor_p_VM(pressure);
		}

		protected bool evacuate_or_rough_as_needed()
		{
			if (v_HV.isClosed && v_LV.isClosed)
			{
				if (p_VM < pressure_switch_LV_to_HV)
					evacuate();
				else
					start_roughing();
			}
			else if (v_HV.isOpened && m_p_Foreline > pressure_switch_HV_to_LV)
				start_roughing();
			else if (v_LV.isOpened && m_p_Foreline < pressure_switch_LV_to_HV)
				evacuate();
			else
				return false;

			return true;
		}

		protected virtual void evacuate_section(sections section, double pressure) { }

		protected void evacuate_IP()
		{
			isolate_sections();
			v_IP_IM.Open();
			evacuate_IM(pressure_ok);
		}
		protected void evacuate_IM() { evacuate_IM(-1); }
		protected void evacuate_IM(double pressure) { evacuate_section(sections.IM, pressure); }
		protected void evacuate_VTT() { evacuate_VTT(-1); }
		protected void evacuate_VTT(double pressure) { evacuate_section(sections.VTT, pressure); }
		protected void evacuate_split() { evacuate_split(-1); }
		protected void evacuate_split(double pressure) { evacuate_section(sections.split, pressure); }
		protected void evacuate_GM() { evacuate_GM(-1); }
		protected void evacuate_GM(double pressure) { evacuate_section(sections.GM, pressure); }

		protected void evacuate_CuAg_split(double pressure)
		{
			ProcessSubStep.Start("Evacuate CuAg..split");
			v_split_GM.Close();
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_GM_VM.Close();
			v_HV.Close();
			waitForActuatorController();

			v_CuAg_MC.Close();
			v_MC_MCU.Open();
			v_MC_MCL.Open();
			v_MC_split.Open();
			v_split_VM.Open();
			roughAndEvacuate(pressure_ok);

			v_MC_MCU.Close();
			v_MC_MCL.Close();
			v_HV.Close();
			v_CuAg_MC.Open();
			roughAndEvacuate(pressure_ok);

			v_MC_MCU.Open();
			v_MC_MCL.Open();
			waitFor_p_VM(pressure);
			ProcessSubStep.End();
		}

		protected void evacuate_VTT_MC()
		{
			v_HV.Close();
			waitForActuatorController();

			close_sections();

			v_He_VTTL.Close();
			v_IM_VTTL.Close();
			v_MC_split.Close(); // This should be the only valve that isn't closed.

			v_VTT_flow.Open();
			v_VTTL_VTTR.Open();
			v_VTTR_CuAg.Open(); // This should be the only valve that isn't open.
			v_CuAg_MC.Open();
			v_MC_MCU.Open();
			v_MC_MCL.Open();

			v_VTTR_VM.Open();

			roughAndEvacuate(pressure_clean);
		}

		protected void evacuate_VTT_CuAg()
		{


			//ProcessSubStep.Start("Rough and evacuate section");
			v_HV.Close();
			waitForActuatorController();

			close_sections();

			v_He_VTTL.Close();
			v_IM_VTTL.Close();
			v_CuAg_MC.Close();

			v_VTTR_CuAg.Open();
			v_VTTR_VM.Open();

			roughAndEvacuate(pressure_clean);
			//ProcessSubStep.End();
		}

		protected void evacuate_IM_VTTL()
		{
			v_HV.Close();
			waitForActuatorController();

			close_sections();

			close_v_QB_IM();
			v_O2_IM.Close();
			v_He_IM.Close();

			v_VTT_flow.Close();
			v_VTTL_VTTR.Close();
			v_IM_VTTL.Open();

			v_IM_VM.Open();

			roughAndEvacuate(pressure_ok);
		}

		protected void evacuate_MC_GM(double pressure)
		{
			ProcessStep.Start("Evacuate MC..GM");

			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_CuAg_MC.Close();
			close_v_BP_GM();
			v_VP_d13C.Close();
			v_d13C_GM.Close();
			close_all_GRs();
			v_GM_VM.Close();

			if (v_split_VM.isOpened && (v_MC_split.isClosed || v_split_GM.isClosed))
			{
				v_split_VM.Close();
				v_MC_split.Open();
				v_split_GM.Open();
				waitForActuatorController();
			}

			if (v_split_VM.isClosed) v_HV.Close();
			v_split_VM.Open();
			waitForActuatorController();

			if (v_HV.isClosed) roughAndEvacuate();
			waitFor_p_VM(pressure);

			ProcessStep.End();
		}

		#endregion Roughing and evacuating

		#region Joining and isolating sections

		protected virtual void isolate_sections()
		{
			ProcessSubStep.Start("Isolate sections");
			v_IM_VTTL.Close();
			v_VTTR_CuAg.Close();
			v_split_GM.Close();
			waitForActuatorController();
			close_sections();
			ProcessSubStep.End();
		}

		protected virtual void close_sections()
		{
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_split_VM.Close();
			v_GM_VM.Close();
			waitForActuatorController();
		}

		protected bool VP_should_be_closed()
		{
			return !(
				VP.State == LinePort.States.Loaded ||
				VP.State == LinePort.States.Prepared);
		}

		protected bool ready_GRs_are_opened()
		{
			foreach (GraphiteReactor gr in GraphiteReactor.List)
				if (gr.isReady && !gr.GMValve.isOpened)
					return false;
			return true;
		}

		protected virtual bool section_is_open(sections section) { return false; }

		protected virtual void open_line() { }

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
			ProcessStep.Start("Initialize Valves");
			v_He_GM.Close();
			v_H2_GM.Close();
			v_CO2_GM.Close();
			v_CO2_GM_flow.Close();

			close_all_GRs();
			close_v_BP_GM();
			v_d13C_GM.Close();
			v_CuAg_MC.Close();
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_split_VM.Close();
			v_GM_VM.Close();
			v_HV.Close();
			waitForActuatorController();
			ProcessStep.End();

			ProcessStep.Start("Join && evacuate MC..GM");
			if (Sample.nAliquots > 1)
				v_MC_MCU.Open();
			else
				v_MC_MCU.Close();
			if (Sample.nAliquots > 2)
				v_MC_MCL.Open();
			else
				v_MC_MCL.Close();
			v_MC_split.Open();
			v_split_GM.Open();
			v_GM_VM.Open();
			roughAndEvacuate(pressure_clean);
			zero_MC();
			ProcessStep.End();

			clean_pressurize_GM(Gases.CO2, ugc_targetSize);
			v_MC_split.Close();
		}

		protected virtual void admitSealedCO2() { admitSealedCO2IP(); }

		protected void admitSealedCO2IP()
		{
			ProcessStep.Start("Evacuate and flush breakseal at IP");
			close_v_QB_IM();
			v_IM_VTTL.Close();
			v_IM_VM.Close();
			v_IP_IM.Open();
			waitForActuatorController();
			evacuate_IP();
			He_flush_IP();
			ProcessSubStep.Start("Wait for p_VM < " + pressure_clean.ToString("0.0e0") + " Torr");
			waitFor_p_VM(pressure_clean);
			ProcessSubStep.End();
			ProcessStep.End();

			admit_IP_He(pressure_over_atm);

			ProcessStep.Start("Release the sample");
			Alert("Operator Needed", "Release sealed sample at IP.");
			MessageHandler.Send("Operator needed",
				"Release the sample by breaking the sealed CO2 tube.\r\n" +
				"Press Ok to continue");
			ProcessStep.End();
		}

		// prepare a carbonate sample for acidification
		protected void prepare_carbonate_sample()
		{
			load_carbonate_sample();
			v_IP_IM.Open();
			evacuate_IP();
			He_flush_IP();
			ProcessStep.Start("Wait for p_VM < " + pressure_clean.ToString("0.0e0") + " Torr");
			waitFor_p_VM(pressure_clean);
			ProcessStep.End();
			Alert("Operator Needed", "Carbonate sample is evacuated");
		}

		protected void load_carbonate_sample()
		{
			ProcessStep.Start("Provide positive He pressure at IP needle");
			close_v_QB_IM();
			v_IM_VTTL.Close();
			v_IP_IM.Close();
			v_IM_VM.Close();
			v_He_IM.Open();
			waitForActuatorController();
			while (m_p_IM < pressure_over_atm) wait();
			v_IP_IM.Open();
			wait(5000);
			//while (m_p_IM.RoC < roc_pIM_rising) wait();   // wait until p_IM clearly rising
			while (m_p_IM < pressure_over_atm) wait();
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Remove previous sample or plug from IP needle");
			while (m_p_IM.RoC > roc_pIM_falling && ProcessStep.Elapsed.TotalMilliseconds < 10000)
				wait(); // wait up to 10 seconds for p_IM clearly falling
			ProcessStep.End();

			ProcessStep.Start("Wait for stable He flow at IP needle");
			while (Math.Abs(m_p_IM.RoC) > roc_pIM_stable) wait();
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Load next sample vial or plug at IP needle");
			while (m_p_IM.RoC < roc_pIM_plugged && ProcessStep.Elapsed.TotalMilliseconds < 20000) wait();
			if (m_p_IM.RoC > roc_pIM_loaded)
				IP.State = LinePort.States.Loaded;
			else
				IP.State = LinePort.States.Complete;
			ProcessStep.End();

			v_IP_IM.Close();
			v_He_IM.Close();
		}

		protected void prepare_new_vial()
		{
			if (!Sample.Take_d13C || VP.State == LinePort.States.Prepared) return;
			ProcessStep.Start("Prepare new vial");
			if (VP.State != LinePort.States.Loaded)
			{
				Alert("Sample Alert!", "d13C vial not available");
				MessageHandler.Send("Error!",
					"Unable to prepare new vial.\r\n" +
					"Vial contains prior d13C sample!",
					Message.Type.Tell);
				return;
			}
			close_sections();

			v_split_GM.Close();

			close_all_GRs();

			v_HV.Close();
			VP.Contents = "";
			v_VP_d13C.Open();
			v_d13C_GM.Open();
			evacuate_GM(pressure_ok);

			VP.State = LinePort.States.Prepared;
			ProcessStep.End();
		}

		#endregion Sample loading and preparation

		#region Sample operation

		protected void edit_process_sequences()
		{
			ShowProcessSequenceEditor();
		}

		protected void enter_sample_data()
		{
			VerifySampleInfo(false);
		}

		int ready_GRs()
		{
			return GraphiteReactor.List.Count(gr => gr.isReady);
		}

		public bool enough_GRs()
		{
			if (Sample.Only_d13C) return true;

			int needed = Sample.nAliquots;
			if (Sample.SulfurSuspected && !isSulfurTrap(next_sulfur_trap(Last_GR)))
				needed++;
			return ready_GRs() >= needed;
		}

		protected void run_sample()
		{
			if (!VerifySampleInfo(true))
				return;

			Sample.ugDC = 0;

			if (m_v_LN_supply < LN_supply_min)
			{
				if (MessageHandler.Send(
						"System Alert!",
						"There might not be enough LN!\r\n" +
							"Press OK to proceed anyway, or Cancel to abort.",
						Message.Type.Warn).Text != "Ok")
					return;
			}

			if (!enough_GRs())
			{
				MessageHandler.Send("Error!",
					"Unable to start process.\r\n" +
					"Not enough GRs ready!",
					Message.Type.Tell);
				return;
			}

			ProcessSequence ps = ProcessSequence.Find(Sample.Process);
			if (ps == null)
			{
				throw new Exception("No such Process Sequence: \"" + Sample.Process + "\"");
			}

			runStarted = true;

			SampleLog.WriteLine("");
			SampleLog.Record(
				"Start Process:\t" + Sample.Process + "\r\n\t" +
				Sample.ID + "\t" + Sample.milligrams.ToString("0.0000") + "\tmg\r\n\t" +
				Sample.nAliquots.ToString() + (Sample.nAliquots == 1 ? "\taliquot" : "\taliquots"));

			turnOnFixedHeaters();

			foreach (ProcessSequenceStep step in ps.Steps)
			{
				ProcessStep.Start(step.Name);
				if (step is CombustionStep)
				{
					var cs = step as CombustionStep;
					combust(cs.Temperature, cs.Minutes, cs.AdmitO2, cs.OpenLine, cs.WaitForSetpoint);
				}
				else if (step is WaitMinutesStep)
				{
					var wms = step as WaitMinutesStep;
					waitMinutes(wms.Minutes);
				}
				else
				{
					ProcessDictionary[step.Name]();
				}
				ProcessStep.End();
			}
			
			turnOffFixedHeaters();
			runStarted = false;  // successful completion

			string msg = Sample.Process + " process complete";
			SampleLog.Record(msg + "\r\n\t" + Sample.ID);
			Alert("System Status", msg);
		}

		#endregion Sample operation

		#region Sample extraction and measurement

		protected void combust(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
		{
			if (admitO2)
			{
				ProcessStep.Start("Combust at " + temperature.ToString() + " °C, " + min_string(minutes));
				admit_IP_O2();
			}
			else
				ProcessStep.Start("Heat IP: " + temperature.ToString() + " °C, " + min_string(minutes));

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
				evacuate_IM(pressure_ok);
				open_line();
			}

			if (waitForSetpoint)
			{
				ProcessStep.End();

				int closeEnough = temperature - 20;
				ProcessStep.Start("Wait for CC_S to reach " + closeEnough.ToString() + " °C");
				while (h_CC_S.Temperature < closeEnough) wait();
				ProcessStep.End();

				ProcessStep.Start("Combust at " + temperature.ToString() + " °C for " + min_string(minutes) + ".");
			}

			waitRemaining(minutes);

			ProcessStep.End();
		}

		enum BleedStates { Starting, Established, Finished }
		protected void VTT_bleed(double bleedPressure)
		{
			Valve v = v_VTT_flow;
			Meter p = m_p_VTT;
			Utilities.RateOfChange pRoC = p.RoC;

			double minBleedPressure = Math.Min(0.05, 0.2 * bleedPressure);
			double deadBand = Math.Max(0.04, 0.05 * bleedPressure);

			Stopwatch stateStopwatch = new Stopwatch();
			Stopwatch actionStopwatch = new Stopwatch();
			int secondsDelay = 2;

			ProcessSubStep.Start("Maintain VTT pressure near " + bleedPressure.ToString("0.00") + " Torr");

			BleedStates state = BleedStates.Starting;
			stateStopwatch.Restart();

			string action = Valve.OpenPulse;
			actionStopwatch.Restart();

			while (state != BleedStates.Finished)
			{
				double secondsLeft = Math.Max(0, secondsDelay - actionStopwatch.ElapsedMilliseconds / 1000);
				bool waited = (secondsLeft == 0);

				double error = p - bleedPressure;
				double anticipate = error + secondsLeft * pRoC.Value;

				if (state == BleedStates.Starting &&
						(error > 0 || stateStopwatch.ElapsedMilliseconds > 150000))
				{
					state = BleedStates.Established;
					stateStopwatch.Restart();
				}

				if (anticipate > deadBand)
				{
					if (v.LastMotion == Valve.States.Opening || waited)
						action = Valve.CloseABit;
				}
				else if (waited && anticipate < -deadBand)
				{
					if (state == BleedStates.Established && p < minBleedPressure && v.LastMotion == Valve.States.Opening)
						action = Valve.OpenValve;
					else if (error < -3 * deadBand && v.LastMotion == Valve.States.Opening)
						action = Valve.OpenABit;
					else
						action = Valve.OpenABitSlower;
				}

				if (action != "")
				{
					v.DoAction(action);
					waitForActuatorController();
					actionStopwatch.Restart();
					action = "";
				}

				if (v.isOpened)
					state = BleedStates.Finished;
				else
				{
					// anticipate vacuum system changes; they cause a big delay
					if (v_HV.isOpened && m_p_Foreline > pressure_switch_HV_to_LV ||
						v_LV.isOpened && m_p_Foreline < pressure_switch_LV_to_HV)
						v.DoAction(Valve.CloseABit);
					if (!evacuate_or_rough_as_needed())
						wait();     // share the CPU
				}
			}
			ProcessSubStep.End();

			if (v_LV.isOpened)
			{
				ProcessSubStep.Start("Close LV when foreline pressure < " + pressure_close_LV.ToString("0.000") + " Torr");
				while (m_p_Foreline > pressure_close_LV) wait();
				v_LV.Close();
				waitForActuatorController();
				ProcessSubStep.End();

				ProcessSubStep.Start("Open B when foreline pressure < " + pressure_max_backing.ToString("0.000") + " Torr");
				while (m_p_Foreline > pressure_max_backing) wait();
				v_B.Open();
				waitForActuatorController();
				ProcessSubStep.End();

				evacuate();
			}
		}

		protected void bleed()
		{

			ProcessStep.Start("Bleed off incondensable gases and trap CO2");

			// Do not bleed to low pressure (< ~mTorr) while temperature is high (> ~450 C)
			// to avoid decomposing carbonates in organic samples.
			turn_off_CC_furnaces();

			ProcessSubStep.Start("Wait for VTT temperature < " + temperature_VTT_cold.ToString() + " °C");
			VTT.Freeze();
			while (VTT.Coldfinger.Temperature >= VTT.Coldfinger.Target) wait();
			VTT.Raise();
			while (VTT.Temperature > temperature_VTT_cold) wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Release incondensables");
			close_v_QB_IM();
			v_IM_VTTL.Close();
			v_IM_VM.Close();
			if (IP.State == LinePort.States.Loaded)
				IP.State = LinePort.States.InProcess;
			v_IP_IM.Open(); // release the sample to IM for measurement

			v_VTTL_VTTR.Open();
			evacuate_VTT();
			waitForActuatorController();
			while (Math.Abs(m_p_VTT.RoC) > roc_pVTT_rising) wait();
			ProcessSubStep.End();

			v_VTT_flow.Close();
			v_VTTL_VTTR.Close();
			waitForActuatorController();

			v_IM_VTTL.Open();
			VTT.Dirty = true;

			VTT_bleed(pressure_VTT_bleed_sample);

			ProcessSubStep.Start("Finish bleed");

			v_VTTL_VTTR.Open();
			v_VTT_flow.Open();
			waitForActuatorController();
			wait(5000);     // wait for 'bump' to start falling

			while (m_p_VTT.RoC < roc_pVTT_falling_very_slowly) wait();     // Torr/sec
			v_IP_IM.Close();
			waitForActuatorController();
			wait(5000);

			while (m_p_VTT.RoC < roc_pVTT_falling_very_slowly) wait();     // Torr/sec
			v_IM_VTTL.Close();
			waitForActuatorController();
			wait(5000);

			while (m_p_VTT.RoC < roc_pVTT_falling_barely) wait();  // Torr/sec
			v_VTT_flow.Close();
			v_VTTR_VM.Close();
			ProcessSubStep.End();

			ProcessStep.End();

			IP.State = LinePort.States.Complete;
		}

		#region Extract

		protected virtual void pressurize_VTT_MC()
		{
			ProcessStep.Start("Zero MC and VTT pressure gauges");
			evacuate_VTT_MC();
			zero_VTT_MC();
			ProcessStep.End();

			ProcessStep.Start("Pressurize VTT..MC with He");
			cycleFlowValve(v_VTT_flow);

			evacuate_IM_VTTL();
			v_IM_VTTL.Close();
			v_VTTR_CuAg.Close();
			v_He_VTTL.Open();
			waitForActuatorController();
			wait(2000);
			v_He_VTTL.Close();

			for (int i = 0; i < 3; i++)
			{
				v_HV.Close();
				v_IM_VTTL.Open();
				waitForActuatorController();
				wait(5000);
				v_IM_VTTL.Close();

				evacuate_IM(pressure_ok);
                v_HV.Close();
				v_VTTR_VM.Open();
				waitForActuatorController();
				wait(5000);
				v_VTTR_VM.Close();
			}
			v_MC_MCU.Close();
			v_MC_MCL.Close();
			v_MC_split.Close();

			v_VTTR_CuAg.Open();

			v_VTT_flow.Open();
			v_VTTL_VTTR.Open();

			// TODO: should these lines be omitted?
			v_CuAg_MC.Open();
			waitForActuatorController();
			wait_VTT_MC_stable();

			ProcessStep.End();
		}

		protected void extractAt(int targetTemp)
		{
			ProcessStep.Start("Extract at " + targetTemp.ToString("0") + " °C");
			SampleLog.Record("\tExtraction target temperature:\t" +
				targetTemp.ToString("0") + "\t°C");

			VTT.Regulate(targetTemp);
			freeze(ftc_MC);

			targetTemp -= 1;            // continue at 1 deg under
			ProcessSubStep.Start("Wait for VTT to reach " + targetTemp.ToString("0") + " °C");
			while (VTT.Temperature < targetTemp) wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait 15 seconds to ensure transfer is well underway");
			wait(15000);
			ProcessSubStep.End();

			wait_VTT_MC_stable();           // assumes transfer has started

			SampleLog.Record("\tCO2 equilibrium temperature:\t" +
				CO2EqTable.Interpolate(m_p_MC).ToString("0") + "\t°C");

			v_CuAg_MC.Close();
			ProcessStep.End();
		}

		double ExtractionPressure()
		{
			// Depends on which chambers are connected
			// During extraction, VTTL..MC should be joined.
			double volVTTL_MC = mL_VTT + mL_CuAg + mL_MC;
			double currentVolume = mL_VTT;
			// if (v_VTTL_VTTR.isClosed)
			//		Flow between VTTL and VTTR is restricted (significantly, depending on v_VTT_flow),
			//			and pressure differential can exist across the valve
			if (v_VTTR_CuAg.isOpened)
			{
				currentVolume += mL_CuAg;
				if (v_CuAg_MC.isOpened)
					currentVolume += mL_MC;
			}
			return m_p_VTT * currentVolume / volVTTL_MC;
		}

		// Extracts gases from the VTT to the MC at a base pressure
		// provided by a small charge of He. The gas evolution
		// temperature is determined by adding the given offset,
		// dTCO2eq, to the CO2 equilibrium temperature for the base 
		// pressure.
		protected void pressurizedExtract(int dTCO2eq)
		{
			double extractionPressure = ExtractionPressure();
			SampleLog.Record("\tExtraction base pressure:\t" +
				extractionPressure.ToString("0.000") + "\tTorr");

			int tCO2eq = (int)CO2EqTable.Interpolate(extractionPressure);
			SampleLog.Record("\tExpected CO2 equilibrium temperature:\t" +
				tCO2eq.ToString("0") + "\t°C");

			extractAt(tCO2eq + dTCO2eq);
		}

		protected void extract()
		{
			pressurize_VTT_MC();
			pressurizedExtract(3);        // targets CO2
			v_VTTR_CuAg.Close();
			VTT.Stop();
		}

		#endregion Extract

		protected void refilter()
		{
			for (int i = 2; i < Sample.Filtrations; i += 2)
			{
				ProcessStep.Start("Refilter (" + i.ToString() + "/" + Sample.Filtrations.ToString() + "), using VTT");
				transfer_CO2_from_MC_to_VTT();
				wait(30000);
				v_CuAg_MC.Close();
				waitForActuatorController();

				clean_CuAg();
				wait(30000);   // H2 soak
				evacuate_CuAg_split(pressure_clean);
				ProcessStep.End();

				extract();
				measure();
			}
		}

		// returns the next available graphite reactor
		GraphiteReactor next_GR(string this_one)
		{
			bool passed_this_one = false;
			GraphiteReactor found_one = null;
			foreach (GraphiteReactor gr in GraphiteReactor.List)
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

		GraphiteReactor next_sulfur_trap(string this_gr)
		{
			bool passed_this_one = false;
			GraphiteReactor found_one = null;
			foreach (GraphiteReactor gr in GraphiteReactor.List)
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
			return next_GR(this_gr);
		}

		protected void open_next_GRs()
		{
			if (Sample.Only_d13C) return;
			string grName = Last_GR;
			for (int i = 0; i < Sample.nAliquots; ++i)
			{
				GraphiteReactor gr = next_GR(grName);
				if (gr != null) gr.GMValve.Open();
			}
			waitForActuatorController();
		}

		protected void open_next_GRs_and_d13C()
		{
			// assumes low pressure or v_HV closed
			open_next_GRs();
			if (Sample.Take_d13C && VP.State == LinePort.States.Prepared)
			{
				//v_d13C_CF.Open();
				v_VP_d13C.Open();
				v_d13C_GM.Open();
				//if (Sample.Only_d13C) v_d13C_CF.Open();
			}
			v_split_GM.Open();
			v_split_VM.Open();
			waitForActuatorController();
		}

		protected void take_measurement(bool first)
		{
			ProcessStep.Start("Take measurement");
			stabilize_MC();

			// this is the measurement
			double ugC = ugCinMC;

			if (first)
			{
				Sample.Aliquots.Clear();    // this line really shouldn't be needed...
				if (Sample.Only_d13C) Sample.nAliquots = 1;
				for (int i = 0; i < Sample.nAliquots; i++)
				{
					Aliquot aliquot = new Aliquot();
					aliquot.Sample = Sample;
					Sample.Aliquots.Add(aliquot);
				}
			}

			if (Sample.Only_d13C)
				Sample.Aliquots[0].ugC = 0;

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
					"\tYield:\t" + (100 * Sample.ugC / Sample.micrograms).ToString("0.00") + "%";

				SampleLog.Record(
					"Sample measurement:\r\n\t" +
					Sample.ID + "\t" + Sample.milligrams.ToString("0.0000") + "\tmg\r\n" +
					"\tCarbon:\t" + Sample.ugC.ToString("0.0") + "\tugC" + yield
				);
			}
			else
			{
				SampleLog.Record(
					"Sample measurement (split discarded):\r\n\t" +
					Sample.ID + "\t" + Sample.milligrams.ToString("0.0000") + "\tmg\r\n" +
					"\tRemaining Carbon:\t" + ugC.ToString("0.0") + "\tugC"
				);
			}

			ProcessStep.End();
		}

		protected void measure()
		{
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_CuAg_MC.Close();
			v_GM_VM.Close();
			waitForActuatorController();

			if (ftc_MC.State >= FTColdfinger.States.Freeze)
			{
				ProcessStep.Start("Release incondensables");

				raise_LN(ftc_MC);
				ProcessSubStep.Start("Wait for MC coldfinger < " + temperature_FTC_frozen.ToString() + " °C");
				while (ftc_MC.Temperature > temperature_FTC_frozen) wait();
				ProcessSubStep.End();

				v_split_GM.Close();
				v_HV.Close();
				if (Sample.nAliquots > 1)
				{
					v_MC_MCU.Open();
					if (Sample.nAliquots > 2) v_MC_MCL.Open();
				}
				v_MC_split.Open();
				v_split_VM.Open();
				roughAndEvacuate(pressure_clean);

				zero_MC();

				if (Sample.nAliquots < 3)
				{
					v_MC_MCL.Close();
					if (Sample.nAliquots < 2) v_MC_MCU.Close();
					waitForActuatorController();
					wait(5000);
				}
				v_MC_split.Close();
				waitForActuatorController();
				ProcessStep.End();
			}

			v_MC_split.Close();
			v_HV.Close();
			v_split_VM.Open();
			open_next_GRs_and_d13C();
			roughAndEvacuate();

			//if (Math.Abs(ftc_MC.Temperature - m_t_MC.Value) > 3)
			if (ftc_MC.Temperature < m_t_MC - 2)
			{
				ProcessStep.Start("Bring MC to uniform temperature");
				ftc_MC.Thaw();
				//while (Math.Abs(ftc_MC.Temperature - m_t_MC.Value) > 3)
				while (ftc_MC.State != FTColdfinger.States.Standby)
					wait();
				ProcessStep.End();
			}

			ProcessStep.Start("Measure Sample");
			take_measurement(true);
			ProcessStep.End();

			// exits with split..VP joined and evacuating
		}

		protected void split()
		{
			ProcessStep.Start("Discard Excess sample");
			while (Sample.Aliquots[0].ugC > ugC_sample_max ||
				Sample.Only_d13C && Sample.d13C_ugC > ugC_d13C_max)
			{
				ProcessSubStep.Start("Evacuate split");
				evacuate_split(0);
				ProcessSubStep.End();

				ProcessSubStep.Start("Split sample");
				v_split_VM.Close();
				v_MC_split.Open();
				waitForActuatorController();
				wait(5000);
				v_MC_split.Close();
				ProcessSubStep.End();

				ProcessSubStep.Start("Discard split");
				evacuate_split(0);
				ProcessSubStep.End();

				take_measurement(false);
			}
			v_split_GM.Open();
			ProcessStep.End();

			// exits with split..GM+nextGR joined and evacuating
			//   (GM..VP's and GRs' union or isolation is unchanged)
			// except for any ports connected to GM
			close_v_BP_GM();
		}

		protected void dilute()
		{
			if (Sample.ugC > mass_small_sample) return;

			double ugCdg_needed = (double)mass_diluted_sample - Sample.ugC;

			ProcessStep.Start("Dilute sample");

			Alert("Sample Alert!", "Small sample! (" +
				Sample.ugC.ToString("0.0") + " ugC) Diluting...");

			ftc_CuAg.Freeze();

			ftc_MC.Thaw();
			v_CuAg_MC.Open();

			ProcessSubStep.Start("Wait for MC coldfinger to thaw.");
			while (ftc_MC.Temperature < m_t_MC - 5) wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait for sample to freeze in the CuAg coldfinger.");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 4 * 60000)
				wait();
			wait(30000);
			ProcessSubStep.End();

			ftc_CuAg.Raise();

			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			wait(15000);
			v_CuAg_MC.Close();
			waitForActuatorController();
			ProcessSubStep.End();

			ftc_CuAg.Thaw();

			// get the dilution gas into the MC
			admitDeadCO2(ugCdg_needed);

			ProcessSubStep.Start("Take measurement");
			stabilize_MC();
			Sample.ugDC = ugCinMC;
			SampleLog.Record(
				"Dilution gas measurement:\t" + Sample.ugDC.ToString("0.0") + "\tugC");
			ProcessSubStep.End();

			ProcessSubStep.Start("Freeze dilution gas");
			freeze(ftc_MC);
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 5000 ||
				(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 60000)
				wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Add sample to dilution gas");
			v_CuAg_MC.Open();
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 30000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 2 * 60000)
				wait();
			raise_LN(ftc_MC);
			v_CuAg_MC.Close();
			ProcessSubStep.End();

			ProcessStep.End();

			// measure diluted sample
			measure();
		}

		protected void divide_aliquots()
		{
			ProcessStep.Start("Divide aliquots");
			v_MC_MCL.Close();
			v_MC_MCU.Close();
			waitForActuatorController();
			ProcessStep.End();
		}

		protected void trapSulfur(GraphiteReactor gr)
		{
			FTColdfinger ftc = gr.Coldfinger;
			Heater h = gr.Furnace;

			ProcessStep.Start("Trap sulfur.");
			SampleLog.Record("Trap sulfur in " + gr.Name + " at " +
				temperature_trap_sulfur.ToString() + " °C for " +
				min_string(minutes_trap_sulfur));
			ftc.Thaw();
			h.TurnOn(temperature_trap_sulfur);
			ProcessSubStep.Start("Wait for " + gr.Name +
				" to reach sulfur trapping temperature (~" +
				temperature_trap_sulfur.ToString() + "°C).");
			while (ftc.Temperature < 0 || h.Temperature < temperature_trap_sulfur - 5)
				wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Hold for " + min_string(minutes_trap_sulfur));
			wait(minutes_trap_sulfur * 60000);
			ProcessSubStep.End();

			h.TurnOff();
			ProcessStep.End();
		}

		protected void removeSulfur()
		{
			if (!Sample.SulfurSuspected) return;

			ProcessStep.Start("Remove sulfur.");

			GraphiteReactor gr = next_sulfur_trap(Last_GR);
			Last_GR = gr.Name;
			gr.Reserve("sulfur");
			gr.State = GraphiteReactor.States.InProcess;

			transfer_CO2_from_MC_to_GR(gr, false);
			trapSulfur(gr);
			transfer_CO2_from_GR_to_MC(gr, false);

			gr.Aliquot.ResidualMeasured = true;     // prevent graphitization retry
			gr.State = GraphiteReactor.States.WaitService;

			ProcessStep.End();
			measure();
		}

		protected void freeze(Aliquot aliquot)
		{
			aliquot.Name = Next_GraphiteNumber.ToString(); Next_GraphiteNumber++;
			GraphiteReactor gr = next_GR(Last_GR);
			if (gr == null)
				throw new Exception("Can't find a GR to freeze the aliquot into.");
			Last_GR = aliquot.GR = gr.Name;
			gr.Reserve(aliquot);

			if (aliquot == aliquot.Sample.Aliquots[0])
				transfer_CO2_from_MC_to_GR(gr, Sample.Take_d13C);
			else if (aliquot == aliquot.Sample.Aliquots[1])
				transfer_CO2_from_MC_to_GR(gr, v_MC_MCU);
			else if (aliquot == aliquot.Sample.Aliquots[2])
				transfer_CO2_from_MC_to_GR(gr, v_MC_MCL);
		}

		protected void add_GR_H2(Aliquot aliquot)
		{
			GraphiteReactor gr = GraphiteReactor.Find(aliquot.GR);
			double mL_GR = gr.MilliLitersVolume;

			double nCO2 = aliquot.ugC * nC_ug;  // number of CO2 particles in the aliquot
			double nH2target = H2_CO2 * nCO2;   // ideal number of H2 particles for the reaction

			// The pressure of nH2target in the frozen GR, where it will be denser.
			aliquot.pH2Final = densityAdjustment * pressure(nH2target, mL_GR, ts_GM.Temperature);
			aliquot.pH2Initial = aliquot.pH2Final + pressure(nH2target, mL_GM, ts_GM.Temperature);

			clean_pressurize_GM(Gases.H2, aliquot.pH2Initial);
			waitFor_LN_peak(gr.Coldfinger);

			double pH2initial = m_p_GM;
			gr.GMValve.Open();
			waitForActuatorController();
			wait(2000);
			gr.GMValve.Close();
			waitForActuatorController();
			wait(5000);
			double pH2final = m_p_GM;

			// this is what we actually got
			double nH2 = nParticles(pH2initial - pH2final, mL_GM, ts_GM.Temperature);
			double pH2ratio = nH2 / nCO2;

			double nExpectedResidual;
			if (pH2ratio > H2_CO2_stoich)
				nExpectedResidual = nH2 - nCO2 * H2_CO2_stoich;
			else
				nExpectedResidual = nCO2 - nH2 / H2_CO2_stoich;
			aliquot.ResidualExpected = TorrPerKelvin(nExpectedResidual, mL_GR);

			SampleLog.Record(
				"GR hydrogen measurement:\r\n\t" + Sample.ID + "\r\n\t" +
				"Graphite " + aliquot.Name + "\t" + aliquot.ugC.ToString("0.0") + "\tugC\t" +
				aliquot.GR + "\t" +
				"pH2:CO2\t" + pH2ratio.ToString("0.00") + "\t" +
				string.Format("{0:0} => {1:0} / {2:0} => {3:0}",
					pH2initial, pH2final, aliquot.pH2Initial, aliquot.pH2Final) + "\r\n\t" +
				"expected residual:\t" + aliquot.ResidualExpected.ToString("0.000") + "\tTorr/K"
				);

			if (pH2ratio < H2_CO2_stoich * 1.05)
			{
				Alert("Sample Alert!", "Not enough H2");
				MessageHandler.Send("Error!",
					"Not enough H2 in " + aliquot.GR +
					"\r\nProcess paused.");
			}
		}

		protected void graphitize_aliquots()
		{
			divide_aliquots();
			foreach (Aliquot aliquot in Sample.Aliquots)
				freeze(aliquot);

			v_split_GM.Close();
			v_split_VM.Close();

			foreach (Aliquot aliquot in Sample.Aliquots)
			{
				ProcessStep.Start("Graphitize aliquot " + aliquot.Name);
				add_GR_H2(aliquot);
				GraphiteReactor.Find(aliquot.GR).Start();
				ProcessStep.End();
			}
			// exits with GM isolated and filled with H2
		}

		protected void clean_CuAg()
		{
			ProcessStep.Start("Start cleaning CuAg");

			if (m_p_GM < 50)
				clean_pressurize_GM(Gases.H2, 100); // just enough to clean the CuAg

			v_split_VM.Close();
			v_VTTR_CuAg.Close();
			v_split_GM.Open();
			v_MC_split.Open();
			v_CuAg_MC.Open();
			waitForActuatorController();
			wait(1000);
			v_CuAg_MC.Close();

			v_HV.Close();

			v_split_VM.Open();
			roughAndEvacuate(pressure_ok);    // evacuate MC..GM via split-VM
			ProcessStep.End();
		}

		protected void take_only_d13C()
		{
			//ProcessSubStep.Start("Freeze sample to d13C coldfinger");
			//freeze_FTC(ftc_d13C);

			//ProcessSubStep.Start("Wait for CO2 to freeze to d13C coldfinger");
			//while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
			//		(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
			//		ProcessSubStep.Elapsed.TotalMilliseconds < 4 * 60000)
			//	wait();
			//sleep(30000);
			//ProcessSubStep.End();

			//raise_FTC(ftc_d13C);
			//v_d13C_GM.Close();
			//waitForActuatorController();
			//ProcessSubStep.End();

			ProcessSubStep.Start("Transfer sample from d13C coldfinger to VP");
			//ftc_d13C.Thaw();
			v_VP_d13C.Open();
			VP.State = LinePort.States.InProcess;
			VP.Contents = Sample.ID;
			ftc_VP.Freeze();

			//need to know when done
			//ProcessSubStep.Start("Wait for d13C coldfinger to thaw");
			//while (!ftc_d13C.isNearAirTemperature())
			//	wait();
			//ProcessSubStep.End();

			ProcessSubStep.Start("Wait for CO2 to freeze in the VP");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 4 * 60000)
				wait();
			wait(30000);
			ProcessSubStep.End();


			raise_LN(ftc_VP);
			waitFor_LN_peak(ftc_VP);
			//			v_d13C_CF.Close();
			ProcessSubStep.End();
		}

		protected void add_d13C_He()
		{
			if (!Sample.Take_d13C) return;
			// normally enters with MC..GM joined and evacuating via split-VM
			// (except when run as an independent process from the UI, perhaps)

			ProcessStep.Start("Add 1 atm He to vial");
			// release VP_incondensables

			raise_LN(ftc_VP);

			ProcessSubStep.Start("Release incondensables");
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			close_all_GRs();
			v_split_GM.Close();
			v_split_VM.Close();
			v_d13C_GM.Open();
			v_GM_VM.Open();
			waitForActuatorController();
			wait(5000);
			waitFor_p_VM(0);

			v_VP_d13C.Close();
			v_GM_VM.Close();
			waitForActuatorController();
			ProcessSubStep.End();

			clean_pressurize_GM(Gases.He, pressure_VP_He_Initial);
			waitFor_LN_peak(ftc_VP);

			double pHeInitial = m_p_GM;
			v_VP_d13C.Open();
			waitForActuatorController();
			wait(2000);
			v_VP_d13C.Close();
			waitForActuatorController();
			ftc_VP.Thaw();

			wait(5000);
			double pHeFinal = m_p_GM;

			// PV = nkT ==> n = PV/k/T;
			double n_He = (pHeInitial - pHeFinal) * (mL_GM + mL_d13C) / kB_Torr_mL / (ts_GM.Temperature + ZeroDegreesC);
			double n_CO2 = Sample.d13C_ugC * nC_ug;
			double n = n_He + n_CO2;
			Sample.d13C_ppm = 1e6 * n_CO2 / n;

			// approximate standard-room-temperature vial pressure (neglects needle port volume)
			double pVP = n * kB_Torr_mL * (temperature_room + ZeroDegreesC) / mL_VP;

			SampleLog.Record(
				"d13C measurement:\r\n\t" + Sample.ID + "\r\n\t" +
				(Sample.Only_d13C ? "" : "Graphite " + Sample.Aliquots[0].Name + "\t") +
				"d13C:\t" + Sample.d13C_ugC.ToString("0.0") + "\tugC\t" +
					Sample.d13C_ppm.ToString("0") + "\tppm\t" +
				"vial pressure:\t" + pVP.ToString("0") + " / " +
					pressure_over_atm.ToString("0") + "\tTorr"
			);

			double pVP_Error = pVP - pressure_over_atm;
			if (Math.Abs(pVP_Error) > pressure_VP_Error)
			{
				SampleLog.Record("Sample Alert! Vial pressure out of range");
				SampleLog.Record(
					"\tpHe: " + pHeInitial.ToString("0") + " => " + pHeFinal.ToString("0") + " / " +
					pressure_VP_He_Initial.ToString("0") + " => " + pressure_VP_He_Final.ToString("0"));
				Alert("Sample Alert!", "Vial He pressure error: " + pVP_Error.ToString("0"));
				if (pVP_Error > 3 * pressure_VP_Error || pVP_Error < -2 * pressure_VP_Error)
					MessageHandler.Send("Error!",
						"Vial He pressure out of range." +
						"\r\nProcess paused.");

				// anything to do here, after presumed remedial action?
			}

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
			refilter();
			split();
			removeSulfur();
			graphitize_etc();
		}

		protected void graphitize_etc()
		{
			try
			{
				dilute();
				graphitize_aliquots();
				clean_CuAg();         // exits with MC..GM joined and evacuating via split-VM
				add_d13C_He();      // exits with GM..d13C filled with He
				open_line();
			}
			catch (Exception e) { MessageHandler.Send(e.ToString()); }

			if (!SampleIsRunning)
				Alert("System Status", ProcessToRun + " process complete");
		}

		#endregion Sample extraction and measurement

		#region Transfer CO2 between sections

		protected void transfer_CO2_from_MC_to_GR(GraphiteReactor gr, Valve v_MCx, bool take_d13C)
		{
			FTColdfinger ftc = gr.Coldfinger;

			ProcessStep.Start("Evacuate graphite reactor" + (take_d13C ? " and VP" : ""));

			// ensure desired state; most of these are probably already there
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_CuAg_MC.Close();
			v_MC_split.Close();
			close_v_BP_GM();
			v_VP_d13C.Close();
			v_d13C_GM.Close();
			close_all_GRs(gr);
			waitForActuatorController();

			if (gr.GMValve.isClosed || take_d13C)
			{
				v_split_VM.Close();
				v_GM_VM.Close();

				if (gr.GMValve.isClosed)
					gr.GMValve.Open();

				if (take_d13C)
				{
					if (VP_should_be_closed())
						throw new Exception("Need to take d13C, but VP is not available.");
					v_VP_d13C.Open();
					open_v_d13C_CF();
					v_d13C_GM.Open();
				}
				waitForActuatorController();
			}

			if (v_split_GM.isClosed && v_split_VM.isClosed)
			{
				v_GM_VM.Close();
				v_split_GM.Open();
				waitForActuatorController();
			}

			if (v_split_VM.isClosed && v_GM_VM.isClosed)
			{
				v_HV.Close();
				v_split_VM.Open();
				waitForActuatorController();
			}

			if (v_HV.isClosed)
				roughAndEvacuate();
			waitFor_p_VM(pressure_clean);

			ProcessStep.End();

			ProcessStep.Start("Expand sample into GM");

			close_v_d13C_CF();
			v_VP_d13C.Close();

			if (take_d13C)
				gr.GMValve.Close();
			else
				v_d13C_GM.Close();

			v_GM_VM.Close();
			v_split_VM.Close();

			if (v_MCx != null) v_MCx.Open();    // take it from from MCU or MCL
			v_MC_split.Open();                // expand sample into GM
			waitForActuatorController();

			ProcessStep.End();

			if (take_d13C)
			{
				if (Sample.Only_d13C)
				{
					take_only_d13C();
					return;
				}
				else
				{
					ProcessSubStep.Start("Take d13C");
					wait(5000);
					v_d13C_GM.Close();
					v_VP_d13C.Open();
					VP.State = LinePort.States.InProcess;
					VP.Contents = Sample.Aliquots[0].Name;
					ftc_VP.Freeze();
					gr.GMValve.Open();
					waitForActuatorController();
					ProcessSubStep.End();
				}
			}

			ProcessStep.Start("Freeze to graphite reactor");
			freeze(ftc);

			ProcessSubStep.Start("Wait for CO2 to freeze into " + gr.Name);
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 3.5 * 60000)
				wait();
			wait(30000);
			raise_LN(ftc);
			wait(15000);
			ProcessSubStep.End();


			ProcessSubStep.Start("Release incondensables");
			v_split_VM.Open();
			wait(5000);
			waitFor_p_VM(0);
			ProcessSubStep.End();

			gr.GMValve.Close();
			if (v_MCx != null) v_MCx.Close();
			waitForActuatorController();

			ProcessStep.End();
		}

		protected void transfer_CO2_from_MC_to_GR(GraphiteReactor gr)
		{
			transfer_CO2_from_MC_to_GR(gr, null, false);
		}

		protected void transfer_CO2_from_MC_to_GR(GraphiteReactor gr, Valve v_MCx)
		{
			transfer_CO2_from_MC_to_GR(gr, v_MCx, false);
		}

		protected void transfer_CO2_from_MC_to_GR(GraphiteReactor gr, bool take_d13C)
		{
			transfer_CO2_from_MC_to_GR(gr, null, take_d13C);
		}

		// TODO: wait after freezing for pGR to stabilize and close MCU and MCL
		protected void transfer_CO2_from_GR_to_MC(GraphiteReactor gr, bool firstFreezeGR)
		{
			FTColdfinger grCF = gr.Coldfinger;

			ProcessStep.Start("Transfer CO2 from GR to MC.");

			if (firstFreezeGR)
				grCF.Freeze();

			evacuate_MC_GM(pressure_clean);
			v_MC_MCU.Close();
			v_MC_MCL.Close();

			if (firstFreezeGR)
			{
				ProcessSubStep.Start("Freeze CO2 in " + gr.Name + ".");
				freeze(grCF);
				raise_LN(grCF);
				ProcessSubStep.Start("Wait one minute.");
				wait(60000);
				ProcessSubStep.End();

				ProcessSubStep.End();

				ProcessSubStep.Start("Evacuate incondensables.");
				v_MC_split.Close();
				v_HV.Close();
				gr.GMValve.Open();
				roughAndEvacuate(pressure_clean);
				v_MC_split.Open();
				waitFor_p_VM(pressure_clean);
				v_split_VM.Close();
				waitForActuatorController();
				ProcessSubStep.End();
			}
			else
			{
				v_split_VM.Close();
				gr.GMValve.Open();
				v_MC_split.Open();
				waitForActuatorController();
			}

			if (grCF.Temperature < ts_GM.Temperature - 5) grCF.Thaw();
			freeze(ftc_MC);

			ProcessSubStep.Start("Wait for sample to freeze in the MC.");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 1.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 2 * 60000)
				wait();
			wait(30000);

			raise_LN(ftc_MC);
			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			wait(15000);
			ProcessSubStep.End();
			v_split_GM.Close();
			gr.GMValve.Close();
			ProcessSubStep.End();

			ProcessStep.End();
		}

		protected void transfer_CO2_from_GR_to_MC()
		{
			transfer_CO2_from_GR_to_MC(GraphiteReactor.Find(Last_GR), true);
		}

		protected void transfer_CO2_from_MC_to_VTT()
		{
			ProcessStep.Start("Transfer CO2 from MC to VTT");
			evacuate_VTT_CuAg();
			v_VTT_flow.Close();
			v_VTTL_VTTR.Close();
			v_VTTR_VM.Close();
			ftc_MC.Thaw();
			v_CuAg_MC.Open();

			ProcessSubStep.Start("Wait for VTT to reach temperature");
			VTT.Freeze();
			while (VTT.Coldfinger.Temperature >= VTT.Coldfinger.Target) wait();
			VTT.Raise();
			while (VTT.Temperature > temperature_VTT_cold) wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Make sure the CO2 has started evolving.");
			while (ftc_MC.Temperature < CO2EqTable.Interpolate(0.07)) wait();
			ProcessSubStep.End();

			wait_VTT_MC_stable();
			ProcessStep.End();
		}

		protected void transfer_CO2_from_MC_to_IP()
		{
			ProcessStep.Start("Evacuate and join IM..split via VM");
			evacuate_IP();
			v_IM_VM.Close();
			evacuate_split();
			v_IM_VM.Open();
			waitFor_p_VM(0);
			ProcessStep.End();

			ProcessStep.Start("Transfer CO2 from MC to IP");
			Alert("Operator Needed", "Put LN on inlet port.");
			MessageHandler.Send("Operator needed", "Almost ready for LN on inlet port.\r\n" +
				"Press Ok to continue, then raise LN onto inlet port tube");

			v_HV.Close();
			v_MC_split.Open();

			ProcessSubStep.Start("Wait for CO2 to freeze in the IP");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 4 * 60000)
				wait();
			ProcessSubStep.End();

			Alert("Operator Needed", "Raise inlet port LN.");
			MessageHandler.Send("Operator needed", "Raise inlet port LN one inch.\r\n" +
				"Press Ok to continue.");

			ProcessSubStep.Start("Wait 30 seconds");
			wait(30000);
			ProcessSubStep.End();

			v_IP_IM.Close();
			ProcessStep.End();
		}

		#endregion Transfer CO2 between sections

		#endregion Process control

		#region Chamber volume calibration routines

		// p1 (v0 + v1) = p0 v0; p0 / p1 = (v0 + v1) / v0 = v1 / v0 + 1
		// v1 / v0 = p0 / p1 - 1
		protected double v1_v0(double[] p0, double[] p1) { return meanQuotient(p0, p1) - 1; }
		protected double v1(double v0, double[] p0, double[] p1) { return v0 * v1_v0(p0, p1); }
		protected double[][] daa(int n, int m)
		{
			double[][] a = new double[n][];
			for (int i = 0; i < n; ++i) a[i] = new double[m];
			return a;
		}
		protected double meanQuotient(double[] numerators, double[] denominators)
		{
			// WARNING: no checking for divide-by-zero or programmer errors
			// like empty or mis-matched arrays, etc
			double s = 0;
			int n = numerators.Length;
			for (int i = 0; i < n; i++)
				s += numerators[i] / denominators[i];
			return s / n;
		}

		//enum cal_v0 { MC_GM, IM_GM, FC_GM };

		protected virtual void admit_cal_gas(CalibrationVolume v0) { }

		protected double measure_volume()
		{
			return measure_volume(null, null);
		}

		protected double measure_volume(Valve v_OpenMe)
		{
			return measure_volume(v_OpenMe, null);
		}

		protected double measure_volume(Valve v_OpenMe, Valve v_CloseMeFirst)
		{
			if (v_OpenMe != null)
			{
				ProcessSubStep.Start("Expand gas via " + v_OpenMe.Name);
				if (v_CloseMeFirst != null) v_CloseMeFirst.Close();
				v_OpenMe.Open();
				ProcessSubStep.End();
			}

			ProcessSubStep.Start("Wait for pressure to stabilize");
			wait(milliseconds_calibration);
			ProcessSubStep.End();

			ProcessSubStep.Start("Observe pressure when ugCRoC < 0.010");
			while (Math.Abs(ugCinMC.RoC) > 0.010) wait();
			ProcessSubStep.End();

			return ugCinMC;
		}

		/// <summary>
		/// Returns the volume ratio MCx / MC
		/// </summary>
		/// <param name="v_MCx">The MC-MCx valve</param>
		/// <param name="p_calibration">Inital pressure to admit into the MC</param>
		/// <param name="repeats">The number of times to repeat the test</param>
		/// <returns></returns>
		protected double measure_MC_MCx(Valve v_MCx, int repeats)
		{
			double[][] obs = daa(2, repeats);

			ProcessStep.Start("Measure volume ratio MC:" + v_MCx.Name.Substring(5, 3));

			SampleLog.Record("MC, MC+MCx:");
			for (int i = 0; i < repeats; i++)
			{
				admit_cal_gas(MC_GM);

				v_MC_split.Close();

				obs[0][i] = measure_volume();
				obs[1][i] = measure_volume(v_MCx);

				ProcessSubStep.Start("Record observed pressures to Sample Data.log");
				SampleLog.Record(
					obs[0][i].ToString("0.0") + "\t" +
					obs[1][i].ToString("0.0"));
				ProcessSubStep.End();
			}
			open_line();

			ProcessStep.End();

			return v1_v0(obs[0], obs[1]);
		}

		/// <summary>
		/// Install the known volume chamber in place of the MCU.
		/// Sets the value of mL_MC.
		/// </summary>
		protected void calibrate_volume_MC(int repeats)
		{
			SampleLog.Write("\r\n");
			SampleLog.Record("Old mL_MC: " + mL_MC.ToString());
			mL_MC = mL_KV / measure_MC_MCx(v_MC_MCU, repeats);
			SampleLog.Record("New mL_MC: " + mL_MC.ToString());
			SampleLog.Write("\r\n");
		}

		protected virtual void calibrate_all_volumes_from_MC(int repeats) { }

		protected void calibrate_volumes_MCL_MCU(int repeats)
		{
			mL_MCL = mL_MC * measure_MC_MCx(v_MC_MCL, repeats);
			mL_MCU = mL_MC * measure_MC_MCx(v_MC_MCU, repeats);
		}

		protected void calibrate_volumes_split_GM(int repeats)
		{
			double[][] obs = daa(3, repeats);   // observations

			ProcessStep.Start("Calibrate volumes split..GM");

			SampleLog.Record("MC, MC..split, MC..GM");
			for (int i = 0; i < repeats; i++)
			{
				admit_cal_gas(MC_GM);

				// make sure the volume to be measured is evacuated
				ProcessSubStep.Start("Evacuate split..GM");
				v_MC_split.Close();
				v_split_GM.Open();

				v_HV.Close();
				v_GM_VM.Open();
				roughAndEvacuate(pressure_ok);
				ProcessSubStep.End();

				obs[0][i] = measure_volume();
				obs[1][i] = measure_volume(v_MC_split, v_split_GM);
				obs[2][i] = measure_volume(v_split_GM, v_GM_VM);

				SampleLog.Record(
					obs[0][i].ToString("0.0") + "\t" +
					obs[1][i].ToString("0.0") + "\t" +
					obs[2][i].ToString("0.0"));
			}
			open_line();

			double v0 = mL_MC;
			mL_split = v1(v0, obs[0], obs[1]);

			v0 += mL_split;
			mL_GM = v1(v0, obs[1], obs[2]);

			ProcessStep.End();
		}

		protected void calibrate_volumes_GR(int repeats)
		{
			double[][] obs = daa(7, repeats);   // observations

			// all GRs must be available
			if (ready_GRs() < 6) return;

			ProcessStep.Start("Calibrate volumes GR1..GR6");

			SampleLog.Record("MC..GM, MC..GR1, MC..GR2, MC..GR3, MC..GR4, MC..GR5, MC..GR6");
			for (int i = 0; i < repeats; i++)
			{
				admit_cal_gas(MC_GM);

				obs[0][i] = measure_volume();
				obs[1][i] = measure_volume(v_GR_GM[0]);
				obs[2][i] = measure_volume(v_GR_GM[1]);
				obs[3][i] = measure_volume(v_GR_GM[2]);
				obs[4][i] = measure_volume(v_GR_GM[3]);
				obs[5][i] = measure_volume(v_GR_GM[4]);
				obs[6][i] = measure_volume(v_GR_GM[5]);

				SampleLog.Record(
					obs[0][i].ToString("0.0") + "\t" +
					obs[1][i].ToString("0.0") + "\t" +
					obs[2][i].ToString("0.0") + "\t" +
					obs[3][i].ToString("0.0") + "\t" +
					obs[4][i].ToString("0.0") + "\t" +
					obs[5][i].ToString("0.0") + "\t" +
					obs[6][i].ToString("0.0"));
			}
			open_line();

			double v0 = mL_MC + mL_split + mL_GM;
			mL_GR1 = v1(v0, obs[0], obs[1]);

			v0 += mL_GR1;
			mL_GR2 = v1(v0, obs[1], obs[2]);

			v0 += mL_GR2;
			mL_GR3 = v1(v0, obs[2], obs[3]);

			v0 += mL_GR3;
			mL_GR4 = v1(v0, obs[3], obs[4]);

			v0 += mL_GR4;
			mL_GR5 = v1(v0, obs[4], obs[5]);

			v0 += mL_GR5;
			mL_GR6 = v1(v0, obs[5], obs[6]);

			ProcessStep.End();
		}

		protected virtual void calibrate_volumes_d13C_VP(int repeats) { }

		protected virtual void calibrate_volume_VM(int repeats) { }

		#endregion Chamber volume calibration routines

		#region Test functions

		protected void admit_IP_O2_300()
		{
			v_IP_IM.Close();
			isolate_sections();

			v_HV.Close();

			for (int i = 0; i < 3; i++)
			{
				v_O2_IM.Open();
				waitForActuatorController();
				wait(1000);
				v_O2_IM.Close();
				waitForActuatorController();

				v_IM_VM.Open();
				waitForActuatorController();
				v_IM_VM.Close();
				waitForActuatorController();
			}

			v_IP_IM.Open();
			waitForActuatorController();
			wait(2000);
			v_IP_IM.Close();
			waitForActuatorController();
			wait(5000);
		}

		protected void CO2_MC_IP_MC_loop()
		{
			freeze_VTT();
			transfer_CO2_from_MC_to_IP();
			admit_IP_O2_300();
			//admit_IP_He(300);
			evacuate_IM();
			Alert("Operator Needed", "Thaw inlet port.");
			MessageHandler.Send("Operator needed", 
				"Remove LN from inlet port and thaw the coldfinger.\r\n" +
				"Press Ok to continue");
			bleed();
			extract();
			measure();
			close_all_GRs();
		}

		protected void measure_CO2_extraction_yield()
		{
			SampleLog.WriteLine("\r\n");
			SampleLog.Record("CO2 Extraction yield test");
			SampleLog.Record("Bleed target: " + pressure_VTT_bleed_sample.ToString());

			ProcessStep.Start("Measure CO2 Extraction yield");
			if (ugCinMC < Sample.micrograms * 0.8)
			{
				VTT.Dirty = false;    // keep cold
				open_line();
				waitFor_p_VM(pressure_clean);
				admitDeadCO2(Sample.micrograms);
			}
			measure();
			int n; try { n = int.Parse(Sample.ID); } catch { n = 1; }
			for (int repeats = 0; repeats < n; repeats++)
			{
				Sample.micrograms = Sample.ugC;
				CO2_MC_IP_MC_loop();
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
			while (ftc_MC.Temperature <= VTT.RegulatedSetpoint + 10) wait();
			ProcessSubStep.End();
			ftc_MC.Stop();    // stop thawing to save time

			// record pressure
			SampleLog.Record("\tPressure of pre-CO2 discarded gases:\t" +
				m_p_MC.Value.ToString("0.000") + "\tTorr");

			v_CuAg_MC.Close();
			discard_MC_gases();
			v_VTTR_CuAg.Open();
			v_MC_split.Close();
			ProcessStep.End();
		}

		protected void step_extract()
		{
			pressurize_VTT_MC();
			// The equilibrium temperature of HCl at pressures from ~(1e-5..1e1)
			// is about 14 degC or more colder than CO2 at the same pressure.
			pressurizedExtract(-13);        // targets HCl
			discardExtractedGases();
			pressurizedExtract(1);        // targets CO2
		}

		protected void StepExtractionYieldTest()
		{
			Sample.ID = "Step Extraction Yield Test";
			//admitDeadCO2(1000);
			measure();

			transfer_CO2_from_MC_to_VTT();
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
