using HACS.Core;
using System;
using System.Text;
using Utilities;

namespace HACS.Components
{
	public class EurothermFurnace : TubeFurnace, IEurothermFurnace,
		EurothermFurnace.IConfig, EurothermFurnace.IDevice
	{

		#region Device constants

		public enum FunctionCode { Read = 3, Write = 16 }
		public enum ErrorResponseCode { IllegalDataAddress = 2, IllegalDataValue = 03 }
		public enum ParameterCode
		{
			ProcessVariable = 1, TargetSetpoint = 2, ControlOutput = 3, WorkingOutput = 4,
			SetpointRateLimit = 35, OutputRateLimit = 37, SummaryStatus = 75,
			InstrumentMode = 199, AutoManual = 273,
			ControlType = 512, Resolution = 12550, PVMinimum = 134,
			SetpointRateLimitActiveStatus = 275, SetpointRateLimitUnits = 531
		}
		public enum AutoManualCode { Auto = 0, Manual = 1, Unknown = -1 }
		public enum InstrumentModeCode { Normal = 0, Configuration = 2, Unknown = -1 }

		// can only be altered when InstrumentMode == Configuration
		// attempting to set ControlType to Manual fails and generates an IllegalDataValue error
		public enum ControlTypeCode { PID = 0, Manual = 2, Unknown = -1 }

		public enum SetpointRateLimitUnitsCode { Seconds = 0, Minutes = 1, Hours = 2, Unknown = -1 }
		public enum ResolutionCode { Full = 0, Integer = 1, Unknown = -1 }

		public enum SummaryStatusBitsCode { AL1 = 1, SensorBroken = 32 }

		#endregion Device constants

		#region Class interface properties and methods

		#region Device interfaces

		public new interface IDevice : TubeFurnace.IDevice
		{
			int SetpointRateLimit { get; set; }
			SetpointRateLimitUnitsCode SetpointRateLimitUnits { get; set; }
			int OutputRateLimit { get; set; }
			int ControlOutput { get; set; }
			AutoManualCode OperatingMode { get; set; }

			int ProcessVariable { get; set; }
			int WorkingOutput { get; set; }
			bool AlarmRelayActivated { get; set; }
			ResolutionCode Resolution { get; set; }
			bool SetpointRateLimitActive { get; set; }
			int PVMinimum { get; set; }
			int ParameterValue { get; set; }
			InstrumentModeCode InstrumentMode { get; set; }
			ControlTypeCode ControlType { get; set; }

		}
		public new interface IConfig : TubeFurnace.IConfig
		{
			int SetpointRateLimit { get; }
			SetpointRateLimitUnitsCode SetpointRateLimitUnits { get; }
			int OutputRateLimit { get; }
			int ControlOutput { get; }
			AutoManualCode OperatingMode { get; }
		}

		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		/// <summary>
		/// Gets or sets the furnace's Setpoint rate limit. Units are
		/// &quot;degrees / SetpointRateLimitUnits&quot;; 0 means no limit.
		/// The furnace thereafter ramps the setpoint
		/// to programmed levels at the given rate.
		/// </summary>
		public int SetpointRateLimit
		{
			get => setpointRateLimit;
			set
			{
				if (value < 0) value = 0;
				else if (value > 99) value = 99;

				Ensure(ref TargetSetpointRateLimit, value, NotifyConfigChanged, nameof(TargetSetpointRateLimit));
			}
		}
		int TargetSetpointRateLimit;
		int IConfig.SetpointRateLimit => TargetSetpointRateLimit;
		int IDevice.SetpointRateLimit
		{
			get => setpointRateLimit;
			set => Ensure(ref setpointRateLimit, value);
		}
		int setpointRateLimit = -1;

		/// <summary>
		/// 
		/// </summary>
		public SetpointRateLimitUnitsCode SetpointRateLimitUnits
		{
			get => setpointRateLimitUnits;
			set => Ensure(ref TargetSetpointRateLimitUnits, value, NotifyConfigChanged, nameof(TargetSetpointRateLimitUnits));
		}
		SetpointRateLimitUnitsCode TargetSetpointRateLimitUnits = SetpointRateLimitUnitsCode.Minutes;
		SetpointRateLimitUnitsCode IConfig.SetpointRateLimitUnits => TargetSetpointRateLimitUnits;
		SetpointRateLimitUnitsCode IDevice.SetpointRateLimitUnits
		{
			get => setpointRateLimitUnits;
			set => Ensure(ref setpointRateLimitUnits, value);
		}
		SetpointRateLimitUnitsCode setpointRateLimitUnits = SetpointRateLimitUnitsCode.Unknown;

		/// <summary>
		/// Gets or sets the ControlOutput rate limit. Units
		/// are &quot;percent / second&quot;; 0 means no limit.
		/// The furnace thereafter ramps the actual (working)
		/// control output to programmed levels at the given rate.
		/// </summary>
		public int OutputRateLimit
		{
			get => outputRateLimit;
			set
			{
				if (value < 0) value = 0;
				else if (value > 99) value = 99;

				Ensure(ref TargetOutputRateLimit, value, NotifyConfigChanged, nameof(TargetOutputRateLimit));
			}
		}
		int TargetOutputRateLimit;
		int IConfig.OutputRateLimit => TargetOutputRateLimit;
		int IDevice.OutputRateLimit
		{
			get => outputRateLimit;
			set => Ensure(ref outputRateLimit, value);
		}
		int outputRateLimit = -1;

		/// <summary>
		/// 
		/// </summary>
		public int ControlOutput
		{
			get => controlOutput;
			set
			{
				if (value < 0) value = 0;
				else if (value > 100) value = 100;

				Ensure(ref TargetControlOutput, value, NotifyConfigChanged, nameof(TargetControlOutput));
			}
		}
		int TargetControlOutput;
		// Note: the control is actually turned off by setting the
		// Operating Mode to Manual and the ControlOutput to 0
		int IConfig.ControlOutput => IsOn ? TargetControlOutput : 0;
		int IDevice.ControlOutput
		{
			get => controlOutput;
			set => Ensure(ref controlOutput, value);
		}
		int controlOutput = -1;

		AutoManualCode IConfig.OperatingMode => 
			Config.State.IsOn() ? AutoManualCode.Auto : AutoManualCode.Manual;


		/// <summary>
		/// 
		/// </summary>
		public AutoManualCode OperatingMode
		{
			get => operatingMode;
			protected set
			{
				// When the Eurotherm controller switches into Manual Mode,
				// the ControlOutput value is ignored by the controller until 
				// a new value is written into the parameter. Meanwhile, the 
				// actual power to the furnace, the WorkingOutput, freezes.
				// In effect, the controller behaves as if the ControlOutput 
				// had been set to WorkingOutput. The following code invalidates
				// the IDevice.ControlOutput whenever the operating mode 
				// changes to Manual, so the state manager will know to update 
				// the controller's ControlOutput parameter.
				if (value == AutoManualCode.Manual) Device.ControlOutput = -1;
				Ensure(ref operatingMode, value);
			}
		}
		AutoManualCode operatingMode = AutoManualCode.Unknown;
		AutoManualCode IDevice.OperatingMode
		{
			get => OperatingMode;
			set => OperatingMode = value;
		}

		int IDevice.ProcessVariable { get; set; } = -1;
		int IDevice.WorkingOutput { get; set; } = -1;
		bool IDevice.AlarmRelayActivated { get; set; }
		ResolutionCode IDevice.Resolution { get; set; } = ResolutionCode.Unknown;
		bool IDevice.SetpointRateLimitActive { get; set; }
		int IDevice.PVMinimum { get; set; } = -1;
		int IDevice.ParameterValue { get; set; } = -1;
		InstrumentModeCode IDevice.InstrumentMode { get; set; } = InstrumentModeCode.Unknown;
		ControlTypeCode IDevice.ControlType { get; set; } = ControlTypeCode.Unknown;

        #region IOnOff

        /// <summary>
        /// Sets the furnace temperature and turns it on.
        /// </summary>
        /// <param name="setpoint">Desired furnace temperature (°C)</param>
        public new void TurnOn(double setpoint)
		{
			if (Device.ControlType != ControlTypeCode.PID)
				throw new Exception(Name + ": ControlType must be PID for setpoint control.");
			base.TurnOn(setpoint);
		}

		#endregion IOnOff


		public int DeviceAddress
		{
			get => deviceAddress;
			set => Ensure(ref deviceAddress, value);
		}
		int deviceAddress;

		/// <summary>
		/// True if the furnace power contactor has been disengaged
		/// by this instance. The class user is responsible for resetting
		/// the contactor and updating this value accordingly.
		/// The furnace heating element cannot receive power with the 
		/// contactor disengaged.
		/// </summary>
		public bool ContactorDisengaged
		{
			// The class user is responsible for resetting the 
			// contactor and updating contactorDisengaged accordingly.
			// It would be better to detect whether the contactor is opened
			// by querying a some parameter.
			get => Device.AlarmRelayActivated;
			set
			{
				Device.AlarmRelayActivated = value;
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// Returns the current furnace power level (%).
		/// </summary>
		public int WorkingOutput => Device.WorkingOutput;


		public override string ToString()
		{
			return $"{Name}: {Temperature}, {IsOn.OnOff()}" +
				Utility.IndentLines(
					$"\r\nSP: {Device.Setpoint:0}" +
						$" PV: {Device.ProcessVariable:0}" +
						$" CO: {Device.WorkingOutput:0}" +
						$" RL: {Device.SetpointRateLimit:0}" +
						$" LA: {(Device.SetpointRateLimitActive ? "Y" : "N")}" +
						$" ({Device.OperatingMode})"
				);
		}


		#endregion Class interface properties and methods

		string ParamToString(object o)
		{
			try
			{
				int i = Convert.ToInt32(o);
				ParameterCode p = (ParameterCode)i;
				return p.ToString();
			}
			catch { return o.ToString(); }
		}

		#region Controller commands

		#region Controller read commands
		//
		// Commands to retrieve information from the controller
		//

		int check = 1, nchecks = 4;
		string CheckStatus()
		{
			string command = "";
			switch (check)
			{
				case 1:
					command = CheckParameter(ParameterCode.ProcessVariable);
					break;
				case 2:
					command = CheckParameter(ParameterCode.WorkingOutput);
					break;
				case 3:
					command = CheckParameter(ParameterCode.SetpointRateLimit);
					break;
				case 4:
					command = CheckParameter(ParameterCode.SetpointRateLimitActiveStatus);
					break;
				//case :
				//	CheckParameter(???);	// contactor
				//	break;
				default:
					break;
			}
			if (++check > nchecks) check = 1;
			return command;
		}

		string CheckParameter(int param) => 
			FrameRead(param, 1);
		string CheckParameter(ParameterCode param) => 
			CheckParameter((int)param);

		#endregion Controller read commands

		#region Controller write commands
		//
		// These functions issue commands to change the physical device,
		// and check whether they worked.

		/// <summary>
		/// Encodes a temperature by rounding it to the nearest integer.
		/// </summary>
		/// <param name="n"></param>
		/// <returns></returns>
		int EncodeTemperature(double n) => n.ToInt();

		//
		string SetInstrumentMode(InstrumentModeCode im)
		{
			var setCommand = SetParameter(ParameterCode.InstrumentMode, (int)im);
			ContactorDisengaged = true;
			return SerialController.ServiceCommand != setCommand ? setCommand : 
				CheckParameter(ParameterCode.InstrumentMode);
		}

		string SetControlType()
		{
			// setting control type requires InstrumentMode == Configuration
			if (Device.InstrumentMode != InstrumentModeCode.Configuration)
				return SetInstrumentMode(InstrumentModeCode.Configuration);

			// PID is the only permitted value
			var setCommand = SetParameter(ParameterCode.ControlType, (int)ControlTypeCode.PID);
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.ControlType);
		}

		string SetSetpoint()
		{
			var setCommand = SetParameter(ParameterCode.TargetSetpoint, EncodeTemperature(Config.Setpoint));
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.TargetSetpoint);
		}

		string SetSetpointRateLimitUnits()
		{
			var setCommand = SetParameter(ParameterCode.SetpointRateLimitUnits, (int)Config.SetpointRateLimitUnits);
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.SetpointRateLimitUnits);
		}

