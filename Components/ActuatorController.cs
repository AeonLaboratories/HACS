using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Utilities;
using static Utilities.Utility;

namespace HACS.Components
{
    public class ActuatorController : SerialDeviceManager, IActuatorController,
        ActuatorController.IConfig, ActuatorController.IDevice
    {
        #region HacsComponent
        #endregion HacsComponent

        #region Device constants

        public enum OperationState
        {
            Free,
            Configuring,
            Confirming,
            Going,
            AwaitingMotion,
            Stopping,
            AwaitingStopped,
            Failed
        }

        /// <summary>
        /// Error codes which may be reported by this device's Controller
        /// </summary>
        [Flags]
        public enum ErrorCodes
        {
            /// <summary>
            /// No error, status is ok
            /// </summary>
			None = 0,
            /// <summary>
            /// ADC out of range (analog-to-digital converter error)
            /// </summary>
			AdcOutOfRange = 1,
            /// <summary>
            /// RS232 input buffer overflow; commands are too frequent
            /// </summary>
			RxBufferOverflow = 2,
            /// <summary>
            /// RS232 CRC error (cyclical redundancy check failed)
            /// </summary>
			CRC = 4,
            /// <summary>
            /// Unrecognized command
            /// </summary>
			BadCommand = 8,
            /// <summary>
            /// Not used
            /// </summary>
			NotUsed16 = 16,
            /// <summary>
            /// Invalid servo (actuator) channel
            /// </summary>
			BadChannel = 32,
            /// <summary>
            /// Datalogging time interval out of range
            /// </summary>
			BadDataLogInterval = 64,
            /// <summary>
            /// Not used
            /// </summary>
			ServoError = 128,
            /// <summary>
            /// Servo control pulse width (CPW) out of range
            /// </summary>
			CpwOutOfRange = 256,
            /// <summary>
            /// Time value for &quot;stop on time limit&quot; out of range
            /// </summary>
			TimeLimitOutOfRange = 512,
            /// <summary>
            /// Both limit switches engaged (opposite extremes)
            /// </summary>
			BothLimitSwitchesEngaged = 1024,
            /// <summary>
            /// Power supply voltage is low
            /// </summary>
			LowPower = 2048,
            /// <summary>
            /// Current limit value out of range
            /// </summary>
			CurrentLimitOutOfRange = 4096,
            /// <summary>
            /// Unrecognized stop limit setting
            /// </summary>
			BadStopLimit = 8192,
        }

        static ErrorCodes ActuatorErrorFilter =
            ErrorCodes.BothLimitSwitchesEngaged;

        /// <summary>
        /// Error codes which may be reported by the AeonServo.
        /// </summary>
        [Flags]
        public enum AeonServoErrorCodes
        {
            /// <summary>
            /// No error, status is ok
            /// </summary>
            None = 0,
            /// <summary>
            /// Invalid position commanded
            /// </summary>
            BadPosition = 1,
            /// <summary>
            /// RS232 input buffer overflow; commands are too frequent
            /// </summary>
            RxBufferOverflow = 2,
            /// <summary>
            /// RS232 CRC error (cyclical redundancy check failed)
            /// </summary>
            CRC = 4,
            /// <summary>
            /// Unrecognized command
            /// </summary>
            BadCommand = 8,
            /// <summary>
            /// Datalogging time interval out of range
            /// </summary>
            BadDataLogInterval = 16,
        }

        //
        // TODO: need to add and use old AeonServoErrorCodes for servos
        // with firmware version below 20200411
        //
        #endregion Device constants

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : SerialDeviceManager.IDevice
        {
            string Model { get; set; }
            string Firmware { get; set; }
            int SerialNumber { get; set; }
            int CpwMin { get; set; }
            int CpwMax { get; set; }
            int SelectedActuator { get; set; }
            double Voltage { get; set; }
            ErrorCodes Errors { get; set; }
            AeonServoErrorCodes AeonServoErrors { get; set; }
        }

        public new interface IConfig : SerialDeviceManager.IConfig { }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region IDeviceManager

        public override bool IsSupported(IManagedDevice d, string key)
        {
            if (IsValidKey(key, "", Channels - 1) && d is ICpwActuator)
                return true;

            Log.Record($"Connect: {d.Name}'s key \"{key}\" and type ({d.GetType()}) are not supported together." +
                $"\r\n\tOne of them is invalid or they are not compatible.");
            return false;
        }

        public override bool Ready =>
            base.Ready &&
            (AeonServo?.Ready ?? true); // Note: AeonServo is optional

        public override bool HasWork =>
            base.HasWork ||
            (AeonServo != null && AeonServo.HasWork);


        void UpdateAeonServoLog()
        {
            if (AeonServo == null) return;
            AeonServo.Log = Log;
            AeonServo.LogEverything = LogEverything;
        }

        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var propertyName = e?.PropertyName;
            if (propertyName == nameof(Log) || propertyName == nameof(LogEverything))
                UpdateAeonServoLog();
            else
                base.OnPropertyChanged(sender, e);
        }


