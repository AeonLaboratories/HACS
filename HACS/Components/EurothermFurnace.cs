﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using System.Xml.Serialization;
using System.Text;
using HACS.Core;

namespace HACS.Components
{
	public class EurothermFurnace : Controller
	{
		public static new List<EurothermFurnace> List;
		public static new EurothermFurnace Find(string name)
		{ try { return List.Find(x => x.Name == name); } catch { return null; } }

		#region Physical device constants

		public enum FunctionCodes { Read = 3, Write = 16 }
		public enum ErrorResponseCodes { IllegalDataAddress = 2, IllegalDataValue = 03 }
		public enum Parameters
		{
			ProcessVariable = 1, TargetSetpoint = 2, ControlOutput = 3, WorkingOutput = 4,
			OutputRateLimit = 37, SummaryStatus = 75, InstrumentMode = 199, AutoManual = 273,
			ControlType = 512, Resolution = 12550, PVMinimum = 134,
			SetpointRateLimitActiveStatus = 275, SetpointRateLimitUnits = 531
		}
		public enum AutoManual { Auto = 0, Manual = 1, Unknown = -1 }
		public enum InstrumentModes { Normal = 0, Configuration = 2, Unknown = -1 }

		// can only be altered when InstrumentMode == Configuration
		// attempting to set ControlType to Manual fails and generates an IllegalDataValue error
		public enum ControlTypes { PID = 0, Manual = 2, Unknown = -1 }

		public enum SetpointRateLimitUnits { Seconds = 0, Minutes = 1, Hours = 2, Unknown = -1 }
		public enum Resolutions { Full = 0, Integer = 1, Unknown = -1 }

		public enum SummaryStatusBits { AL1 = 1, SensorBroken = 32 }

		#endregion

		#region Class Interface Values - Check the device state using these properties and methods
		//
		// These properties expose the state of the physical
		// device to the class user.
		//

		/// <summary>
		/// Returns the current furnace temperature (°C).
		/// </summary>
		[XmlIgnore] public double Temperature { get { return DeviceState.ProcessVariable; } }

		/// <summary>
		/// Returns the current furnace power level (%).
		/// </summary>
		[XmlIgnore] public int WorkingOutput { get { return DeviceState.WorkingOutput; } }

		[XmlIgnore]
		bool DeviceOn
		{
			get
			{
				return
					(DeviceState.OperatingMode == EurothermFurnace.AutoManual.Auto) ||
					 (DeviceState.OperatingMode == EurothermFurnace.AutoManual.Manual &&
						DeviceState.ControlOutput > 0);
			}
		}
		
		public bool UseTimeLimit { get; set; }
		
		public int TimeLimit { get; set; }

		/// <summary>
		/// True if the furnace is on, except during system startup, 
		/// when the returned value indicates whether the furnace 
		/// is supposed to be on, instead.
		/// </summary>
		public bool IsOn
		{
			get { return Initialized ? DeviceOn : _IsOn; }
			set { if (!Initialized) _IsOn = value; }
		}
		bool _IsOn;

		/// <summary>
		/// True if the furnace power contactor has been disengaged
		/// by this instance. The class user is responsible for resetting
		/// the contactor and updating this value accordingly.
		/// The furnace heating element cannot receive power with the 
		/// contactor disengaged.
		/// </summary>
		[XmlIgnore] public bool contactorDisengaged
		{
			// The class user is responsible for resetting the 
			// contactor and updating contactorDisengaged accordingly.
			// It would be better to detect whether the contactor is opened
			// by querying a some parameter.
			get { return DeviceState.AlarmRelayActivated; }
			set { DeviceState.AlarmRelayActivated = value; }
		}

		public override string ToString()
		{
			return Name + ":\r\n" +
				Utility.IndentLines(
					Utility.byte_string(Report) + "\r\n" +
					"SP: " + DeviceState.Setpoint.ToString("0") +
					" PV: " + DeviceState.ProcessVariable.ToString("0") +
					" CO: " + DeviceState.WorkingOutput.ToString("0") +
					" (" + DeviceState.OperatingMode.ToString() + ")"
				);
		}

		#endregion

