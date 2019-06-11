using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using HACS.Core;
using Utilities;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;

namespace HACS.Components
{
    public class ActuatorOperation
    {
        [XmlAttribute]
        public string Name;             // a name for the operation
        public int Value;               // typically position or movement amount code
        public bool Incremental;        // whether Value is absolute or incremental
        public string Configuration;    // typically a space-delimited list of controller commands

        public override string ToString()
        {
            return $"{Name}: {Value} {(Incremental ? "Inc" : "Abs")} \"{Configuration}\"";
        }
    }

    public interface IActuator : IOperatable
    {
        bool Active { get; set; }
        ActuatorOperation Operation { get; set; }
        bool Configured { get; }
        bool InMotion { get; }
        bool MotionInhibited { get; }
        bool Stopping { get; }
        bool Stopped { get; }
        bool Idle { get; }
        void WaitForIdle();
    }

    /// <summary>
    /// An actuator whose position is defined by a control pulse width like an RC servo.
    /// </summary>
    public class CpwActuator : HacsComponent, IActuator
    {
        #region Component Implementation

        public static readonly new List<CpwActuator> List = new List<CpwActuator>();
        public static new CpwActuator Find(string name) { return List.Find(x => x?.Name == name); }

        protected virtual void Connect()
        {
            Controller?.Connect(this);
        }

        protected virtual void Initialize()
        {
            StateChanged?.Invoke();
        }

        public CpwActuator()
        {
            List.Add(this);
            OnConnect += Connect;
            OnInitialize += Initialize;
        }

		#endregion Component Implementation

		[JsonProperty]
		public HacsComponent<ActuatorController> ControllerRef { get; set; }
        protected ActuatorController Controller => ControllerRef?.Component;
		[JsonProperty]
		public int Channel { get; set; }

		/// <summary>
		/// A list of actuator operations that this actuator supports.
		/// </summary>
		[JsonProperty]
		public virtual List<ActuatorOperation> ActuatorOperations { get; set; }

        [XmlIgnore] public virtual List<string> Operations => ActuatorOperations?.Select(x => x.Name).ToList();

        /// <summary>
        /// The number of operations which have been submitted 
        /// to the Controller for this valve, but which have
        /// not been completed.
        /// </summary>
        [XmlIgnore] public int PendingOperations = 0;

        // Whether this actuator currently has any actions pending
        public virtual bool Idle => PendingOperations == 0;

        /// <summary>
        /// Whether this actuator is currently being operated by the controller.
        /// </summary>
        /// 
        [XmlIgnore]
        public virtual bool Active
        {
            get { return _Active; }
            set
            {
                if (_Active && !value)
                {
                    lock (this) { PendingOperations--; }
                    Stopping = false;
                }
                _Active = value;
                CpwActuatorState.Active = value;
                CpwActuatorState.StateChanged?.Invoke();
            }
        }
        bool _Active = false;


        /// <summary>
        /// Set by controller when actuator becomes Active. This value
        /// persists (as indication of the prior operation) after actuator
        /// becomes inactive.
        /// </summary>
		[XmlIgnore]
        public virtual ActuatorOperation Operation
        {
            get { return _Operation; }
            set { _Operation = ValidateOperation(value); }
        }
        ActuatorOperation _Operation;

        /// <summary>
        /// Whether the current valve operation is being stopped 
        /// before completion, by a request that occurred after
        /// the operation started.
        /// </summary>
        [XmlIgnore]
        public virtual bool Stopping
        {
            get
            {
                if (!Active || Stopped)
                    _Stopping = false;
                return _Stopping;
            }
            set { _Stopping = value; }
        }
        bool _Stopping = false;

        /// <summary>
        /// Interrupt the valve motion; make it stop.
        /// (Effective only when this valve is Actively being operated).
        /// </summary>
        public virtual void Stop()
        {
            if (Active) Stopping = true;
        }

        /// <summary>
        /// Returns the ActuatorOperation with the given name.
        /// </summary>
        /// <param name="name">The name of the actuator operation</param>
        /// <returns></returns>
        public ActuatorOperation FindOperation(string name) =>
            ActuatorOperations?.Find(x => x?.Name == name);

