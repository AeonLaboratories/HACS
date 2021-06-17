using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Utilities;


namespace HACS.Components
{
    /// <summary>
    /// Process Sensors Corporation
    /// Model: PSC-SR54N
    /// Range: 800-2500 C => 4-20 mA (Quotient)
    /// Optics: 0.9 diameter spot size @ 250 mm focal length
    /// laser aiming light
    /// Interface: RS-485 @ 19.2 kBd @ Adr: 1
    /// </summary>
    public class Pyrometer : Thermometer, IPyrometer, Pyrometer.IDevice, Pyrometer.IConfig
    {
        #region Device constants
        public enum MessageTypeCode
        {
            ReadBit = 0x01,
            ReadRegister = 0x03,
            WriteBit = 0x05,
            WriteRegister = 0x06,
            WriteRegisters = 0x10,          // 16 decimal
            ReadParameterLimits = 0x68      // 104 decimal
        }

        ////////////////////////////////////////////////////////////////////
        // [R] = read only
        // [R/W] = read/write
        // [R/S] = read/store (write takes effect after Reset)
        // [min/max] = supports ReadParameterLimits command
        public enum RegisterCode
        {
            HardwareStatus = 0x0000,                // [R]
            ItemNumber = 0x0002,                    // [R] stored as value - 3000000000
            SerialNumber = 0x0004,                  // [R]
            Name = 0x0006,                          // [R] 8 bytes = "DSR 54N "
            InternalTemperature = 0x0016,           // [R] stored as (degC + 273.15)*16
            BaudRateAndAddress = 0x0018,            // [R/S][min/max]
            FocalLength = 0x0019,                   // [R] stored as millimeters
            MinimumSpotSize = 0x001a,               // [R] stored as millimeters * 100
            Status = 0x0100,                        // [R]
            StatusAndTemperature = 0x0100,          // [R]
            Temperature = 0x0101,                   // [R] stored as (degC + 273.15)*16
            Emissivity = 0x0102,                    // [R/W][min/max] stored as (emissivity * 1000)
            ResponseTime = 0x0103,                  // [R/W][min/max] stored as a TimeCodes code
            DeletionMode = 0x0104,                  // [R/W][min/max] stored as a DeletionModes code

            PossiblyTransmission = 0x0105,          // [R/W] value 0x03E8 == 1000 = 100% * 10?   // TODO is this right?

            OneColorTemperature = 0x0108,           // [R][min/max] stored as (degC + 273.15)*16
            TwoColorTemperature = 0x0109,           // [R][min/max] stored as (degC + 273.15)*16
            FlameTemperature = 0x010a,              // [R][min/max] stored as (degC + 273.15)*16
            EmissivityRatio = 0x010b,               // [R/W][min/max] stored as (value * 1000)
            Attenuation = 0x010c,                   // [R][min/max] tau-factor, 
            MinimumIntensity = 0x010d,              // [R/W] min permitted tau
            OpticsDeterminationWarning = 0x010e,    // [R/W][min/max] min tau
            OpticalThickness = 0x0110,              // [R][min/max]      // TODO what is this? Is it aperture, stored as mm * 10?

            PossiblyAperture = 0x0113,              // [R] value 0x03E8 == 1000 (10 mm * 100_?

            TemperatureRangeMinimum = 0x0115,       // [R]
            TemperatureRangeMaximum = 0x0116,       // [R]
            TemperatureSubRangeMinimum = 0x0117,    // [R/W]
            TemperatureSubRangeMaximum = 0x0118,    // [R/W]
            ModeAnalaogOutput = 0x0119,             // [R/W][min/max]
            Reset = 0x0B00,                         // [W]
            Laser = 0x0b01,                         // [R/W]
            Delete = 0x0b02                         // [R/W]
        }

        public enum ErrorCode
        {
            IllegalFunction = 0x01,
            InvalidRegister = 0x02,
            WrongDataLength = 0x03,
            DeviceBusy = 0x05,
            DataOutOfRange = 0x13
        }

        UInt16 OnValue = 0xFF00;
        UInt16 OffValue = 0x0000;

        ////////////////////////////////////////////////////////////////////
        // Baud rate / address decoding
        //
        //    Byte1 bits: M000 0BBB
        //      M: 0 = RTU, 1 = ASCII comms     RTU comms
        //      BBB: 0 = 9600, 1 = 19200, 2 = 38400, 3 = 57600, 4 = 115200
        //    Byte0 = Device RS-485 Address
        //
        ////////////////////////////////////////////////////////////////////