		#region Class Interface Methods -- Control the device using these functions
		//
		// These methods expose the functionality of the physical
		// device to the class user.
		//

		/// <summary>
		/// Turns off the furnace.
		/// </summary>
		public void TurnOff()
		{ SetControlOutput(0); }

		/// <summary>
		/// Sets the furnace temperature and turns it on.
		/// If the furnace is on when the specified time elapses, it is turned off.
		/// </summary>
		/// <param name="setpoint">Desired furnace temperature (°C)</param>
		/// <param name="minutes">Maximum number of minutes to remain on</param>
		public void TurnOn(int setpoint, int minutes)
		{
			TimeLimit = minutes;
			UseTimeLimit = true;
			TurnOn(setpoint);
		}

		/// <summary>
		/// Sets the furnace temperature and turns it on.
		/// </summary>
		/// <param name="setpoint">Desired furnace temperature (°C)</param>
		public void TurnOn(int setpoint)
		{
			if (DeviceState.ControlType != ControlTypes.PID)
				throw new Exception(Name + ": ControlType must be PID for setpoint control.");
			SetSetpoint(setpoint);
			TurnOn();
		}

		/// <summary>
		/// Turns the furnace on.
		/// </summary>
		public void TurnOn()
		{ TargetState.OperatingMode = AutoManual.Auto; }

		/// <summary>
		/// Sets the desired furnace temperature.
		/// </summary>
		/// <param name="setpoint"></param>
		public void SetSetpoint(int setpoint)
		{ TargetState.Setpoint = setpoint; }

		/// <summary>
		/// Puts the furnace in manual mode and sets the power level to the given value.
		/// </summary>
		/// <param name="controlOutput">The desired furnace power level (0..100%)</param>
		public void SetControlOutput(int controlOutput)
		{
			TargetState.OperatingMode = AutoManual.Manual;
			TargetState.ControlOutput = controlOutput;
		}


		/// <summary>
		/// Sets the ControlOutput rate limit (%/second; 0 means no limit).
		/// The furnace thereafter ramps the actual control output 
		/// to programmed levels at the given rate.
		/// </summary>
		/// <param name="limit"></param>
		public void SetOutputRateLimit(int percentPerSecond)
		{
			TargetState.OutputRateLimit = percentPerSecond;
		}

		#endregion



		#region main class

		public int DeviceAddress { get; set; }

		[XmlIgnore] public Action StateChanged;
		
		[XmlIgnore] public bool logComms			// a debugging aid
		{
			get { return _logComms; }
			set
			{
				_logComms = value;
				if (_logComms && log == null)
					log = new LogFile(@"Controller " + Name + " Log.txt");
			}
		}
		bool _logComms = false;

		public Stopwatch StateStopwatch { get; set; }
		[XmlIgnore] public int MinutesOn { get { return IsOn ? MinutesInState : 0; } }
		[XmlIgnore] public int MinutesOff { get { return IsOn ? MinutesInState : 0; } }
		[XmlIgnore] public int MinutesInState { get { return (int)StateStopwatch.Elapsed.TotalMinutes; } }

		public EurothermFurnace() : base()
		{
			StateStopwatch = new Stopwatch();
		}

		public EurothermFurnace(string name, SerialPortSettings portSettings)
			: base(name, portSettings) { }

