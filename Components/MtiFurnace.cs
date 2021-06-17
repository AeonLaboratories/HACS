using HACS.Core;
using System;
using Utilities;

namespace HACS.Components
{
    public class MtiFurnace : TubeFurnace, IMtiFurnace,
        MtiFurnace.IConfig, MtiFurnace.IDevice
    {
        #region Device constants

        public enum ParameterCode { Setpoint = 0x1A, PowerMode = 0x15 }
        public enum MessageTypeCode { Read = 0x52, Write = 0x43 }
        public enum PowerModeCode { Hold = 0x04, Stop = 0x0C };

        #endregion Device constants

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : TubeFurnace.IDevice { }

        public new interface IConfig : TubeFurnace.IConfig { }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        public byte InstrumentId
        {
            get => instrumentID;
            set => Ensure(ref instrumentID, value);
        }
        byte instrumentID = 1;

        public override string ToString()
		{
			return $"{Name}: {Temperature}, {Config.State.IsOn().OnOff()}" +
				Utility.IndentLines(
                    $"\r\nSP: {Config.Setpoint:0.0}" +
			            $" WSP: {RampingSetpoint:0.0}" +
			            $" TFSP: {Device.Setpoint:0.0}" +
			            $" PV: {Temperature:0.0}" +
			            $" On: {IsOn.YesNo()}" +
				        $" RR: {SetpointRamp.Rate:0.0}"
                );
		}

        #endregion Class interface properties and methods

        #region Controller commands

        #region Controller read commands
        //
        // Commands to retrieve information from the controller
        //
        ParameterCode Parameter = ParameterCode.Setpoint;     // default -- anything but PowerMode
        
		string GetStatus()
		{
            byte[] instruction = new byte[8];
            instruction[0] = (byte)(0x80 + InstrumentId);
            instruction[1] = instruction[0];
            instruction[2] = (byte)MessageTypeCode.Read;
            instruction[3] = (byte)ParameterCode.PowerMode;
            instruction[4] = 0;
            instruction[5] = 0;
            int ecc = (0x100 * instruction[3] + instruction[2] + InstrumentId) & 0xFFFF;
            instruction[6] = ecc.Byte0();
            instruction[7] = ecc.Byte1();

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record("GetStatus()");
            }

            Parameter = ParameterCode.PowerMode;
            return instruction.ToStringToo();
        }

        #endregion Controller read commands

        #region Controller write commands
        //
        // These functions issue commands to change the physical device,
        // and check whether they worked.
        //

        /// <summary>
        /// Encodes a temperature as an integer in tenths of
        /// a degree C.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        int EncodeTemperature(double n) => (n * 10).ToInt();

        /// <summary>
        /// The OnOffState that corresponds to the specified 
        /// PowerMode parameter response. Note: 8 == Stopped,
        /// lesser values indicate the furnace is on.
        /// </summary>
        /// <param name="value">PowerMode parameter response value</param>
        /// <returns></returns>
        OnOffState DecodePowerModeResponse(int value) =>
            value < 8 ? OnOffState.On : OnOffState.Off;

        /// <summary>
        /// The PowerModeCode that corresponds to an on/off condition.
        /// </summary>
        /// <param name="on"></param>
        /// <returns></returns>
        PowerModeCode EncodePowerMode(bool on) =>
            on ? PowerModeCode.Hold : PowerModeCode.Stop;



        // Setpoint is transmitted as integer in tenths of a degree C
        string SetSetpoint() => SetParameter(ParameterCode.Setpoint, 
            EncodeTemperature(RampingSetpoint));

        string SetPowerEnabled() => SetParameter(ParameterCode.PowerMode,
            (int)EncodePowerMode(Config.State.IsOn()));

        string SetParameter(ParameterCode param, int value)
        {
            byte[] instruction = new byte[8];
            instruction[0] = (byte)(0x80 + InstrumentId);
            instruction[1] = instruction[0];
            instruction[2] = (byte)MessageTypeCode.Write;
            instruction[3] = (byte)param;
            instruction[4] = value.Byte0();
            instruction[5] = value.Byte1();
            int ecc = (0x100 * instruction[3] + instruction[2] + value + InstrumentId) & 0xFFFF;
            instruction[6] = ecc.Byte0();
            instruction[7] = ecc.Byte1();

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record($"SetParameter {param:X} to {value}");
            }
            Parameter = param;
            return instruction.ToStringToo();
        }

        #endregion Controller write commands

        #endregion Controller commands

        #region Controller interactions

        protected override SerialController.Command SelectService()
        {
            string command;
            bool hurry = true;

            if (UpdatesReceived < 1 || Device.OnOffState.IsUnknown())
            {
                command = GetStatus();
            }
            else if (UseTimeLimit && MinutesOn >= TimeLimit)
            {
                TurnOff();
                command = SetPowerEnabled();
                UseTimeLimit = false;
            }
            else if (EncodeTemperature(Device.Setpoint) != EncodeTemperature(RampingSetpoint))
            {
                command = SetSetpoint();
            }
            else if (IsOn != Config.State.IsOn())
            {
                command = SetPowerEnabled();
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
				byte[] report = response.ToASCII8ByteArray();

                if (report.Length != 10)
                {
                    if (LogEverything) Log.Record("Unrecognized response");
                    return false;
                }

                int pv = 0x100 * report[1] + report[0];
                int sv = 0x100 * report[3] + report[2];
                byte  mv = report[4];
                byte alarm = report[5];
                int value = 0x100 * report[7] + report[6];
                int ecc = 0x100 * report[9] + report[8];

                int eccCheck = (pv + sv + 0x100 * alarm + mv + value + InstrumentId) & 0xFFFF;

                if (ecc != eccCheck)
                {
                    if (LogEverything)
                        Log.Record($"ECC mismatch: is {eccCheck:X}, should be {ecc:X}");
                    return false;
                }

                // alarm and mv normally do have meaningful values
                // typically mv = 0x46 (01000110) when furnace is on; 0 when furnace is off
                if (LogEverything)
                    Log.Record($"PV={pv} SV={sv} MV={mv.ToBinaryString()} AL={alarm.ToBinaryString()} VAL={value}");

                Device.Temperature = pv/10;
                Device.Setpoint = sv/10;
                if (Parameter == ParameterCode.PowerMode)
                    Device.OnOffState = DecodePowerModeResponse(value);
                Device.UpdatesReceived++;
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