        #endregion IDeviceManager

        #region Settings

        [JsonProperty, DefaultValue(64)]
        public int Channels     // hardware limit
        {
            get => channels;
            set => Ensure(ref channels, value);
        }
        int channels;

        /// <summary>
        /// This SerialController is for communicating directly
        /// with one of Aeon's RS232 servos.
        /// </summary>
        [JsonProperty]
        public SerialController AeonServo
        {
            get => aeonServo;
            set
            {
                aeonServo = value;
                if (aeonServo != null)
                {
                    UpdateAeonServoLog();
                    aeonServo.SelectServiceHandler = SelectAeonServoService;
                    aeonServo.ResponseProcessor = ValidateAeonServoResponse;
                    aeonServo.LostConnection -= OnAeonServoLost;
                    aeonServo.LostConnection += OnAeonServoLost;
                }
                NotifyPropertyChanged();
            }
        }
        SerialController aeonServo;

        #endregion Settings

        #region Retrieved device values

        /// <summary>
        /// The device model identifier.
        /// </summary>
        public string Model => model;
        string IDevice.Model
        {
            get => model;
            set => Set(ref model, value);
        }
        string model;

        /// <summary>
        /// The firmware revision identifier.
        /// </summary>
        public string Firmware => firmware;
        string IDevice.Firmware
        {
            get => firmware;
            set => Set(ref firmware, value);
        }
        string firmware;

        /// <summary>
        /// The device serial number.
        /// </summary>
        public int SerialNumber => serialNumber;
        int IDevice.SerialNumber
        {
            get => serialNumber;
            set => Set(ref serialNumber, value);
        }
        int serialNumber;

        /// <summary>
        /// The minimum supported control pulse width.
        /// </summary>
        public int MinimumControlPulseWidthMicroseconds => cpwMin;
        int IDevice.CpwMin
        {
            get => cpwMin;
            set => Set(ref cpwMin, value);
        }
        int cpwMin;

        /// <summary>
        /// The maximum supported control pulse width.
        /// </summary>
        public int MaximumControlPulseWidthMicroseconds => cpwMax;
        int IDevice.CpwMax
        {
            get => cpwMax;
            set => Set(ref cpwMax, value);
        }
        int cpwMax;

        /// <summary>
        /// The channel number of the currently selected actuator.
        /// </summary>
        public int SelectedActuator => selectedActuator;
        int IDevice.SelectedActuator
        {
            get => selectedActuator;
            set => Set(ref selectedActuator, value);
        }
        int selectedActuator;

        /// <summary>
        ///  Servo power supply voltage
        /// </summary>
        public double Voltage => voltage;
        double IDevice.Voltage
        {
            get => voltage;
            set => Set(ref voltage, value);
        }
        double voltage;

        /// <summary>
        /// Error codes reported by the controller.
        /// </summary>
        public ErrorCodes Errors => errors;
        ErrorCodes IDevice.Errors
        {
            get => errors;
            set => Ensure(ref errors, value);
        }
        ErrorCodes errors;