		new public void Initialize()
		{
			if (logComms) log.Record("Initializing...");

			base.Initialize();
			ResponseProcessor = ProcessResponse;

			cmdQThread = new Thread(ProcessCommands);
			cmdQThread.IsBackground = true;
			cmdQThread.Start();

			if (logComms) log.Record("Setting state StateChanged handlers.");

			TargetState.StateChanged = targetStateChanged;
			DeviceState.StateChanged = deviceStateChanged;

			if (logComms) log.Record("Starting stateThread");

			stateThread = new Thread(ManageState);
			stateThread.IsBackground = true;
			stateThread.Start();

			StateStopwatch.Restart();

			Initialized = true;
			if (logComms) log.Record("Initialization complete");
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
		ManualResetEvent stateSignal = new ManualResetEvent(false);

		public EurothermTargetState TargetState;
		[XmlIgnore] public EurothermDeviceState DeviceState = new EurothermDeviceState();

		// TODO: remove this debugging aid, and it's use everywhere
		string WhoWasIt = "Unknown";

		void targetStateChanged()
		{
			WhoWasIt = "targetState: " + TargetState.ItWasMe;
			stateSignal.Set();
		}

		void deviceStateChanged()
		{
			WhoWasIt = "deviceState: " + DeviceState.ItWasMe;
			stateSignal.Set();
		}

		// Note: The ControlType cannot be altered unless the controller is 
		// in Configuration mode, but changing to Configuration mode 
		// releases the furnace power contactor, which requires human 
		// intervention to reset.
		void ManageState()
		{
			try
			{
				while (true)
				{
					int timeout = 300;                  // TODO: consider moving this into settings.xml
					if (commandQ.Count == 0)
					{
						if (DeviceState.InstrumentMode == InstrumentModes.Unknown)
						{
							if (logComms) log.Record("Checking InstrumentMode.");
							CheckParameter(Parameters.InstrumentMode);
						}
						else if (DeviceState.ControlType == ControlTypes.Unknown)
						{
							if (logComms) log.Record("Checking ControlType.");
							CheckParameter(Parameters.ControlType);
						}
						else if (DeviceState.ControlType != ControlTypes.PID)
							SetControlType();	// the only reason IntrumentMode might not be "Normal"
						else if (DeviceState.InstrumentMode != InstrumentModes.Normal)
						{
							DeviceState.AlarmRelayActivated = true;
							SetInstrumentMode(InstrumentModes.Normal);
						}
						else if (DeviceState.OutputRateLimit != TargetState.OutputRateLimit)
							SetOutputRateLimit();
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

					stateSignal.Reset();
					if (stateSignal.WaitOne(timeout) && logComms)
					{
                        log.Record("Signal received from " + WhoWasIt);
                        WhoWasIt = "Unknown";
					}
					else if (logComms)
                        log.Record(timeout.ToString() + " ms timeout");
				}
			}
			catch (Exception e)
            {
                if (logComms)
                    log.Record("Exception in ManageState(): " + e.ToString());
                else
                    MessageBox.Show(e.ToString());      // TODO: consider using throw(e), instead
            }
		}

		#endregion

		#region Controller commands

		#region Controller read commands
		//
		// Commands to retrieve information from the controller
		//

		int check = 1, nchecks = 2;
		void CheckStatus()
		{
			switch (check)
			{
				case 1:
					CheckParameter(Parameters.ProcessVariable);
					break;
				case 2:
					CheckParameter(Parameters.WorkingOutput);
					break;
				//case :
				//	CheckParameter(???);	// contactor
				//	break;
				default:
					break;
			}
			if (++check > nchecks) check = 1;
		}

		public void CheckParameter(int param)
		{ enQCommand(FrameRead(param, 1)); }
		void CheckParameter(Parameters param)
		{ CheckParameter((int)param); }

		#endregion

		#region Controller write commands
		//
		// These functions issue commands to change the physical device,
		// and check whether they worked.
		//
		void SetInstrumentMode(InstrumentModes im)
		{
			SetParameter(Parameters.InstrumentMode, (int)im);
			contactorDisengaged = true;
			CheckParameter(Parameters.InstrumentMode);
		}

		void SetControlType()
		{
			// setting control type requires InstrumentMode == Configuration
			if (DeviceState.InstrumentMode != InstrumentModes.Configuration)
			{
				SetInstrumentMode(InstrumentModes.Configuration);
				return;
			}

			// PID is the only permitted value
			SetParameter(Parameters.ControlType, (int)ControlTypes.PID);
			CheckParameter(Parameters.ControlType);
		}

		void SetSetpoint()
		{
			SetParameter(Parameters.TargetSetpoint, TargetState.Setpoint);
			CheckParameter(Parameters.TargetSetpoint);
		}

		void SetOperatingMode()
		{
			SetParameter(Parameters.AutoManual, (int)TargetState.OperatingMode);
			CheckParameter(Parameters.AutoManual);
		}

		void SetControlOutput()
		{
			SetParameter(Parameters.ControlOutput, TargetState.ControlOutput);
			CheckParameter(Parameters.ControlOutput);
		}

		void SetOutputRateLimit()
		{
			SetParameter(Parameters.OutputRateLimit, TargetState.OutputRateLimit);
			CheckParameter(Parameters.OutputRateLimit);
		}

		/// <summary>
		/// This is a dangerous function.
		/// Don't use it unless you know exactly what you are doing.
		/// </summary>
		/// <param name="param"></param>
		/// <param name="value"></param>
		public void SetParameter(int param, int value)
		{ enQCommand(FrameWrite(param, intToCharArray(value))); }
		void SetParameter(Parameters param, int value)
		{ SetParameter((int)param, value); }

		#endregion

		#region Controller command generators
		//
		// These functions construct ModBus-format commands 
		// for communicating with a Eurotherm series 2000 controller.
		// The constructed commands do not include the CRC code; 
		// that part is automatically appended by the SerialDevice
		// class on transmission.
		//
		string FrameRead(int param, ushort words)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append((char)DeviceAddress);
			sb.Append((char)FunctionCodes.Read);
			sb.Append(intToCharArray(param));
			sb.Append(intToCharArray(words));
			return sb.ToString();
		}
		string FrameRead(Parameters param, ushort words)
		{ return FrameRead((int)param, words); }

		string FrameWrite(int param, char[] data)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append((char)DeviceAddress);
			sb.Append((char)FunctionCodes.Write);
			sb.Append(intToCharArray(param));
			sb.Append(intToCharArray((data.Length + 1) / 2));
			sb.Append((char)(data.Length));
			sb.Append(data);
			return sb.ToString();
		}
		string FrameWrite(Parameters param, char[] data)
		{ return FrameWrite((int)param, data); }

