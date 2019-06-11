using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using System.Xml.Serialization;
using System.Text;
using HACS.Core;

namespace HACS.Components
{
	// derive from HVSPressure ?
	public class HighVacuumSystem : Component
	{
		public static new List<HighVacuumSystem> List;
		public static new HighVacuumSystem Find(string name)
		{ try { return List.Find(x => x.Name == name); } catch { return null; } }

		[XmlElement("HVSPressure")]
		public string HVSPressureName { get; set; }
		/// <summary>
		/// Returns the current furnace temperature (°C).
		/// </summary>
		[XmlIgnore] HVSPressure Pressure;

		[XmlElement("ForelinePressureMeter")]
		public string ForelinePressureMeterName { get; set; }
		[XmlIgnore] public Meter m_p_foreline;

		[XmlElement("v_HV")]
		public string v_HVName { get; set; }
		[XmlIgnore] Valve v_HV;

		[XmlElement("v_LV")]
		public string v_LVName { get; set; }
		[XmlIgnore] Valve v_LV;

		[XmlElement("v_B")]
		public string v_BName { get; set; }
		[XmlIgnore] Valve v_B;

		[XmlElement("v_R")]
		public string v_RName { get; set; }
		[XmlIgnore] Valve v_R;

		public bool IonGaugeAuto { get; set; }      // ion gauge operating mode


		public double pressure_baseline;
		public double pressure_backstreaming_safe;  // min roughing VM pressure
		public double pressure_backstreaming_limit; // min foreline pressure for LV open
		public double pressure_close_LV;
		public double pressure_open_HV;
		public double pressure_switch_LV_to_HV;
		public double pressure_switch_HV_to_LV;
		public double pressure_max_HV;

		[XmlIgnore] public double pressure_foreline_empty;
		[XmlIgnore] public double pressure_max_backing;
			pressure_foreline_empty = pressure_backstreaming_limit;
			pressure_max_backing = pressure_open_HV;


		#region Class Interface Values - Check the device state using these properties and methods
		//
		// These properties expose the physical device state to the class user.
		//

		public override string ToString()
		{
			return Name + ":\r\n" +
				Utility.IndentLines(
					Utility.byte_string(Report) + "\r\n" +
					"SP: " + DeviceState.Setpoint.ToString("0") +
					" PV: " + DeviceState.ProcessVariable.ToString("0") +
					" CO: " + DeviceState.WorkingOutput.ToString("0") +
					" RL: " + DeviceState.SetpointRateLimit.ToString("0") +
					" LA: " + (DeviceState.SetpointRateLimitActive ? "Y" : "N") +
					" (" + DeviceState.OperatingMode.ToString() + ")"
				);
		}

		#endregion

		#region Class Interface Methods -- Control the device using these functions
		//
		// These methods expose the functionality of the physical
		// device to the class user.
		//

		public void Isolate() { }
		public void Rough() { }				// ?
		public void RoughAndEvacuate() { }  // ?
		public void RoughOrEvacuateAsNeeded() { }		// simply "Evacuate?"; eliminate Rough(), and RoughAndEvacuate() ??

		#endregion



		#region main class

		public Stopwatch StateStopwatch { get; set; }

		public HighVacuumSystem()
		{
			StateStopwatch = new Stopwatch();
		}

		new public void Initialize()
		{
			base.Initialize();

			cmdQThread = new Thread(ProcessCommands)
			{
				IsBackground = true
			};
			cmdQThread.Start();

			stateThread = new Thread(ManageState)
			{
				IsBackground = true
			};
			stateThread.Start();

			StateStopwatch.Restart();

			Initialized = true;
		}

		#endregion



		#region State Manager
		//
		// These functions control the physical device
		// to achieve the desired "TargetState" which is
		// (indirectly) defined by the user of this 
		// class. The TargetState properties are managed 
		// by the Class Interface Methods.
		//

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