        /// <summary>
        /// Error codes reported by the AeonServo.
        /// </summary>
        public AeonServoErrorCodes AeonServoErrors => aeonServoErrors;
        AeonServoErrorCodes IDevice.AeonServoErrors
        {
            get => aeonServoErrors;
            set => Ensure(ref aeonServoErrors, value);
        }
        AeonServoErrorCodes aeonServoErrors;

        #endregion Retrieved device values

        /// <summary>
        /// 
        /// </summary>
        public OperationState State
        {
            get => state;
            private set => Ensure(ref state, value);
        }
        OperationState state;

        public override string ToString()
        {
            var sb = new StringBuilder(base.ToString());
            sb.Append($": {Model} S/N: {SerialNumber} {Firmware}");
            sb.Append($": {State}");
            var sb2 = new StringBuilder();
            sb2.Append($"\r\nCh: {SelectedActuator}");
            sb2.Append($"\r\nSPS: {Voltage:0.00} V");

            sb.Append(Utility.IndentLines(sb2.ToString()));
            return sb.ToString();
        }

        #endregion Class interface properties and methods

        #region IDeviceManager

        protected override IManagedDevice FindSupportedDevice(string name)
        {
            if (Find<ICpwActuator>(name) is ICpwActuator d) return d;
            return null;
        }

        #endregion IDeviceManager

        #region State management
        #endregion State management

        ICpwActuator CurrentActuator => ServiceDevice as ICpwActuator;

        /// <summary>
        /// The most recently transmitted command fragment.
        /// </summary>
        string LastCommand => SerialController.CommandMessage;
        OperationState priorState = OperationState.Free;        // used only for debugging
        IActuatorOperation operation = null;
        bool stopping = false;
        void OperateActuator()
        {
            var a = CurrentActuator;
            bool done = false;

            if (LogEverything && State != priorState) Log.Record($"State = {priorState = State}");

            // was premature stop requested by actuator?
            if (State > OperationState.Configuring && a.StopRequested && !stopping)
                State = OperationState.Stopping;

            switch (State)
            {
                case OperationState.Free:                    
                    State = InitiateOperation(a);
                    break;
                case OperationState.Configuring:
                    State = ConfigureController(a);
                    break;
                case OperationState.Confirming:
                    State = ConfirmConfiguration(a);
                    break;
                case OperationState.Going:
                    State = CommandGo(a);
                    break;
                case OperationState.AwaitingMotion:
                    State = CheckForMotion(a);
                    break;
                case OperationState.Stopping:
                    State = StopOperation(a);
                    break;
                case OperationState.AwaitingStopped:
                    State = CheckForStopped(a);
                    break;
                default:
                    break;
            }
            if (State == OperationState.Free) done = true;
            if (State == OperationState.Failed)
            {
                // is any corrective action needed?
                done = true;
            }
            if (done)
            {
                if (a.Device.Active) a.Device.Active = false;
                State = OperationState.Free;
                if (LogEverything) Log.Record("Operation done.");
            }
            if (LogEverything && State != priorState) Log.Record($"State = {priorState = State}");
        }

        // OperationState == Free
        OperationState InitiateOperation(ICpwActuator a)
        {
            SetServiceValues($"n{ChannelNumber} c");

            operation = a.ValidateOperation(a.FindOperation(ServiceRequest ?? ""));
            a.Device.Operation = operation;
            stopping = false;
            a.Device.Active = true;

            if (LogEverything)
            {
                if (operation == null)
                    Log.Record($"Selecting {a.Name}");
                else
                    Log.Record($"Initiating \"{operation?.Name}\" for {a.Name}");
            }
            return OperationState.Configuring;
        }

        // OperationState == Configuring
        OperationState ConfigureController(ICpwActuator a)
        {
            if (operation == null)
                return OperationState.Free;
            
            var config = $"{a.Config.Settings}";
            if (!(a is IRS232Valve))
                config = $"p{a.Operation.Value} " + config;

            SetServiceValues(config);
            return OperationState.Confirming;
        }

