using System.Collections.Generic;
using Utilities;
using System.Xml.Serialization;
using System.Threading;
using HACS.Core;
using System;
using System.Linq;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
    public interface IValve : IOperatable
    {
        ValveStates ValveState { get; }
        bool Ready { get; }
        void Open();
        void Close();
        bool IsOpened { get; }
        bool IsClosed { get; }
        void WaitForIdle();
        void OpenWait();
        void CloseWait();
        void Exercise();
    }

    // A valve with a more precise actuator (e.g., a flow metering valve).
    public interface IXValve : IValve
    {
        int Position { get; set; }              // Abosolute position "Value"
        int PositionMin { get; set; }
        int PositionMax { get; set; }
        int OpenedValue { get; set; }
        int ClosedValue { get; set; }
        int OneTurn { get; set; }               // unique Positions per turn
        int ClosedOffset { get; set; }          // The Closed position is this much open from the current-limited stop
        int CoarseCurrentLimit { get; set; }    // milliamps
        int FineCurrentLimit { get; set; }
        void Calibrate();
    }

    /// <summary>
    /// This is just a convenience class for Finding HacsComponents that implement IValve.
    /// The returned objects will be some other class, that doesn't even derive from this one.
    /// </summary>
    public static class Valve
    {
        public static IValve Find(string name) =>
            HacsComponent.List.Where(x => x.Name == name && x is IValve).First() as IValve;
    }

    /// <summary>
    /// A Switchbank-controlled valve.
    /// </summary>
    public class SolenoidValve : OnOffDevice, IValve
    {
        #region Component Implementation

        public static readonly new List<SolenoidValve> List = new List<SolenoidValve>();
        public static new SolenoidValve Find(string name) { return List.Find(x => x?.Name == name); }

        public SolenoidValve()
        {
            List.Add(this);
        }

        #endregion Component Implementation

        public bool Ready => !Controller.Disconnected;

        /// <summary>
        /// The powered valve state; either Closed or Opened. A "Normally Closed"
        /// valve will have PoweredState == Opened;
        /// </summary>
        public ValveStates PoweredState { get; set; } = ValveStates.Opened;

        /// <summary>
        /// The valve never takes longer than this to physically change states.
        /// With no means to check the physical valve state, this time is treated as
        /// if required to assure the requested state has been reached.
        /// </summary>
        public int MillisecondsToChangeState { get; set; }

        /// <summary>
        /// The valve could potentially be in motion if this value is true.
        /// </summary>
        public bool InMotion => Active;
        ValveStates motionState(ValveStates vstate) => vstate == ValveStates.Opened ? ValveStates.Opening : ValveStates.Closing;
        ValveStates presentState(ValveStates vstate) => InMotion ? motionState(vstate) : vstate;

        public ValveStates ValveState => presentState((PoweredState == ValveStates.Opened) == ActiveState ? ValveStates.Opened : ValveStates.Closed);
         
        public List<string> Operations { get; } = new List<string> { "Close", "Open" };

        /// <summary>
        /// The number of operations which have been submitted 
        /// to the Controller for this valve, but which have
        /// not been completed.
        /// </summary>
        [XmlIgnore] public int PendingOperations = 0;

        public void DoOperation(string operation)
        {
            lock (this) { PendingOperations++; }
            if (operation == "Open") Open(); else Close();
        }

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
                }
                _Active = value;
            }
        }
        bool _Active = false;

        /// <summary>
        /// Set by the controller when this valve becomes Active.
        /// </summary>
        public bool ActiveState { get { return Active ? _ActiveState : IsReallyOn; } set { _ActiveState = value; } }
        bool _ActiveState;

        // Whether this actuator currently has any actions pending
        [XmlIgnore] public virtual bool Idle => PendingOperations == 0;

        public virtual void WaitForIdle() { while (!Idle) Thread.Sleep(5); }
        public void Close() => TurnOnOff(PoweredState == ValveStates.Closed);
        public void Open() => TurnOnOff(PoweredState == ValveStates.Opened);
        public void CloseWait() { Close(); WaitForIdle(); }
        public void OpenWait() { Open(); WaitForIdle(); }
        public bool IsClosed => ValveState == ValveStates.Closed;
        public bool IsOpened => ValveState == ValveStates.Opened;

        public void Exercise()
        {
            if (Idle)
            {
                if (IsOpened)
                { Close(); Open(); }
                else if (IsClosed)
                { Open(); Close(); }
            }
        }

        public override string ToString()
        {
            return $"{Name} ({Channel}): {ValveState}";
        }
    }


    /// <summary>
    /// A valve moved pneumatically via an air solenoid valve,
    /// controlled by a Switchbank.
    /// In instances of this class, the PoweredState property must reflect 
    /// the valve state of the pneumatic valve when the solenoid valve is
    /// powered, and not the valve state of the solenoid valve (i.e., regardless
    /// of whether the powered solenoid valve is opened or closed, the consequent
    /// state of the pneumatic valve is what matters).
    /// </summary>
    public class PneumaticValve : SolenoidValve { }

       
    /// <summary>
    /// A valve whose position is determined by a 
    /// control pulse width signal, like an RC servo.
    /// </summary>
    public class CpwValve : CpwActuator, IValve
    {
        #region Component Implementation

        public static readonly new List<CpwValve> List = new List<CpwValve>();
        public static new CpwValve Find(string name) { return List.Find(x => x?.Name == name); }

        protected override void Connect()
        {
            CpwActuatorState.StateChanged = ValveStateChanged;
            base.Connect();
        }

        protected override void Initialize()
        {
            //base.Initialize();
            OpenedValue = FindOperation("Open")?.Value ?? 1750;
            ClosedValue = FindOperation("Close")?.Value ?? 1250;
            ValveStateChanged();
        }

        public CpwValve()
        {
            List.Add(this);
        }

        #endregion Component Implementation

        public bool Ready => !Controller.Disconnected;
        [XmlIgnore] public int OpenedValue { get; set; }
        [XmlIgnore] public int ClosedValue { get; set; }
        public int Center => (OpenedValue + ClosedValue) / 2;
        public bool OpenIsPositive => OpenedValue >= ClosedValue;

        protected virtual ValveStates cmdDirection(int cmd, int refcmd)
        {
            if (cmd == refcmd) // they match; return the abolute direction
                refcmd = Center;

            if (cmd == refcmd)                  // both command and ref are center
                return ValveStates.Unknown;      // direction can't be determined
            else if ((cmd > refcmd) == OpenIsPositive)
                return ValveStates.Opening;
            else
                return ValveStates.Closing;
        }

        public virtual ValveStates OperationDirection(ActuatorOperation operation)
        {
            if (operation == null)
            {
                if (Operation == null)
                    return ValveStates.Unknown;
                else
                    return OperationDirection(Operation);
            }

            if (operation.Incremental)
            {
                if (operation.Value == 0)
                    return ValveStates.Unknown;
                return cmdDirection(operation.Value, 0);
            }

            if (Operation == null)
                return cmdDirection(operation.Value, Center);

            return cmdDirection(operation.Value, Operation.Value);
        }

        public ValveStates LastMotion => OperationDirection(null);
        
        public override ActuatorOperation ValidateOperation(ActuatorOperation operation)
        {
            var dir = OperationDirection(operation);
            if ((dir == ValveStates.Closing && IsClosed) || (dir == ValveStates.Opening && IsOpened))
                return null;
            else
                return operation;
        }

        // called whenever the valve is "Active" and a report is received,
        // and also once when the valve becomes inactive
        public virtual void ValveStateChanged()
        {
            if (Operation != null)
            {
                var dir = OperationDirection(Operation);  // normally "Opening" or "Closing"

                ValveState =
                    Active ?
                        dir :
                    (PositionDetectable ? !LimitDetected : !ActionSucceeded) ? ValveStates.Unknown :
                    dir == ValveStates.Opening ? ValveStates.Opened :
                    dir == ValveStates.Closing ? ValveStates.Closed :
                    ValveStates.Unknown;
            }
            StateChanged?.Invoke();
        }

		[JsonProperty]
		public virtual ValveStates ValveState { get; set; }

        /// <summary>
        /// Opens the valve.
        /// </summary>
        public virtual void Open() => DoOperation("Open");

        /// <summary>
        /// Closes the valve.
        /// </summary>
        public virtual void Close() => DoOperation("Close");

        [XmlIgnore]
        public virtual bool IsOpened => ValveState == ValveStates.Opened;

        [XmlIgnore]
        public virtual bool IsClosed => ValveState == ValveStates.Closed;

        /// <summary>
        /// Command the valve to Open() and then WaitForIdle(),
        /// i.e., until it has no commanded operations pending.
        /// </summary>
        public void OpenWait() { Open(); WaitForIdle(); }

        /// <summary>
        /// Command the valve to Close() and then WaitForIdle(),
        /// i.e., until it has no commanded operations pending.
        /// </summary>
        public void CloseWait() { Close(); WaitForIdle(); }

        /// <summary>
        /// Cycles the valve state, provided that it is idle.
        /// </summary>
        public virtual void Exercise()
        {
            if (Idle)
            {
                if (IsOpened)
                { Close(); Open(); }
                else if (IsClosed)
                { Open(); Close(); }
            }
        }


        public override string ToString()
        {
            string s = string.IsNullOrEmpty(CpwActuatorState.Report) ? "" : CpwActuatorState.ReportHeader + CpwActuatorState.Report + "\r\n";
            s += CpwActuatorState.ToString() + "\r\n";
            s += $"PendingOperations:{PendingOperations} LastMotion:{LastMotion} Succeeded:{ActionSucceeded}";

            return $"{Name} ({ValveState}) \r\n" +
                Utility.IndentLines(s);
        }
    }


    /// <summary>
    /// A valve that receives commands and responds using
    /// RS232 serial communications.
    /// </summary>
    public class RS232Valve : CpwValve, IXValve
    {
        #region Component Implementation

        public static readonly new List<RS232Valve> List = new List<RS232Valve>();
        public static new RS232Valve Find(string name) { return List.Find(x => x?.Name == name); }

        public RS232Valve()
        {
            List.Add(this);
        }

        #endregion Component Implementation

        [XmlIgnore] public RS232ActuatorState RS232ActuatorState = new RS232ActuatorState();

        public bool WaitingToGo => RS232ActuatorState.WaitingToGo;

        public override bool InMotion => base.InMotion && RS232ActuatorState.InMotion;

        [XmlIgnore]
        public override bool Active
        {
            get { return base.Active; }
            set
            {
                base.Active = value;
                RS232ActuatorState.Active = value;
                RS232ActuatorState.StateChanged?.Invoke();
            }
        }

        public bool ControllerStopped => CpwActuatorState.Stopped;
        public bool ActuatorStopped => RS232ActuatorState.Stopped;
        public override bool Stopped => ControllerStopped && ActuatorStopped;

		[JsonProperty]
		public int Position { get; set; } = 0;          // "Value"?
		[JsonProperty]
		public int PositionMin { get; set; } = 0;
		[JsonProperty]
		public int PositionMax { get; set; } = 0;
		[JsonProperty]//, DefaultValue(96)]
		public int OneTurn { get; set; } = 96;          // unique Positions per turn
		[JsonProperty]//, DefaultValue(10)]
		public int ClosedOffset { get; set; } = 10;     // Closed position is this much open from the current-limited stop
		[JsonProperty]
		public int CoarseCurrentLimit { get; set; }     // milliamps
		[JsonProperty]
		public int FineCurrentLimit { get; set; }

        public override ActuatorOperation ValidateOperation(ActuatorOperation operation)
        {
            if (isCalibrating) return operation;       // trust the calibration routine
            return base.ValidateOperation(operation);
        }

        public override void DoOperation(string operationName)
        {
            if (operationName == "Calibrate")
                Calibrate();
            else
                base.DoOperation(operationName);
        }


        // called whenever the valve is "Active" and a report is received,
        // and also once when the valve becomes inactive
        public override void ValveStateChanged()
        {
            StateChanged?.Invoke();
        }

        public override ValveStates ValveState =>
            Active ? OperationDirection(Operation) :
            Position == OpenedValue ? ValveStates.Opened :
            Position == ClosedValue ? ValveStates.Closed :
            ValveStates.Unknown;

        /// <summary>
        /// Disable exercising these valves.
        /// </summary>
        public override void Exercise() { }

        private bool isCalibrating = false;
        /// <summary>
        /// Calibrates the valve position based on the torque required to turn it.
        /// Returns with the valve in the calibrated closed position.
        /// </summary>
        public void Calibrate()
        {
            WaitForIdle();

            isCalibrating = true;

            // Every valve should have a "Close" command, intended to close it as fully as possible
            int closedPosition = FindOperation("Close")?.Value ?? PositionMax - ClosedOffset;

            // PositionMax integrity check? (in case calibrate is aborted with PositionMax altered)
            if (PositionMax != closedPosition + ClosedOffset)
                PositionMax = closedPosition + ClosedOffset;

            int openOneTurn = -OneTurn;
            int coarseCommand = 5;
            int fineCommand = 1;
            int restMilliseconds = 400;

            ActuatorOperation operation = new ActuatorOperation()
            {
                Name = "Move",
                Value = openOneTurn,
                Incremental = true
            };

            if (Position > closedPosition + 2 * openOneTurn)
            {
                CpwActuatorState.Clear();
                DoOperation(operation);
                WaitForIdle();
                Thread.Sleep(restMilliseconds);
            }

            operation.Value = coarseCommand;
            operation.Configuration = $"i{CoarseCurrentLimit}";
            CpwActuatorState.Clear(); RS232ActuatorState.Clear();
            do
            {
                ClosedValue = PositionMax = Position + operation.Value + 1;
                DoOperation(operation);
                WaitForIdle();
                Thread.Sleep(restMilliseconds);
            } while (!CpwActuatorState.CurrentLimitDetected);

            operation.Value = -3 * coarseCommand;
            operation.Configuration = "";       // no current limit
            DoOperation(operation);
            WaitForIdle();
            Thread.Sleep(3 * restMilliseconds);

            operation.Value = fineCommand;
            operation.Configuration = $"i{FineCurrentLimit}";
            CpwActuatorState.Clear(); RS232ActuatorState.Clear();
            do
            {
                ClosedValue = PositionMax = Position + operation.Value + 1;
                DoOperation(operation);
                WaitForIdle();
                Thread.Sleep(restMilliseconds);
            } while (!CpwActuatorState.CurrentLimitDetected);

            PositionMax = closedPosition + ClosedOffset;
            Position = PositionMax + 1;
            ClosedValue = closedPosition;
            CloseWait();

            isCalibrating = false;
        }


        public override string ToString()
        {
            string s = string.IsNullOrEmpty(CpwActuatorState.Report) ? "" : CpwActuatorState.ReportHeader + CpwActuatorState.Report + "\r\n";
            s += CpwActuatorState.ToString() + "\r\n";
            s += $"PendingOperations:{PendingOperations} LastMotion:{LastMotion} Succeeded:{ActionSucceeded}";

            if (!string.IsNullOrEmpty(RS232ActuatorState.Report))
                s += RS232ActuatorState.ReportHeader + RS232ActuatorState.Report + "\r\n";
            s += RS232ActuatorState.ToString() + "\r\n";


            return $"{Name} ({ValveState}: {Position}):\r\n" +
                Utility.IndentLines(s);
        }
    }

}