        public enum TimeCode
        {
            Unknown = -1,
            Minimum = 0,
            _1μs = 1,
            _2μs = 2,
            _5μs = 3,
            _10μs = 4,
            _20μs = 5,
            _50μs = 6,
            _100μs = 7,
            _200μs = 8,
            _500μs = 9,
            _1ms = 10,
            _2ms = 11,
            _5ms = 12,
            _10ms = 13,
            _20ms = 14,
            _50ms = 15,
            _100ms = 16,
            _200ms = 17,
            _500ms = 18,
            _1s = 19,
            _2s = 20,
            _5s = 21,
            _10s = 22,
            _20s = 23,
            _50s = 24,
            _100s = 25
        }

        public enum AnalogOutputModeCode
        {
            Off = 0,
            ZeroTo_20Milliamps = 1,
            FourTo_20Milliamps = 2
        }


        ////////////////////////////////////////////////////////////////////
        // Hardware status byte decoding:
        //     hardware features    0x0101 0xa003
        // =====================    ====== ======
        // (MSB) Byte3 AB00 0000	  0000   0001
        //       Byte2 00RC VDPL	  0000   0001
        //       Byte1 0000 0000	  1010   0000
        // (LSB) Byte0 0000 0000	  0000   0011
        //
        // device feature if bit = 1:                       indication
        // ===============================================  ===========================
        //  A: fiber optic                                  no fiber optic
        //  B: adjustable focus optics                      fixed optics
        //  R: controller module                            no controller module
        //  C: GoldCap / battery driven real time clock     no real time clock
        //  V: video module                                 no video module
        //  D: view finder                                  no view finder
        //  P: switchable LED targeting light               no LED light
        //  L: switchable laser targeting light             switchable laser targeting light is present
        //
        //  NOTE: there is unexpected data in bytes 1 and 0
        ////////////////////////////////////////////////////////////////////

        #endregion Device constants


        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : Thermometer.IDevice, Switch.IDevice
        {
            double Emissivity { get; set; }
            double RatioCorrection { get; set; }
            double Transmission { get; set; }
            TimeCode ResponseTime { get; set; }
            double MillimetersMeasuringDistance { get; set; }
            double TemperatureRangeMinimum { get; set; }
            double TemperatureRangeMaximum { get; set; }
            double MillimetersFocalLength { get; set; }
            double MillimetersFieldDiameterMinimum { get; set; }
            double MillimetersAperture { get; set; }
            int StatusByte { get; set; }
            double InternalTemperature { get; set; }
        }

        public new interface IConfig : Thermometer.IConfig, Switch.IConfig
        {
            double Emissivity { get; }
            double RatioCorrection { get; }
            double Transmission { get; }
            TimeCode ResponseTime { get; }
            double MillimetersMeasuringDistance { get; }

        }
        public new IDevice Device => this;
        public new IConfig Config => this;
        Switch.IDevice ISwitch.Device => this;
        Switch.IConfig ISwitch.Config => this;
        OnOff.IDevice IOnOff.Device => this;
        OnOff.IConfig IOnOff.Config => this;


        #endregion Device interfaces



        #region Settings

        [JsonProperty, DefaultValue(0x01)]
        public byte Address
        {
            get => address;
            set => Ensure(ref address, value);
        }
        byte address = 0x01;

        /// <summary>
        /// The Laser is on.
        /// </summary>
        public bool IsOn => onOffState.IsOn();
        public bool IsOff => onOffState.IsOff();

        /// <summary>
        /// This method returns whether IsOn was changed, 
        /// whereas IsOn = value wouldn't.
        /// </summary>
        protected virtual bool UpdateTargetState(bool value)
        {
            if (IsOn != value && (!value || LaserOffSeconds >= LaserCooldownSeconds))
            {
                Set(ref TargetState, value.ToSwitchState(), nameof(TargetState));
                SerialController.Hurry = true;
                return true;
            }
            return false;
        }

        public SwitchState State { get => TargetState; set => UpdateTargetState(value.IsOn()); }
        [JsonProperty("Laser")]
        SwitchState TargetState;
        SwitchState Switch.IConfig.State => TargetState;