		#endregion

		#endregion

		#region Controller responses

		[XmlIgnore] public string Report
		{
			get { return _Report; }
			set
			{
				if (logComms) log.Record("in: " + Utility.byte_string(value));
				_Report = value;
				if (!InterpretReport() && logComms) log.Record("Couldn't interpret Report");
				StateChanged?.Invoke();
			}
		}
		string _Report = "";

		string paramToString(object o)
		{
			try
			{
				int i = Convert.ToInt32(o);
				Parameters p = (Parameters)i;
				return p.ToString();
			}
			catch { return o.ToString(); }
		}
		public bool InterpretReport()
		{
			try
			{
				string lc = latestCommand;
				//if (logComms) log.Record("lc: " + Utility.byte_string(lc));

				EurothermFurnace.FunctionCodes commandFunctionCode = (EurothermFurnace.FunctionCodes)lc[1];
				EurothermFurnace.FunctionCodes reportFunctionCode = (EurothermFurnace.FunctionCodes)Report[1];

				if (reportFunctionCode != commandFunctionCode)
				{
					if (reportFunctionCode == commandFunctionCode + 128)
					{
						ErrorResponseCodes errorCode = (ErrorResponseCodes)Report[2];
						throw new Exception("Error Reponse: " + 
							(
								(errorCode == ErrorResponseCodes.IllegalDataAddress) ? 
									"Illegal parameter: [" :
								(errorCode == ErrorResponseCodes.IllegalDataValue) ?
								"Illegal parameter value: [" :
								"Unknown Error: ["
							)	
							+ Utility.byte_string(lc) +  "]");
					}
					else
						throw new Exception("Command/Report mismatch");
				}

				// The following information is not present in Eurotherm controller responses
				// to parameter queries, so it must be assumed from the last issued command.
				short firstParam = Utility.getMSBFirstInt16(lc, 2);

				if (reportFunctionCode == FunctionCodes.Read)
				{
					int wordsRequested = Utility.getMSBFirstInt16(lc, 4);
					int bytesIn = (byte)Report[2];
					int wordsIn = bytesIn / 2;
					if (wordsIn != wordsRequested)
						throw new Exception("read parameter count mismatcch");

					int[] values = new int[wordsIn];
					for (int i = 0; i < wordsIn; i++)
						values[i] = Utility.getMSBFirstInt16(Report, 3 + 2 * i);
					int firstValue = values[0];

					if (logComms)
						log.Record(paramToString(firstParam)  + " param read: [" + firstParam.ToString() + "==" + firstValue.ToString() + "]");

					bool isOn = IsOn;

					if (firstParam == (int)Parameters.ProcessVariable)
						DeviceState.ProcessVariable = firstValue;
					else if (firstParam == (int)Parameters.AutoManual)
						DeviceState.OperatingMode = (AutoManual)firstValue;
					else if (firstParam == (int)Parameters.ControlOutput)
						DeviceState.ControlOutput = firstValue;
					else if (firstParam == (int)Parameters.ControlType)
						DeviceState.ControlType = (ControlTypes)firstValue;
					else if (firstParam == (int)Parameters.InstrumentMode)
						DeviceState.InstrumentMode = (InstrumentModes)firstValue;
					else if (firstParam == (int)Parameters.TargetSetpoint)
						DeviceState.Setpoint = firstValue;
					else if (firstParam == (int)Parameters.OutputRateLimit)
						DeviceState.OutputRateLimit = firstValue;
					else if (firstParam == (int)Parameters.WorkingOutput)
						DeviceState.WorkingOutput = firstValue;
					else if (firstParam == (int)Parameters.Resolution)
						DeviceState.Resolution = (Resolutions)firstValue;
					else if (firstParam == (int)Parameters.PVMinimum)
						DeviceState.PVMinimum = firstValue;
					else if (firstParam == (int)Parameters.SetpointRateLimitActiveStatus)
						DeviceState.SetpointRateLimitActive = firstValue == 0 ? false : true;
					else if (firstParam == (int)Parameters.SetpointRateLimitUnits)
						DeviceState.SetpointRateLimitUnits = (SetpointRateLimitUnits)firstValue;
					else if (firstParam == (int)Parameters.SummaryStatus)
					{
						if ((firstValue & (int)SummaryStatusBits.AL1) != 0 ||
							(firstValue & (int)SummaryStatusBits.SensorBroken) != 0)
							DeviceState.AlarmRelayActivated = true;
					}
					else
						DeviceState.ParameterValue = firstValue;

					if (isOn != IsOn)
						StateStopwatch.Restart();

					if (UseTimeLimit && MinutesOn >= TimeLimit)
					{
						UseTimeLimit = false;
						TurnOff();
					}
				}
				else if (reportFunctionCode == FunctionCodes.Write)
				{
					if (logComms)
					{
						int firstValue = Utility.getMSBFirstInt16(lc, 7);
						log.Record(paramToString(firstParam) + " param written: [" + firstParam.ToString() + "==" + firstValue.ToString() + "]");
					}
				}

				return true;
			}
			catch (Exception e)
			{
				if (logComms) log.Record(e.ToString());
				return false; 
			}
		}