        // OperationState == Confirming
        OperationState ConfirmConfiguration(ICpwActuator a)
        {
            // TODO: Perhaps add a SerialController.StateLoopTimeout callback, and
            // if LastCommand != "r" && command == "r", give the device enough 
            // time to complete the operation.
            if (LastCommand != "r")
            {
                if (LogEverything) Log.Record($"Waiting {40} ms for the device to configure itself.");
                Thread.Sleep(40);       // give the device time to complete the configuration operations
                SetServiceValues("r", 1);
                return State;
            }

            if (!a.Configured)
                return OperationState.Configuring;  // try again...

            if (a.Config.Settings.CurrentLimit > 0 && a.Current > a.IdleCurrentLimit)
            {
                if (LogEverything)
                    Log.Record($"{a.Name}: current limit = {a.Config.Settings.CurrentLimit}; waiting for {a.IdleCurrentLimit}, current = {a.Current}");
                // wait (a limited time?) for idle current
                SetServiceValues("r", 1);

                // The following line was required in the Purdue version; delete it
                // and this comment if the resetting is no longer needed.
                //a.State.Clear();

                return State;
            }


            if (a is IRS232Valve v && v.Device.RS232UpdatesReceived < 1) // Servo config needed
            {
                var state = OperateAeonServo(v, true, 200);     // Servo config should take < 100 ms
                if (!AeonServo.Idle || state != State)
                    if (LogEverything) Log.Record($"Failed to configure Servo.");
                return state;
            }
            return OperationState.Going;
        }
        
        // OperationState == Going
        OperationState CommandGo(ICpwActuator a)
        {
            if (a.Device.ControlPulseEnabled) 
                return OperationState.AwaitingMotion;

            if (LastCommand != "g")
            {
                SetServiceValues("g");
                if (a is IRS232Valve)
                    OperateAeonServo(a);
            }
            else
            {
                if (LogEverything) Log.Record($"Waiting {20} ms for the device to start.");
                Thread.Sleep(20);
                SetServiceValues("r", 1);
            }

            return State;
        }
        
        // OperationState == AwaitingMotion
        OperationState CheckForMotion(ICpwActuator a)
        {
            if (a.InMotion || a.MotionInhibited)
                return OperationState.AwaitingStopped;

            // (CommandedMovement != 0) => MovementNeeded
            if (a is IRS232Valve && CommandedMovement != 0)
                return OperateAeonServo(a);

            //SetServiceValues("r", 1);
            return State;
        }
        
        // OperationState == Stopping
        OperationState StopOperation(ICpwActuator a)
        {
            stopping = true;
            if (a.Device.ControlPulseEnabled)
                SetServiceValues("s");

            if (a is IRS232Valve v && v.Linked && !v.EnoughMatches)
                OperateAeonServo(v);

            return OperationState.AwaitingStopped;
        }

        // OperationState == AwaitingStopped
        OperationState CheckForStopped(ICpwActuator a)
        {
            var aStopped = !a.ControlPulseEnabled;
            var v = a as IRS232Valve;
            var vStopped = v == null || !v.Linked || v.EnoughMatches;
            var vCleared = v == null || (vStopped && v.CommandedMovement == 0);

            if (aStopped)
            {
                if (vCleared)
                    return OperationState.Free;

                if (vStopped)
                {
                    OperateAeonServo(a, true, 100);
                    return State;
                }
            }

            if (!vStopped)
            {
                OperateAeonServo(v, aStopped, 100);
                vStopped = v.EnoughMatches;
            }

            if (!aStopped)
            {
                if (LastCommand != "r")
                {
                    if (LogEverything) Log.Record($"Waiting {20} ms for the device to stop.");
                    Thread.Sleep(20);
                }
                SetServiceValues("r", 1);
            }

            if (LastCommand == "r" && a is IRS232Valve && aStopped != vStopped)
                return OperationState.Stopping;

            return State;
        }

