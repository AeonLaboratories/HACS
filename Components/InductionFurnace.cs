using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace HACS.Components
{
    public class InductionFurnace : TubeFurnace, IInductionFurnace,
        InductionFurnace.IConfig, InductionFurnace.IDevice
    {
        #region HacsComponent

        [HacsConnect]
        protected override void Connect()
        {
            base.Connect();
            Pyrometer = Find<IPyrometer>(pyrometerName);
            SetpointRamp.MinimumStartValue = MinimumControlledTemperature;
            Pid.GetSetpoint = () => RampingSetpoint;
            Pid.GetProcessVariable = () => Temperature;
            Pid.UpdateControlOutput = (double powerLevel) => PowerLevel = powerLevel.ToInt();
        }

        [HacsStop]
        protected virtual void Stop()
        {
            Pid.Stop();
        }

        #endregion HacsComponent

        #region Device constants

        public char DeviceAddress { get; private set; } = '3';  // address of interface board in heat station
        public char HostAddress { get; private set; } = '4';    // address of this computer

        public enum CommandCode
        {
            HeatOn = (byte)'2',
            HeatOff = (byte)'3',
            SetPwr = (byte)'4',
            /// <summary>
            /// reset control board (e.g., after an error)
            /// </summary>
            ResetCB = (byte)'9',
            ReadData = (byte)'A',
            GetPwr = (byte)'B',
            GetErr = (byte)'E',
            SetMode = (byte)'L',   // TypeMod2 in the manual
            GetVoltage = (byte)'v',
            GetCurrent = (byte)'i',
            GetFreq = (byte)'f'
        }

        public enum ControlModeCode
        {
            /// <summary>
            /// Host computer operates induction heater;
            /// called "Control mode" in manual.
            /// </summary>
            Remote = 0x0,
            /// <summary>
            /// PSU front panel operates induction heater;
            /// called "Listening mode" in manual.
            /// </summary>
            Local = 0x01
        }

        [Flags]
        public enum DataByteCodes
        {
            // DataByte1 bits
            ReadyAuxRelay = 0x0200,
            HeatOnRelay = 0x0100,
            // DataByte2 bits
            RemPanelControl = 0x0080,
            AuxFaultCleared = 0x0040,
            RemoteHeatOn = 0x0020,
            ExternalHeatOn = 0x0010,
            ExternalHeatOff = 0x0008,
            FootSwitchPresent = 0x0004,
            FootSwitchOn = 0x0002,
            FootSwitchOff = 0x0001,
        }

        public enum ErrorCode
        {
            Phase = 1,
            Current = 2,
            AutoTune = 5,
            Communication = 9,
            HighFreq = 21,
            LowFreq = 22,
            DCReg = 23,
            EUTemp = 27,
            EUWater = 28,
            Coolant = 29,
            HSTemp = 30,
            MissingPhase = 32,
            PowerSupply = 34,
            InterfaceBoard = 36,
            HighFrequencyLimit = 38,
            TankCapOver = 39,
            WaterFault1 = 45,
            EmergencyStop = 66
        }

        Dictionary<ErrorCode, string> ErrorMessages = new Dictionary<ErrorCode, string>()
        {
            { ErrorCode.Phase , "Primary V & I out of phase"},
            { ErrorCode.Current , "Output current exceeds the limit"},
            { ErrorCode.AutoTune , "AutoTune failed. Resonant frequency not found"},
            { ErrorCode.Communication , "Communication error between Panel and Control Board"},
            { ErrorCode.HighFreq , "Resonant frequency of tank circuit is above the high limit"},
            { ErrorCode.LowFreq , "Resonant frequency of tank circuit is below the low limit"},
            { ErrorCode.DCReg , "DC current of chopper exceeds the limit"},
            { ErrorCode.EUTemp , "Chopper/inverter heatsink overheated (> 70 °C)"},
            { ErrorCode.EUWater , "Inadequate coolant flow (< 2 L/min)"},
            { ErrorCode.Coolant , "Inadequate coolant flow (< 3 L/min)"},
            { ErrorCode.HSTemp , "Heat station overheated (> 70 °C)"},
            { ErrorCode.MissingPhase , "A mains power phase is missing"},
            { ErrorCode.PowerSupply , "Low voltage SMPS module failed"},
            { ErrorCode.InterfaceBoard , "Communication error between Panel and Interface Board"},
            { ErrorCode.HighFrequencyLimit , "Resonant frequency of tank circuit is above the high limit"},
            { ErrorCode.TankCapOver , "Tank capacitor bank voltage is over limit"},
            { ErrorCode.WaterFault1 , "Inadequate coolant flow (< 3 L/min)"},
            { ErrorCode.EmergencyStop , "Emergency Stop button is pressed"},
        };

        public string ErrorMessage() => ErrorMessage(Error);
        public string ErrorMessage(int errorCode)
        {
            try
            {
                if (errorCode == 0) return "";
                return ErrorMessages[(ErrorCode)errorCode];
            }
            catch { return $"Unknown error code: {errorCode:X2}"; }
        }

        #endregion Device constants

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : TubeFurnace.IDevice
        {
            int PowerLevel { get; set; }
            int PowerLimit { get; set; }
            int Error { get; set; }
            int Voltage { get; set; }
            double Current { get; set; }
            int Frequency { get; set; }
            int Status { get; set; }
            string InterfaceBoardRevision { get; set; }
        }

        public new interface IConfig : TubeFurnace.IConfig
        {
            ControlModeCode ControlMode { get; }
            int PowerLevel { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        /// <summary>
        /// In "remote" control mode, the host computer operates the induction heater.
        /// In "local" control mode, the heater is managed by the physical control panel
        /// on the front of the Power Supply unit.
        /// </summary>
        public ControlModeCode ControlMode => 
            RemoteControl ? ControlModeCode.Remote : ControlModeCode.Local;
        ControlModeCode IConfig.ControlMode => ControlModeCode.Remote;

        /// <summary>
        /// Control Output value, in %.
        /// </summary>
        public int PowerLevel
        {
            get => powerLevel;
            set => Ensure(ref TargetPowerLevel, value, NotifyConfigChanged, nameof(TargetPowerLevel));
        }
        [JsonProperty("PowerLevel")]
        int TargetPowerLevel;
        int IConfig.PowerLevel => TargetPowerLevel;
        int IDevice.PowerLevel
        {
            get => powerLevel;
            set => Ensure(ref powerLevel, value);

        }
        int powerLevel = -1;

        /// <summary>
        /// 
        /// </summary>
        public int PowerLimit => powerLimit;
        int IDevice.PowerLimit
        {
            get => powerLimit;
            set => Ensure(ref powerLimit, value);

        }
        int powerLimit = -1;

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
        /// Coil voltage
        /// </summary>
        public int Voltage => voltage;
        int IDevice.Voltage
        {
            get => voltage;
            set => Ensure(ref voltage, value);
        }
        int voltage = -1;

        /// <summary>
        /// Coil current
        /// </summary>
        public double Current => current;
        double IDevice.Current
        {
            get => current;
            set => Ensure(ref current, value);
        }
        double current = -1;

        /// <summary>
        /// Coil frequency
        /// </summary>
        public int Frequency => frequency;
        int IDevice.Frequency
        {
            get => frequency;
            set => Ensure(ref frequency, value);
        }
        int frequency = -1;

        /// <summary>
        /// 
        /// </summary>
        public int Status => status;
        int IDevice.Status
        {
            get => status;
            set
            {
                if (Ensure(ref status, value))
                {
                    Device.OnOffState = 
                        ((Device.Status & (int)DataByteCodes.HeatOnRelay) != 0).ToOnOffState();
                }
            }
        }
        int status = -1;

        /// <summary>
        /// Interface board revision
        /// </summary>
        public string InterfaceBoardRevision => interfaceBoardRevision;
        string IDevice.InterfaceBoardRevision
        {
            get => interfaceBoardRevision;
            set => Ensure(ref interfaceBoardRevision, value);
        }
        string interfaceBoardRevision = "";


        [JsonProperty("Pyrometer")]
        string PyrometerName { get => Pyrometer?.Name; set => pyrometerName = value; }
        string pyrometerName;
        /// <summary>
        /// The pyrometer that monitors the temperature of the inductively heated load.
        /// </summary>
        public IPyrometer Pyrometer { 
            get => pyrometer;
            set => Ensure(ref pyrometer, value, NotifyPropertyChanged); 
        }
        IPyrometer pyrometer;

        [JsonProperty("PidControl")]
        PidControl Pid
        { 
            get => pid; 
            set => Ensure(ref pid, value, NotifyPropertyChanged); 
        }
        PidControl pid;


        #region TubeFurnace overrides and additions

        public int MinimumControlledTemperature => (int)(Pyrometer?.TemperatureRangeMinimum ?? 800);

        public override bool Ready => base.Ready &&
            (Device.Status & (int)DataByteCodes.ReadyAuxRelay) != 0;

        /// <summary>
        /// Turns off the furnace.
        /// </summary>
        public override bool TurnOff()
        {
            if (!base.TurnOff())
                return false;
            Pid.Stop();
            return true;
        }

        /// <summary>
        /// Turns the furnace on.
        /// </summary>
        public override bool TurnOn()
        {
            if (!base.TurnOn())
                return false;
            Pid.Start();
            return true;
        }

        #endregion TubeFurnace overrides and additions


        public override string ToString()
        {
            var sb = new StringBuilder($"{Name}: {Temperature:0} °C, {IsOn.OnOff()}: {PowerLevel} %");
            sb.Append($"\r\nSP: {Setpoint} WSP: {RampingSetpoint} Error: {Error:X2}");
            if (Error != 0) sb.Append($"\r\n{ErrorMessage()}");
            return sb.ToString();
        }

        #endregion Class interface properties and methods

        /// <summary>
        /// In "remote" control mode, the host computer operates the induction heater.
        /// In "local" control mode, the heater is managed by the physical control panel
        /// on the front of the Power Supply unit.
        /// </summary>
        bool RemoteControl => (Device.Status & (int)DataByteCodes.RemPanelControl) != 0;

        #region Controller commands

        string command => SerialController?.ServiceCommand ?? "";
        CommandCode priorCommand => (CommandCode)command[2];

        string TakeControl => takeControl ?? (takeControl =
            EncodeCommand(CommandCode.SetMode, (byte)ControlModeCode.Remote));
        string takeControl;

        string HeatOn => heatOn ?? (heatOn =
            EncodeCommand(CommandCode.HeatOn));
        string heatOn;

        string HeatOff => heatOff ?? (heatOff =
            EncodeCommand(CommandCode.HeatOff));
        string heatOff;

        string ResetCB => resetCB ?? (resetCB =
            EncodeCommand(CommandCode.ResetCB));
        string resetCB;

        string CheckStatus => checkStatus ?? (checkStatus =
            EncodeCommand(CommandCode.ReadData));
        string checkStatus;

        string CheckError => checkError ?? (checkError =
            EncodeCommand(CommandCode.GetErr));
        string checkError;

        string CheckPower => checkPower ?? (checkPower =
            EncodeCommand(CommandCode.GetPwr));
        string checkPower;

        string CheckVoltage => checkVoltage ?? (checkVoltage =
            EncodeCommand(CommandCode.GetVoltage));
        string checkVoltage;

        string CheckCurrent => checkCurrent ?? (checkCurrent =
            EncodeCommand(CommandCode.GetCurrent));
        string checkCurrent;

        string CheckFrequency => checkFrequency ?? (checkFrequency =
            EncodeCommand(CommandCode.GetFreq));
        string checkFrequency;


        string EncodeCommand(CommandCode commandCode, byte data, byte data2) => EncodeCommand(commandCode, new byte[] { data, data2 });
        string EncodeCommand(CommandCode commandCode, byte data) => EncodeCommand(commandCode, new byte[] { data });
        string EncodeCommand(CommandCode commandCode) => EncodeCommand(commandCode, new byte[] { });
        string EncodeCommand(CommandCode commandCode, byte[] data)
        {
            var sb = new StringBuilder();
            sb.Append('S');
            sb.Append(DeviceAddress);
            sb.Append((char)commandCode);
            foreach (byte b in data)
                sb.Append(b.ToString("X2"));
            sb.Append(crc(sb).ToString("X2"));
            sb.Append('K');
            return sb.ToString();
        }

        int crc(StringBuilder sb)
        {
            int crc = 0;
            for (int i = 0; i < sb.Length; ++i)
                crc -= sb[i];
            crc &= 0xFF;                // is this ever needed?
            return crc;
        }

        #endregion Controller commands

        #region Controller interactions

        int errorState;
        protected override SerialController.Command SelectService()
        {
            string command = "";
            bool hurry = true;

            if (IsOn && UseTimeLimit && MinutesOn >= TimeLimit)
            {
                UseTimeLimit = false;
                TurnOff();
            }

            if (Device.Status == -1)    // initial status check is needed
                command = checkStatus;
            else if (Error == -1)       // initial error check is needed
                command = checkError;
            else if (Error != 0)        // there is an error
            {
                if (errorState == 0)
                {
                    // let operator know there is an error?
                    // This can be a "normal" condition, when the system 
                    // is not currently using the induction furnace
                    if (LogEverything)
                        Log.Record($"{Name} Error {Error:X2} detected: {ErrorMessage()}");
                    errorState = 1;
                }
                if (errorState == 1)
                {
                    if (IsOn)               // heat is on; turn it off
                    {
                        command = heatOff;
                        errorState = 2;
                    }
                    else                    // heat is off; try resetting the control board
                    {
                        command = resetCB;
                        errorState = 3;
                    }
                }
                else if (errorState == 2)   // heat was commanded off; check status
                {
                    command = checkStatus;
                    errorState = 1;
                }
                else if (errorState == 3)   // control board was reset
                {
                    command = checkError;
                    errorState = 0;
                }
            }
            else if (ControlMode != ControlModeCode.Remote)
                command = priorCommand == CommandCode.SetMode ? checkStatus : takeControl;
            else if (IsOn && !Config.State.IsOn())
                command = priorCommand == CommandCode.HeatOff ? checkStatus : heatOff;
            else if (!IsOn && Config.State.IsOn())
                command = priorCommand == CommandCode.HeatOn ? checkStatus : heatOn;
            else if (IsOn && Device.PowerLevel != Config.PowerLevel)
                command = priorCommand == CommandCode.SetPwr ? checkPower :
                    EncodeCommand(CommandCode.SetPwr, (byte)Config.PowerLevel);
            else               // cycle status checks
            {
                switch (priorCommand)
                {
                    case CommandCode.ReadData:
                        command = checkError;
                        break;
                    case CommandCode.GetPwr:
                        command = IsOn ? checkVoltage : checkStatus;
                        break;
                    case CommandCode.GetVoltage:
                        command = IsOn ? checkCurrent : checkStatus;
                        break;
                    case CommandCode.GetCurrent:
                        command = IsOn ? checkFrequency : checkStatus;
                        break;
                    case CommandCode.GetErr:
                    case CommandCode.GetFreq:
                    default:
                        command = IsOn ? checkPower : checkStatus;
                        hurry = false;
                        break;
                }
            }

            return command.IsBlank() ? SerialController.DefaultCommand :
                new SerialController.Command(command, 1, hurry);
        }

        bool crcError(string msg)
        {
            var crcPos = msg.Length - 3;
            return msg.Substring(crcPos, 2) != crc(new StringBuilder(msg.Substring(0, crcPos))).ToString("X2");
        }

        int word(int msb, int lsb) => (msb << 8) | lsb;

        protected override bool ValidateResponse(string response, int which)
        {
            try
            {
                var len = response.Length;
                // minimum valid response is $"S{HostAddress}{CommandCode}{crc:X2}K"
                if (response.Length < 6 ||
                        response[0] != 'S' ||
                        response[1] != HostAddress ||
                        response[len - 1] != 'K' ||
                        (CommandCode)response[2] != priorCommand ||
                        crcError(response)
                    )
                    return false;

                if (len == 6) return true;      // done

                bool isOn = IsOn;

                // decode response data
                var dataLen = len - 6;
                var sdata = response.Substring(3, dataLen);
                var nbytes = dataLen / 2;
                int[] data = new int[nbytes];
                for (int i = 0; i < nbytes; i++)
                    data[i] = int.Parse(sdata.Substring(i + i, 2), System.Globalization.NumberStyles.HexNumber);

                switch (priorCommand)
                {
                    case CommandCode.ReadData:
                        Device.Status = word(data[0], data[1]);
                        break;
                    case CommandCode.GetPwr:
                        Device.PowerLevel = data[0];
                        Device.PowerLimit = data[1];
                        break;
                    case CommandCode.GetErr:
                        Device.Error = data[0];
                        break;
                    case CommandCode.SetMode:
                        Device.InterfaceBoardRevision = $"{data[0] / 10:0.0}";
                        break;
                    case CommandCode.GetVoltage:
                        Device.Voltage = word(data[0], data[1]);
                        break;
                    case CommandCode.GetCurrent:
                        Device.Current = word(data[0], data[1]) / 10.0;
                        break;
                    case CommandCode.GetFreq:
                        Device.Frequency = word(data[0], data[1]);
                        break;
                    default:
                        return false;
                }
                if (isOn != IsOn)
                    StateStopwatch.Restart();

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