		string SetSetpointRateLimit()
		{
			var setCommand = SetParameter(ParameterCode.SetpointRateLimit, Config.SetpointRateLimit);
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.SetpointRateLimit);
		}

		string SetOperatingMode()
		{
			var setCommand = SetParameter(ParameterCode.AutoManual, (int)Config.OperatingMode);
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.AutoManual);
		}

		string SetControlOutput()
		{
			var setCommand = SetParameter(ParameterCode.ControlOutput, Config.ControlOutput);
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.ControlOutput);
		}

		string SetOutputRateLimit()
		{
			var setCommand = SetParameter(ParameterCode.OutputRateLimit, Config.OutputRateLimit);
			return SerialController.ServiceCommand != setCommand ? setCommand :
				CheckParameter(ParameterCode.OutputRateLimit);
		}

		/// <summary>
		/// This is a dangerous function.
		/// Don't use it unless you know exactly what you are doing.
		/// </summary>
		/// <param name="param"></param>
		/// <param name="value"></param>
		string SetParameter(int param, int value) =>
			FrameWrite(param, Utility.MSBLSB(value));
		string SetParameter(ParameterCode param, int value) =>
			SetParameter((int)param, value);

		#endregion Controller write commands

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
			sb.Append((char)FunctionCode.Read);
			sb.Append(Utility.MSBLSB(param));
			sb.Append(Utility.MSBLSB(words));
			return sb.ToString();
		}
		string FrameRead(ParameterCode param, ushort words)
		{ return FrameRead((int)param, words); }

		string FrameWrite(int param, char[] data)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append((char)DeviceAddress);
			sb.Append((char)FunctionCode.Write);
			sb.Append(Utility.MSBLSB(param));
			sb.Append(Utility.MSBLSB((data.Length + 1) / 2));
			sb.Append((char)(data.Length));
			sb.Append(data);
			return sb.ToString();
		}
		string FrameWrite(ParameterCode param, char[] data)
		{ return FrameWrite((int)param, data); }

		int ResponseExpected(string command) =>
			command.IsBlank() || command[1] == (char)FunctionCode.Write ? 0 : 1;

		#endregion Controller command generators

		#endregion Controller commands

		#region Controller interactions

		// Note: The ControlType cannot be altered unless the controller is 
		// in Configuration mode, but changing to Configuration mode 
		// releases the furnace power contactor, which requires human 
		// intervention to reset.
		protected override SerialController.Command SelectService()
		{
			string command;
			bool hurry = true;

			if (Device.InstrumentMode == InstrumentModeCode.Unknown)
			{
				if (LogEverything) Log.Record("Checking InstrumentMode.");
				command = CheckParameter(ParameterCode.InstrumentMode);
			}
			else if (Device.ControlType == ControlTypeCode.Unknown)
			{
				if (LogEverything) Log.Record("Checking ControlType.");
				command = CheckParameter(ParameterCode.ControlType);
			}
			else if (Device.ControlType != ControlTypeCode.PID)
			{
				command = SetControlType();   // the only reason IntrumentMode might not be "Normal"
			}
			else if (Device.InstrumentMode != InstrumentModeCode.Normal)
			{
				Device.AlarmRelayActivated = true;
				command = SetInstrumentMode(InstrumentModeCode.Normal);
			}
			else if (Device.OutputRateLimit != Config.OutputRateLimit)
			{
				command = SetOutputRateLimit();
			}
			else if (Device.SetpointRateLimitUnits != SetpointRateLimitUnitsCode.Minutes)
			{
				command = SetSetpointRateLimitUnits();
			}
			else if (Device.SetpointRateLimit != Config.SetpointRateLimit)
			{
				command = SetSetpointRateLimit();
			}
			else if (Device.Setpoint != Config.Setpoint)
			{
				command = SetSetpoint();
			}
			else if (Device.OperatingMode != Config.OperatingMode)
			{
				command = SetOperatingMode();
			}
			else if (Device.OperatingMode == AutoManualCode.Manual && Device.ControlOutput != Config.ControlOutput)
			{
				command = SetControlOutput();
			}
			else
			{
				command = CheckStatus();
				hurry = false;
			}

			int responsesExpected = ResponseExpected(command);

			return command.IsBlank() ? SerialController.DefaultCommand :
				new SerialController.Command(command, responsesExpected, hurry);
		}

		protected override bool ValidateResponse(string response, int which)
		{
			try
			{
				string command = SerialController.ServiceCommand;
				if (LogEverything) Log.Record("command: " + command.ToByteString());

				FunctionCode commandFunctionCode = (FunctionCode)command[1];
				FunctionCode reportFunctionCode = (FunctionCode)response[1];

				if (reportFunctionCode != commandFunctionCode)
				{
					if (reportFunctionCode == commandFunctionCode + 128)
					{
						ErrorResponseCode errorCode = (ErrorResponseCode)response[2];
						throw new Exception("Error Reponse: " +
							(
								(errorCode == ErrorResponseCode.IllegalDataAddress) ?
									"Illegal parameter: [" :
								(errorCode == ErrorResponseCode.IllegalDataValue) ?
								"Illegal parameter value: [" :
								"Unknown Error: ["
							)
							+ command.ToByteString() + "]");
					}
					else
						throw new Exception("Command/Report mismatch");
				}

				// The following information is not present in Eurotherm controller responses
				// to parameter queries, so it must be retrieved from the last issued command.
				short firstParam = (short)Utility.toInt(command, 2);

				if (reportFunctionCode == FunctionCode.Read)
				{
					int wordsRequested = Utility.toInt(command, 4);
					int bytesIn = (byte)response[2];
					int wordsIn = bytesIn / 2;
					if (wordsIn != wordsRequested)
						throw new Exception("read parameter count mismatcch");

					int[] values = new int[wordsIn];
					for (int i = 0; i < wordsIn; i++)
						values[i] = Utility.toInt(response, 3 + 2 * i);
					int firstValue = values[0];

					if (LogEverything)
						Log.Record(ParamToString(firstParam) + " param read: [" + firstParam.ToString() + "==" + firstValue.ToString() + "]");

					bool isOn = IsOn;

					if (firstParam == (int)ParameterCode.ProcessVariable)
						Device.ProcessVariable = firstValue;
					else if (firstParam == (int)ParameterCode.AutoManual)
						Device.OperatingMode = (AutoManualCode)firstValue;
					else if (firstParam == (int)ParameterCode.ControlOutput)
						Device.ControlOutput = firstValue;
					else if (firstParam == (int)ParameterCode.ControlType)
						Device.ControlType = (ControlTypeCode)firstValue;
					else if (firstParam == (int)ParameterCode.InstrumentMode)
						Device.InstrumentMode = (InstrumentModeCode)firstValue;
					else if (firstParam == (int)ParameterCode.TargetSetpoint)
						Device.Setpoint = firstValue;
					else if (firstParam == (int)ParameterCode.OutputRateLimit)
						Device.OutputRateLimit = firstValue;
					else if (firstParam == (int)ParameterCode.SetpointRateLimit)
						Device.SetpointRateLimit = firstValue;
					else if (firstParam == (int)ParameterCode.WorkingOutput)
						Device.WorkingOutput = firstValue;
					else if (firstParam == (int)ParameterCode.Resolution)
						Device.Resolution = (ResolutionCode)firstValue;
					else if (firstParam == (int)ParameterCode.PVMinimum)
						Device.PVMinimum = firstValue;
					else if (firstParam == (int)ParameterCode.SetpointRateLimitActiveStatus)
						Device.SetpointRateLimitActive = firstValue == 0 ? false : true;
					else if (firstParam == (int)ParameterCode.SetpointRateLimitUnits)
						Device.SetpointRateLimitUnits = (SetpointRateLimitUnitsCode)firstValue;
					else if (firstParam == (int)ParameterCode.SummaryStatus)
					{
						if ((firstValue & (int)SummaryStatusBitsCode.AL1) != 0 ||
							(firstValue & (int)SummaryStatusBitsCode.SensorBroken) != 0)
							Device.AlarmRelayActivated = true;
					}
					else
						Device.ParameterValue = firstValue;

					if (isOn != IsOn)
						StateStopwatch.Restart();

					if (UseTimeLimit && MinutesOn >= TimeLimit)
					{
						UseTimeLimit = false;
						TurnOff();
					}
				}
				else if (reportFunctionCode == FunctionCode.Write)
				{
					if (LogEverything)
					{
						int firstValue = Utility.toInt(command, 7);
						Log.Record(ParamToString(firstParam) + " param written: [" + firstParam.ToString() + "==" + firstValue.ToString() + "]");
					}
				}

				return true;
			}
			catch (Exception e)
			{
				if (LogEverything) Log.Record(e.ToString());
				return false;
			}
		}

		#endregion Controller interactions
	}
}