        OperationState OperateAeonServo(ICpwActuator a, bool waitForIdle = false, int timeout = -1)
        {
            if (AeonServo == null)
            {
                Log?.Record($"OperateAeonServo Failed: {nameof(AeonServo)} is null");
                return OperationState.Failed;
            }

            if (!AeonServo.Ready)
            {
                Log?.Record($"OperateAeonServo Failed: {nameof(AeonServo)} isn't ready");
                return OperationState.Failed;
            }

            if (a is IRS232Valve)
            {
                AeonServo.Hurry = true;     // make it check for something to do
                if (waitForIdle)
                {
                    if (LogEverything) Log.Record($"OperateAeonServo: Waiting for AeonServo Idle...");
                    AeonServo.WaitForIdle(timeout);
                    if (LogEverything) Log.Record($"OperateAeonServo: ...AeonServo is Idle.");
                }
                return State;
            }
            else
            {
                Log.Record($"OperateAeonServo Failed: {CurrentActuator?.Name}'s type ({CurrentActuator.GetType()}) isn't supported.");
                return OperationState.Failed;
            }
        }

        #region Controller commands
        string ControllerDataCommand => "z";

        #endregion Controller commands

        #region Controller interactions

        protected override void SelectDeviceService()
        {
            if (LogEverything)
                Log.Record($"SelectDeviceService: Device = {ServiceDevice?.Name}, Request = \"{ServiceRequest}\"");
            SetServiceValues("");       // default to nothing needed

            if (ServiceDevice == this)
            {
                if (Device.UpdatesReceived == 0)
                    SetServiceValues(ControllerDataCommand, 1);
            }
            else if (ServiceDevice is ICpwActuator a)
            {
                if (ServiceRequest != InitServiceRequest)       // these devices don't need to be initialized
                {
                    if (a is IRS232Valve && (AeonServo == null || !AeonServo.Ready))
                    {
                        Log.Record($"ActuatorController {Name} SelectDeviceService: Can't operate {a.Name} because {nameof(AeonServo)} is missing or not Ready.");
                    }
                    // Repeat until there's a command to transmit, or
                    // the operation is complete.
                    do
                        OperateActuator();
                    while (ServiceCommand.IsBlank() && State != OperationState.Free);
                }
            }
            else
            {
                Log.Record($"{ServiceDevice?.Name}'s device type ({ServiceDevice?.GetType()}) is not supported.");
            }
            if (LogEverything)
                Log.Record($"ServiceDevice = {ServiceDevice?.Name}, ServiceCommand = \"{ServiceCommand}\", ResponsesExpected = {ResponsesExpected}");
        }

        protected override bool ValidateResponse(string response, int which)
        {
            try
            {
                var lines = response.GetLines();
                if (lines.Length == 0) return false;
                var values = lines[0].GetValues();
                var n = values.Length;

                if (LastCommand[0] == ControllerDataCommand[0])       // Controller data
                {
                    var line = 0;
                    if (LengthError(lines, 3, "controller data line"))
                        return false;

                    if (LengthError(values, 4, "value", $"on controller data line {line}"))
                        return false;

                    Device.Model = values[2];
                    Device.Firmware = values[3];

                    values = lines[++line].GetValues();
                    n = values.Length;

                    if (LengthError(values, 2, "value", $"on controller data line {line}"))
                        return false;

                    Device.SerialNumber = int.Parse(values[1]);

                    values = lines[++line].GetValues();
                    n = values.Length;

                    if (LengthError(values, 4, "value", $"on controller data line {line}"))
                        return false;

                    Device.CpwMin = int.Parse(values[1]);
                    Device.CpwMax = int.Parse(values[3]);

                    Device.UpdatesReceived++;
                }
                else if (LastCommand[0] == 'r')       // report
                {
                    if (LengthError(lines, 1, "status report line"))
                        return false;

                    if (LengthError(values, 11, "report value"))
                        return false;

                    var i = int.Parse(values[0]);
                    if (ErrorCheck(i < 0 || i >= Channels,
                            $"Invalid channel in status report: {i}"))
                        return false;
                    Device.SelectedActuator = i;

                    var key = $"{i}";
                    if (ErrorCheck(!Devices.ContainsKey(key),
                            $"Report received, but no actuator is assigned to channel {i}"))
                        return false;

                    var a = Devices[key] as ICpwActuator;
                    if (ErrorCheck(a == null,
                            $"The device at {key} isn't a {typeof(ICpwActuator)}"))
                        return false;

                    if (LogEverything)
                        Log.Record($"Response received: {lines[0]}");

                    var cpw = int.Parse(values[1]);
                    a.Device.ControlPulseEnabled = int.Parse(values[2]) == 1;
                    var limit0Enabled = values[3][0] == '1';
                    a.Device.LimitSwitch0Engaged = values[3][1] == '1';
                    var limit1Enabled = values[4][0] == '1';
                    a.Device.LimitSwitch1Engaged = values[4][1] == '1';
                    var currentLimit = int.Parse(values[5]);
                    a.Device.Current = int.Parse(values[6]);
                    var timeLimit = double.Parse(values[7]);
                    a.Device.Elapsed = double.Parse(values[8]);
                    a.Device.Settings = new CpwActuator.OperationSettings(
                        cpw, limit0Enabled, limit1Enabled, currentLimit, timeLimit);
                    Device.Voltage = double.Parse(values[9]);
                    ErrorCodes errors = (ErrorCodes)int.Parse(values[10]);
                    a.Device.Errors = errors & ActuatorErrorFilter;  // device-specific errors
                    Device.Errors = errors & ~ActuatorErrorFilter;   // other (controller) errors
                    a.Device.UpdatesReceived++;
                }
                else
                {
                    if (LogEverything)
                        Log.Record($"Unrecognized response");
                    return false;       // unrecognized response
                }
                if (LogEverything)
                    Log.Record($"Response successfully decoded");
                return true;
            }
            catch (Exception e)
            {
                //if (LogEverything)
                Log.Record($"{e}");
                return false;
            }
        }