		void ManageState()
		{
			try
			{
				while (true)
				{
					int timeout = 300;				// TODO: consider moving this into settings.xml
					if (commandQ.Count == 0)
					{
						if (DeviceState.InstrumentMode == InstrumentModes.Unknown)
						{
							if (logComms) Log.Record("Checking InstrumentMode.");
							CheckParameter(Parameters.InstrumentMode);
						}
						else if (DeviceState.ControlType == ControlTypes.Unknown)
						{
							if (logComms) Log.Record("Checking ControlType.");
							CheckParameter(Parameters.ControlType);
						}
						else if (DeviceState.ControlType != ControlTypes.PID)
							SetControlType();   // the only reason IntrumentMode might not be "Normal"
						else if (DeviceState.InstrumentMode != InstrumentModes.Normal)
						{
							DeviceState.AlarmRelayActivated = true;
							SetInstrumentMode(InstrumentModes.Normal);
						}
						else if (DeviceState.OutputRateLimit != TargetState.OutputRateLimit)
							SetOutputRateLimit();
						else if (DeviceState.SetpointRateLimitUnits != SetpointRateLimitUnits.Minutes)
							SetSetpointRateLimitUnits();
						else if (DeviceState.SetpointRateLimit != TargetState.SetpointRateLimit)
							SetSetpointRateLimit();
						else if (DeviceState.Setpoint != TargetState.Setpoint)
							SetSetpoint();
						else if (DeviceState.OperatingMode != TargetState.OperatingMode)
							SetOperatingMode();
						else if (DeviceState.OperatingMode == AutoManual.Manual && DeviceState.ControlOutput != TargetState.ControlOutput)
							SetControlOutput();
						else
						{
							CheckStatus();
							//timeout += timeout;		// wait longer if idle and simply monitoring status (?)
						}
					}

					if (stateSignal.WaitOne(timeout) && logComms)
					{
						Log.Record("Signal received from " + WhoWasIt);
						WhoWasIt = "Unknown";
					}
					else if (logComms)
						Log.Record(timeout.ToString() + " ms timeout");
				}
			}
			catch (Exception e)
			{
				if (logComms)
					Log.Record("Exception in ManageState(): " + e.ToString());
				else
					MessageBox.Show(e.ToString());	// TODO: consider using throw(e), instead
			}
		}

		#endregion

		#region Roughing and evacuating

		protected void start_roughing()
		{
			waitForActuatorController();
			v_HV.Close();   // will normally be closed already
			v_LV.Close();   // will normally be closed already
			waitForValve(v_LV);

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
				waitForValve(v_LV);
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
				waitForValve(v_LV);
				ProcessSubStep.End();

				ProcessSubStep.Start("Open B when foreline pressure < " + pressure_max_backing.ToString("0.000") + " Torr");
				while (m_p_Foreline > pressure_max_backing) wait();
				v_B.Open();
				waitForValve(v_B);
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
			waitForValve(v_LV);

			while (m_p_Foreline > pressure_max_backing) wait();
			v_B.Open();
			v_HV.Open();
			waitForValve(v_HV);

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
		protected void evacuate_MC() { evacuate_MC(-1); }
		protected void evacuate_MC(double pressure) { evacuate_section(sections.MC, pressure); }
		protected void evacuate_GM() { evacuate_GM(-1); }
		protected void evacuate_GM(double pressure) { evacuate_section(sections.GM, pressure); }

		protected void evacuate_CuAg_split(double pressure)
		{
			ProcessSubStep.Start("Evacuate CuAg..split");
			v_GM_split.Close();
			v_IM_VM.Close();
			v_VTTR_VM.Close();
			v_HV.Close();
			waitForValve(v_HV);

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
			v_VP_d13C.Close();
			v_d13C_GM.Close();
			close_all_GRs();

			if (v_split_VM.isOpened && (v_MC_split.isClosed || v_GM_split.isClosed))
			{
				v_split_VM.Close();
				v_MC_split.Open();
				v_GM_split.Open();
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
















	}

	public class HighVacuumSystemState
	{
		public Action StateChanged;

		// Initialize backing variables to invalid values so ManageState() knows to check.
		public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
				_Setpoint = value;
				StateChanged?.Invoke();
			}
		}
		int _Setpoint = -1;

		public int SetpointRateLimit
		{
			get { return _SetpointRateLimit; }
			set
			{
				_SetpointRateLimit = value;
				StateChanged?.Invoke();
			}
		}
		int _SetpointRateLimit = -1;

		public int OutputRateLimit
		{
			get { return _OutputRateLimit; }
			set
			{
				_OutputRateLimit = value;
				StateChanged?.Invoke();
			}
		}
		int _OutputRateLimit = -1;

		public int ControlOutput
		{
			get { return _ControlOutput; }
			set
			{
				_ControlOutput = value;
				StateChanged?.Invoke();
			}
		}
		int _ControlOutput = -1;

		public EurothermFurnace.AutoManual OperatingMode
		{
			get { return _OperatingMode; }
			set
			{
				_OperatingMode = value;
				StateChanged?.Invoke();
			}
		}
	}
}


