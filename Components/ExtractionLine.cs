using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Windows.Forms;
using static Utilities.Utility;

namespace HACS.Components
{
	/// <summary>
	/// A CO2-liberator for quartz. ExtractionLine is not a good name,
	/// but it's familiar. So, for now...
	/// </summary>
	public class ExtractionLine : ProcessManager, IExtractionLine
    {
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			//TODO
		}

		[HacsPostConnect]
        protected void PostConnect()
		{
            VacuumSystem.ProcessStep = ProcessStep;
            O2GasSupply.ProcessStep = ProcessStep;
            HeGasSupply.ProcessStep = ProcessStep;


            purgeFlowManager = new gasFlowManager()
            {
                GasSupply = HeGasSupply,
                Pressure = TubeFurnaceManometer,
                Reference = AmbientManometer
            };

            // TODO: delete this code after it appears in the settings file
            TubeFurnaceRateManager.MillisecondsTimeout = 35;
            TubeFurnaceRateManager.SecondsCycle = 0.75;
            TubeFurnaceRateManager.FlowValve = v_TF_flow;
            TubeFurnaceRateManager.Meter = TubeFurnaceManometer;
            TubeFurnaceRateManager.Lag = 60;
            TubeFurnaceRateManager.Deadband = 0.02;
            TubeFurnaceRateManager.DeadbandIsFractionOfTarget = true;
            TubeFurnaceRateManager.Gain = 15;
            TubeFurnaceRateManager.DivideGainByDeadband = false;
            TubeFurnaceRateManager.UseRateOfChange = true;

            // TODO: delete this code after it appears in the settings file
            TubeFurnacePressureManager.MillisecondsTimeout = 35;
            TubeFurnacePressureManager.SecondsCycle = 0.75;
            TubeFurnacePressureManager.FlowValve = v_TF_flow;
            TubeFurnacePressureManager.Meter = TubeFurnaceManometer;
            TubeFurnacePressureManager.Lag = 60;
            TubeFurnacePressureManager.Gain = -1;
            TubeFurnacePressureManager.DivideGainByDeadband = true;
            TubeFurnacePressureManager.UseRateOfChange = false;
		}

		#endregion HacsComponent

		[JsonProperty] public CEGS CEGS { get; set; }
		[JsonProperty] public TubeFurnace TubeFurnace { get; set; }
		[JsonProperty] public VacuumSystem VacuumSystem { get; set; }
		[JsonProperty] public IManometer TubeFurnaceManometer { get; set; }
		[JsonProperty] public MassFlowController MFC { get; set; }
		[JsonProperty] public GasSupply O2GasSupply { get; set; }
		[JsonProperty] public GasSupply HeGasSupply { get; set; }
		[JsonProperty] public IValve CegsValve { get; set; }
		[JsonProperty] public IValve v_TF_VM { get; set; }
		[JsonProperty] public IRS232Valve v_TF_flow { get; set; }
		[JsonProperty] public IValve v_TF_flow_shutoff { get; set; }
		[JsonProperty] public ISection TubeFurnaceSection { get; set; }
		[JsonProperty] public ILinePort TubeFurnacePort { get; set; }
		[JsonProperty] public IFlowManager TubeFurnaceRateManager { get; set; }
		[JsonProperty] public IFlowManager TubeFurnacePressureManager { get; set; }

		gasFlowManager purgeFlowManager = new gasFlowManager();

		public double OkPressure { get; set; }
		public double CleanPressure { get; set; }          // clean enough to start a new sample
		public IManometer AmbientManometer { get; set; }
		public HacsLog SampleLog { get; set; }

		#region process management

		public override void AbortRunningProcess()
        {
            if (purgeFlowManager.Busy)
                purgeFlowManager.Stop();
            if (TubeFurnaceRateManager.Busy)
                TubeFurnaceRateManager.Stop();
			if (TubeFurnacePressureManager.Busy)
				TubeFurnacePressureManager.Stop();
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
            ProcessDictionary.Add("Suspend IG", suspendVSManometer);
            ProcessDictionary.Add("Restore IG", restoreVSManometer);

            base.BuildProcessDictionary();
        }
		#endregion ProcessDictionary


		#region TubeFurnace processes