		// This hack is required in order to interpret a report, because the 
		// Eurotherm controller's response to a parameter value query doesn't
		// indicate which parameters the returned data is for.
		[XmlIgnore] public string latestCommand			// public only for debugging purposes
		{ get { return SerialDevice == null ? "" : SerialDevice.LatestCommand; } }

		#endregion

		#region Communications management

		Queue<string> commandQ = new Queue<string>();
		Thread cmdQThread;
		ManualResetEvent commandSignal = new ManualResetEvent(false);
		ManualResetEvent responseSignal = new ManualResetEvent(false);

		void enQCommand(string cmd)
		{
			lock (commandQ) commandQ.Enqueue(cmd);
			commandSignal.Set();
		}

		void ProcessCommands()
		{
			string cmd;
			while (true)
			{
				if (commandQ.Count > 0)
				{
					lock (commandQ) cmd = commandQ.Dequeue();
					if (logComms) log.Record("out: " + Utility.byte_string(cmd));
					Command(cmd);
					responseSignal.Reset();
					responseSignal.WaitOne(500);
				}
				else
				{
					commandSignal.Reset();
					commandSignal.WaitOne(500);
				}
			}
		}

		void ProcessResponse(string s)
		{
			Report = s;
			responseSignal.Set();
		}

		#endregion