        public OnOffState OnOffState => Device.OnOffState;
        OnOffState OnOff.IDevice.OnOffState
        {
            get => onOffState;
            set
            {
                if (Ensure(ref onOffState, value))
                    LaserStateStopwatch.Restart();
            }
        }
        OnOffState onOffState = OnOffState.Unknown;



        // TODO: is it worth making this in some way dependent on the On time?
        /// <summary>
        /// How long the laser must be off before turning it
        /// back on.
        /// </summary>
        [JsonProperty]
        public int LaserCooldownSeconds
        {
            get => laserCooldownSeconds;
            set => Ensure(ref laserCooldownSeconds, value);
        }
        int laserCooldownSeconds;

        /// <summary>
        /// Automatically turn the laser off after it's been on this
        /// long.
        /// </summary>
        [JsonProperty]
        public int LaserOnMaxSeconds
        {
            get => laserOnMaxSeconds;
            set => Ensure(ref laserOnMaxSeconds, value);
        }
        int laserOnMaxSeconds;

        Stopwatch LaserStateStopwatch = new Stopwatch();
        double LaserInStateSeconds => LaserStateStopwatch.Elapsed.TotalSeconds;
        double LaserOnSeconds => IsOn ? LaserInStateSeconds : 0;
        double LaserOffSeconds => IsOn ? 0 : LaserInStateSeconds;

        public long MillisecondsInState => LaserStateStopwatch.ElapsedMilliseconds;
        public long MillisecondsOn => IsOn ? MillisecondsInState : 0;
        public long MillisecondsOff => IsOff ? 0 : MillisecondsInState;


        /// <summary>
        /// What to do with the aiming laser when this instance is Stopped.
        /// </summary>
        [JsonProperty]
        public virtual StopAction StopAction
        {
            get => stopAction;
            set => Ensure(ref stopAction, value);
        }
        [JsonProperty("StopAction"), DefaultValue(StopAction.None)]
        StopAction stopAction = StopAction.None;




        /// <summary>
        /// The emissivity of the surface being measured.
        /// </summary>
        public double Emissivity
        {
            get => emissivity;
            set => Ensure(ref TargetEmissivity, value, NotifyConfigChanged, nameof(TargetEmissivity));
        }
        [JsonProperty("Emissivity")]
        double TargetEmissivity;
        double IConfig.Emissivity => TargetEmissivity;
        double IDevice.Emissivity
        {
            get => emissivity;
            set => Ensure(ref emissivity, value);

        }
        double emissivity;

        /// <summary>
        /// 
        /// </summary>
        public double RatioCorrection
        {
            get => ratioCorrection;
            set => Ensure(ref TargetRatioCorrection, value, NotifyConfigChanged, nameof(TargetRatioCorrection));
        }
        [JsonProperty("RatioCorrection")]
        double TargetRatioCorrection;
        double IConfig.RatioCorrection => TargetRatioCorrection;
        double IDevice.RatioCorrection
        {
            get => ratioCorrection;
            set => Ensure(ref ratioCorrection, value);

        }
        double ratioCorrection;

        /// <summary>
        /// 
        /// </summary>
        public double Transmission
        {
            get => transmission;
            set => Ensure(ref TargetTransmission, value, NotifyConfigChanged, nameof(TargetTransmission));
        }
        [JsonProperty("Transmission")]
        double TargetTransmission;
        double IConfig.Transmission => TargetTransmission;
        double IDevice.Transmission
        {
            get => transmission;
            set => Ensure(ref transmission, value);

        }
        double transmission;

        /// <summary>
        /// 
        /// </summary>
        public TimeCode ResponseTime
        {
            get => responseTime;
            set => Ensure(ref TargetResponseTime, value, NotifyConfigChanged, nameof(TargetResponseTime));
        }
        [JsonProperty("ResponseTime")]
        TimeCode TargetResponseTime;
        TimeCode IConfig.ResponseTime => TargetResponseTime;
        TimeCode IDevice.ResponseTime
        {
            get => responseTime;
            set => Ensure(ref responseTime, value);

        }
        TimeCode responseTime;


        public double MillimetersMeasuringDistance
        {
            get => millimetersMeasuringDistance;
            set => Ensure(ref TargetMillimetersMeasuringDistance, value, NotifyConfigChanged, nameof(TargetMillimetersMeasuringDistance));
        }
        [JsonProperty("MillimetersMeasuringDistance")]
        double TargetMillimetersMeasuringDistance;
        double IConfig.MillimetersMeasuringDistance => TargetMillimetersMeasuringDistance;
        double IDevice.MillimetersMeasuringDistance
        {
            get => millimetersMeasuringDistance;
            set => Ensure(ref millimetersMeasuringDistance, value);
        }
        double millimetersMeasuringDistance = -1;