		void isolateTF()
        {
            VacuumSystem.IsolateManifold();
            // TODO replace this functionality: VacuumSystem.IsolateSections();
            O2GasSupply.ShutOff();
            HeGasSupply.ShutOff();
			CegsValve.Close();
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
            TubeFurnaceRateManager.Start(-1.5);     // 1.5 Torr / sec
            waitForPressureBelow(50);
            TubeFurnaceRateManager.Stop();

            v_TF_flow.Open();

            waitForPressureBelow(20);
            v_TF_VM.Open();
            v_TF_flow_shutoff.Close();
        }

        void evacuateTF()
        {
            TubeFurnaceSection.OpenAndEvacuate();
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
            TubeFurnaceSection.OpenAndEvacuate(pressure);
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
            while (TubeFurnaceManometer.Pressure > pressure)
                Wait();
            ProcessStep.End();
        }


        void waitForPressureAbove(double pressure)
        {
            ProcessStep.Start($"Wait for tube pressure > {pressure} Torr");
            while (TubeFurnaceManometer.Pressure < pressure)
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
			MFC.TurnOn(MFC.MaximumSetpoint);
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
            TubeFurnacePort.State = LinePort.States.InProcess;
            ProcessStep.Start("Prepare tube furnace for opening");

			pressurizeHe(AmbientManometer.Pressure + 20);
			purgeFlowManager.Start();

			Alert("Operator Needed", "Tube furnace ready to be opened");
			ProcessStep.CurrentStep.Description = "Tube furnace ready to be opened";
			MessageBox.Show("Purge flow is active\r\n",
					"Dismiss this window when furnace is closed again", MessageBoxButtons.OK);

			purgeFlowManager.Stop();

			ProcessStep.End();
            TubeFurnacePort.State = LinePort.States.Complete;
        }

