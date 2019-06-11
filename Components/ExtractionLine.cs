using System.Collections.Generic;
using System.Xml.Serialization;
using System.Windows.Forms;
using System;
using System.Threading;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	/// <summary>
	/// A CO2-liberator for quartz. ExtractionLine is not a good name,
	/// but it's familiar. So, for now...
	/// </summary>
	public class ExtractionLine : ProcessManager
    {
		#region Component Implementation

		public static readonly new List<ExtractionLine> List = new List<ExtractionLine>();
		public static new ExtractionLine Find(string name) { return List.Find(x => x?.Name == name); }

        protected void PostConnect()
		{
            VacuumSystem.ProcessStep = ProcessStep;
            O2GasSupply.ProcessStep = ProcessStep;
            HeGasSupply.ProcessStep = ProcessStep;


            purgeFlowManager = new gasFlowManager()
            {
                GasSupply = HeGasSupply,
                Pressure = p_TF,
                Reference = p_Ambient
            };

            // TODO: delete this code after it appears in the settings file
            TFRateManager.MillisecondsTimeout = 35;
            TFRateManager.SecondsCycle = 0.75;
            TFRateManager.FlowValveRef = v_TF_flowRef;
            TFRateManager.ValueRef = p_TFRef;
            TFRateManager.Lag = 60;
            TFRateManager.Deadband = 0.02;
            TFRateManager.DeadbandIsFractionOfTarget = true;
            TFRateManager.Gain = 15;
            TFRateManager.DivideGainByDeadband = false;
            TFRateManager.UseRoC = true;

            // TODO: delete this code after it appears in the settings file
            TFPressureManager.MillisecondsTimeout = 35;
            TFPressureManager.SecondsCycle = 0.75;
            TFPressureManager.FlowValveRef = v_TF_flowRef;
            TFPressureManager.ValueRef = p_TFRef;
            TFPressureManager.Lag = 60;
            TFPressureManager.Gain = -1;
            TFPressureManager.DivideGainByDeadband = true;
            TFPressureManager.UseRoC = false;
		}

        public ExtractionLine()
		{
			List.Add(this);
			OnPostConnect += PostConnect;
		}

		#endregion Component Implementation

		public HacsComponent<CEGS> CEGSRef { get; set; }
        public CEGS CEGS => CEGSRef?.Component;

        public HacsComponent<TubeFurnace> TubeFurnaceRef { get; set; }
        public TubeFurnace TubeFurnace => TubeFurnaceRef?.Component;

        public HacsComponent<VacuumSystem> VacuumSystemRef { get; set; }
        public VacuumSystem VacuumSystem => VacuumSystemRef?.Component;

        public HacsComponent<DynamicQuantity> p_TFRef { get; set; }
        public DynamicQuantity p_TF => p_TFRef?.Component;

        public HacsComponent<MassFlowController> MFCRef { get; set; }
        public MassFlowController MFC => MFCRef?.Component;

        public HacsComponent<GasSupply> O2GasSupplyRef { get; set; }
        public GasSupply O2GasSupply => O2GasSupplyRef?.Component;

        public HacsComponent<GasSupply> HeGasSupplyRef { get; set; }
        public GasSupply HeGasSupply => HeGasSupplyRef?.Component;

        public HacsComponent<HacsComponent> v_CEGSRef { get; set; }
        public IValve v_CEGS => v_CEGSRef?.Component as IValve;

        public HacsComponent<HacsComponent> v_TF_VMRef { get; set; }
		public IValve v_TF_VM => v_TF_VMRef?.Component as IValve;

        public HacsComponent<RS232Valve> v_TF_flowRef { get; set; }
		public RS232Valve v_TF_flow => v_TF_flowRef?.Component;

        public HacsComponent<HacsComponent> v_TF_flow_shutoffRef { get; set; }
        public IValve v_TF_flow_shutoff => v_TF_flow_shutoffRef?.Component as IValve;

        public HacsComponent<Section> TFSectionRef { get; set; }
        public Section TFSection => TFSectionRef?.Component;

        public HacsComponent<LinePort> TFPortRef { get; set; }
        public LinePort TFPort => TFPortRef?.Component;

        public HacsComponent<FlowManager> TFRateManagerRef { get; set; }
        public FlowManager TFRateManager => TFRateManagerRef?.Component;

        public HacsComponent<FlowManager> TFPressureManagerRef { get; set; }
        public FlowManager TFPressureManager => TFPressureManagerRef?.Component;


        [XmlIgnore] gasFlowManager purgeFlowManager = new gasFlowManager();


        [XmlIgnore] public Action<string, string> Alert;
		[XmlIgnore] public double pressure_ok { get; set; }
		[XmlIgnore] public double pressure_clean { get; set; }          // clean enough to start a new sample
		[XmlIgnore] public Meter p_Ambient { get; set; }
		[XmlIgnore] public HacsLog SampleLog { get; set; }

		#region process management

		public override void AbortRunningProcess()
        {
            if (purgeFlowManager.Busy)
                purgeFlowManager.Stop();
            if (TFRateManager.Busy)
                TFRateManager.Stop();
			if (TFPressureManager.Busy)
				TFPressureManager.Stop();
			base.AbortRunningProcess();
        }


        #region parameterized processes
        protected override void Combust(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
        {
            TubeFurnace.TurnOn(temperature);
            ProcessStep.Start($"Waiting for temperature to reach {temperature} °C");
            while (TubeFurnace.Temperature < temperature - 10)
                Wait(1000);
            ProcessStep.End();

        }
        #endregion parameterized processes

        #region ProcessDictionary
        protected override void BuildProcessDictionary()
        {
			ProcessDictionary.Add("Open and evacuate line", openLine);
			ProcessDictionary.Add("Isolate tube furnace", isolateTF);
            ProcessDictionary.Add("Pressurize tube furnace to 50 torr O2", pressurizeO2);
            ProcessDictionary.Add("Evacuate tube furnace", evacuateTF);
            ProcessDictionary.Add("Evacuate tube furnace over 10 minutes", pacedEvacuate);
            ProcessDictionary.Add("TurnOff tube furnace", turnOffTF);
			ProcessDictionary.Add("Prepare tube furnace for opening", prepareForOpening);
			ProcessDictionary.Add("Bake out tube furnace", bakeout);
			ProcessDictionary.Add("Degas LiBO2", degas);
			ProcessDictionary.Add("Begin extract", beginExtract);
			ProcessDictionary.Add("Finish extract", finishExtract);
            ProcessDictionary.Add("Bleed", bleed);
            ProcessDictionary.Add("Remaining P in TF", remaining_P);
            ProcessDictionary.Add("Suspend IG", suspendIG);
            ProcessDictionary.Add("Restore IG", restoreIG);

            base.BuildProcessDictionary();
        }
		#endregion ProcessDictionary


		#region TubeFurnace processes

		void isolateTF()
        {
            VacuumSystem.Isolate();
            VacuumSystem.IsolateSections();
            O2GasSupply.ShutOff();
            HeGasSupply.ShutOff();
			v_CEGS.Close();
        }

        void pacedEvacuate()
        {
            isolateTF();
            v_TF_flow.Close();
            v_TF_flow.Calibrate();
            v_TF_flow_shutoff.Open();
            VacuumSystem.Evacuate();

            // evacuate TF over ~10 minutes
            //TFFlowManager.Start(-p_TF / (10 * 60));     // negative to target a falling pressure
            TFRateManager.Start(-1.5);     // 1.5 Torr / sec
            waitForPressureBelow(50);
            TFRateManager.Stop();

            v_TF_flow.Open();

            waitForPressureBelow(20);
            v_TF_VM.Open();
            v_TF_flow_shutoff.Close();
        }

        void evacuateTF()
        {
            TFSection.OpenAndEvacuate();
        }

        void openLine()
        {
            isolateTF();
            evacuateTF();
        }
        
		void turnOffTF() { TubeFurnace.TurnOff(); }

        void evacuateTF(double pressure)
        {
            ProcessStep.Start($"Evacuate to {pressure:0.0e0} Torr");
            TFSection.OpenAndEvacuate(pressure);
            ProcessStep.End();
        }

//		void waitForPressure(double pressure)
//		{
//			while (p_TF < pressure)
//				Wait();
//		}


        void waitForPressureBelow(double pressure)
        {
            ProcessStep.Start($"Wait for tube pressure < {pressure} Torr");
            while (p_TF > pressure)
                Wait();
            ProcessStep.End();
        }


        void waitForPressureAbove(double pressure)
        {
            ProcessStep.Start($"Wait for tube pressure > {pressure} Torr");
            while (p_TF < pressure)
                Wait();
            ProcessStep.End();
        }

        void waitForTemperatureAbove(int temperature)
		{
			ProcessStep.Start($"Wait for tube temperature > {temperature} °C");
			while (TubeFurnace.Temperature < temperature)
				Wait();
			ProcessStep.End();
		}

		void WaitForTemperatureBelow(int temperature)
		{
			ProcessStep.Start($"Wait for tube temperature < {temperature} °C");
			while (TubeFurnace.Temperature > temperature)
				Wait();
			ProcessStep.End();
		}

        void pressurizeO2() => pressurizeO2(50);

        void pressurizeO2(double pressure)
        {
			ProcessStep.Start($"Pressurize tube to {pressure:0} Torr with {O2GasSupply.GasName}");
			MFC.TurnOn(MFC.SetpointMax);
			O2GasSupply.Admit(pressure);
			MFC.TurnOff();
			ProcessStep.End();
		}

		void pressurizeHe(double pressure)
		{
			ProcessStep.Start($"Pressurize tube to {pressure:0} Torr with {HeGasSupply.GasName}");
			HeGasSupply.Admit(pressure);
			ProcessStep.End();
		}

		void prepareForOpening()
		{
            TFPort.State = LinePort.States.InProcess;
            ProcessStep.Start("Prepare tube furnace for opening");

			pressurizeHe(p_Ambient + 20);
			purgeFlowManager.Start();

			Alert("Operator Needed", "Tube furnace ready to be opened");
			ProcessStep.CurrentStep.Description = "Tube furnace ready to be opened";
			MessageBox.Show("Purge flow is active\r\n",
					"Dismiss this window when furnace is closed again", MessageBoxButtons.OK);

			purgeFlowManager.Stop();

			ProcessStep.End();
            TFPort.State = LinePort.States.Complete;
        }

        // Bake furnace tube
        void bakeout()
        {
            ProcessStep.Start($"{Name}: Bakeout tube furnace");
            isolateTF();

            if (MessageBox.Show("Has the prior sample been removed?!",
                    "Ok to Continue?", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            TFPort.State = LinePort.States.InProcess;

            SampleLog.WriteLine();
            SampleLog.Record($"{Name}: Start Process: Bakeout tube furnace");

            double O2Pressure = 50;
            int bakeTemperature = 1200;
            int bakeMinutes = 60;
            int bakeCycles = 4;

            evacuateTF(0.01);
            pressurizeO2(O2Pressure);

            TubeFurnace.TurnOn(bakeTemperature);
            waitForTemperatureAbove(bakeTemperature-10);

            for (int i = 0; i < bakeCycles; ++i)
            {
                pressurizeO2(O2Pressure);

                ProcessStep.Start($"Bake at {bakeTemperature} °C for {min_string(bakeMinutes)} min, cycle {i+1} of {bakeCycles}");
                while (ProcessStep.Elapsed.TotalMinutes < bakeMinutes)
                {
                    Wait(1000);
                }
                ProcessStep.End();
                evacuateTF(0.1);
            }

            TubeFurnace.TurnOff();
            evacuateTF(pressure_ok);
            SampleLog.Record($"{Name}: Tube bakeout process complete");
            Alert("Sytem Status", $"{Name}: Tube bakeout process complete");
            ProcessStep.End();
            TFPort.State = LinePort.States.Complete;
        }


        // Degas LiBO2, boat, and quartz sleeve on Day 1 Process
        void degas()
        {
            TFPort.State = LinePort.States.InProcess;

            SampleLog.WriteLine();
            SampleLog.Record($"{Name}: Start Process: Degas LiBO2, boat, and sleeve");

            double ptarget = 50;
            int bleedTemperature = 1200;
            int bleedMinutes = 60;
            int t_LiBO2_frozen = 800;

            pacedEvacuate();
            evacuateTF(0.01);
            isolateTF();
            MFC.ResetTrackedFlow();
            pressurizeO2(ptarget);

            TubeFurnace.TurnOn(bleedTemperature);
            waitForTemperatureAbove(bleedTemperature - 10);

            v_TF_flow.Close();

            ProcessStep.Start($"Bleed O2 over sample for {min_string(bleedMinutes)}");
            MFC.TurnOn(5);
            O2GasSupply.Admit();

            VacuumSystem.Isolate();
            v_TF_flow_shutoff.Open();
            VacuumSystem.Evacuate();

            TFPressureManager.Start(ptarget);

            while (ProcessStep.Elapsed.TotalMinutes < bleedMinutes)
            {
                Wait(1000);
            }

            ProcessStep.End();

            ProcessStep.Start($"Cool to below {t_LiBO2_frozen} °C");

            TubeFurnace.SetSetpoint(100);
            WaitForTemperatureBelow(t_LiBO2_frozen);

            TFPressureManager.Stop();

            ProcessStep.End();

            TubeFurnace.TurnOff();
            MFC.TurnOff();
            O2GasSupply.ShutOff();

            evacuateTF();

            Alert("Sytem Status", $"{Name}: Degas LiBO2, boat, and sleeve process complete");
            SampleLog.Record($"{Name}: Degas LiBO2, boat, and sleeve process complete");
            SampleLog.Record($"Degas O2 bleed volume\t{MFC.TrackedFlow}\tcc");

            TFPort.State = LinePort.States.Prepared;
        }

        //void beginExtract() { beginExtract(50, 600, 60, 1100, 110); }
        void beginExtract() { beginExtract(50, 600, 10, 1100, 10); }

        void beginExtract(double targetPressure, int bleedTemperature, int bleedMinutes, int extractTemperature, int extractMinutes)
		{
            TFPort.State = LinePort.States.InProcess;

            SampleLog.WriteLine();
            SampleLog.Record($"{Name}: Start Process: Sample extraction");

            pacedEvacuate();
            evacuateTF(pressure_ok);
            isolateTF();
            MFC.ResetTrackedFlow();
            pressurizeO2(targetPressure);

            ProcessStep.Start("Low-T sample combustion");
            {
                TubeFurnace.TurnOn(bleedTemperature);
                waitForTemperatureAbove(bleedTemperature - 10);

                v_TF_flow.Close();
                v_TF_flow.Calibrate();

                ProcessStep.Start($"Bleed O2 over sample for {min_string(bleedMinutes)}");
                {
                    MFC.TurnOn(5);
                    O2GasSupply.Admit();

                    VacuumSystem.Isolate();
                    v_TF_flow_shutoff.Open();
                    VacuumSystem.Evacuate();

                    TFPressureManager.Start(targetPressure);

                    while (ProcessStep.Elapsed.TotalMinutes < bleedMinutes)
                    {
                        Wait(1000);
                    }
                    TFPressureManager.Stop();

                    MFC.TurnOff();
                    O2GasSupply.ShutOff();

                    evacuateTF(pressure_ok);
                }
                ProcessStep.End();

                ProcessStep.Start("Flush & evacuate TF");
                {
                    pressurizeO2(targetPressure);
                    evacuateTF(1e-3);
                }
                ProcessStep.End();
            }
            ProcessStep.End();

            SampleLog.Record($"{Name}: Finish low-T sample combustion");
            SampleLog.Record($"{Name}: Low-T O2 bleed volume\t{MFC.TrackedFlow}\tcc");

            isolateTF();
            MFC.ResetTrackedFlow();
            pressurizeO2(targetPressure);

            TubeFurnace.SetSetpoint(extractTemperature);

            ProcessStep.Start($"Combust sample at {extractTemperature} °C for {min_string(extractMinutes)}");
            {                    
                waitForTemperatureAbove(extractTemperature - 10);
                while (ProcessStep.Elapsed.TotalMinutes < extractMinutes)
                    Wait(1000);
            }
            ProcessStep.End();
        }

        void finishExtract()
		{
			int bakeMinutes = 10;

			ProcessStep.Start($"Continue bake for {min_string(bakeMinutes)} more");
			while (ProcessStep.Elapsed.TotalMinutes < bakeMinutes)
				Wait(1000);
			ProcessStep.End();

            MFC.TurnOn(5);
            O2GasSupply.Admit();

            VacuumSystem.Isolate();
            v_TF_flow.Close();
            v_TF_flow_shutoff.OpenWait(); //doesn't proceed until in state requested

        }

        bool IGWasAuto;

        void suspendIG()
        {
            IGWasAuto = VacuumSystem.IonGaugeAuto;
            VacuumSystem.IonGaugeAuto = false;
            VacuumSystem.IGDisable();
        }

        void restoreIG()
        {
            VacuumSystem.IonGaugeAuto = IGWasAuto;
        }

        // Bleed O2 through tube furnace to CEGS
        void bleed()
        {
            int t_LiBO2_frozen = 800;
            int bleedMinutes = 10; //60;
            int targetPressure = 50;

            // disable ion gauge while low vacuum flow is expected
            suspendIG();

            ProcessStep.Start($"Bleed O2 over sample for {min_string(bleedMinutes)} + cool down");
            {

                TFPressureManager.Start(targetPressure);

                while (ProcessStep.Elapsed.TotalMinutes < bleedMinutes)
                {
                    Wait(1000);
                }

                TubeFurnace.SetSetpoint(100);
                WaitForTemperatureBelow(t_LiBO2_frozen);

                TFPressureManager.Stop();

                TubeFurnace.TurnOff();
                MFC.TurnOff();
                O2GasSupply.ShutOff();
                v_TF_VM.Open();
            }

            ProcessStep.End();

            restoreIG();

            SampleLog.Record($"{Name}: Finish high-T sample combustion and bleed");
            SampleLog.Record($"{Name}: High T O2 bleed volume\t{MFC.TrackedFlow}\tcc");

            TFPort.State = LinePort.States.Complete;

        }

        void remaining_P()
        {
            SampleLog.Record($"{Name}: Pressure remaining after bleed\t{p_TF.Value}\ttorr");
        }


            #endregion TubeFurnace processes

            #endregion process manager



        protected class gasFlowManager
		{
			public GasSupply GasSupply { get; set; }
			public DynamicQuantity Pressure { get; set; }
			public DynamicQuantity Reference { get; set; }
			public double Overpressure { get; set; } = 20;

			double pressure_min => Reference + Overpressure / 2;
			double pressure_max => Reference + Overpressure;

			Thread managerThread;
			AutoResetEvent stopSignal = new AutoResetEvent(false);

            public bool Busy => managerThread != null && managerThread.IsAlive;

            public void Start()
			{
				if (managerThread != null && managerThread.IsAlive)
					return;

				managerThread = new Thread(manageFlow)
				{
					Name = $"{Pressure.Name} purgeFlowManager",
					IsBackground = true
				};
				managerThread.Start();
			}

			public void Stop() { stopSignal.Set(); }

			void manageFlow()
			{
				int timeout = 100;
				bool stopRequested = false;
				while (!stopRequested)
				{
					if (Pressure < pressure_min && !GasSupply.v_source.IsOpened)
						GasSupply.v_source.Open();
					else if (Pressure > pressure_max && GasSupply.v_source.IsOpened)
						GasSupply.v_source.Close();
					stopRequested = stopSignal.WaitOne(timeout);
				}
				GasSupply.v_source.Close();
			}
		}
    }
}