        /// <summary>
        /// Executes the requested actuator operation.
        /// Invalid or empty operationName is 
        /// </summary>
        /// <param name="operationName"></param>
        public virtual void DoOperation(string operationName)
        {
            if (operationName == "Stop")
                Stop();
            else
                DoOperation(FindOperation(operationName));
        }

        /// <summary>
        /// Requests the controller to schedule the provided operation.
        /// </summary>
        /// <param name="operation">the null operation represents a "select" functionality</param>
        public virtual void DoOperation(ActuatorOperation operation)
        {
            lock (this) { PendingOperations++; }
            Controller?.RequestService(this, operation);
        }

        /// <summary>
        /// Validates the operation, substituting an alternative if the one supplied
        /// is not valid.
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public virtual ActuatorOperation ValidateOperation(ActuatorOperation operation)
        {
            return operation;  // no validation
        }

        public virtual void WaitForIdle() { while (!Idle) Thread.Sleep(35); }

        [XmlIgnore] public CpwActuatorState CpwActuatorState = new CpwActuatorState();


        bool enablesLimit0(string config) => config != null && config.Contains("l10");
        bool enablesLimit1(string config) => config != null && config.Contains("l11");

        bool disablesLimit0(string config) => config != null && config.Contains("l-10");
        bool disablesLimit1(string config) => config != null && config.Contains("l-11");

        bool LimitSwitchEnabled => Operation != null && (enablesLimit0(Operation.Configuration) || enablesLimit1(Operation.Configuration));

        public virtual bool LimitsMatch(string config)
        {
            int currentLimit = 0;
            double timeLimit = 0;

            foreach (var token in config.Split(' '))
            {
                if (token[0] == 'i')
                    int.TryParse(token.Substring(1), out currentLimit);
                else if (token[0] == 't')
                    double.TryParse(token.Substring(1), out timeLimit);
            }

            return
                (enablesLimit0(config) ? CpwActuatorState.Limit0Enabled : disablesLimit0(config) ? !CpwActuatorState.Limit0Enabled : true) &&
                (enablesLimit1(config) ? CpwActuatorState.Limit1Enabled : disablesLimit1(config) ? !CpwActuatorState.Limit1Enabled : true) &&
                CpwActuatorState.CurrentLimit == currentLimit &&
                CpwActuatorState.TimeLimit == timeLimit;
        }

        public virtual bool ConfigurationMatches()
        {
            if (Operation == null || Operation.Name == "Select")
                return true;
            else
            {
                return
                    CpwActuatorState.CPW == Operation.Value &&
                    LimitsMatch(Operation.Configuration);
            }
        }

        /// <summary>
        /// True if the reported controller parameters match the current
        /// Operation's configuration string.
        /// </summary>
        public bool Configured => CpwActuatorState.ReportValid && ConfigurationMatches();

        public virtual bool PositionDetectable => Operation != null && LimitSwitchEnabled;

        public virtual bool MotionInhibited => CpwActuatorState.MotionInhibited;

        public virtual bool InMotion => CpwActuatorState.InMotion;

        /// <summary>
        /// Whether valve motion has ceased.
        /// </summary>
        public virtual bool Stopped => CpwActuatorState.Stopped;

        public virtual bool LimitDetected => CpwActuatorState.LimitSwitchDetected;

        public virtual bool CurrentLimitDetected => CpwActuatorState.CurrentLimitDetected;

        public virtual bool TimeLimitDetected => CpwActuatorState.TimeLimitDetected;

        public virtual bool ActionSucceeded => Operation == null || (Configured &&
            (LimitDetected || CurrentLimitDetected || TimeLimitDetected));

    }


    public class CpwActuatorState
    {
        // Servo Controller Report Response:
        // SRV __CPW G L0 L1 ILIM ___I __TLIM __ELAP _____V Error
        // ### ##### # ## ## #### #### ###.## ###.## ##.### #####
        public static string ReportHeader = "SRV __CPW G L0 L1 ILIM ___I __TLIM __ELAP _____V Error\r\n";
        public static int ReportLength = ReportHeader.Length;   // line terminator included