        //read-only parameters -- initialize to invalid values
        // constants
        public double TemperatureRangeMinimum
        {
            get => temperatureRangeMinimum;
            protected set => Ensure(ref temperatureRangeMinimum, value);
        }
        double temperatureRangeMinimum = -1;

        double IDevice.TemperatureRangeMinimum
        {
            get => TemperatureRangeMinimum;
            set => TemperatureRangeMinimum = value;
        }

        public double TemperatureRangeMaximum
        {
            get => temperatureRangeMaximum;
            protected set => Ensure(ref temperatureRangeMaximum, value);
        }
        double temperatureRangeMaximum = -1;

        double IDevice.TemperatureRangeMaximum
        {
            get => TemperatureRangeMaximum;
            set => TemperatureRangeMaximum = value;
        }

        public double MillimetersFocalLength
        {
            get => millimetersFocalLength;
            set => Ensure(ref millimetersFocalLength, value);
        }
        double millimetersFocalLength = -1;

        double IDevice.MillimetersFocalLength
        {
            get => MillimetersFocalLength;
            set => MillimetersFocalLength = value;
        }

        public double MillimetersFieldDiameterMinimum
        {
            get => millimetersFieldDiameterMinimum;
            set => Ensure(ref millimetersFieldDiameterMinimum, value);
        }
        double millimetersFieldDiameterMinimum = -1;

        double IDevice.MillimetersFieldDiameterMinimum
        {
            get => MillimetersFieldDiameterMinimum;
            set => MillimetersFieldDiameterMinimum = value;
        }

        public double MillimetersAperture
        {
            get => millimetersAperture;
            set => Ensure(ref millimetersAperture, value);
        }
        double millimetersAperture = -1;

        double IDevice.MillimetersAperture
        {
            get => MillimetersAperture;
            set => MillimetersAperture = value;
        }

        // variable


        /// <summary>
        ///  Temperature status byte (includes laser status)
        /// </summary>
        /// 
        public int StatusByte => statusByte;
        int IDevice.StatusByte
        {
            get => statusByte;
            set
            {
                if (Ensure(ref statusByte, value))
                    (this as IOnOff).Device.OnOffState = ((Device.StatusByte & 0x10) != 0).ToOnOffState();
            }
        }
        int statusByte = -1;
        // The Device.StatusByte is interpreted as follows:
        //   msb to lsb:  GOUL XTTT
        //   status condition if the corresponding bit is 1:	
        //     G: ready
        //     O: temperature is overrange
        //     U: temperature is underrange
        //     L: laser is on
        //     X: external deletion input is active
        //     TTT: The temperature stored in register 0101	ratio
        //          and provided as the analog output.
        //          0 = 1-channel, 1 = ratio, 2 = flame, 	
        //          ...?..., 7 = 10/12 mA test current
        // For now, only the O, U, and L bits are interpreted here.
        public override bool OverRange => (Device.StatusByte & 0x40) != 0;
        public override bool UnderRange => (Device.StatusByte & 0x20) != 0;


        /// <summary>
        /// The Pyrometer's internal device temperature.
        /// </summary>
        public double InternalTemperature
        {
            get => internalTemperature;
            protected set => Ensure(ref internalTemperature, value);
        }
        double internalTemperature = -1;
        double IDevice.InternalTemperature
        {
            get => InternalTemperature;
            set => InternalTemperature = value;
        }

        [JsonProperty]
        public virtual SerialController SerialController
        {
            get => serialController;
            set
            {
                serialController = value;
                if (serialController != null)
                {
                    serialController.SelectServiceHandler = SelectService;
                    serialController.ResponseProcessor = ValidateResponse;
                    //serialController.LostConnection -= OnControllerLost;
                    //serialController.LostConnection += OnControllerLost;
                }
                NotifyPropertyChanged();
            }
        }
        SerialController serialController;

        #endregion Settings