        // Bake furnace tube
        void bakeout()
        {
            ProcessStep.Start($"{Name}: Bakeout tube furnace");
            isolateTF();

            if (MessageBox.Show("Has the prior sample been removed?!",
                    "Ok to Continue?", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            TubeFurnacePort.State = LinePort.States.InProcess;

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

                ProcessStep.Start($"Bake at {bakeTemperature} °C for {MinutesString(bakeMinutes)} min, cycle {i+1} of {bakeCycles}");
                while (ProcessStep.Elapsed.TotalMinutes < bakeMinutes)
                {
                    Wait(1000);
                }
                ProcessStep.End();
                evacuateTF(0.1);
            }

            TubeFurnace.TurnOff();
            evacuateTF(OkPressure);
            SampleLog.Record($"{Name}: Tube bakeout process complete");
            Alert("Sytem Status", $"{Name}: Tube bakeout process complete");
            ProcessStep.End();
            TubeFurnacePort.State = LinePort.States.Complete;
        }


        // Degas LiBO2, boat, and quartz sleeve on Day 1 Process
        void degas()
        {
            TubeFurnacePort.State = LinePort.States.InProcess;

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

            ProcessStep.Start($"Bleed O2 over sample for {MinutesString(bleedMinutes)}");
            MFC.TurnOn(5);
            O2GasSupply.Admit();

            VacuumSystem.Isolate();
            v_TF_flow_shutoff.Open();
            VacuumSystem.Evacuate();

            TubeFurnacePressureManager.Start(ptarget);

            while (ProcessStep.Elapsed.TotalMinutes < bleedMinutes)
            {
                Wait(1000);
            }

            ProcessStep.End();

            ProcessStep.Start($"Cool to below {t_LiBO2_frozen} °C");

            TubeFurnace.Setpoint = 100;
            WaitForTemperatureBelow(t_LiBO2_frozen);

            TubeFurnacePressureManager.Stop();

            ProcessStep.End();

            TubeFurnace.TurnOff();
            MFC.TurnOff();
            O2GasSupply.ShutOff();

            evacuateTF();

            Alert("Sytem Status", $"{Name}: Degas LiBO2, boat, and sleeve process complete");
            SampleLog.Record($"{Name}: Degas LiBO2, boat, and sleeve process complete");
            SampleLog.Record($"Degas O2 bleed volume\t{MFC.TrackedFlow}\tcc");

            TubeFurnacePort.State = LinePort.States.Prepared;
        }

        //void beginExtract() { beginExtract(50, 600, 60, 1100, 110); }
        void beginExtract() { beginExtract(50, 600, 10, 1100, 10); }

        void beginExtract(double targetPressure, int bleedTemperature, int bleedMinutes, int extractTemperature, int extractMinutes)
		{
            TubeFurnacePort.State = LinePort.States.InProcess;

            SampleLog.WriteLine();
            SampleLog.Record($"{Name}: Start Process: Sample extraction");

            pacedEvacuate();
            evacuateTF(OkPressure);
            isolateTF();
            MFC.ResetTrackedFlow();
            pressurizeO2(targetPressure);

            ProcessStep.Start("Low-T sample combustion");
            {
                TubeFurnace.TurnOn(bleedTemperature);
                waitForTemperatureAbove(bleedTemperature - 10);

                v_TF_flow.Close();
                v_TF_flow.Calibrate();

                ProcessStep.Start($"Bleed O2 over sample for {MinutesString(bleedMinutes)}");
                {
                    MFC.TurnOn(5);
                    O2GasSupply.Admit();

                    VacuumSystem.Isolate();
                    v_TF_flow_shutoff.Open();
                    VacuumSystem.Evacuate();

                    TubeFurnacePressureManager.Start(targetPressure);

                    while (ProcessStep.Elapsed.TotalMinutes < bleedMinutes)
                    {
                        Wait(1000);
                    }
                    TubeFurnacePressureManager.Stop();

                    MFC.TurnOff();
                    O2GasSupply.ShutOff();

                    evacuateTF(OkPressure);
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

            TubeFurnace.Setpoint = extractTemperature;

            ProcessStep.Start($"Combust sample at {extractTemperature} °C for {MinutesString(extractMinutes)}");
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

			ProcessStep.Start($"Continue bake for {MinutesString(bakeMinutes)} more");
			while (ProcessStep.Elapsed.TotalMinutes < bakeMinutes)
				Wait(1000);
			ProcessStep.End();

            MFC.TurnOn(5);
            O2GasSupply.Admit();

            VacuumSystem.Isolate();
            v_TF_flow.Close();
            v_TF_flow_shutoff.OpenWait(); //doesn't proceed until in state requested

        }

        bool vsManometerWasAuto;
        void suspendVSManometer()
        {
            vsManometerWasAuto = VacuumSystem.AutoManometer;
            VacuumSystem.AutoManometer = false;
            VacuumSystem.DisableManometer();
        }

        void restoreVSManometer()
        {
            VacuumSystem.AutoManometer = vsManometerWasAuto;
        }

        // Bleed O2 through tube furnace to CEGS
        void bleed()
        {
            int t_LiBO2_frozen = 800;
            int bleedMinutes = 10; //60;
            int targetPressure = 50;

            // disable ion gauge while low vacuum flow is expected
            suspendVSManometer();

            ProcessStep.Start($"Bleed O2 over sample for {MinutesString(bleedMinutes)} + cool down");
            {

                TubeFurnacePressureManager.Start(targetPressure);

                while (ProcessStep.Elapsed.TotalMinutes < bleedMinutes)
                {
                    Wait(1000);
                }

                TubeFurnace.Setpoint = 100;
                WaitForTemperatureBelow(t_LiBO2_frozen);

                TubeFurnacePressureManager.Stop();

                TubeFurnace.TurnOff();
                MFC.TurnOff();
                O2GasSupply.ShutOff();
                v_TF_VM.Open();
            }

            ProcessStep.End();

            restoreVSManometer();

            SampleLog.Record($"{Name}: Finish high-T sample combustion and bleed");
            SampleLog.Record($"{Name}: High T O2 bleed volume\t{MFC.TrackedFlow}\tcc");

            TubeFurnacePort.State = LinePort.States.Complete;

        }

        void remaining_P()
        {
            SampleLog.Record($"{Name}: Pressure remaining after bleed\t{TubeFurnaceManometer.Value}\ttorr");
        }


            #endregion TubeFurnace processes

            #endregion process manager



        protected class gasFlowManager
		{
			public GasSupply GasSupply { get; set; }
			public IManometer Pressure { get; set; }
			public IManometer Reference { get; set; }
			public double Overpressure { get; set; } = 20;

			double pressure_min => Reference.Value + Overpressure / 2;
			double pressure_max => Reference.Value + Overpressure;

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
					if (Pressure.Value < pressure_min && !GasSupply.SourceValve.IsOpened)
						GasSupply.SourceValve.Open();
					else if (Pressure.Value > pressure_max && GasSupply.SourceValve.IsOpened)
						GasSupply.SourceValve.Close();
					stopRequested = stopSignal.WaitOne(timeout);
				}
				GasSupply.SourceValve.Close();
			}
		}
    }
}