        public Action StateChanged;

        public int Channel { get; set; }
        public int CPW { get; set; }            // control pulse width
        public bool CPEnabled { get; set; }     // control pulses enabled
        public bool Limit0Enabled { get; set; }
        public bool Limit1Enabled { get; set; }
        public int CurrentLimit { get; set; }
        public double TimeLimit { get; set; }

        public bool Limit0Engaged { get; set; }
        public bool Limit1Engaged { get; set; }
        public int Current { get; set; }
        public double Elapsed { get; set; }
        public double ControllerVoltage { get; set; }
        public int Errors { get; set; }
        /* errors
		_ErrorServoOutOfRange = (Errors & 1) > 0;
		_ErrorCommandOutOfRange = (Errors & 2) > 0;
		_ErrorTimeLimitOutOfRange = (Errors & 4) > 0;
		_ErrorBothLimitSwitchesEngaged = (Errors & 8) > 0;
		_ErrorLowSpsVoltage = (Errors & 16) > 0 ?;
		_ErrorInvalidCommand = (Errors & 32) > 0 ?;
		_ErrorCurrentLimitOutOfRange = (Errors & 64) > 0;
		_ErrorInvalidStopLimit = (Errors & 128) > 0;
		_ErrorDataLoggingIntervalOutOfRange = (Errors & 256) > 0;
		_ErrorRS232InputBufferOverflow = (Errors & 512) > 0;
		*/

        public int ReportCount = 0; // reports received since last Clear()
        public bool ReportValid { get; set; }
        public string Report
        {
            get { return _Report; }
            set
            {
                if (Active) // ignore reports when controller is not operating the actuator
                {
                    _Report = value;
                    ReportCount++;
                    ReportValid = InterpretReport();
                    if (ReportValid)
                        StateChanged?.Invoke();
                }
            }
        }
        string _Report;

        public virtual bool Active
        {
            get { return _Active; }
            set
            {
                if (value) Clear();
                _Active = value;
            }
        }
        bool _Active;   // whether this Actuator is currently being operated

        public CpwActuatorState()
        {
            Clear();
        }

        public virtual void Clear()
        {
            ReportValid = false;
            Current = 0;
            Elapsed = 0;
            ReportCount = 0;
        }

        public virtual bool InterpretReport()
        {
            try
            {
                // parse the report values
                //           1         2         3         4         5         6
                // 0123456789012345678901234567890123456789012345678901234567890
                // SRV __CPW G L0 L1 ILIM ___I __TLIM __ELAP _____V Error
                // ### ##### # ## ## #### #### ###.## ###.## ##.### #####
                int rChannel = int.Parse(_Report.Substring(0, 3));  // also parsed by Controller
                int rCPW = int.Parse(_Report.Substring(4, 5));  // control pulse width, in microseconds
                bool rCPEnabled = int.Parse(_Report.Substring(10, 1)) == 1;
                bool rLimit0Enabled = int.Parse(_Report.Substring(12, 1)) == 1;
                bool rLimit0Engaged = int.Parse(_Report.Substring(13, 1)) == 1;
                bool rLimit1Enabled = int.Parse(_Report.Substring(15, 1)) == 1;
                bool rLimit1Engaged = int.Parse(_Report.Substring(16, 1)) == 1;
                int rCurrentLimit = int.Parse(_Report.Substring(18, 4));    // milliamps
                int rCurrent = int.Parse(_Report.Substring(23, 4));         // milliamps
                double rTimeLimit = double.Parse(_Report.Substring(28, 6)); // seconds
                double rElapsed = double.Parse(_Report.Substring(35, 6));   // seconds
                double rControllerVoltage = double.Parse(_Report.Substring(42, 6));   // volts
                int rErrors = int.Parse(_Report.Substring(49, 5));

                // parsing succeeded
                Channel = rChannel;
                CPW = rCPW;
                CPEnabled = rCPEnabled;
                Limit0Enabled = rLimit0Enabled;
                Limit0Engaged = rLimit0Engaged;
                Limit1Enabled = rLimit1Enabled;
                Limit1Engaged = rLimit1Engaged;
                CurrentLimit = rCurrentLimit;
                Current = rCurrent;
                TimeLimit = rTimeLimit;
                Elapsed = rElapsed;
                ControllerVoltage = rControllerVoltage;
                Errors = rErrors;
                return true;
            }
            catch { return false; }
        }