        /// <summary>
        /// The diameter of the approximately circular area on the target 
        /// from which the intensities of certain electromagnetic wavelengths
        /// are measured, in order to infer the target temperature. 
        /// The spot size can be controlled to a specific diameter (&gt;= its 
        /// minimum) by adjusting the measuring distance, the distance from
        /// the pyrometer to the target. The diameter is at its smallest when
        /// the measuring distance equals the focal length of the pyrometer 
        /// optics. It is larger when the target is either nearer or farther
        /// than the focal length. 
        /// </summary>
        public double MeasuringFieldDiameter
        {
            get
            {
                var a = MillimetersFocalLength;
                var M = MillimetersFieldDiameterMinimum;
                if (a == 0) return M;
                var aX = MillimetersMeasuringDistance;
                var D = MillimetersAperture;
                if (aX < a) D = -D;
                return aX / a * (M + D) - D;
            }
        }

        /// <summary>
        /// Turn the aiming laser on.
        /// </summary>
        /// <returns>True if it wasn't already on.</returns>
        public virtual bool TurnOn() => UpdateTargetState(true);

        /// <summary>
        /// Turn the aiming laser off.
        /// </summary>
        /// <returns>True if it wasn't already off.</returns>
        public virtual bool TurnOff() => UpdateTargetState(false);

        /// <summary>
        /// Turn the aiming laser on or off according to the parameter.
        /// </summary>
        /// <param name="on">true => on, false => off</param>
        public virtual bool TurnOnOff(bool on)
        {
            if (on) return TurnOn();
            return TurnOff();
        }



        public override string ToString()
        {
            var sb = new StringBuilder($"{Name}:");
            sb.Append($" {(UnderRange ? "<" : OverRange ? ">" : "")}{Temperature:0.0} {UnitSymbol}");
            sb.Append($", {IsOn.ToString("Laser On", "Laser Off")}");

            var sb2 = new StringBuilder();
            sb2.Append($"\r\nInternal temperature: {InternalTemperature:0}");
            sb2.Append($"\r\nTemperature range: {TemperatureRangeMinimum:0}..{TemperatureRangeMaximum:0} °C");
            sb2.Append($"\r\nEmissivity: {Emissivity:0.000}");
            sb2.Append($"\r\nEmissivity ratio correction: {RatioCorrection:0.000}");
            sb2.Append($"\r\nTransmission: {Transmission:0.000}");
            sb2.Append($"\r\nResponse time: {ResponseTime}");
            sb2.Append($"\r\nAperture: {MillimetersAperture:0} mm");
            sb2.Append($"\r\nFocal length: {MillimetersFocalLength:0} mm");
            sb2.Append($"\r\nMinimum field diameter: {MillimetersFieldDiameterMinimum:0.00} mm");
            sb2.Append($"\r\nMeasuring distance: {MillimetersMeasuringDistance:0} mm");
            sb2.Append($"\r\nMeasuring field diameter: {MeasuringFieldDiameter:0.00} mm");

            return sb.Append(Utility.IndentLines(sb2.ToString())).ToString();
        }

        #endregion Class interface properties and methods

        #region Controller commands

        /// <summary>
        /// Construct a message for the Pyrometer.
        /// </summary>
        /// <param name="messageType">Read or Write</param>
        /// <param name="register">First register to read, or register to write</param>
        /// <param name="dataValue">Number of registers to read, or value to write</param>
        /// <returns></returns>
        string EncodeCommand(MessageTypeCode messageType, RegisterCode register, UInt16 dataValue)
        {
            var sb = new StringBuilder();
            sb.Append((char)Address);
            sb.Append((char)messageType);
            sb.Append(Utility.MSBLSB((int)register));    // starting register for Reads
            sb.Append(Utility.MSBLSB((int)dataValue));   // value to write or number of registers to read
            return sb.ToString();
        }

        // construct constant command strings on first-time use
        string CheckStatusCommand => checkStatusCommand ??= 
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.StatusAndTemperature, 2);
        string checkStatusCommand;

        string LaserOnCommand => laserOnCommand ??=
            EncodeCommand(MessageTypeCode.WriteBit, RegisterCode.Laser, OnValue);
        string laserOnCommand;

        string LaserOffCommand => laserOffCommand ??= 
            EncodeCommand(MessageTypeCode.WriteBit, RegisterCode.Laser, OffValue);
        string laserOffCommand;

