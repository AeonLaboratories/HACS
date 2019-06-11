using System;
using System.Collections.Generic;
using System.Linq;
using HACS.Components;
using System.Xml.Serialization;
using System.Threading;
using System.IO;
using System.Net.Mail;
using Utilities;
using System.Text;

namespace HACS.Core
{
	public class CEGS : ProcessManager
	{
        #region Component Implementation

        public static new List<CEGS> List = new List<CEGS>();
        public static new CEGS Find(string name) { return List?.Find(x => x?.Name == name); }

        public override void Connect()
        {
            List.Add(this);

			// HacsLog needs these early
			HacsLog.LogFolder = LogFolder;
			HacsLog.ArchiveFolder = ArchiveFolder;

			findComponents();		// the bulk of the CEGS.Connect() work

            BuildProcessDictionary();
            Combust = combust;

            ftc_MC.AirTemperatureSensor = m_t_MC;		// TODO: name and add to settings.xml ?

            m_p_MC.StateChanged += updateSampleMeasurement;

			// Note: CEGS itself is not in ProcessManagers
            foreach (var d in ProcessManagers)
            {
                d.ShowProcessSequenceEditor = ShowProcessSequenceEditor;
                d.EventLog = EventLog;
            }

            VacuumSystem.ProcessStep = ProcessSubStep;
            foreach (var gs in GasSupplies)
                gs.ProcessStep = ProcessSubStep;

            // TODO: add Chambers list to SystemComponents
            // ...and have Calibrate() update the volume there
            // GR can derive from Chamber, or contain one...
            GR[0].MilliLitersVolume = mL_GR1;
            GR[1].MilliLitersVolume = mL_GR2;
            GR[2].MilliLitersVolume = mL_GR3;
            GR[3].MilliLitersVolume = mL_GR4;
            GR[4].MilliLitersVolume = mL_GR5;
            GR[5].MilliLitersVolume = mL_GR6;

            calculateDerivedConstants();
        }

		/// <summary>
		/// Part of Connect()
		/// </summary>
		protected virtual void findComponents()
		{
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

			#region DAQs
			// the list is used
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
			m_t_Ambient = Meter.Find("m_t_Ambient");
			m_p_GR = new Meter[6];
			m_p_GR[0] = Meter.Find("m_p_GR1");
			m_p_GR[1] = Meter.Find("m_p_GR2");
			m_p_GR[2] = Meter.Find("m_p_GR3");
			m_p_GR[3] = Meter.Find("m_p_GR4");
			m_p_GR[4] = Meter.Find("m_p_GR5");
			m_p_GR[5] = Meter.Find("m_p_GR6");
			m_V_5VMainsDetect = Meter.Find("m_V_5VMainsDetect");
			#endregion Meters

			#region DigitalOutputs
			IonGaugeEnable = DigitalOutput.Find("IonGaugeEnable");
			#endregion DigitalOutputs

			#region VSPressures
			VSPressure = VSPressure.Find("VSPressure");
			#endregion VSPressures

			#region VacuumSystems
			VacuumSystem = VacuumSystem.Find("VacuumSystem");
			#endregion VacuumSystems

			#region Sections
			IMSection = Section.Find("IMSection");
			VttSection = Section.Find("VttSection");
			MCSection = Section.Find("MCSection");
			SplitSection = Section.Find("SplitSection");
			GMSection = Section.Find("GMSection");
			#endregion Sections

			#region ActuatorControllers
			ActuatorController0 = ActuatorController.Find("ActuatorController0");
			#endregion ActuatorControllers

			#region ThermalControllers
			// the list is used
			#endregion ThermalControllers

			#region Valves
			v_HV = Valve.Find("v_HV");
			v_LV = Valve.Find("v_LV");
			v_B = Valve.Find("v_B");
			v_R = Valve.Find("v_R");

			v_IM_VM = Valve.Find("v_IM_VM");
			v_VTT_VM = Valve.Find("v_VTT_VM");
			v_split_VM = Valve.Find("v_split_VM");

			v_He_IM = Valve.Find("v_He_IM");
			v_O2_IM = Valve.Find("v_O2_IM");
			v_IP_IM = Valve.Find("v_IP_IM");
			v_IP2_IM = Valve.Find("v_IP2_IM");      // it's ok for this to fail and for v_IP2_IM to be null

			v_IM_VTT = Valve.Find("v_IM_VTT");
			v_VTT_CuAg = Valve.Find("v_VTT_CuAg");
			v_CuAg_MC = Valve.Find("v_CuAg_MC");

			v_MC_MCU = Valve.Find("v_MC_MCU");
			v_MC_MCL = Valve.Find("v_MC_MCL");
			v_MC_split = Valve.Find("v_MC_split");
			v_GM_split = Valve.Find("v_GM_split");

			v_He_split = Valve.Find("v_He_split");
			v_H2_GM = Valve.Find("v_H2_GM");
			v_CO2_MC = Valve.Find("v_CO2_MC");

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
			v_He_split_flow = Valve.Find("v_He_split_flow");
			v_H2_GM_flow = Valve.Find("v_H2_GM_flow");
			v_CO2_MC_flow = Valve.Find("v_CO2_MC_flow");

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
			v_LN_drain = Valve.Find("v_LN_drain");
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
			SwitchBank0 = SwitchBank.Find("SwitchBank0");
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
			fan_pump_HV = OnOffDevice.Find("fan_pump_HV");
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

			#region GasSupplies
			gs_O2_IM = GasSupply.Find("gs_O2_IM");
			gs_He_IM = GasSupply.Find("gs_He_IM");
			gs_He_IP = GasSupply.Find("gs_He_IP");
			gs_He_VTT = GasSupply.Find("gs_He_VTT");
			gs_He_GM = GasSupply.Find("gs_He_GM");
			gs_He_MC = GasSupply.Find("gs_He_MC");
			gs_He_MC_GM = GasSupply.Find("gs_He_MC_GM");
			gs_He_IM_GM = GasSupply.Find("gs_He_IM_GM");
			gs_CO2_MC = GasSupply.Find("gs_CO2_MC");
			gs_H2_GM = GasSupply.Find("gs_H2_GM");
			gs_He_VTT_via_split = GasSupply.Find("gs_He_VTT_via_split");
			#endregion GasSupplies

		}

		// These empty overrides are needed to prevent base implementations
		// from doing their thing until specifically invoked by a CEGS method.
		public override void Initialize() { }
        public override void ComponentStart() { }
		public override void ComponentStop() { }

		#endregion Component Implementation

		#region System configuration

		#region Component lists

		public List<ActuatorController> ActuatorControllers { get; set; }
        public List<AnalogOutput> AnalogOutputs { get; set; }
		public List<DigitalOutput> DigitalOutputs { get; set; }
		public List<DynamicQuantity> DynamicQuantities { get; set; }
		public List<FTColdfinger> FTCs { get; set; }
		public List<GasSupply> GasSupplies { get; set; }
		public List<GraphiteReactor> GRs { get; set; }
		public List<HacsLog> Logs { get; set; }
		public List<Heater> Heaters { get; set; }
		public List<LabJackDaq> DAQs { get; set; }
		public List<LinePort> LinePorts { get; set; }
		public List<MassFlowController> MFCs { get; set; }
		public List<Meter> Meters { get; set; }
		public List<OnOffDevice> OnOffDevices { get; set; }
		public List<ProcessManager> ProcessManagers { get; set; }
		public List<Sample> Samples { get; set; }
		public List<SampleSource> SampleSources { get; set; }
		public List<Section> Sections { get; set; }
		public List<SwitchBank> SwitchBanks { get; set; }
		public List<Tank> Tanks { get; set; }
		public List<TempSensor> TempSensors { get; set; }
		public List<ThermalController> ThermalControllers { get; set; }
		public List<TubeFurnace> TubeFurnaces { get; set; }
		public List<VacuumSystem> VacuumSystems { get; set; }
		public List<Valve> Valves { get; set; }
		public List<VSPressure> VSPressures { get; set; }
		public List<VTT> VTTs { get; set; }

		#endregion Component lists

		#region SystemComponents

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
		//[XmlIgnore] public HacsLog EventLog;      // declared in ProcessManager
		#endregion Data Logs

		#region LabJack DAQ
		[XmlIgnore] public int[] LabJack_LocalID;   // list of LabJack Local IDs
		#endregion LabJack DAQ

		#region Meters
		[XmlIgnore] public Meter m_p_MC;
		[XmlIgnore] public Meter m_p_Foreline;
		[XmlIgnore] public Meter m_p_VTT;
		[XmlIgnore] public Meter m_p_IM;
		[XmlIgnore] public Meter m_p_GM;
		[XmlIgnore] public Meter m_p_VM_IG;
		[XmlIgnore] public Meter m_p_VM_HP;
		[XmlIgnore] public Meter[] m_p_GR;
		[XmlIgnore] public Meter m_v_LN_supply;
		[XmlIgnore] public Meter m_t_MC;
		[XmlIgnore] public Meter m_V_5VPower;
		[XmlIgnore] public Meter m_p_Ambient;
		[XmlIgnore] public Meter m_t_Ambient;
		[XmlIgnore] public Meter m_V_5VMainsDetect;
		#endregion Meters

		#region Digital IO
		[XmlIgnore] public DigitalOutput IonGaugeEnable;
		#endregion Digital IO

		#region Serial devices
		[XmlIgnore] public ActuatorController ActuatorController0;
		//[XmlIgnore] public List<ThermalController> ThermalControllers;
		#endregion Serial devices

		#region Valves
		[XmlIgnore] public Valve v_HV;
		[XmlIgnore] public Valve v_LV;
		[XmlIgnore] public Valve v_B;
		[XmlIgnore] public Valve v_R;

		[XmlIgnore] public Valve v_IM_VM;
		[XmlIgnore] public Valve v_VTT_VM;
		[XmlIgnore] public Valve v_split_VM;

		[XmlIgnore] public Valve v_He_IM;
		[XmlIgnore] public Valve v_O2_IM;
		[XmlIgnore] public Valve v_IP2_IM;
		[XmlIgnore] public Valve v_IP_IM;
		[XmlIgnore] public Valve v_IM_VTT;
		[XmlIgnore] public Valve v_VTT_flow;
		[XmlIgnore] public Valve v_VTT_CuAg;
		[XmlIgnore] public Valve v_CuAg_MC;
		[XmlIgnore] public Valve v_MC_MCU;
		[XmlIgnore] public Valve v_MC_MCL;
		[XmlIgnore] public Valve v_MC_split;
		[XmlIgnore] public Valve v_GM_split;
		[XmlIgnore] public Valve v_He_split;
		[XmlIgnore] public Valve v_He_split_flow;
		[XmlIgnore] public Valve v_H2_GM;
		[XmlIgnore] public Valve v_H2_GM_flow;
		[XmlIgnore] public Valve v_CO2_MC;
		[XmlIgnore] public Valve v_CO2_MC_flow;
		[XmlIgnore] public Valve[] v_GR_GM;
		[XmlIgnore] public Valve v_d13C_GM;
		[XmlIgnore] public Valve v_VP_d13C;

		[XmlIgnore] public Valve v_LN_VTT;
		[XmlIgnore] public Valve v_LN_CuAg;
		[XmlIgnore] public Valve v_LN_MC;
		[XmlIgnore] public Valve[] v_LN_GR;
		[XmlIgnore] public Valve v_LN_VP;
		[XmlIgnore] public Valve v_LN_drain;
		#endregion Valves;

		#region VSPressures
		[XmlIgnore] public VSPressure VSPressure;
		#endregion VSPressures

		#region VacuumSystems
		[XmlIgnore] public VacuumSystem VacuumSystem;
		#endregion VacuumSystems

		#region Sections
		[XmlIgnore] public Section IMSection;
		[XmlIgnore] public Section VttSection;
		[XmlIgnore] public Section MCSection;
		[XmlIgnore] public Section SplitSection;
		[XmlIgnore] public Section GMSection;
		#endregion Sections