		// TODO: move these to Utilities? replace with BitConverter.GetBytes()?
		char LSB(int i) { return (char)(i & 0xFF); }
		char MSB(int i) { return (char)((i >> 8) & 0xFF); }
		char[] intToCharArray(int i)
		{
			char[] ca = new char[2];
			ca[0] = MSB(i);
			ca[1] = LSB(i);
			return ca;
		}
	}

	public class EurothermTargetState
	{
		// TODO: remove this debugging aid, and it's use everywhere
		[XmlIgnore] public string ItWasMe = "Unknown";

		[XmlIgnore] public Action StateChanged;
	
		public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
				if (value <= 0) _Setpoint = 1;
				else if (value > 1250) _Setpoint = 1200;
				else _Setpoint = value;
				ItWasMe = "Setpoint";
				StateChanged?.Invoke();
			}
		}
		int _Setpoint = -1;

		public int OutputRateLimit
		{
			get { return _OutputRateLimit; }
			set
			{
				if (value < 0) _OutputRateLimit = 0;
				else if (value > 99) _OutputRateLimit = 99;
				else _OutputRateLimit = value;
				ItWasMe = "OutputRateLimit";
				StateChanged?.Invoke();
			}
		}
		int _OutputRateLimit = -1;

		public int ControlOutput
		{
			get { return _ControlOutput; }
			set
			{
				if (value < 0) _ControlOutput = 0;
				else if (value > 100) _ControlOutput = 100;
				else _ControlOutput = value;
				ItWasMe = "ControlOutput";
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
				ItWasMe = "OperatingMode";
				StateChanged?.Invoke();
			}
		}
		EurothermFurnace.AutoManual _OperatingMode = EurothermFurnace.AutoManual.Unknown;
	}

	public class EurothermDeviceState
	{
		// TODO: remove this debugging aid, and it's use everywhere
		public string ItWasMe = "Unknown";

		public Action StateChanged;

		// Initialize backing variables to invalid values so ManageState() knows to check.
		public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
				_Setpoint = value;
				ItWasMe = "Setpoint";
				StateChanged?.Invoke();
			}
		}
		int _Setpoint = -1;

		public int OutputRateLimit
		{
			get { return _OutputRateLimit; }
			set
			{
				_OutputRateLimit = value;
				ItWasMe = "OutputRateLimit";
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
				ItWasMe = "ControlOutput";
				StateChanged?.Invoke();
			}
		}
		int _ControlOutput = -1;

		public EurothermFurnace.AutoManual OperatingMode
		{
			get { return _OperatingMode; }
			set
			{
				// When the Eurotherm controller switches into Manual Mode,
				// the ControlOutput value is ignored by the controller until 
				// a new value is written into the parameter. Meanwhile, the 
				// actual power to the furnace, the WorkingOutput, freezes.
				// In effect, the controller behaves as if the ControlOutput 
				// had been set to WorkingOutput. The following code invalidates
				// the deviceState's ControlOutput whenever the operating mode 
				// changes to Manual, so the state manager will know to update 
				// the controller's ControlOutput parameter.
				if (value == EurothermFurnace.AutoManual.Manual) _ControlOutput = -1;
				
				_OperatingMode = value;
				ItWasMe = "OperatingMode";
				StateChanged?.Invoke();
			}
		}
		EurothermFurnace.AutoManual _OperatingMode = EurothermFurnace.AutoManual.Unknown;

		public int ProcessVariable = -1;
		public int WorkingOutput = -1;
		public bool AlarmRelayActivated = false;
		public EurothermFurnace.Resolutions Resolution = EurothermFurnace.Resolutions.Unknown;
		public bool SetpointRateLimitActive = false;
		public EurothermFurnace.SetpointRateLimitUnits SetpointRateLimitUnits = EurothermFurnace.SetpointRateLimitUnits.Unknown;
		public int PVMinimum = -1;
		public int ParameterValue = -1;
		public EurothermFurnace.InstrumentModes InstrumentMode = EurothermFurnace.InstrumentModes.Unknown;
		public EurothermFurnace.ControlTypes ControlType = EurothermFurnace.ControlTypes.Unknown;

	}
}