        #region controller response validation helpers

        // TODO: These two helper methods are present in multiple places.
        // Where should they be moved to?
        bool ErrorCheck(bool errorCondition, string errorMessage)
        {
            if (errorCondition)
            {
                if (LogEverything)
                    Log.Record($"{errorMessage}");
                return true;
            }
            return false;
        }
        bool LengthError(object[] elements, int nExpected, string elementDescription = "value", string where = "")
        {
            var n = elements.Length;
            if (!where.IsBlank()) where = $" {where}";
            return ErrorCheck(n != nExpected,
                $"Expected {ToUnitsString(nExpected, elementDescription)}{where}, not {n}.");
        }

        #endregion controller response validation helpers

        #endregion Controller interactions

        #region AeonServo

        protected virtual SerialController.Command SelectAeonServoService()
        {
            if (!(CurrentActuator is IRS232Valve v))
                return SerialController.DefaultCommand;

            string command = "";
            switch (State)
            {
                case OperationState.Confirming:
                    if (v.Device.RS232UpdatesReceived < 1 || v.CommandedMovement != 0)
                        command = "c r";
                    break;
                case OperationState.Going:
                    if (v.CommandedMovement == 0 && NeedsToMove(v))
                        command = $"g{CommandedMovement} r";
                    break;
                case OperationState.AwaitingMotion:
                    // (CommandedMovement != 0) => MovementNeeded // valid after NeedsToMove()
                    // (v.CommandedMovement == 0) => MovementCommandNotDetected
                    if (CommandedMovement != 0 && v.CommandedMovement == 0)
                    {
                        if (LogEverything) Log.Record($"CommandedMovement = {CommandedMovement}, v.CommandedMovement = {v.CommandedMovement}");
                        command = v.ControlPulseEnabled && !stopping && !v.StopRequested ? "r" : "s r";
                    }
                    break;
                case OperationState.Stopping:
                case OperationState.AwaitingStopped:
                    if (v.EnoughMatches)
                    {
                        if (v.CommandedMovement != 0)
                            command = "c r";
                    }
                    else
                    {
                        if (LogEverything) Log.Record($"CommandedMovement = {v.CommandedMovement}, Movement = {v.Movement}");
                        var stop = (v.Movement != v.CommandedMovement) &&
                            (stopping || v.StopRequested || !v.ControlPulseEnabled);
                        command = stop ? (v.Movement == 0 || v.CommandedMovement == 0 ? "c r" : "s r") : "r";
                    }
                    break;
                default:
                    break;
            }

            return command.IsBlank() ? SerialController.DefaultCommand :
                new SerialController.Command(command, command.EndsWith("r") ? 1 : 0, true);
        }