		#region Heaters
		[XmlIgnore] public Heater h_VTT;
		[XmlIgnore] public Heater h_CuAg;
		[XmlIgnore] public Heater[] h_GR;
		[XmlIgnore] public Heater h_CC_Q;
		[XmlIgnore] public Heater h_CC_S;
		[XmlIgnore] public Heater h_CC_S2;
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
		[XmlIgnore] public SwitchBank SwitchBank0;
		#endregion SwitchBanks

		#region OnOffDevices
		[XmlIgnore] public OnOffDevice air_VTT_FTC;
		[XmlIgnore] public OnOffDevice air_CuAg_FTC;
		[XmlIgnore] public OnOffDevice air_MC_FTC;
		[XmlIgnore] public OnOffDevice[] air_GR_FTC;
		[XmlIgnore] public OnOffDevice air_VP_FTC;

		[XmlIgnore] public OnOffDevice LN_Tank_LN;

		[XmlIgnore] public OnOffDevice fan_pump_HV;

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
		[XmlIgnore] public DynamicQuantity ugCinMC;
		#endregion Dynamic Quantities

		#region Gas Supplies
		[XmlIgnore] public GasSupply gs_O2_IM;
		//[XmlIgnore] public GasSupply gs_O2_MC;    // to be used for volume calibrations in the future
		[XmlIgnore] public GasSupply gs_He_IM;
		[XmlIgnore] public GasSupply gs_He_IP;
		[XmlIgnore] public GasSupply gs_He_VTT;
		[XmlIgnore] public GasSupply gs_He_GM;
		[XmlIgnore] public GasSupply gs_He_MC;
		[XmlIgnore] public GasSupply gs_He_MC_GM;
		[XmlIgnore] public GasSupply gs_He_IM_GM;
		[XmlIgnore] public GasSupply gs_CO2_MC;
		[XmlIgnore] public GasSupply gs_H2_GM;
		[XmlIgnore] public GasSupply gs_He_VTT_via_split;
		#endregion Gas Supplies

		#endregion SystemComponents

		#region Globals

		public string LastAlertMessage { get; set; }
		public ContactInfo ContactInfo { get; set; }
		public SmtpInfo SmtpInfo { get; set; }

		#region UI Communications

		[XmlIgnore] public MessageSender MessageHandler = new MessageSender();		
		[XmlIgnore] public Func<bool, bool> VerifySampleInfo;
		[XmlIgnore] public Action PlaySound;

		#endregion UI Communications

		#region Logging
		
		public string LogFolder { get; set; }
		public string ArchiveFolder { get; set; }

		#endregion Logging

		#region System state & operations
		
		public bool EnableWatchdogs { get; set; }
		public bool EnableAutozero { get; set; }
		public string Last_GR { get; set; }
		public int Next_GraphiteNumber { get; set; }

		[XmlIgnore] public bool PowerFailed { get; set; }

		#endregion System state & operations

		public int CurrentSample { get; set; } = 0;   // Future proofing. Stays constant for now.
		[XmlIgnore] public Sample Sample
        {
            get { return Samples[CurrentSample]; }
			set { Samples[CurrentSample] = value; }
		}

		#endregion Globals

        #region Constants
        // the only way to alter constants is to edit settings.xml
        // derived constants should be tagged [XmlIgnore]

        #region Pressure Constants

        public double pressure_over_atm { get; set; }
		public double pressure_ok { get; set; }				// clean enough to join sections for drying
		public double pressure_clean { get; set; }			// clean enough to start a new sample

		// When He is admitted from GM+d13C into VP, the nominal pGM drop is:
		//	pressure_VP_He_Drop = pressure_over_atm * vVP / (vGM + vd13C)
		// The value for pressure_VP_He_Initial is found empirically, by filling
		// GM..d13C with a pressure of He such that, when the gas is expanded into
		// GM..VP with the VP_FTC in the "Raise" state, the GM pressure falls by
		// <pressure_VP_He_Drop> Torr.
		public double pressure_VP_He_Initial { get; set; }
		public double pressure_VP_Error { get; set; }				// abs(pVP - pressure_over_atm) < this value is nominal
		[XmlIgnore] public double pressure_VP_He_Drop;
		[XmlIgnore] public double pressure_VP_He_Final;

		public double pressure_IM_O2 { get; set; }
		public double pressure_VTT_bleed_sample { get; set; }
		public double pressure_VTT_bleed_cleaning { get; set; }
		public double pressure_VTT_near_end_of_bleed { get; set; }

		public double pressure_Fe_prep_H2 { get; set; }

		[XmlIgnore] public double pressure_foreline_empty;
		[XmlIgnore] public double pressure_max_backing;

		public double pressure_calibration { get; set; }	// Torr of He

		#endregion Pressure Constants

		#region Rate of Change Constants

		public double roc_pVTT_falling_very_slowly { get; set; }
		public double roc_pVTT_falling_barely { get; set; }

		public double roc_pIM_plugged { get; set; }
		public double roc_pIM_loaded { get; set; }

		#endregion Rate of Change Constants

		#region Temperature Constants
		public int temperature_room { get; set; }		// "standard" room temperature
		public int temperature_warm { get; set; }
		public int temperature_CO2_evolution { get; set; }
		public int temperature_CO2_collection_min { get; set; }
		public int temperature_FTC_frozen { get; set; }
		public int temperature_FTC_raised { get; set; }
		public int temperature_VTT_cold { get; set; }
		public int temperature_VTT_cleanup { get; set; }
		public int temperature_trap_sulfur { get; set; }

		public int temperature_Fe_prep { get; set; }
		public int temperature_Fe_prep_max_error { get; set; }

		#endregion

		#region Time Constants
		public int minutes_Fe_prep { get; set; }
		public int minutes_CC_Q_Warmup { get; set; }
		public int minutes_trap_sulfur { get; set; }
		public int seconds_FTC_raised { get; set; }
		public int seconds_flow_supply_purge { get; set; }
		public int milliseconds_power_down_max { get; set; }
		public int milliseconds_UpdateLoop_interval { get; set; }
		public int milliseconds_calibration { get; set; }		// milliseconds of settling time
		#endregion

		#region Sample Measurement Constants

		// fundamental constants
		public double L { get; set; }				// Avogadro's number (particles/mol)
		public double kB { get; set; }				// Boltzmann constant (Pa * m^3 / K)
		public double Pa { get; set; }				// Pascals (1/atm)
		public double Torr { get; set; }				// (1/atm)
		public double mL { get; set; }				// milliliters per liter
		public double m3 { get; set; }				// cubic meters per liter

		public double ZeroDegreesC { get; set; }		// kelvins
		public double ugC_mol { get; set; }         // mass of carbon per mole, in micrograms,
                                                    // assuming standard isotopic composition

        // chamber volumes (mL)
		public double mL_KV { get; set; }			// known volume
		public double mL_VM { get; set; }			// vacuum manifold
		public double mL_IM { get; set; }			// intake manifold
		public double mL_IP { get; set; }			// inlet port
		public double mL_VTT { get; set; }			// VTT
		public double mL_CuAg { get; set; }			// copper/silver trap
		public double mL_MC { get; set; }			// measurement chamber
		public double mL_MCU { get; set; }			// upper aliquot
		public double mL_MCL { get; set; }			// lower aliquot
		public double mL_split { get; set; }			// split chamber
		public double mL_GM { get; set; }			// graphite manifold
		public double mL_GR1 { get; set; }			// graphite reactor 1
		public double mL_GR2 { get; set; }
		public double mL_GR3 { get; set; }
		public double mL_GR4 { get; set; }
		public double mL_GR5 { get; set; }
		public double mL_GR6 { get; set; }
		public double mL_d13C { get; set; }			// d13C aliquant
		public double mL_VP { get; set; }			// vial port (with vial)

		public double H2_CO2_stoich { get; set; }	// stoichiometric
		public double H2_CO2 { get; set; }			// target H2:CO2 ratio for graphitization

		// The value below is an average of pressure ratios observed for a quantity of H2 
		// in the GRs. The denominator pressures are observed with the GR at room 
		// temperature (same as GM). The numerator pressures are several minimums observed 
		// with the GRs cooled by FTCs to the "raise" state.
		public double densityAdjustment { get; set; }   // pressure reduction due to higher density of H2 in GR coldfinger

		public int mass_small_sample { get; set; }
		public int mass_diluted_sample { get; set; }
		public int ugC_sample_max { get; set; }

		// kB using Torr and milliliters instead of pascals and cubic meters
		[XmlIgnore] public double kB_Torr_mL;
		[XmlIgnore] public double nC_ug;			// average number of carbon atoms per microgram

		// Useful volume ratios
		[XmlIgnore] public double rAMS;		// remaining for AMS after d13C is taken
		[XmlIgnore] public double rMCU;
		[XmlIgnore] public double rMCL;

		[XmlIgnore] public int ugC_d13C_max;

		#endregion Sample Measurement Constants

		public int LN_supply_min { get; set; }
		public double V_5VMainsDetect_min { get; set; }

		[XmlIgnore] LookupTable CO2EqTable = new LookupTable(@"CO2 eq.dat");


		#endregion Constants

		#endregion System configuration

		#region System elements not saved/restored in Settings

		[XmlIgnore] public string SettingsFilename = @"settings.xml";

		// for requesting user interface services
		[XmlIgnore] public EventHandler RequestService;

		protected XmlSerializer XmlSerializer;

		#region Threading

		protected Timer updateTimer;

		// alert system
		protected Queue<AlertMessage> QAlertMessage = new Queue<AlertMessage>();
		protected Thread alertThread;
		protected AutoResetEvent alertSignal = new AutoResetEvent(false);
		protected Stopwatch AlertTimer = new Stopwatch();

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
		[XmlIgnore] public bool SampleIsRunning
        { get { return ProcessType == ProcessTypes.Sequence && !RunCompleted; } }

        [XmlIgnore] protected Stopwatch PowerDownTimer = new Stopwatch();

		#endregion System elements not saved in/restored from Settings

		#region Startup and ShutDown

		// nonstandard Start() -- i.e., this is not ComponentStart()
		// Caller must invoke Component.ConnectAll() and Component.InitializeAll(),
		// in that order, before invoking this method.
		public virtual void Start()
		{
			StartAll();             // start all the components
			base.Initialize();      // delayed so UI can come up before system runs
			SystemRunTime.Start();
			initializeThreads();
		}

		// nonstandard Stop()
		public virtual void Stop()
		{
			try
			{
				ShuttingDown = true;
				updateTimer.Dispose();
				EventLog.Record("System shutting down");

				closeLNValves();

				while (lowPriorityThread != null && lowPriorityThread.IsAlive)
					Thread.Sleep(1);

				saveSettings(SettingsFilename);

				LN_Tank.IsActive = false;
				h_VTT.TurnOff();

				// TODO: this test is too specialized
				// we should either:
				//		1. not turnOff any devices, or
				//		2. maintain a list of devices that should be (or shouldn't be)
				//			forced to a given state on shutdown
				foreach (var d in OnOffDevices)
				{
					if (d != fan_pump_HV && d != fan_IP)
						d.TurnOff();
				}

				// stop the logs last so Components can log shutdown
				StopAll(Logs.Cast<Component>().ToList());
				Logs?.ForEach(x => x?.ComponentStop());

				SerialPortMonitor.Stop();

				base.ComponentStop();
			}
			catch (Exception e)
			{
				MessageHandler.Send(e.ToString());
			}
		}


		protected void calculateDerivedConstants()
		{
			pressure_VP_He_Drop = pressure_over_atm * mL_VP / (mL_GM + mL_d13C);
			pressure_VP_He_Final = pressure_VP_He_Initial - pressure_VP_He_Drop;

			#region Sample measurement constants
			kB_Torr_mL = kB * Torr / Pa * mL / m3;
			nC_ug = L / ugC_mol;		// number of atoms per microgram of carbon (standard isotopic distribution)

			rAMS = 1 - mL_d13C / (mL_MC + mL_split + mL_GM + mL_d13C);

			rMCU = mL_MCU / mL_MC;
			rMCL = mL_MCL / mL_MC;

			ugC_d13C_max = (int)((1 - rAMS) * (double)ugC_sample_max);
			#endregion Sample measurement constants
		}

