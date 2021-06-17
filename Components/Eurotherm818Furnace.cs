using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Text;
using Utilities;

namespace HACS.Components
{
    public class Eurotherm818Furnace : TubeFurnace, IEurotherm818Furnace,
        Eurotherm818Furnace.IConfig, Eurotherm818Furnace.IDevice
	{
        #region Device constants

        public enum ParameterCode
        {
            Setpoint = 0,
            WorkingSetpoint = 1,
            Temperature = 2,
            OutputPower = 3,
            OutputPowerLimit = 4
        }
        public static string[] ParameterMnemonics = { "SL", "SP", "PV", "OP", "HO" };
        public static int ParameterCount = ParameterMnemonics.Length;

        #endregion Device constants

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : TubeFurnace.IDevice
        {
            int Error { get; set; }
            int OutputPowerLimit { get; set; }
            int WorkingSetpoint { get; set; }
            int OutputPower { get; set; }

        }
        public new interface IConfig : TubeFurnace.IConfig
        {
            int OutputPowerLimit { get;  }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        /// <summary>
        /// 
        /// </summary>
        public int OutputPowerLimit
        {
            get => outputPowerLimit;
            set
            {
                if (value <= 0) value = 0;
                else if (value > 100) value = 100;

                Ensure(ref TargetOutputPowerLimit, value, NotifyConfigChanged, nameof(TargetOutputPowerLimit));
            }
        }
        int TargetOutputPowerLimit;
        // Note: the control is actually turned on by setting the
        // OutputPowerLimit to 100, and off by setting it to 0
        int IConfig.OutputPowerLimit => IsOn ? 100 : 0;
        int IDevice.OutputPowerLimit
        {
            get => outputPowerLimit;
            set => Ensure(ref outputPowerLimit, value);
        }
        int outputPowerLimit = -1;

        /// <summary>
        /// 
        /// </summary>
        public int Error => error;
        int IDevice.Error
        {
            get => error;
            set => Ensure(ref error, value);
        }
        int error = -1;

        /// <summary>
        /// The furnace's internal working setpoint. This is
        /// not the same as the SetpointRamp.WorkingSetpoint.
        /// </summary>
        public int WorkingSetpoint => workingSetpoint;
        int IDevice.WorkingSetpoint
        {
            get => workingSetpoint;
            set => Ensure(ref workingSetpoint, value);
        }
        int workingSetpoint = -1;

        /// <summary>
        /// 
        /// </summary>
        public int OutputPower => outputPower;
        int IDevice.OutputPower
        {
            get => outputPower;
            set => Ensure(ref outputPower, value);
        }
        int outputPower = -1;

        [JsonProperty]
        public string InstrumentId
        {
            get => instrumentID;
            set => Ensure(ref instrumentID, value);
        }
        string instrumentID = "0000";


        public override string ToString()
		{
			return $"{Name}: {Temperature}, {IsOn.OnOff()}" +
				Utility.IndentLines(
                    $"\r\nSP: {Setpoint}" +
						$" WSP: {WorkingSetpoint}" +
						$" TFSL: {Device.Setpoint}" +
						$" TFSP: {Device.WorkingSetpoint}" +
						$" PV: {Temperature}" +
						$" OP: {OutputPower}" +
						$" HO: {OutputPowerLimit}" +
						$" RR: {SetpointRamp.Rate}"
                );
		}


        #endregion Class interface properties and methods

        #region Controller commands

        #region Controller read commands
        //
        // Commands to retrieve information from the controller
        //
        string CheckSetpoint() => GetParameter(ParameterCode.Setpoint);
        string CheckWorkingSetpoint() => GetParameter(ParameterCode.WorkingSetpoint);
        string CheckTemperature() => GetParameter(ParameterCode.Temperature);
        string CheckOutputPower() => GetParameter(ParameterCode.OutputPower);
        string CheckOutputPowerLimit() => GetParameter(ParameterCode.OutputPowerLimit);

        // used when idle
        int paramToGet = 0;
		string GetStatus()
		{
            var command = GetParameter((ParameterCode)paramToGet);
            ++paramToGet;
            if (paramToGet >= ParameterCount)
                paramToGet = 0;
            return command;
		}

        string GetParameter(ParameterCode param)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)ASCIICode.EOT);
            sb.Append(InstrumentId);
            sb.Append(ParameterMnemonics[(int)param]);
            sb.Append((char)ASCIICode.ENQ);

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record($"GetParameter {ParameterMnemonics[(int)param]}");
            }
            return sb.ToString();
        }

        #endregion Controller read commands

        #region Controller write commands
        //
        // These functions issue commands to change the physical device,
        // and check whether they worked.
        //

		string SetSetpoint()
		{
            return SetParameter(ParameterCode.Setpoint, RampingSetpoint.ToInt());
		}

        string SetOutputPowerLimit()
        {
            return SetParameter(ParameterCode.OutputPowerLimit, Config.OutputPowerLimit);
        }

        string SetParameter(ParameterCode param, int value)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();

            sb.Append((char)ASCIICode.EOT);
            sb.Append(InstrumentId);
            sb.Append((char)ASCIICode.STX);
            sb2.Append(ParameterMnemonics[(int)param]);
            sb2.Append($"{value:0.0}".PadLeft(6));
            sb2.Append((char)ASCIICode.ETX);
            sb.Append(sb2);
            sb.Append((char)bcc(sb2.ToString()));

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record($"SetParameter {ParameterMnemonics[(int)param]} to {value}");
            }
            return sb.ToString();
        }

        byte bcc(string s)
        {
            byte bcc = 0;
            for (int i = 0; i < s.Length; i++)
                bcc ^= (byte)s[i];
            return bcc;
        }


        #endregion Controller commands

        #endregion Controller commands


        #region Controller interactions

        // TODO: The timeout was previously set to this constant
        // value. Check that it is ok to use the normal 
        // IdleTimeout/ResponseTimeout values instead
        //protected override int StateLoopTimeout => 3 * SerialDevice.MillisecondsBetweenMessages;
        protected override SerialController.Command SelectService()
        {
            string command;
            bool hurry = true;


            if (Device.Temperature < -274)     // it's initialized to an impossible value
            {
                command = CheckTemperature();
            }
            else if (Device.OutputPowerLimit < 0)
            {
                command = CheckOutputPowerLimit();
            }
            else if (Device.Setpoint < -274)
            {
                command = CheckSetpoint();
            }
            else if (UseTimeLimit && MinutesOn >= TimeLimit)
            {
                TurnOff();
                command = SetOutputPowerLimit();
                UseTimeLimit = false;
            }
            else if (Config.OutputPowerLimit == 0 && Device.OutputPowerLimit != 0)
            {
                command = SetOutputPowerLimit();
            }
            else if (Device.Setpoint.ToInt() != RampingSetpoint.ToInt())
            {
                command = SetSetpoint();
            }
            else if (Device.OutputPowerLimit != Config.OutputPowerLimit)
            {
                command = SetOutputPowerLimit();
            }
            else
            {
                command = GetStatus();
                hurry = false;
            }

            return command.IsBlank() ? SerialController.DefaultCommand :
                new SerialController.Command(command, 1, hurry);
        }


        protected override bool ValidateResponse(string response, int which)
		{
			try
			{
                if (response.Length != 11)
                {
                    if (response[0] == (char)ASCIICode.ACK)
                    {
                        if (LogEverything) Log.Record("ACK received");
                        Device.Error = 0;
                        return true;
                    }
                    else
                    {
                        if (LogEverything) Log.Record("NAK or unrecognized response");
                        Device.Error = 1;
                        return false;
                    }
                }

                if (bcc(response.Substring(1, response.Length - 2)) != response[response.Length - 1])
                {
                    if (LogEverything) Log.Record("BCC mismatch");
                    Device.Error = 4;
                    return false;
                }

                string param = response.Substring(1, 2);
                if (!double.TryParse(response.Substring(3, 6), out double doubleValue))
                {
                    if (LogEverything) Log.Record($"Unrecognized parameter value: [{response.Substring(3, 6)}]");
                    Device.Error = 16;
                    return false;
                }
                int value = (int)doubleValue;

                if (LogEverything) Log.Record($"Param [{param}] = [{value}]");

                if (param == ParameterMnemonics[(int)ParameterCode.Setpoint])
                {
                    if (LogEverything) Log.Record($"Setpoint received");
                    Device.Setpoint = value;
                }
                else if (param == ParameterMnemonics[(int)ParameterCode.WorkingSetpoint])
                {
                    if (LogEverything) Log.Record($"Working Setpoint received");
                    Device.WorkingSetpoint = value;
                }
                else if (param == ParameterMnemonics[(int)ParameterCode.Temperature])
                {
                    if (LogEverything) Log.Record($"Temperature received");
                    Device.Temperature = value;
                }
                else if (param == ParameterMnemonics[(int)ParameterCode.OutputPower])
                {
                    if (LogEverything) Log.Record($"Output Power Level received");
                    Device.OutputPower = value;
                }
                else if (param == ParameterMnemonics[(int)ParameterCode.OutputPowerLimit])
                {
                    if (LogEverything) Log.Record($"Output Power Limit received");
                    Device.OutputPowerLimit = value;
                }
                else
                {
                    if (LogEverything) Log.Record($"Unrecognized parameter received");
                    Device.Error = 8;
                    return false;
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