        string GetTemperatureRangesCommand => getTemperatureRangesCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.TemperatureRangeMinimum, 2);
        string getTemperatureRangesCommand;
        
        string GetFocalLengthCommand => getFocalLengthCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.FocalLength, 1);
        string getFocalLengthCommand;
        
        string GetFieldDiameterMinimumCommand => getFieldDiameterMinimumCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.MinimumSpotSize, 1);
        string getFieldDiameterMinimumCommand;

        string GetApertureCommand => getApertureCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.OpticalThickness, 1);       // TODO is this correct?
        string getApertureCommand;

        string GetInternalTemperatureCommand => getInternalTemperatureCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.InternalTemperature, 1);
        string getInternalTemperatureCommand;

        string GetEmissivityCommand => getEmissivityCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.Emissivity, 1);
        string getEmissivityCommand;

        string GetRatioCorrectionCommand => getRatioCorrectionCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.EmissivityRatio, 1);
        string getRatioCorrectionCommand;

        string GetTransmissionCommand => getTransmissionCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.PossiblyTransmission, 1);
        string getTransmissionCommand;

        string GetResponseTimeCommand => getResponseTimeCommand ??=
            EncodeCommand(MessageTypeCode.ReadRegister, RegisterCode.ResponseTime, 1);
        string getResponseTimeCommand;


        /// <summary>
        /// The parameter's read command if the current command is its write command;
        /// otherwise, the parameter's write command.
        /// </summary>
        /// <param name="set">The parameter's write command</param>
        /// <param name="get">The parameter's read command</param>
        /// <returns></returns>
        string setThenGet(string set, string get) => command == set ? get : set;
        string emissivityCommand => setThenGet(
            EncodeCommand(MessageTypeCode.WriteRegister, RegisterCode.Emissivity, (UInt16)(Config.Emissivity * 1000)),
            GetEmissivityCommand);
        string ratioCorrectionCommand => setThenGet(
            EncodeCommand(MessageTypeCode.WriteRegister, RegisterCode.EmissivityRatio, (UInt16)Config.RatioCorrection),
            GetRatioCorrectionCommand);
        string transmissionCommand => setThenGet(
            EncodeCommand(MessageTypeCode.WriteRegister, RegisterCode.PossiblyTransmission, (UInt16)(Config.Transmission * 1000)),
            GetTransmissionCommand);
        string responseTimeCommand => setThenGet(
            EncodeCommand(MessageTypeCode.WriteRegister, RegisterCode.ResponseTime, (UInt16)Config.ResponseTime), 
            GetResponseTimeCommand);
        string laserCommand => setThenGet(
            Config.State.IsOn() ? LaserOnCommand : LaserOffCommand,
            CheckStatusCommand);

        string command => SerialController?.ServiceCommand ?? "";
        int addressByte => command[0];
        int functionByte => command[1];
        int registerWord => Utility.toInt(command, 2);
        int dataWord => Utility.toInt(command, 4);

        #endregion Controller commands


        #region Controller interactions

        protected virtual bool LogEverything => SerialController?.LogEverything ?? false;
        protected virtual LogFile Log => SerialController?.Log;

        /// <summary>
        /// Decodes a Pyrometer temperature by converting it from
        /// sixteenths of kelvins into degrees Celsius. (Pyrometer
        /// temperatures are transmitted as sixteenths of kelvins; i.e., 
        /// 1 K is sent as 16.)
        /// </summary>
        /// <param name="K16">sixteenths of kelvins</param>
        /// <returns>degrees Celsius</returns>
        double DecodeTemperature(int K16) => K16 / 16.0 - 273.15;

        /// <summary>
        /// Encodes a temperature (given in degrees C) into an integer
        /// as sixteenths of kelvins. (Pyrometer temperatures are 
        /// transmitted as sixteenths of kelvins; i.e., 1 K is sent as 16.)
        /// </summary>
        /// <param name="degC">degrees Celsius</param>
        /// <returns>sixteenths of kelvins</returns>
        int EncodeTemperature(double degC) => (16.0 * (degC + 273.15)).ToInt();


        bool deviceConstantsValid = false;
        protected virtual SerialController.Command SelectService()
        {
            string nextCommand = "";
            bool hurry = true;      // assume there is

            if (!deviceConstantsValid)
            {
                // value < 0 => invalid
                if (TemperatureRangeMinimum < 0 || TemperatureRangeMaximum < 0)
                    nextCommand = GetTemperatureRangesCommand;
                else if (MillimetersFocalLength < 0)
                    nextCommand = GetFocalLengthCommand;
                else if (MillimetersFieldDiameterMinimum < 0)
                    nextCommand = GetFieldDiameterMinimumCommand;
                else if (MillimetersAperture < 0)
                    nextCommand = GetApertureCommand;
                else if (Emissivity < 0)
                    nextCommand = GetEmissivityCommand;
                else if (RatioCorrection < 0)
                    nextCommand = GetRatioCorrectionCommand;
                else if (Transmission < 0)
                    nextCommand = GetTransmissionCommand;
                else if (ResponseTime < 0)
                    nextCommand = GetResponseTimeCommand;
                else
                    deviceConstantsValid = true;
            }
            if (deviceConstantsValid)        // it may have just changed
            {
                if (Emissivity != Config.Emissivity)
                    nextCommand = emissivityCommand;
                else if (RatioCorrection != Config.RatioCorrection)
                    nextCommand = ratioCorrectionCommand;
                else if (Transmission != Config.Transmission)
                    nextCommand = transmissionCommand;
                else if (ResponseTime != Config.ResponseTime)
                    nextCommand = responseTimeCommand;
                else if (IsOn && !Config.State.IsOn() || LaserOnSeconds > LaserOnMaxSeconds)
                    nextCommand = laserCommand;
                else if (!IsOn && Config.State.IsOn() && LaserOffSeconds > LaserCooldownSeconds)
                    nextCommand = laserCommand;
                else                // idling commands
                {
                    if (command == CheckStatusCommand)
                        nextCommand = GetInternalTemperatureCommand;
                    else
                        nextCommand = CheckStatusCommand;
                    hurry = false;
                }
            }

            var m = (MessageTypeCode)functionByte;
            int responsesExpected =
                (m == MessageTypeCode.ReadBit ||
                 m == MessageTypeCode.ReadRegister ||
                 m == MessageTypeCode.ReadParameterLimits) ? 1 : 0;

            return nextCommand.IsBlank() ? SerialController.DefaultCommand :
                new SerialController.Command(nextCommand, responsesExpected, hurry);
        }

        protected virtual bool ValidateResponse(string response, int which)
        {
            try
            {
                // minimum valid response length is 3 (for an error)
                //  [addrByte] [functionByte | 0x80] [byte]
                if (response.Length < 3)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Response too short");
                    return false;
                }

                // if command is rejected by pyrometer, the command functionByte is returned with its msb set
                if ((response[1] & 0x80) != 0)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Device rejected command: {(ErrorCode)response[2]}");
                    return false;
                }

                // Minimum valid response length for accepted command is 5
                //   [addrByte] [functionByte] [byteCountByte] [dataWord]
                if (response.Length < 5)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Response too short to decode");
                    return false;
                }

                if (response[0] != Address)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Response from wrong device (???)");
                    return false;       // response is not from this device (should be impossible)
                }

                // Response to a WriteBit or WriteRegister command should simply echo the command.
                if (response[1] == (byte)MessageTypeCode.WriteBit || response[1] == (byte)MessageTypeCode.WriteRegister)
                {
                    var confirmed = response == command;
                    if (LogEverything)
                        Log.Record($"{base.Name} Write command {(confirmed ? "" : "un")}confirmed");
                    return confirmed;
                }

                // Response to Multiple-register write echoes only up to the registerCount (dataWord)
                if (response[1] == (byte)MessageTypeCode.WriteRegisters)
                {
                    var confirmed = (Utility.toInt(response, 2) == registerWord) && (Utility.toInt(response, 4) == dataWord);
                    if (LogEverything)
                        Log.Record($"{base.Name} Write command {(confirmed ? "" : "un")}confirmed");
                    return confirmed;
                }

                // ReadParameterLimits is not implemented; would need to add min and max values 
                // for each parameter to DeviceState.
                // Command structure is like ReadRegister:
                //    [addressByte] [0x68] [registerWord = start register] [dataWord = nRegisters]
                // Response structure is similar to ReadRegister, but returns four bytes per register
                // instead of two.
                //    [addressByte] [0x68] [byteCountByte] {[minWord_0][maxWord_0]...[minWord_n][maxWord_n]}
                // ReadParameterLimits is only valid for Registers
                if (response[1] == (byte)MessageTypeCode.ReadParameterLimits)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} ReadParameterLimits response not implemented");
                    return false;
                }

                // The only remaining supported message type is ReadRegister
                if (response[1] != (byte)MessageTypeCode.ReadRegister)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Message type not supported: {(byte)response[1]:X2}");
                    return false;
                }

                // Read command response should contain 2 data bytes for each register read.
                if (response.Length != 3 + response[2])
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Incorrect response length: {response.Length} (expected {3 + response[2]})");
                    return false;       // incorrect response length for number of data bytes
                }

                if (response[2] != 2 * dataWord)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Unexpected response length: {response[2]} (expected {2 * dataWord})");
                    return false;       // response length doesn't match requested amount of data
                }

                var register = (RegisterCode)registerWord;
                var n = Utility.toInt(response, 3);

                if (register == RegisterCode.Status)
                {
                    // The status is in the first of the two bytes in the register word; the 2nd byte is a counter (ignored)
                    Device.StatusByte = response[3];
                    if (LogEverything)
                        Log.Record($"{base.Name} StatusByte received: {Device.StatusByte:X2}");
                    if (dataWord == 2)      // update the temperature, too
                    {
                        var t = DecodeTemperature(Utility.toInt(response, 5));
                        if (LogEverything)
                            Log.Record($"{base.Name} Temperature received: {t:0}");
                        Device.Temperature = t;
                    }
                }
                else if (register == RegisterCode.Temperature)
                {
                    var t = DecodeTemperature(n);
                    if (LogEverything)
                        Log.Record($"{base.Name} Temperature received: {t:0}");
                    Device.Temperature = t;
                }
                else if (register == RegisterCode.InternalTemperature)
                {
                    var t = DecodeTemperature(n);
                    if (LogEverything)
                        Log.Record($"{base.Name} Internal Temperature received: {t:0}");
                    Device.InternalTemperature = t;
                }
                else if (register == RegisterCode.TemperatureRangeMinimum)
                {
                    var t = DecodeTemperature(n);
                    if (LogEverything)
                        Log.Record($"{base.Name} Temperature Range Minimum received: {t:0}");
                    Device.TemperatureRangeMinimum = t;
                    if (dataWord == 2)      // update the max, too
                    {
                        t = DecodeTemperature(Utility.toInt(response, 5));
                        if (LogEverything)
                            Log.Record($"{base.Name} Temperature Range Maximum received: {t:0}");
                        Device.TemperatureRangeMaximum = t;
                    }
                }
                else if (register == RegisterCode.Emissivity)
                {
                    var d = n / 1000.0;
                    if (LogEverything)
                        Log.Record($"{base.Name} Emissivity received: {d:0.000}");
                    Device.Emissivity = d;
                }
                else if (register == RegisterCode.EmissivityRatio)
                {
                    var d = n / 1000.0;
                    if (LogEverything)
                        Log.Record($"{base.Name} Emissivity ratio received: {d:0.000}");
                    Device.RatioCorrection = d;
                }
                else if (register == RegisterCode.PossiblyTransmission)
                {
                    var d = n / 1000.0;            // TODO confirm this is correct
                    if (LogEverything)
                        Log.Record($"{base.Name} Transmission received: {d:0.000}");
                    Device.Transmission = d;
                }
                else if (register == RegisterCode.ResponseTime)
                {
                    var value = (TimeCode)n;
                    if (LogEverything)
                        Log.Record($"{base.Name} Response time received: {value}");
                    Device.ResponseTime = value;
                }
                else if (register == RegisterCode.FocalLength)
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Focal length received: {n:0}");
                    Device.MillimetersFocalLength = n;
                }
                else if (register == RegisterCode.MinimumSpotSize)
                {
                    var d = n / 100.0;
                    if (LogEverything)
                        Log.Record($"{base.Name} Minimum Field Diameter (spot size) received: {d:0.00}");
                    Device.MillimetersFieldDiameterMinimum = d;
                }
                else if (register == RegisterCode.OpticalThickness) // TODO is this right?
                {
                    var d = n / 10.0;
                    if (LogEverything)
                        Log.Record($"{base.Name} Optical Thickness (Aperture?) received: {d:0.0}");
                    Device.MillimetersAperture = d;
                }
                else
                {
                    if (LogEverything)
                        Log.Record($"{base.Name} Unrecognized register");
                    return false;       // unrecognized register
                }

                //if (Controller.LogEverything)
                //  Controller.Log.Record($"{Name} Response successfully decoded");
                return true;
            }
            catch (Exception e)
            {
                if (LogEverything)
                    Log.Record(e.ToString());
                return false;
            }
        }

        #endregion Controller interactions

    }
}