		protected void initializeThreads()
		{
			alertThread = new Thread(AlertHandler)
			{
                Name = $"{Name} AlertHandler",
                IsBackground = true
			};
			alertThread.Start();

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

				var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
				XmlSerializer.Serialize(stream, this);
				stream.Close();
			}
			catch (Exception e)
			{
				EventLog.Record("Exception saving settings\r\n" + e.ToString());
			}
		}

		#endregion Startup and ShutDown

		#region Periodic system activities & maintenance

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
			ugCinMC.Update(ugC(m_p_MC, mL_MC, m_t_MC));
		}

		#region Logging
		// To be replaced by a database system in the future

		[XmlIgnore] public double old_VSPressure;
		[XmlIgnore] public string VMrpt = "";
		protected virtual void logP_VMStatus()
		{ logPvmStatus(VMPLog, VSPressure, ref old_VSPressure, ref VMrpt); }

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
				Math.Abs(old_pVTT - m_p_VTT) > 0.001 ||
				Math.Abs(old_tVTT - VTT.Temperature) >= 0.4 ||
				VTTLog.ElapsedMilliseconds > 60000
				)
			{
				old_pVTT = m_p_VTT;
				old_tVTT = VTT.Temperature;
                VTTLog.Record($"{old_pVTT:0.000}\t{old_tVTT:0.0}\t{VTT.Coldfinger.Temperature:0.0}\t{VTT.WireTempSensor.Temperature:0.0}\t{ts_VTT_top.Temperature:0.0}");
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
                MCLog.Record($"{m_p_MC.Value:0.000}\t{m_t_MC.Value:0.00}\t{ugCinMC.Value:0.0}\t{ftc_MC.Temperature:0.0}");
			}
		}

		[XmlIgnore] public double old_pIM, old_pGM, old_pVTT, old_pForeline;
		protected virtual void logPressureStatus()
		{
			if (Math.Abs(old_pIM - m_p_IM) > 2 ||
				Math.Abs(old_pGM - m_p_GM) > 2 ||
				Math.Abs(old_pForeline - m_p_Foreline) > 0.1 ||
				PLog.ElapsedMilliseconds > 30000
				)
			{
				old_pIM = m_p_IM;
				old_pGM = m_p_GM;
				old_pVTT = m_p_VTT;
				old_pForeline = VacuumSystem.pForeline;
                PLog.Record($"{m_p_Ambient.Value:0.00}\t{old_pIM:0}\t{old_pGM:0}\t{VacuumSystem.pForeline.Value:0.000}\t{VSPressure.Pressure:0.0e0}");
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

                FTCLog.Record($"{old_tGRFTC[0]:0}\t{old_tGRFTC[0]:0}\t{old_tGRFTC[0]:0}\t{old_tGRFTC[0]:0}\t{old_tGRFTC[0]:0}\t{old_tGRFTC[0]:0}\t{old_tVTTFTC:0}\t{old_tCuAgFTC:0}\t{old_tMCFTC:0}\t{old_tVPFTC:0}\t{old_tLNTank:0}");
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
                    $"{ts_LN_Tank.Temperature:0.0}\t" +
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
                        if (!Initialized) continue;
                        try { HacsLog.UpdateAll(); ; }
                        catch (Exception e) { MessageHandler.Send(e.ToString()); }
                    }
				}
			}
			catch (Exception e) { MessageHandler.Send(e.ToString()); }
		}

		#endregion Logging

		// value > Km * sensitivity ==> meter needs zeroing
		protected void ZeroIfNeeded(Meter m, double Km)
		{
			if (Math.Abs(m) >= Km * m.Sensitivity)
				m.ZeroNow();
		}

		protected void ZeroPressureGauges()
		{
			// ensure baseline VM pressure & steady state
			if (VacuumSystem.BaselineTimer.Elapsed.TotalSeconds < 10)
				return;

			//ZeroIfNeeded(m_p_Foreline, 20);	// calibrate this zero manually with Turbo Pump evacuating foreline
				//write VacuumSystem code to do this

			if ((v_VTT_VM.isOpened || v_IM_VM.isOpened && v_IM_VTT.isOpened))
				ZeroIfNeeded(m_p_VTT, 5);

			if (v_split_VM.isOpened && v_MC_split.isOpened)
				ZeroIfNeeded(m_p_MC, 5);

			if (v_IM_VM.isOpened)
				ZeroIfNeeded(m_p_IM, 10);

			if (v_split_VM.isOpened && v_GM_split.isOpened)
			{
				ZeroIfNeeded(m_p_GM, 10);
				foreach (GraphiteReactor gr in GRs)
					if (gr.GMValve.isOpened)
					{
						//if (gr.Name == "GR")
						//MessageBox.Show(gr.Name)
						ZeroIfNeeded(gr.PressureMeter, 5);
					}
			}
		}

		protected void UpdateTimerCallback(object state)
		{
			try
			{
				if (!ShuttingDown)
					Update();
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
			foreach (LabJackDaq lj in DAQs)
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
				systemLogSignal.Set();  // logSystemStatus();
			}
			#endregion 200 ms

			#region 500 ms
			if (daqOk && Initialized && msUpdateLoop % 500 == 0)
			{
				#region manage graphite reactors
				foreach (GraphiteReactor gr in GRs)
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
							if (busy_GRs() == 1 && !SampleIsRunning)  // the 1 is this GR; "Stop" is still 'busy'
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
					if (VTT.State != VTT.States.Stop && VTT.State != VTT.States.Standby) VTT.Stop();
					foreach (FTColdfinger ftc in FTCs)
						if (ftc.State > FTColdfinger.States.Stop) ftc.Stop();
					LN_Tank.IsActive = false;
					Alert("System Warning!", "LN leak.");
				}
				else
				{
					foreach (FTColdfinger ftc in FTCs)
						ftc.Update();
					bool whatItShouldBe = LN_Tank.KeepActive;
					if (!LN_Tank.KeepActive)
					{
						foreach (FTColdfinger ftc in FTCs)
							if (ftc.State >= FTColdfinger.States.Freeze)
							{
								whatItShouldBe = true;
								break;
							}
					}
					if (LN_Tank.IsActive != whatItShouldBe)
						LN_Tank.IsActive = whatItShouldBe;
				}

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

		protected virtual void HandlePowerFailure()
		{
			if (EnableWatchdogs && Initialized && !PowerFailed)
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
						MessageHandler.Send("System Failure", "Mains Power Failure", Message.Type.Tell);
						ProcessThread.Abort();
						VacuumSystem.Isolate();
						// fan_pump_HV.TurnOff();
						v_IM_VM.Close();
						v_VTT_VM.Close();
						v_split_VM.Close();
						ActuatorController0.WaitForIdle();
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
                    if (ShuttingDown) break;
                    if (alertSignal.WaitOne(500))
                    {
                        while (QAlertMessage.Count > 0)
                        {
                            lock (QAlertMessage) alert = QAlertMessage.Dequeue();
                            SendMail(alert.Subject, alert.Message);
                        }
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
				MailMessage mail = new MailMessage
				{
					From = new MailAddress(SmtpInfo.Username, Name)
				};
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
				SmtpClient SmtpServer = new SmtpClient(SmtpInfo.Host, SmtpInfo.Port)
				{
					EnableSsl = true,
					Credentials = new System.Net.NetworkCredential(SmtpInfo.Username, SmtpInfo.Password)
				};
				SmtpServer.Send(mail);
			}
			catch { }
		}

		#endregion Alerts

		#region Process Management

		protected void wait_for_operator()
		{
			Alert("Operator Needed", "Operator needed");
			MessageHandler.Send("Operator needed",
				"Waiting for Operator.\r\n" +
				"Press Ok to continue");
		}

        #region Valve operation

        protected virtual void exerciseAllValves()
        {
            ProcessStep.Start("Exercise all opened valves");

            foreach (Valve v in Valves)
            {
                if (v.isOpened && v.Idle && !v.Talks)
                    exerciseValve(v);
            }

            ProcessStep.End();
        }

        protected virtual void exerciseValve(Valve v)
		{
            ProcessSubStep.Start("Exercising " + v.Name);
            ActuatorController0.WaitForIdle();
			//EventLog.Record("Exercising " + v.Name + " on channel " + v.Channel.ToString());
		    v.Close();
            v.WaitForIdle();
            v.Open();
            v.WaitForIdle();
			ProcessSubStep.End();
		}

		protected virtual void exerciseLNValves()
		{
			ProcessStep.Start("Exercise all LN tank valves");
			exerciseLNValve(v_LN_VTT);
			exerciseLNValve(v_LN_CuAg);
			foreach (Valve v in v_LN_GR)
				exerciseLNValve(v);
			exerciseLNValve(v_LN_MC);
			exerciseLNValve(v_LN_VP);
			ProcessStep.End();
		}

		protected virtual void exerciseLNValve(Valve v)
		{
			v.Open();
			ActuatorController0.WaitForIdle();
			v.Close();
		}

		protected virtual void closeLNValves()
		{
			foreach (FTColdfinger f in FTCs)
				f.LNValve.Close();
		}

		protected virtual void calibrate_flow_valves()
		{
			v_VTT_flow.Calibrate();
			v_CO2_MC_flow.Calibrate();
			v_H2_GM_flow.Calibrate();
			v_He_split_flow.Calibrate();
		}

		protected virtual void open_v_d13C_CF() { }

		protected virtual void close_v_d13C_CF() { }

		#endregion Valve operation

		#region Support and general purpose functions

		protected void waitFor_VSPressure(double pressure)
		{
			ActuatorController0.WaitForIdle();  // make sure there are no pending valve motions
            VacuumSystem.WaitForPressure(pressure);
		}

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
			WaitRemaining(minutes_CC_Q_Warmup);

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
			v_IP2_IM?.Close();
			v_IM_VTT.Close();
			v_IP_IM.Close();
			v_IM_VM.Close();
			v_O2_IM.Open();
			ActuatorController0.WaitForIdle();
			Wait(10000);
			v_O2_IM.Close();
			ActuatorController0.WaitForIdle();

			if (m_p_IM < pressure_IM_O2)
			{
				Alert("Sample Alert!", "Not enough O2");
				MessageHandler.Send("Sample Alert!", "Not enough O2 in IM");
			}

			v_IP_IM.Open();
			ActuatorController0.WaitForIdle();
			Wait(2000);
			v_IP_IM.Close();
			ActuatorController0.WaitForIdle();
			Wait(5000);
		}

		protected void admit_IP_He(double IM_pressure)
		{
			ProcessStep.Start("Admit He into the IP");
            v_IP2_IM?.Close();
			v_IP2_IM?.Close();
			v_IM_VTT.Close();
			v_IP_IM.Close();
			v_IM_VM.Close();
			v_He_IM.Open();
			ActuatorController0.WaitForIdle();
			ProcessSubStep.Start("Wait for pIM ~" + IM_pressure.ToString("0"));
			while (m_p_IM < IM_pressure) Wait();
			ProcessSubStep.End();
			v_He_IM.Close();
			v_IP_IM.Open();
			ActuatorController0.WaitForIdle();
			Wait(2000);
			v_IP_IM.Close();
			ActuatorController0.WaitForIdle();
			ProcessStep.End();
		}

		protected void discard_IP_gases()
		{
			ProcessStep.Start("Discard gases at inlet port (IP)");
			isolate_sections();
			v_IP_IM.Open();
			Wait(10000);				// give some time to record a measurement
			evacuate_IM(pressure_ok);	// allow for high pressure due to water
			ProcessStep.End();
		}

		protected void discard_MC_gases()
		{
			ProcessStep.Start("Discard sample from MC");
			v_GM_split.Close();
			v_split_VM.Close();
			v_MC_split.Open();
			evacuate_MC(0);
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
			v_IM_VTT.Close();
			for (int i = 1; i <= flushes; i++)
			{
				ProcessStep.Start("Flush IP with He (" + i.ToString() + " of " + flushes.ToString() + ")");
				v_IP_IM.Close();
				v_IM_VM.Close();
				v_He_IM.Open();
				ActuatorController0.WaitForIdle();
				v_He_IM.Close();
				v_IP_IM.Open();
				ActuatorController0.WaitForIdle();
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
			// 0.01% of 1000 is 0.1 Torr.
			waitFor_VSPressure(0.1);
			v_IP_IM.Close();
		}

		protected void freeze_VTT()
		{
			ProcessStep.Start("Freeze VTT");

			if (VTT.State != VTT.States.Freeze && VTT.State != VTT.States.Raise) VTT.Freeze();

            if (!IMSection.IsOpened)
				v_IM_VTT.Close();

			if (!SplitSection.IsOpened)
				v_VTT_CuAg.Close();

			ActuatorController0.WaitForIdle();

			if (!VttSection.IsOpened)
				VttSection.Evacuate(pressure_clean);
			else
				VacuumSystem.WaitForPressure(pressure_clean);

			v_VTT_VM.Close();

			ProcessStep.End();
		}

		protected void clean_VTT()
		{
            ProcessStep.Start("Pressurize VTT with He");

            ProcessSubStep.Start("Calibrate VTT flow valve");
            v_VTT_flow.Close();
            v_VTT_flow.Calibrate();
            ProcessSubStep.End();

            ProcessSubStep.Start("Admit He into the VTT");
            gs_He_VTT.Admit();
            ProcessSubStep.End();

            v_IM_VTT.Close();
            v_IM_VTT.WaitForIdle();
            evacuate_IM();     // IM gets pressurized, too
            ProcessStep.End();

			ProcessStep.Start("Bleed He through warm VTT");
			VTT.Regulate(temperature_VTT_cleanup);
			evacuate_VTT();
			while (VTT.Temperature < -5)		// start the flow before too much water starts coming off
				Wait();
			VTT_bleed(pressure_VTT_bleed_cleaning);
			while (VTT.Temperature < temperature_VTT_cleanup)
				Wait();
			ProcessStep.End();
			VTT.Stop();

			ProcessStep.Start("Evacuate VTT");
			v_VTT_flow.Open();
			v_VTT_flow.WaitForIdle();
			waitFor_VSPressure(pressure_ok);
			ProcessStep.End();

			VTT.Dirty = false;
		}

		bool VTT_MC_stable()
		{
			double delta = Math.Abs(m_p_VTT - m_p_MC);
			double div = Math.Max(Math.Min(m_p_VTT, m_p_MC), 0.060);
			double unbalance = delta / div;
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

		protected void waitForMCStable(int seconds)
		{
			ProcessSubStep.Start("Wait for μgC in MC to stabilize for " + ToUnitsString(seconds, "second"));
			while (!ugCinMC.IsStable) Wait();
			Stopwatch sw = new Stopwatch();
			sw.Restart();
			while (sw.ElapsedMilliseconds < seconds * 1000)
			{
				Wait();
				if (!ugCinMC.IsStable)
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
				Wait(minutes * 60000);
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
			while (m_p_MC.Zeroing) Wait();
			ProcessSubStep.End();
		}

		#region FTC operation

		protected void freeze(FTColdfinger ftc)
		{
			ftc.Freeze();

			ProcessSubStep.Start("Wait for " + ftc.Name + " < " + temperature_CO2_collection_min.ToString() + " °C");
			while (ftc.Temperature > temperature_CO2_collection_min) Wait();
			ProcessSubStep.End();
		}

		protected void raise_LN(FTColdfinger ftc)
		{
			ftc.Raise();
			ProcessSubStep.Start("Wait for " + ftc.Name + " < " + temperature_FTC_raised.ToString() + " °C");
			while (ftc.Temperature > temperature_FTC_raised) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait " + seconds_FTC_raised.ToString() + " seconds with LN raised");
			Wait(seconds_FTC_raised * 1000);
			ProcessSubStep.End();
		}

		protected void waitFor_LN_peak(FTColdfinger ftc)
		{
			Valve v = ftc.LNValve;
			ProcessSubStep.Start("Wait until " + ftc.Name + " LN level is at max");
			while (!v.isOpened) Wait();
			while (ftc.Temperature > ftc.Target || !v.isClosed) Wait();
			ProcessSubStep.End();
			ProcessSubStep.Start("Wait for 5 seconds for equilibrium");
			Wait(5000);	// wait for equilibrium
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
			foreach (GraphiteReactor gr in GRs)
				if (gr != exceptGR) gr.GMValve.Close();
			ActuatorController0.WaitForIdle();
		}

		int busy_GRs()
		{
			return GRs.Count(gr => gr.isBusy);
		}

		protected void open_ready_GRs()
		{
			foreach (GraphiteReactor gr in GRs)
				if (gr.isReady) gr.GMValve.Open();
			ActuatorController0.WaitForIdle();
		}

		protected void close_ready_GRs()
		{
			foreach (GraphiteReactor gr in GRs)
				if (gr.isReady) gr.GMValve.Close();
			ActuatorController0.WaitForIdle();
		}

		#endregion GR operation

		#endregion Support and general purpose functions

		#region GR service

		protected void pressurize_GRs_with_He(List<GraphiteReactor> grs)
		{
			v_d13C_GM.Close();
			close_all_GRs();
			isolate_sections();

			// clean_pressurize_GM("He", pressure_over_atm);
			// fast pressurize GM to > 1 atm He
			v_He_split.Open();
			v_He_split_flow.Open();
			ActuatorController0.WaitForIdle();
			while (m_p_GM < m_p_Ambient + 20)
				Wait();

			foreach (GraphiteReactor gr in grs)
				gr.GMValve.Open();
			ActuatorController0.WaitForIdle();

			Wait(3000);
			while (m_p_GM < m_p_Ambient + 20)
				Wait();

			v_He_split.Close();
			close_all_GRs();
			v_He_split_flow.Close();
		}


		protected void prepare_GRs_for_service()
		{
			var grs = new List<GraphiteReactor>();
			foreach (GraphiteReactor gr in GRs)
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

		protected void He_flush_GM(int n)
		{
			for (int i = 0; i < n; i++)
				He_flush_GM();
			v_He_split_flow.Close();
		}

		/// <summary>
		/// caller should close v_He_GM_flow when done
		/// </summary>
		protected void He_flush_GM()
		{
			ProcessSubStep.Start("Flush GM with He");
			v_GM_split.Close();

			v_He_split_flow.Open();
			v_He_split.Open();
			v_He_split.WaitForIdle();
			Wait(5000);
			v_He_split.Close();
			//v_He_GM_flow.Close();		// caller's responsibility

			evacuate_GM();
			ProcessSubStep.End();
		}

		//protected void normalize_GM_gas_flow(GasSupply supply)
		//{
		//	ProcessStep.Start("Normalize " + supply.GasWhereName + " flow conditions");

		//	//this valve state must be set by the calling routine, as needed
		//	//v_MC_split.Close();
		//	v_IM_VM.Close();		// move to caller?
		//	v_VTT_VM.Close();      // move to caller?

		//	VacuumSystem.Isolate();
		//	supply.v_flow.Calibrate();
		//	supply.v_flow.Close();

		//	supply.v_source.Open();
		//	v_GM_split.Open();      // list of valves in path to VacuumSystem
		//	v_split_VM.Open();
		//	v_split_VM.WaitForIdle();

		//	VacuumSystem.Rough();
		//	while 
		//	(
		//		VacuumSystem.State != VacuumSystem.States.Roughing && 
		//		VacuumSystem.State != VacuumSystem.States.Isolated
		//	)
		//		wait();

		//	ProcessSubStep.Start("Wait up to 2 seconds for Foreline to sense gas");
		//	while (VacuumSystem.pForeline < supply.PurgePressure &&
		//			ProcessSubStep.Elapsed.TotalMilliseconds < 2000)
		//		wait();
		//	ProcessSubStep.End();

		//	ProcessSubStep.Start("Drain flow-supply volume");
		//	while (VacuumSystem.pForeline > supply.PurgePressure &&
		//			ProcessSubStep.Elapsed.TotalMilliseconds < 1000 * seconds_flow_supply_purge)
		//		wait();
		//	if (VacuumSystem.pForeline > supply.PurgePressure)
		//		Alert("Sample Alert!", supply.v_flow.Name + " isn't closed");
		//	ProcessSubStep.End();

		//	v_split_VM.Close();			// just the last valve in the path list? or all of them?
		//	VacuumSystem.Isolate();

		//	ProcessStep.End();
		//}

		//protected void pressurize_GM(GasSupply supply, double targetValue)
		//{
		//	double ppr = supply.PosPerUnitRoC;   // initial estimate of valve position change to cause a unit-change in roc

		//	int maxMovement = 24;		// of the valve, in servo Positions
		//	double maxRate = 15.0;        // supply.Value units/sec
		//	double lowRate = 1.0;
		//	double coastSeconds = 15;   // when to "Wait for pressure" instead of managing flow
		//	double lowRateSeconds = 20; // time to settle into lowRate before coasting

		//	double coastToDo = lowRate * coastSeconds;
		//	double lowRateToDo = coastToDo + lowRate * lowRateSeconds;

		//	int rampDownCycles = 10;
		//	// rampStart is in supplyValue units:
		//	//		When toDo < rampStart, scale the target rate down from max to low; 
		//	//		Allow rampDownCycles cycles for ramp down. Effective rate should be average of max and low
		//	double rampStart = (maxRate+lowRate)/2 * rampDownCycles * supply.MillisecondsCycleTime/1000;

		//	var rateSpan = maxRate - lowRate;
		//	var rampLen = rampStart - lowRateToDo;

		//	bool gasIsCO2 = supply.Name.Contains("CO2");
		//	if (gasIsCO2)
		//		ProcessStep.Start($"Admit {targetValue:0} {supply.Units} into the MC");
		//	else
		//		ProcessStep.Start($"Pressurize GM to {targetValue:0} {supply.Units} with {supply.GasName}");

		//	supply.v_source.Open();
		//	supply.v_flow.DoAction(new ActuatorAction("StartFlow", supply.StartFlowPosition, false, false, 0, 0, false));
		//	supply.v_flow.WaitForIdle();
		//	// wait some time for flow rate to settle and a few readings after
		//	wait(supply.MillisecondsCycleTime);

		//	int priorPos = supply.v_flow.Position;
		//	double toDo = targetValue - supply.Value;
		//	double priorRoC = 0;
		//	double roc;
		//	int amountToMove = 0;
		//	ActuatorAction action = new ActuatorAction("Move", 0, false, false, 0, 0, true);

		//	Stopwatch loopTimer = new Stopwatch();
		//	while (toDo > coastToDo)
		//	{
		//		loopTimer.Restart();

		//		var rampFraction = (toDo - lowRateToDo) / rampLen;
		//		if (rampFraction > 1) rampFraction = 1;
		//		if (rampFraction < 0) rampFraction = 0;
		//		var rTarget = lowRate + rateSpan * rampFraction;

		//		roc = supply.Value.RoC;
		//		double drate = roc - priorRoC;
		//		double dpos = supply.v_flow.Position - priorPos;
		//		if (Math.Abs(drate) > 0.5 && Math.Abs(dpos) > 2)
		//		{
		//			var latestPpr = dpos / drate;
		//			if (latestPpr < 0)
		//				ppr = DigitalFilter.WeightedUpdate(latestPpr, ppr, 0.4);
		//		}
		//		amountToMove = (int) (ppr * (rTarget - roc));

		//		//ProcessSubStep.Start($"rTg = {rTarget:0.0}, roc: {roc:0.0}, ppr: {ppr:0.0}, dpos: {amountToMove}");

		//		if (amountToMove != 0)
		//		{
		//			if (amountToMove > maxMovement) amountToMove = maxMovement;
		//			else if (amountToMove < -maxMovement) amountToMove = -maxMovement;

		//			priorPos = supply.v_flow.Position;
		//			priorRoC = roc;

		//			action.Command = amountToMove;
		//			supply.v_flow.DoAction(action);
		//			supply.v_flow.WaitForIdle();
		//		}

		//		int tRemaining;
		//		while (toDo > coastToDo && (tRemaining = supply.MillisecondsCycleTime - (int)loopTimer.ElapsedMilliseconds) > 0)
		//		{
		//			wait(Math.Min(20, tRemaining));
		//			toDo = targetValue - supply.Value;
		//		}

		//		//ProcessSubStep.End();
		//	}

		//	//ProcessSubStep.Start($"Wait for {targetValue} {supply.Units}");

		//	while (supply.Value.IsRising && supply.Value + supply.Value.RoC * supply.SecondsSettlingTime < targetValue)
		//		wait();
		//	//ProcessSubStep.End();

		//	supply.v_source.Close();
		//	supply.v_flow.Close();

		//	ProcessSubStep.Start("Wait 20 seconds");
		//	wait(20000);
		//	ProcessSubStep.End();

		//	ProcessStep.End();
		//}

		//protected void clean_pressurize_GM(GasSupply supply, double pressure)
		//{
		//	normalize_GM_gas_flow(supply);
		//	pressurize_GM(supply, pressure);
		//}

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

			foreach (GraphiteReactor gr in GRs)
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
			v_d13C_GM.Close();
			foreach (GraphiteReactor gr in (GRs.Except(grs)))
				gr.GMValve.Close();
			isolate_sections();

			foreach (GraphiteReactor gr in grs)
			{
				gr.GMValve.Open();
				gr.Furnace.TurnOn(temperature_Fe_prep);
			}
			ActuatorController0.WaitForIdle();
			evacuate_GM(pressure_ok);
			ProcessStep.End();

			int targetTemp = temperature_Fe_prep - temperature_Fe_prep_max_error;
			ProcessStep.Start("Wait for GRs to reach " + targetTemp.ToString() + " °C.");
			while (anyUnderTemp(grs, targetTemp)) Wait();
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM(3);
			waitFor_VSPressure(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Admit H2 into GRs");
			v_GM_split.Close();
			v_GM_split.WaitForIdle();
            gs_H2_GM.Pressurize(pressure_Fe_prep_H2);
			ProcessStep.End();

			ProcessStep.Start("Reduce iron for " + min_string(minutes_Fe_prep));
			foreach (GraphiteReactor gr in grs)
				gr.GMValve.Close();
			evacuate_GM(pressure_ok);
			open_line();
			WaitRemaining(minutes_Fe_prep);
			ProcessStep.End();

			ProcessStep.Start("Evacuate GRs");
			v_d13C_GM.Close();
			close_all_GRs();
			isolate_sections();
			VacuumSystem.Isolate();
			foreach (GraphiteReactor gr in grs)
			{
				gr.Furnace.TurnOff();
				gr.GMValve.Open();
			}
			evacuate_GM(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM(3);
			ProcessStep.End();

			foreach (GraphiteReactor gr in grs)
				gr.PreparationComplete();

			open_line();
			Alert("Operator Needed", "Graphite reactor preparation complete");
		}

		protected void change_sulfur_Fe()
		{
			var grs = new List<GraphiteReactor>();

			foreach (GraphiteReactor gr in GRs)
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
			v_d13C_GM.Close();
			isolate_sections();

			foreach (GraphiteReactor gr in grs)
				gr.GMValve.Open();
			ActuatorController0.WaitForIdle();
			evacuate_GM(pressure_ok);
			ProcessStep.End();

			ProcessStep.Start("Flush GRs with He");
			He_flush_GM(3);
			ProcessStep.End();

			foreach (GraphiteReactor gr in grs)
				gr.PreparationComplete();

			open_line();
		}

		#endregion GR service

		#region Vacuum System

		protected void Evacuate() { VacuumSystem.Evacuate(); }

		protected void Evacuate(double pressure) { VacuumSystem.Evacuate(pressure); }

        protected virtual void evacuate_section(Section section, double pressure)
        {
            section.Evacuate(pressure);
        }

        protected void evacuate_IP()
        {
            isolate_sections();
            v_IP_IM.Open();
            evacuate_IM(pressure_ok);
        }
        protected void evacuate_IM() { evacuate_IM(-1); }
        protected void evacuate_IM(double pressure) { evacuate_section(IMSection, pressure); }
        protected void evacuate_VTT() { evacuate_VTT(-1); }
        protected void evacuate_VTT(double pressure) { evacuate_section(VttSection, pressure); }
        protected void evacuate_split() { evacuate_split(-1); }
        protected void evacuate_split(double pressure) { evacuate_section(SplitSection, pressure); }
        protected void evacuate_MC() { evacuate_MC(-1); }
        protected void evacuate_MC(double pressure) { evacuate_section(MCSection, pressure); }
        protected void evacuate_GM() { evacuate_GM(-1); }
        protected void evacuate_GM(double pressure) { evacuate_section(GMSection, pressure); }

        protected void evacuate_CuAg_split(double pressure)
		{
            Section.IsolateAll(VacuumSystem);

			ProcessSubStep.Start("Evacuate CuAg..split");
			v_GM_split.Close();
			v_IM_VM.Close();
			v_VTT_VM.Close();
			VacuumSystem.Isolate();

			v_CuAg_MC.Close();
			v_MC_MCU.Open();
			v_MC_MCL.Open();
			v_MC_split.Open();
			v_split_VM.Open();
			Evacuate(pressure_ok);

			v_MC_MCU.Close();
			v_MC_MCL.Close();
			VacuumSystem.Isolate();
			v_CuAg_MC.Open();
			Evacuate(pressure_ok);

			v_MC_MCU.Open();
			v_MC_MCL.Open();
			waitFor_VSPressure(pressure);
			ProcessSubStep.End();
		}

		protected void evacuate_VTT_MC()
		{
			VacuumSystem.Isolate();

			close_sections();

			v_IM_VTT.Close();
			v_MC_split.Close(); // This should be the only valve that isn't closed.

			v_VTT_flow.Open();
			v_VTT_CuAg.Open(); // This should be the only valve that isn't open.
			v_CuAg_MC.Open();
			v_MC_MCU.Open();
			v_MC_MCL.Open();

			v_VTT_VM.Open();

			Evacuate(pressure_clean);
		}

		protected void evacuate_VTT_CuAg()
		{
			//ProcessSubStep.Start("Rough and evacuate section");
			VacuumSystem.Isolate();

			close_sections();

			v_IM_VTT.Close();
			v_CuAg_MC.Close();

			v_VTT_CuAg.Open();
			v_VTT_VM.Open();

            VacuumSystem.Evacuate(pressure_clean);
			Evacuate(pressure_clean);
			//ProcessSubStep.End();
		}

		protected void evacuate_IM_VTT()
		{
			VacuumSystem.Isolate();
			close_sections();

			v_IP2_IM?.Close();
			v_O2_IM.Close();
			v_He_IM.Close();

			v_VTT_flow.Close();
			v_IM_VTT.Open();

			v_IM_VM.Open();

			Evacuate(pressure_ok);
		}

		protected void evacuate_MC_GM(double pressure)
		{
			ProcessStep.Start("Evacuate MC..GM");

			v_IM_VM.Close();
			v_VTT_VM.Close();
			v_CuAg_MC.Close();
			v_VP_d13C.Close();
			v_d13C_GM.Close();
			close_all_GRs();

			if (v_split_VM.isOpened && (v_MC_split.isClosed || v_GM_split.isClosed))
			{
				v_split_VM.Close();
				v_MC_split.Open();
				v_GM_split.Open();
				ActuatorController0.WaitForIdle();
			}

			if (v_split_VM.isClosed)
				VacuumSystem.Isolate();
			v_split_VM.Open();
			ActuatorController0.WaitForIdle();

			if (VacuumSystem.State == VacuumSystem.States.Isolated) Evacuate();
			waitFor_VSPressure(pressure);

			ProcessStep.End();
		}

		#endregion Vacuum System

		#region Joining and isolating sections

		protected virtual void open_line() { }

		protected virtual void isolate_sections()
		{
            Section.IsolateAll(VacuumSystem);
		}

		protected virtual void close_sections()
		{
			VacuumSystem.ManifoldValves.Close();
		}

		protected virtual bool section_is_open(Section section)
		{
			return section.IsOpened;
		}

		protected bool VP_should_be_closed()
		{
			return !(
				VP.State == LinePort.States.Loaded ||
				VP.State == LinePort.States.Prepared);
		}

		protected bool ready_GRs_are_opened()
		{
			foreach (GraphiteReactor gr in GRs)
				if (gr.isReady && !gr.GMValve.isOpened)
					return false;
			return true;
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
			ProcessStep.Start("Initialize Valves");
            Section.IsolateAll(VacuumSystem);
            v_CuAg_MC.Close();
			ProcessStep.End();

			ProcessStep.Start("Join && evacuate MC..VM");
			if (Sample.nAliquots > 1)
				v_MC_MCU.Open();
			if (Sample.nAliquots > 2)
				v_MC_MCL.Open();

            v_MC_split.Open();
			v_split_VM.Open();
			Evacuate(pressure_clean);

			if (Sample.nAliquots < 2)
				v_MC_MCU.Close();
			if (Sample.nAliquots < 3)
				v_MC_MCL.Close();
			ActuatorController0.WaitForIdle();

			waitFor_VSPressure(pressure_clean);
			zero_MC();
			ProcessStep.End();

            ProcessStep.Start("Admit CO2 into the MC");
            gs_CO2_MC.CleanPressurize(ugc_targetSize);
            ProcessStep.End();
        }

        protected virtual void admitSealedCO2() { admitSealedCO2IP(); }

		protected void admitSealedCO2IP()
		{
			ProcessStep.Start("Evacuate and flush breakseal at IP");
			v_IP2_IM?.Close();
			v_IM_VTT.Close();
			v_IM_VM.Close();
			v_IP_IM.Open();
			ActuatorController0.WaitForIdle();
			evacuate_IP();
			He_flush_IP();
			ProcessSubStep.Start("Wait for p_VM < " + pressure_clean.ToString("0.0e0") + " Torr");
			waitFor_VSPressure(pressure_clean);
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
			waitFor_VSPressure(pressure_clean);
			ProcessStep.End();
			Alert("Operator Needed", "Carbonate sample is evacuated");
		}

		protected void load_carbonate_sample()
		{
			ProcessStep.Start("Provide positive He pressure at IP needle");
			v_IP2_IM?.Close();
			v_IM_VTT.Close();
			v_IP_IM.Close();
			v_IM_VM.Close();
			v_He_IM.Open();
			ActuatorController0.WaitForIdle();
			while (m_p_IM < pressure_over_atm) Wait();
			v_IP_IM.Open();
			Wait(5000);
			//while (!m_p_IM.IsRising) wait();   // wait until p_IM clearly rising
			while (m_p_IM < pressure_over_atm) Wait();
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Remove previous sample or plug from IP needle");
			while (!m_p_IM.IsFalling && ProcessStep.Elapsed.TotalMilliseconds < 10000)
				Wait(); // wait up to 10 seconds for p_IM clearly falling
			ProcessStep.End();

			ProcessStep.Start("Wait for stable He flow at IP needle");
			while (!m_p_IM.IsStable) Wait();
			ProcessStep.End();

			PlaySound();
			ProcessStep.Start("Load next sample vial or plug at IP needle");
			while (m_p_IM.RoC < roc_pIM_plugged && ProcessStep.Elapsed.TotalMilliseconds < 20000) Wait();
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

			v_GM_split.Close();

			close_all_GRs();

			VacuumSystem.Isolate();
			VP.Contents = "";
			v_VP_d13C.Open();
			v_d13C_GM.Open();
			evacuate_GM(pressure_ok);
			He_flush_GM(3);

			VP.State = LinePort.States.Prepared;
			ProcessStep.End();
		}

		#endregion Sample loading and preparation

		#region Sample operation

		protected void edit_process_sequences()
		{
			ShowProcessSequenceEditor(this);
		}

		protected void enter_sample_data()
		{
			VerifySampleInfo(false);
		}

		int ready_GRs()
		{
			return GRs.Count(gr => gr.isReady);
		}

		public bool enough_GRs()
		{
			int needed = Sample.nAliquots;
			if (Sample.SulfurSuspected && !isSulfurTrap(next_sulfur_trap(Last_GR)))
				needed++;
			return ready_GRs() >= needed;
		}

        public override void RunProcess(string processToRun)
        {
            if (processToRun == "Run Sample")
                run_sample();
            else
                base.RunProcess(processToRun);
        }

        // if "Run Sample" is chosen, select the process first
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

			SampleLog.WriteLine("");
			SampleLog.Record(
				"Start Process:\t" + Sample.Process + "\r\n\t" +
				Sample.ID + "\t" + Sample.milligrams.ToString("0.0000") + "\tmg\r\n\t" +
				Sample.nAliquots.ToString() + (Sample.nAliquots == 1 ? "\taliquot" : "\taliquots"));

            base.RunProcess(Sample.Process);

            while (ProcessState != ProcessStates.Finished)
                Wait(1000);
			
			string msg = Sample.Process + $" process {(RunCompleted? "complete" : "aborted")}";
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
				while (h_CC_S.Temperature < closeEnough) Wait();
				ProcessStep.End();

				ProcessStep.Start("Combust at " + temperature.ToString() + " °C for " + min_string(minutes) + ".");
			}

			WaitRemaining(minutes);

			ProcessStep.End();
		}

		protected void VTT_bleed(double bleedPressure)
		{
			Valve v = v_VTT_flow;
			Meter p = m_p_VTT;
			var pRoC = p.RoC;

			int openMovement = -3;
			int closeMovement = 1;

			double secondsDelay = 0.75;
			int startingMovement = -0;

			ProcessSubStep.Start("Maintain VTT pressure near " + bleedPressure.ToString("0.00") + " Torr");

			ActuatorAction action = new ActuatorAction("Move", startingMovement, false, false, 0, 0, true);
			Stopwatch actionStopwatch = new Stopwatch();

			// disable ion gauge while low vacuum flow is expected
			var IGWasAuto = VacuumSystem.IonGaugeAuto;
			VacuumSystem.IonGaugeAuto = false;
			VacuumSystem.IGDisable();
			VacuumSystem.Evacuate();	// use low vacuum or high vacuum as needed

			// starting motion
			v.DoAction(action);
			v.WaitForIdle();
			actionStopwatch.Restart();

			bool reachedBleedPressure = false;
			while (!v.isOpened)
			{
				var secondsLeft = Math.Max(0, secondsDelay - actionStopwatch.ElapsedMilliseconds / 1000);
				var waited = (secondsLeft == 0);
				var anticipatedPressure = p + secondsLeft * pRoC.Value;
				var error = anticipatedPressure - bleedPressure;
				int amountToMove = 0;

				if (!reachedBleedPressure && p >= bleedPressure) reachedBleedPressure = true;

				if (anticipatedPressure > 1.05 * bleedPressure)		// 5% overshoot
				{
					if (waited || v.LastMotion == Valve.States.Opening)
					{
						amountToMove = closeMovement;				// close a bit
						if (openMovement < -1) openMovement++;
					}
				}
				else if (waited && error < 0)
				{
					if (v.LastMotion == Valve.States.Opening && reachedBleedPressure)
						openMovement += (int)(15 * error / bleedPressure);
					amountToMove = openMovement;
				}

				//ProcessSubStep.CurrentStep.Description = $"mv: {amountToMove} rch: {reachedBleedPressure} wt: {waited} er: {error:0.00}";
				if (amountToMove != 0)
				{
					action.Command = amountToMove;
					v.DoAction(action);
					v.WaitForIdle();
					actionStopwatch.Restart();
				}

				Wait();
			}
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
				turn_off_CC_furnaces();
			}

            ProcessSubStep.Start("Calibrate VTT flow valve");
            v_VTT_flow.Close();
            v_VTT_flow.Calibrate();
            v_VTT_flow.Open();
            ProcessSubStep.End();

            ProcessSubStep.Start("Wait for VTT temperature < " + temperature_VTT_cold.ToString() + " °C");

			if (VTT.State != VTT.States.Freeze && VTT.State != VTT.States.Raise)
				VTT.Freeze();
			while (VTT.Coldfinger.Temperature > temperature_FTC_frozen) Wait();
			if (VTT.State != VTT.States.Raise)
				VTT.Raise();
			while (VTT.Temperature > temperature_VTT_cold) Wait();

			ProcessSubStep.End();

			v_IM_VTT.Close();
			v_IM_VM.Close();

			// release the sample to IM for measurement
			startBleed();

			ProcessSubStep.Start("Release incondensables");
			evacuate_VTT();
			ActuatorController0.WaitForIdle();
			while (m_p_VTT.IsRising) Wait();
			ProcessSubStep.End();

            v_VTT_flow.Close();
            v_VTT_flow.WaitForIdle();

			v_IM_VTT.Open();
			VTT.Dirty = true;

			VTT_bleed(pressure_VTT_bleed_sample);

			ProcessSubStep.Start("Wait for remaining IM pressure to bleed down");
			while (m_p_IM > 5 || m_p_VTT.RoC < roc_pVTT_falling_very_slowly)
				Wait();
			ProcessSubStep.End();

			while (m_p_VTT.Value > pressure_VTT_near_end_of_bleed)
				Wait();

			finishBleed();

			foreach (Valve v in Sample.Source.PathToVTT.Valves)
			{
				ProcessSubStep.Start($"Waiting to close {v.Name}");
				Wait(5000);
				while (m_p_VTT.RoC < roc_pVTT_falling_very_slowly) Wait();	// Torr/sec
				v.Close();
				v.WaitForIdle();
				ProcessSubStep.End();
			}


			// TODO: why close the flow valve here?
			//ProcessSubStep.Start($"Waiting to close {v_VTT_flow.Name}");
			ProcessSubStep.Start($"Waiting to close {v_VTT_VM.Name}");
			while (m_p_VTT.RoC < roc_pVTT_falling_barely) Wait();  // Torr/sec
			//v_VTT_flow.Close();
			v_VTT_VM.Close();
			ProcessSubStep.End();

			ProcessStep.End();

            Sample.Source.LinePort.State = LinePort.States.Complete;
		}

		// release the sample to IM for measurement
		protected virtual void startBleed()
		{
			v_IP2_IM?.Close();
			if (IP.State == LinePort.States.Loaded)
				IP.State = LinePort.States.InProcess;
			v_IP_IM.Open();
		}

		protected virtual void finishBleed() { }

		#region Extract

        // TODO: this process needs a complete revision, using the new methodology
		protected virtual void pressurize_VTT_MC()
		{
			ProcessStep.Start("Zero MC and VTT pressure gauges");
			evacuate_VTT_MC();
			zero_VTT_MC();
			ProcessStep.End();

			ProcessStep.Start("Pressurize VTT..MC with He");
			v_VTT_flow.Close();

			evacuate_IM_VTT();
			v_IM_VTT.Close();
			v_VTT_CuAg.Close();
//			v_He_VTT.Open();
//			v_He_VTT.WaitForIdle();
			Wait(2000);
//			v_He_VTT.Close();

			for (int i = 0; i < 2; i++)
			{
				VacuumSystem.Isolate();
				v_IM_VTT.Open();
				v_IM_VTT.WaitForIdle();
				Wait(5000);
				v_IM_VTT.Close();

				evacuate_IM(pressure_ok);
				VacuumSystem.Isolate();
				v_VTT_VM.Open();
				v_VTT_VM.WaitForIdle();
				v_VTT_VM.Close();
			}
			v_MC_MCU.Close();
			v_MC_MCL.Close();
			v_MC_split.Close();

			v_VTT_CuAg.Open();

			v_VTT_flow.Open();

			// TODO: should these lines be omitted? / moved to caller?
			v_CuAg_MC.Open();
			v_CuAg_MC.WaitForIdle();
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

			targetTemp -= 1;			// continue at 1 deg under
			ProcessSubStep.Start("Wait for VTT to reach " + targetTemp.ToString("0") + " °C");
			while (VTT.Temperature < targetTemp) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait 15 seconds to ensure transfer is well underway");
			Wait(15000);
			ProcessSubStep.End();

			wait_VTT_MC_stable();		// assumes transfer has started

			SampleLog.Record("\tCO2 equilibrium temperature:\t" +
				CO2EqTable.Interpolate(m_p_MC).ToString("0") + "\t°C");

			v_CuAg_MC.Close();
			ProcessStep.End();
		}

		double ExtractionPressure()
		{
			// Depends on which chambers are connected
			// During extraction, VTT..MC should be joined.
			double volVTT_MC = mL_VTT + mL_CuAg + mL_MC;
			double currentVolume = mL_VTT;
			// if (v_VTT_VTT.isClosed)
			//		Flow between VTT and VTT is restricted (significantly, depending on v_VTT_flow),
			//			and pressure differential can exist across the valve
			if (v_VTT_CuAg.isOpened)
			{
				currentVolume += mL_CuAg;
				if (v_CuAg_MC.isOpened)
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
			pressurizedExtract(3);		// targets CO2
			v_VTT_CuAg.Close();
			VTT.Stop();
		}

		#endregion Extract

		// returns the next available graphite reactor
		GraphiteReactor next_GR(string this_one)
		{
			bool passed_this_one = false;
			GraphiteReactor found_one = null;
			foreach (GraphiteReactor gr in GRs)
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
			foreach (GraphiteReactor gr in GRs)
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
			string grName = Last_GR;
			for (int i = 0; i < Sample.nAliquots; ++i)
			{
				GraphiteReactor gr = next_GR(grName);
				if (gr != null) gr.GMValve.Open();
			}
			ActuatorController0.WaitForIdle();
		}

		protected void open_next_GRs_and_d13C()
		{
			// assumes low pressure or VacuumSystem.Isolated
			open_next_GRs();
			if (Sample.Take_d13C && VP.State == LinePort.States.Prepared)
			{
				v_VP_d13C.Open();
				v_d13C_GM.Open();
			}
			v_GM_split.Open();
			v_split_VM.Open();
			ActuatorController0.WaitForIdle();
		}

		protected void take_measurement(bool first)
		{
			ProcessStep.Start("Take measurement");
			stabilize_MC();

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
			ProcessStep.Start("Prepare to measure MC contents");
			ProcessSubStep.Start("Evacuate split..GM");
			v_CuAg_MC.Close();
			v_MC_split.Close();
			v_IM_VM.Close();
			v_VTT_VM.Close();
			VacuumSystem.Isolate();
			v_GM_split.Open();
			v_split_VM.Open();
			Evacuate(pressure_clean);
			ProcessSubStep.End();

			if (ftc_MC.State >= FTColdfinger.States.Freeze)
			{
				ProcessStep.Start("Release incondensables");

				raise_LN(ftc_MC);
				ProcessSubStep.Start("Wait for MC coldfinger < " + temperature_FTC_frozen.ToString() + " °C");
				while (ftc_MC.Temperature > temperature_FTC_frozen) Wait();
				ProcessSubStep.End();

				v_GM_split.Close();
				VacuumSystem.Isolate();
				if (Sample.nAliquots > 1)
				{
					v_MC_MCU.Open();
					if (Sample.nAliquots > 2) v_MC_MCL.Open();
				}
				v_MC_split.Open();
				v_split_VM.Open();
				Evacuate(pressure_clean);

				zero_MC();

				if (Sample.nAliquots < 3)
				{
					v_MC_MCL.Close();
					if (Sample.nAliquots < 2) v_MC_MCU.Close();
					ActuatorController0.WaitForIdle();
					Wait(5000);
				}
				v_MC_split.Close();
				ActuatorController0.WaitForIdle();
				ProcessStep.End();
			}

			v_MC_split.Close();
			VacuumSystem.Isolate();
			v_split_VM.Open();
			open_next_GRs_and_d13C();
			Evacuate();

			if (!ftc_MC.isThawed())
			{
				ProcessStep.Start("Bring MC to uniform temperature");
				ftc_MC.Thaw();
				while (!ftc_MC.isThawed())
					Wait();
				ProcessStep.End();
			}

			ProcessStep.End();

			ProcessStep.Start("Measure Sample");
			take_measurement(true);
			ProcessStep.End();

			// exits with split..VP joined and evacuating
		}

		protected void split()
		{
			ProcessStep.Start("Discard Excess sample");
			while (Sample.Aliquots[0].ugC > ugC_sample_max)
			{
				ProcessSubStep.Start("Evacuate split");
				evacuate_split(0);
				ProcessSubStep.End();

				ProcessSubStep.Start("Split sample");
				v_split_VM.Close();
				v_MC_split.Open();
				v_MC_split.WaitForIdle();
				Wait(5000);
				v_MC_split.Close();
				ProcessSubStep.End();

				ProcessSubStep.Start("Discard split");
				evacuate_split(0);
				ProcessSubStep.End();

				take_measurement(false);
			}
			v_GM_split.Open();
			ProcessStep.End();

			// exits with split..GM+nextGR joined and evacuating
			//   (GM..VP's and GRs' union or isolation is unchanged)
			// except for any ports connected to GM
		}

		protected void dilute()
		{
			if (Sample.ugC > mass_small_sample) return;

			double ugCdg_needed = (double)mass_diluted_sample - Sample.ugC;

			ProcessStep.Start("Dilute sample");

			Alert("Sample Alert!", "Small sample! (" +
				Sample.ugC.ToString("0.0") + " ugC) Diluting...");

			v_VTT_CuAg.Close();
			ftc_CuAg.Freeze();

			ftc_MC.Thaw();
			v_CuAg_MC.Open();

			ProcessSubStep.Start("Wait for MC coldfinger to thaw.");
			while (ftc_MC.Temperature < m_t_MC - 5) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Wait for sample to freeze in the CuAg coldfinger.");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 4 * 60000)
				Wait();
			Wait(30000);
			ProcessSubStep.End();

			ftc_CuAg.Raise();

			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			Wait(15000);
			v_CuAg_MC.Close();
			ActuatorController0.WaitForIdle();
			ProcessSubStep.End();

			ftc_CuAg.Thaw();

			// get the dilution gas into the MC
			admitDeadCO2(ugCdg_needed);

			// discard excess dilution gas
			VacuumSystem.Isolate();
			v_split_VM.Open();
			v_GM_split.Open();
			Evacuate();

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
				Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Add sample to dilution gas");
			v_CuAg_MC.Open();

			while (ProcessSubStep.Elapsed.TotalMilliseconds < 30000 ||
					(ftc_CuAg.Temperature < 0 || ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 2 * 60000)
				Wait();
			raise_LN(ftc_MC);
			v_CuAg_MC.Close();
			ftc_CuAg.Stop();
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
			ActuatorController0.WaitForIdle();
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

			GraphiteReactor gr = next_sulfur_trap(Last_GR);
			Last_GR = gr.Name;
			gr.Reserve("sulfur");
			gr.State = GraphiteReactor.States.InProcess;

			transfer_CO2_from_MC_to_GR(gr, false);
			trapSulfur(gr);
			transfer_CO2_from_GR_to_MC(gr, false);

			gr.Aliquot.ResidualMeasured = true;	// prevent graphitization retry
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

			// The GM pressure drifts a bit after the H2 is introduced, generally downward.
			// This value compensates for the consequent average error, which was about -4,
			// averaged over 14 samples in Feb-Mar 2018.
			// The compensation is bumped by a few more Torr to shift the variance in
			// target error toward the high side, as a slight excess of H2 is not 
			// deleterious, whereas a deficiency could be.
			double driftAndVarianceCompensation = 9;

			v_MC_split.Close();
            gs_H2_GM.CleanPressurize(aliquot.pH2Initial + driftAndVarianceCompensation);
            v_GM_split.Close();
			waitFor_LN_peak(gr.Coldfinger);

			double pH2initial = m_p_GM;
			gr.GMValve.Open();
			gr.GMValve.WaitForIdle();
			Wait(2000);
			gr.GMValve.Close();
			gr.GMValve.WaitForIdle();
			Wait(5000);
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

			v_GM_split.Close();
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

		/// <summary>
		///  exits with MC..GM joined and evacuating via split-VM
		/// </summary>
		protected void clean_CuAg()
		{
            // this code for USGS line, which is omitting CuAg trap
            //VacuumSystem.Isolate();
            //v_VTT_CuAg.Close();
            //v_CuAg_MC.Open();
            //v_GM_split.Open();
            //v_MC_split.Open();
            //v_split_VM.Open();
            //Evacuate(pressure_ok);  // evacuate MC..GM via split-VM
            //v_CuAg_MC.Close();
            //return;

            ProcessStep.Start("Start cleaning CuAg");
            close_all_GRs();
            v_d13C_GM.Close();
            v_MC_MCU.Close();
            v_MC_MCL.Close();
            v_MC_split.Close();


            if (m_p_GM < 50)
                gs_H2_GM.CleanPressurize(100);      // just enough to clean the CuAg

            v_split_VM.Close();
            v_VTT_CuAg.Close();
            v_GM_split.Open();
            v_MC_split.Open();
            v_CuAg_MC.Open();
            v_CuAg_MC.WaitForIdle();
            Wait(1000);
            v_CuAg_MC.Close();

            VacuumSystem.Isolate();

            v_split_VM.Open();
            Evacuate(pressure_ok);  // evacuate MC..GM via split-VM
            ProcessStep.End();
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
			v_VTT_VM.Close();
			close_all_GRs();
			v_MC_split.Close();
			v_d13C_GM.Open();
			v_GM_split.Open();
			v_split_VM.Open();
			v_split_VM.WaitForIdle();
			Wait(5000);
			VacuumSystem.WaitForPressure(pressure_clean);

			v_VP_d13C.Close();      // should already be closed
			v_VP_d13C.WaitForIdle();
			ProcessSubStep.End();

            gs_He_GM.CleanPressurize(pressure_VP_He_Initial);
			v_GM_split.Close();
			v_GM_split.WaitForIdle();
			waitFor_LN_peak(ftc_VP);

			double pHeInitial = m_p_GM;
			v_VP_d13C.Open();
			v_VP_d13C.WaitForIdle();
			Wait(5000);
			v_VP_d13C.Close();
			v_VP_d13C.WaitForIdle();
			ftc_VP.Thaw();

			Wait(5000);
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
				("Graphite " + Sample.Aliquots[0].Name + "\t") +
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
				clean_CuAg();		// exits with MC..GM joined and evacuating via split-VM
				add_d13C_He();	// exits with GM..d13C filled with He
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
			v_VTT_VM.Close();
			v_CuAg_MC.Close();
			v_MC_split.Close();
			v_VP_d13C.Close();
			v_d13C_GM.Close();
			close_all_GRs(gr);
			ActuatorController0.WaitForIdle();

			if (gr.GMValve.isClosed || take_d13C)
			{
				v_split_VM.Close();

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
				ActuatorController0.WaitForIdle();
			}

			if (v_GM_split.isClosed || v_split_VM.isClosed)
			{
				VacuumSystem.Isolate();
				v_GM_split.Open();
				v_split_VM.Open();
				v_split_VM.WaitForIdle();
			}
			Evacuate(pressure_clean);

			ProcessStep.End();

			ProcessStep.Start("Expand sample into GM");

			close_v_d13C_CF();
			v_VP_d13C.Close();

			if (take_d13C)
				gr.GMValve.Close();
			else
				v_d13C_GM.Close();
			
			v_split_VM.Close();

			if (v_MCx != null) v_MCx.Open();	// take it from from MCU or MCL
			v_MC_split.Open();				// expand sample into GM
			ActuatorController0.WaitForIdle();

			ProcessStep.End();

			if (take_d13C)
			{
				ProcessSubStep.Start("Take d13C");
				Wait(5000);
				v_d13C_GM.Close();
				v_VP_d13C.Open();
				VP.State = LinePort.States.InProcess;
				VP.Contents = Sample.Aliquots[0].Name;
				ftc_VP.Freeze();
				gr.GMValve.Open();
				ActuatorController0.WaitForIdle();
				ProcessSubStep.End();
			}

			ProcessStep.Start("Freeze to graphite reactor");
			freeze(ftc);

			ProcessSubStep.Start("Wait for CO2 to freeze into " + gr.Name);
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 3.5 * 60000)
				Wait();
			Wait(30000);
			raise_LN(ftc);
			Wait(15000);
			ProcessSubStep.End();


			ProcessSubStep.Start("Release incondensables");
			v_split_VM.Open();
			Wait(5000);
			waitFor_VSPressure(0);
			ProcessSubStep.End();

			gr.GMValve.Close();
			if (v_MCx != null) v_MCx.Close();
			ActuatorController0.WaitForIdle();

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
				Wait(60000);
				ProcessSubStep.End();

				ProcessSubStep.End();

				ProcessSubStep.Start("Evacuate incondensables.");
				v_MC_split.Close();
				VacuumSystem.Isolate();
				gr.GMValve.Open();
				Evacuate(pressure_clean);
				v_MC_split.Open();
				waitFor_VSPressure(pressure_clean);
				v_split_VM.Close();
				ActuatorController0.WaitForIdle();
				ProcessSubStep.End();
			}
			else
			{
				v_split_VM.Close();
				gr.GMValve.Open();
				v_MC_split.Open();
				ActuatorController0.WaitForIdle();
			}

			if (grCF.Temperature < ts_GM.Temperature - 5) grCF.Thaw();
			freeze(ftc_MC);

			ProcessSubStep.Start("Wait for sample to freeze in the MC.");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 1.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 2 * 60000)
				Wait();
			Wait(30000);

			raise_LN(ftc_MC);
			ProcessSubStep.Start("Wait 15 seconds with LN raised.");
			Wait(15000);
			ProcessSubStep.End();
			v_GM_split.Close();
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
			v_VTT_VM.Close();
			ftc_MC.Thaw();
			v_CuAg_MC.Open();

			ProcessSubStep.Start("Wait for VTT to reach temperature");
			VTT.Freeze();
			while (VTT.Coldfinger.Temperature >= VTT.Coldfinger.Target) Wait();
			VTT.Raise();
			while (VTT.Temperature > temperature_VTT_cold) Wait();
			ProcessSubStep.End();

			ProcessSubStep.Start("Make sure the CO2 has started evolving.");
			while (ftc_MC.Temperature < CO2EqTable.Interpolate(0.07)) Wait();
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
			waitFor_VSPressure(0);
			ProcessStep.End();

			ProcessStep.Start("Transfer CO2 from MC to IP");
			Alert("Operator Needed", "Put LN on inlet port.");
			MessageHandler.Send("Operator needed", "Almost ready for LN on inlet port.\r\n" +
				"Press Ok to continue, then raise LN onto inlet port tube");

			VacuumSystem.Isolate();
			v_MC_split.Open();

			ProcessSubStep.Start("Wait for CO2 to freeze in the IP");
			while (ProcessSubStep.Elapsed.TotalMilliseconds < 60000 ||
					(ugCinMC > 0.5 || ugCinMC.RoC < 0) &&
					ProcessSubStep.Elapsed.TotalMilliseconds < 4 * 60000)
				Wait();
			ProcessSubStep.End();

			Alert("Operator Needed", "Raise inlet port LN.");
			MessageHandler.Send("Operator needed", "Raise inlet port LN one inch.\r\n" +
				"Press Ok to continue.");

			ProcessSubStep.Start("Wait 30 seconds");
			Wait(30000);
			ProcessSubStep.End();

			v_IP_IM.Close();
			ProcessStep.End();
		}

		#endregion Transfer CO2 between sections

		#endregion Process Management

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

		enum cal_v0 { MC_GM, IM_GM, FC_GM };

        protected virtual void admit_cal_gas(GasSupply gasSupply)
		{
            ProcessSubStep.Start("Open line");
            open_line();
            ProcessSubStep.End();

            ProcessSubStep.Start("Admit calibration gas into calibration initial volume");
            gasSupply.CleanPressurize(pressure_calibration);
            ProcessSubStep.End();
		}


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
                if (v_CloseMeFirst != null)
                {
                    if (v_CloseMeFirst == v_HV)
                        VacuumSystem.Isolate();
                    else
                        v_CloseMeFirst.Close();
                }

				v_OpenMe.Open();
				ProcessSubStep.End();
			}

			ProcessSubStep.Start("Wait for pressure to stabilize");
			Wait(milliseconds_calibration);
			ProcessSubStep.End();

			ProcessSubStep.Start("Observe pressure when ugCRoC < 0.010");
			while (Math.Abs(ugCinMC.RoC) > 0.010) Wait();
			ProcessSubStep.End();

			return ugCinMC;
		}

        protected double MeasureVolume(List<Valve> valves = null, VacuumSystem vacuumSystem = null)
        {
            if (valves != null && valves.Any())
            {
                ProcessSubStep.Start("Expand gas via " + valves[0].Name);
                int n = valves.Count() - 1;
                for (int i = 1; i < n; i++)
                {
                    if (valves[i] == v_HV)
                        VacuumSystem.Isolate();
                    else if (vacuumSystem != null && valves[i] == Valve.Find(vacuumSystem.v_HighVacuumName))
                        vacuumSystem.Isolate();
                    else
                        valves[i].Close();
                }
                ProcessSubStep.End();
                valves[0].Open();
            }

            ProcessSubStep.Start($"Wait a minimum of {milliseconds_calibration} milliseconds");
            Wait(milliseconds_calibration);
            ProcessSubStep.End();

            ProcessSubStep.Start($"Wait for >= {5} seconds of ugCinMC stability");
            waitForMCStable(5);
            ProcessSubStep.End();

            return ugCinMC;
        }


        protected virtual void CalibrateVolumes(GasSupply InitialVolume, List<List<Valve>> expansions, int repeats = 5)
        {

            double[][] obs = daa(expansions.Count + 1, repeats);   // observations

            ProcessStep.Start("\r\nCalibrate volume");

            //need a smart way to know this
            //SampleLog.Record("IM..GM, IM..GM+VM");
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                admit_cal_gas(InitialVolume);

                // make sure the volume to be measured is evacuated
                ProcessSubStep.Start("Evacuate calibration volume");
                InitialVolume.VacuumSystem.Isolate();
                InitialVolume.PathToVacuum.Open();
                InitialVolume.VacuumSystem.Evacuate(pressure_ok);
                ProcessSubStep.End();

                int ob = 0;
                obs[ob][repeat] = MeasureVolume();
                foreach (var expansion in expansions)
                    obs[ob++][repeat] = MeasureVolume(expansion);

                int n = ob;
                ob = 0;
                var sb = new StringBuilder($"{obs[ob][repeat]:0.0}");
                for (int i = 1; i < n; i++)
                    sb.Append($"\t{obs[ob][repeat]:0.0}");
                SampleLog.Record(sb.ToString());
            }

            ProcessSubStep.Start("Evacuate calibration volume");
            InitialVolume.VacuumSystem.Isolate();
            InitialVolume.PathToVacuum.Open();
            InitialVolume.VacuumSystem.Evacuate();
            ProcessSubStep.End();

            // need to store volumes somewhere smart......
            double v0 = mL_IM + mL_VTT + mL_CuAg + mL_MC + mL_split + mL_GM;
            var prior = mL_VM;
            mL_VM = v1(v0, obs[0], obs[1]);
            SampleLog.Record($"mL_VM: {prior}=>{mL_VM}");

            ProcessStep.End();
        }





        /// <summary>
        /// Returns the volume ratio MCx / MC
        /// </summary>
        /// <param name="v_MCx">The MC-MCx valve</param>
        /// <param name="p_calibration">Inital pressure to admit into the MC</param>
        /// <param name="repeats">The number of times to repeat the test</param>
        /// <returns></returns>
        protected double measure_MC_MCx(Valve v_MCx, int repeats = 5)
		{
			double[][] obs = daa(2, repeats);

			string MCx = v_MCx.Name.Substring(5, 3);
			ProcessStep.Start($"Measure volume ratio MC:{MCx}");

            SampleLog.WriteLine();
			SampleLog.Record($"MC, MC+{MCx}:");
			for (int i = 0; i < repeats; i++)
			{
				admit_cal_gas(gs_He_MC);

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
		protected void calibrate_volume_MC() { calibrate_volume_MC(5); }
		protected void calibrate_volume_MC(int repeats = 5)
		{
            SampleLog.WriteLine();
            SampleLog.Record("Old mL_MC: " + mL_MC.ToString());
			mL_MC = mL_KV / measure_MC_MCx(v_MC_MCU, repeats);
			SampleLog.Record("New mL_MC: " + mL_MC.ToString());
            SampleLog.WriteLine();
        }

        protected void calibrate_all_volumes_from_MC() { calibrate_all_volumes_from_MC(5); }
		protected virtual void calibrate_all_volumes_from_MC(int repeats = 5) { }

		protected void calibrate_volumes_MCL_MCU(int repeats = 5)
		{
            var prior = mL_MCU;
            mL_MCU = mL_MC * measure_MC_MCx(v_MC_MCU, repeats);
            SampleLog.Record($"mL_MCU: {prior}=>{mL_MCU}");

            prior = mL_MCL;
			mL_MCL = mL_MC * measure_MC_MCx(v_MC_MCL, repeats);
			SampleLog.Record($"mL_MCL: {prior}=>{mL_MCL}");
		}

		protected void calibrate_volumes_split_GM(int repeats = 5)
		{
			double[][] obs = daa(3, repeats);   // observations

			ProcessStep.Start("Calibrate volumes split..GM");

            SampleLog.WriteLine();
            SampleLog.Record("MC, MC..split, MC..GM");
			for (int i = 0; i < repeats; i++)
			{
				admit_cal_gas(gs_He_MC);

				// make sure the volume to be measured is evacuated
				ProcessSubStep.Start("Evacuate split..GM");
				VacuumSystem.Isolate();
				v_MC_split.Close();
				v_GM_split.Open();
				v_split_VM.Open();
				Evacuate(pressure_ok);
				ProcessSubStep.End();

				v_split_VM.Close();
				v_split_VM.WaitForIdle();

				obs[0][i] = measure_volume();
				obs[1][i] = measure_volume(v_MC_split, v_GM_split);
				obs[2][i] = measure_volume(v_GM_split);

				SampleLog.Record(
					obs[0][i].ToString("0.0") + "\t" +
					obs[1][i].ToString("0.0") + "\t" +
					obs[2][i].ToString("0.0"));
			}
			open_line();

			double v0 = mL_MC;
			var prior = mL_split;
			mL_split = v1(v0, obs[0], obs[1]);
			SampleLog.Record($"mL_split: {prior}=>{mL_split}");

			v0 += mL_split;
			prior = mL_GM;
			mL_GM = v1(v0, obs[1], obs[2]);
			SampleLog.Record($"mL_GM: {prior}=>{mL_GM}");

			ProcessStep.End();
		}

		protected void calibrate_volumes_GR(int repeats = 5)
		{
			double[][] obs = daa(7, repeats);   // observations

			// all GRs must be available
			if (ready_GRs() < 6) return;

			ProcessStep.Start("Calibrate volumes GR1..GR6");

            SampleLog.WriteLine();
            SampleLog.Record("MC..GM, MC..GR1, MC..GR2, MC..GR3, MC..GR4, MC..GR5, MC..GR6");
			for (int i = 0; i < repeats; i++)
			{
				admit_cal_gas(gs_He_MC_GM);

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
			var prior = mL_GR1;
			mL_GR1 = v1(v0, obs[0], obs[1]);
			SampleLog.Record($"mL_GR1: {prior}=>{mL_GR1}");

			v0 += mL_GR1;
			prior = mL_GR2;
			mL_GR2 = v1(v0, obs[1], obs[2]);
			SampleLog.Record($"mL_GR2: {prior}=>{mL_GR2}");

			v0 += mL_GR2;
			prior = mL_GR3;
			mL_GR3 = v1(v0, obs[2], obs[3]);
			SampleLog.Record($"mL_GR3: {prior}=>{mL_GR3}");

			v0 += mL_GR3;
			prior = mL_GR4;
			mL_GR4 = v1(v0, obs[3], obs[4]);
			SampleLog.Record($"mL_GR4: {prior}=>{mL_GR4}");

			v0 += mL_GR4;
			prior = mL_GR5;
			mL_GR5 = v1(v0, obs[4], obs[5]);
			SampleLog.Record($"mL_GR5: {prior}=>{mL_GR5}");

			v0 += mL_GR5;
			prior = mL_GR6;
			mL_GR6 = v1(v0, obs[5], obs[6]);
			SampleLog.Record($"mL_GR6: {prior}=>{mL_GR6}");

			ProcessStep.End();
		}

		protected virtual void calibrate_volumes_d13C_VP(int repeats = 5) { }

		protected virtual void calibrate_volume_VM(int repeats = 5) { }

		#endregion Chamber volume calibration routines

		#region Test functions

		protected void admit_IP_O2_300()
		{
			v_IP_IM.Close();
			isolate_sections();

			VacuumSystem.Isolate();

			for (int i = 0; i < 3; i++)
			{
				v_O2_IM.Open();
				ActuatorController0.WaitForIdle();
				Wait(1000);
				v_O2_IM.Close();
				ActuatorController0.WaitForIdle();

				v_IM_VM.Open();
				ActuatorController0.WaitForIdle();
				v_IM_VM.Close();
				ActuatorController0.WaitForIdle();
			}

			v_IP_IM.Open();
			ActuatorController0.WaitForIdle();
			Wait(2000);
			v_IP_IM.Close();
			ActuatorController0.WaitForIdle();
			Wait(5000);
		}

		protected void CO2_MC_IP_MC_loop()
		{
			freeze_VTT();
			transfer_CO2_from_MC_to_IP();
			admit_IP_O2_300();		// carrier gas
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

		/// <summary>
		/// Set the Sample ID to the desired number of loops
		/// Set the Sample mass to the desired starting quantity
		/// If there is at least 80% of the desired starting quantity
		/// already in the measurement chamber, that will be used
		/// instead of admitting fresh gas.
		/// </summary>
		protected void measure_CO2_extraction_yield()
		{
			SampleLog.WriteLine("\r\n");
			SampleLog.Record("CO2 Extraction yield test");
			SampleLog.Record($"Bleed target: {pressure_VTT_bleed_sample} Torr");

			ProcessStep.Start("Measure CO2 Extraction yield");
			if (ugCinMC < Sample.micrograms * 0.8)
			{
				VTT.Dirty = false;	// keep cold
				open_line();
				waitFor_VSPressure(pressure_clean);
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
			while (ftc_MC.Temperature <= VTT.RegulatedSetpoint + 10) Wait();
			ProcessSubStep.End();
			ftc_MC.Stop();	// stop thawing to save time

			// record pressure
			SampleLog.Record("\tPressure of pre-CO2 discarded gases:\t" +
				m_p_MC.Value.ToString("0.000") + "\tTorr");

			v_CuAg_MC.Close();
			discard_MC_gases();
			v_VTT_CuAg.Open();
			v_MC_split.Close();
			ProcessStep.End();
		}

		protected void step_extract()
		{
			pressurize_VTT_MC();
			// The equilibrium temperature of HCl at pressures from ~(1e-5..1e1)
			// is about 14 degC or more colder than CO2 at the same pressure.
			pressurizedExtract(-13);		// targets HCl
			discardExtractedGases();
			pressurizedExtract(1);		// targets CO2
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