        int PriorMovement { get; set; } = 0;
        int CommandedMovement { get; set; }  = 0;
        bool NeedsToMove(IRS232Valve v)
        {
            int tgtpos = v.Operation.Value + (v.Operation.Incremental ? v.Position : 0);
            if (tgtpos > v.MaximumPosition) tgtpos = v.MaximumPosition;
            if (tgtpos < v.MinimumPosition) tgtpos = v.MinimumPosition;

            PriorMovement = 0;
            CommandedMovement = tgtpos - v.Position;

            if (LogEverything)
                Log.Record($"{nameof(AeonServo)}: op.Value = {v.Operation.Value}{v.Operation.Incremental.ToString(" (Inc)", "")}, Pos = {v.Position}, TgtPos = {tgtpos}, Move = {CommandedMovement}");

            return CommandedMovement != 0;
        }


        /// <summary>
        /// Accepts a response string from the SerialController
        /// and returns whether it is a valid response or not.
        /// The default implementation ignores the response
        /// and always returns true.
        /// </summary>
        /// <param name="response"></param>
        /// <returns>true if the response is valid</returns>
        protected virtual bool ValidateAeonServoResponse(string response, int which)
        {
            try
            {
                if (!(CurrentActuator is IRS232Valve v))
                    return true;        // ignore the response

                var lines = response.GetLines();
                if (lines.Length == 0) return false;
                var values = lines[0].GetValues();
                var n = values.Length;

                var command = AeonServoCommandChar;
                if (command == '\0')
                {
                    if (LogEverything)
                        Log.Record($"{nameof(AeonServo)}: Unexpected response");
                    return false;
                }
                else if (command == 'r')       // status report
                {
                    if (LengthError(lines, 1, "status report line"))
                        return false;

                    if (LengthError(values, 4, "report value"))
                        return false;

                    var priorCommandedMovement = v.CommandedMovement;
                    v.Device.CommandedMovement = int.Parse(values[0]);
                    v.Device.Movement = int.Parse(values[1]);
                    v.Device.ControlOutput = int.Parse(values[2]);
                    v.Device.RS232UpdatesReceived++;

                    if (v.CommandedMovement != 0)
                        v.Device.Position += v.Movement - PriorMovement;
                    PriorMovement = v.Movement;

                    if (v.RS232UpdatesReceived > 1 && v.Movement == v.CommandedMovement && v.CommandedMovement == priorCommandedMovement)
                        v.Device.ConsecutiveMatches++;
                    else
                        v.Device.ConsecutiveMatches = 0;

                    if (LogEverything)
                        Log.Record($"{nameof(AeonServo)}: ConsecutiveMatches = {v.ConsecutiveMatches}");
                    Device.AeonServoErrors = (AeonServoErrorCodes)int.Parse(values[3]);

                    // StateSignal.Set()?
                    // SerialController.Hurry = true;    // cue the controller, to monitor servo current and time limits
                }
                else if (command == 'z')       // Controller data
                {
                    if (LengthError(lines, 1, "controller data line"))
                        return false;

                    if (LengthError(values, 4, "value", "on controller data line 1"))
                        return false;

                    Device.Model = values[2];
                    Device.Firmware = values[3];
                    Device.UpdatesReceived++;
                }
                else
                {
                    if (LogEverything)
                        Log.Record($"{nameof(AeonServo)}: Unrecognized response");
                    return false;       // unrecognized response
                }
                if (LogEverything)
                    Log.Record($"{nameof(AeonServo)}: Response successfully decoded");

                return true;
            }
            catch (Exception e)
            {
                //if (LogEverything)
                Log.Record($"{nameof(AeonServo)}: {e}");
                return false;
            }
        }

        char AeonServoCommandChar => FinalChar(AeonServo.ServiceCommand);
        char FinalChar(string s) => s.IsBlank() ? '\0' : s[^1];


        /// <summary>
        /// What to do if the SerialController loses contact with
        /// the hardware.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnAeonServoLost(object sender, EventArgs e)
        {
            if (LogEverything) Log.Record($"SerialController {AeonServo.Name} lost connection");
            // do nothing?
        }

        #endregion AeonServo
    }
}