        public virtual bool InMotion => ReportValid && CPEnabled;
        public virtual bool Stopped => ReportValid && !CPEnabled;

        public virtual bool LimitSwitchDetected => ReportValid &&
            Limit0Enabled && Limit0Engaged || Limit1Enabled && Limit1Engaged;

        public virtual bool CurrentLimitDetected => ReportValid &&
            CurrentLimit > 0 && Current >= CurrentLimit;

        public virtual bool TimeLimitDetected => ReportValid &&
            TimeLimit > 0 && Elapsed >= TimeLimit;

        /// <summary>
        /// Motion is prevented by a condition detected by the servo controller
        /// </summary>
        public virtual bool MotionInhibited =>
            LimitSwitchDetected || CurrentLimitDetected || TimeLimitDetected;

        public override string ToString()
        {
            return $"({ReportCount} reports) Ch:{Channel} {CPW}:{(CPEnabled ? 1 : 0)} " +
                $"L0:{(Limit0Engaged ? 1 : 0)}/{(Limit0Enabled ? 1 : 0)} " +
                $"L1:{(Limit1Engaged ? 1 : 0)}/{(Limit1Enabled ? 1 : 0)} " +
                $"I:{Current}/{CurrentLimit} t:{Elapsed:0.00}/{TimeLimit:0.00} {Errors}";
        }
    }

    public class RS232ActuatorState
    {
        // Servo Report Response:
        // --Cmd --Pos ---CO Error
        // ##### ##### ##### #####
        public static string ReportHeader = "--Cmd --Pos ---CO Error\r\n";
        public static int ReportLength = ReportHeader.Length;   // line terminator included

        public Action StateChanged;

        public int ReportCount = 0; // reports received since last Clear()
        public bool ReportValid { get; set; }
        public string Report
        {
            get { return _Report; }
            set
            {
                if (Active) // ignore reports when controller is not operating the actuator
                {
                    _Report = value;
                    ReportCount++;
                    ReportValid = InterpretReport();
                    if (ReportValid)
                        StateChanged?.Invoke();
                }
            }
        }
        string _Report;

        public virtual bool Active
        {
            get { return _Active; }
            set
            {
                if (value) Clear();
                _Active = value;
            }
        }
        bool _Active;   // whether this Actuator is currently being operated

        public RS232ActuatorState()
        {
            Clear();
        }

        public void Clear()
        {
            ReportCount = 0;
            ReportValid = false;
            CommandedMovement = 0;
            Movement = 0;
        }


        public int CommandedMovement { get; set; }
        public int Movement { get; set; }
        public int CO { get; set; }
        public int Errors { get; set; }

        public bool InterpretReport()
        {
            try
            {
                // parse the report values
                //           1         2         3         4         5         6
                // 0123456789012345678901234567890123456789012345678901234567890
                // --Cmd --Pos ---CO Error
                // ##### ##### ##### #####
                int rCommandedMovement = int.Parse(_Report.Substring(0, 5));          // total requested Movement (Position change)
                int rMovement = int.Parse(_Report.Substring(6, 5));         // Movement progress (Position change)
                int rCO = int.Parse(_Report.Substring(12, 5));              // Servo Motor Control Output
                int rErrors = int.Parse(_Report.Substring(18, 5));

                // parsing succeeded
                CommandedMovement = rCommandedMovement;
                Movement = rMovement;
                CO = rCO;
                Errors = rErrors;

                return true;
            }
            catch { return false; }
        }

        public bool WaitingToGo => ReportValid && CommandedMovement == 0;

        public bool InMotion => ReportValid && CommandedMovement != 0 && Movement != CommandedMovement;

        public bool Stopped => ReportValid && Movement == CommandedMovement;

        public override string ToString()
        {
            return $"({ReportCount} reports) Cmd:{CommandedMovement} Pos:{Movement} CO:{CO} {Errors}";
        }
    }
}
