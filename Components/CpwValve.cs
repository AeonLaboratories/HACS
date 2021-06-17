using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
    /// <summary>
    /// A valve whose position is determined by a 
    /// control signal's pulse width, like an RC servo.
    /// </summary>
    public class CpwValve : CpwActuator, ICpwValve, CpwValve.IDevice, CpwValve.IConfig
    {

        #region Device interfaces

        public new interface IDevice : CpwActuator.IDevice, Valve.IDevice { }
        public new interface IConfig : CpwActuator.IConfig, Valve.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region Valve

        Valve.IDevice IValve.Device => this;
        Valve.IConfig IValve.Config => this;

        [JsonProperty]
        public virtual ValveState ValveState
        {
            get => valveState;
            protected set => Ensure(ref valveState, value); 
        }
        ValveState valveState = ValveState.Unknown;
        ValveState Valve.IDevice.ValveState
        {
            get => ValveState;
            set => ValveState = value;
        }

        public virtual bool IsOpened => ValveState == ValveState.Opened;
        public virtual bool IsClosed => ValveState == ValveState.Closed;
        public virtual void Open() => DoOperation("Open");
        public virtual void Close() => DoOperation("Close");
        public virtual void OpenWait() { Open(); WaitForIdle(); }
        public virtual void CloseWait() { Close(); WaitForIdle(); }
        public virtual void DoWait(ActuatorOperation operation) { DoOperation(operation) ; WaitForIdle(); }

        public virtual void Exercise()
        {
            if (Idle)
            {
                if (IsOpened)
                { Close(); OpenWait(); }
                else if (IsClosed)
                { Open(); CloseWait(); }
            }
        }

        #endregion Valve

        public virtual int Position
        { 
            get => Operation?.Value ?? CenterValue;
            protected set { }
        }

        [JsonProperty, DefaultValue(0.0)]
        public virtual double OpenedVolumeDelta
        {
            get => openedVolumeDelta;
            set => Ensure(ref openedVolumeDelta, value);
        }
        double openedVolumeDelta = 0.0;

        protected override void OnOperationChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ObservableItemsCollection<ActuatorOperation> list && e == null)
            {
                foreach (var op in list)
                    OnOperationChanged(op, null);
            }
            else if (sender is ActuatorOperation op)
            {
                UpdateOpenedAndClosedValues(op);
            }
        }    

        void UpdateOpenedAndClosedValues(ActuatorOperation op)
        {
            if (op.Name == "Open")
                OpenedValue = op.Value;
            else if (op.Name == "Close")
                ClosedValue = op.Value;
        }

        /// <summary>
        /// Position at which the valve is in the opened state.
        /// </summary>
        public virtual int OpenedValue
        {
            get => openedValue ?? 0;
            protected set => Ensure(ref openedValue, value);
        }
        int? openedValue;

        /// <summary>
        /// Position at which the valve is in the closed state.
        /// </summary>
        public virtual int ClosedValue
        {
            get => closedValue ?? 0;
            protected set => Ensure(ref closedValue, value);
        }
        int? closedValue;

        public virtual int CenterValue => (OpenedValue + ClosedValue) / 2;

        public virtual bool OpenIsPositive => OpenedValue >= ClosedValue;


        protected virtual ValveState cmdDirection(int cmd, int refcmd)
        {
            if (cmd == refcmd) // they match; return the abolute direction
                refcmd = CenterValue;

            if (cmd == refcmd)                  // both command and ref are center
                return ValveState.Unknown;      // direction can't be determined
            else if ((cmd > refcmd) == OpenIsPositive)
                return ValveState.Opening;
            else
                return ValveState.Closing;
        }

        protected virtual ValveState OperationDirection(IActuatorOperation operation)
        {
            if (operation == null)
            {
                if (Operation == null)
                    return ValveState.Unknown;
                else
                    return OperationDirection(Operation);
            }

            if (operation.Incremental)
            {
                if (operation.Value == 0)
                    return ValveState.Unknown;
                return cmdDirection(operation.Value, 0);
            }

            return cmdDirection(operation.Value, Position);
        }

        public ValveState LastMotion =>
            ValveState == ValveState.Opened ? ValveState.Opening :
            ValveState == ValveState.Closed ? ValveState.Closing :
            OperationDirection(null);
        public override bool ActionSucceeded => base.ActionSucceeded && LastMotion != ValveState.Unknown && !StopRequested;

        public override IActuatorOperation ValidateOperation(IActuatorOperation operation)
        {
            var dir = OperationDirection(operation);
            if ((dir == ValveState.Closing && IsClosed) || (dir == ValveState.Opening && IsOpened))
                return null;
            else
                return operation;
        }

        // Called when the valve becomes "Active", whenever a report 
        // is received while active, and finally, once when the valve 
        // becomes inactive.
        protected virtual void UpdateValveState()
        {
            if (Operation != null)
            {
                var dir = OperationDirection(Operation);  // normally "Opening" or "Closing"

                ValveState =
                    Active ?
                        dir :
                    (PositionDetectable ? !LimitSwitchDetected : !ActionSucceeded) ? ValveState.Unknown :
                    dir == ValveState.Opening ? ValveState.Opened :
                    dir == ValveState.Closing ? ValveState.Closed :
                    ValveState.Unknown;
            }
        }

        public override bool Active 
        { 
            get => base.Active;
            protected set
            { 
                base.Active = value; 
                UpdateValveState();
            } 
        }

        public override long UpdatesReceived
        { 
            get => base.UpdatesReceived;
            protected set
            {
                base.UpdatesReceived = value;
                UpdateValveState();
            }
        }

        public CpwValve(IHacsDevice d = null) : base(d) { }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Name}: {ValveState}");
            var sb2 = new StringBuilder();
            sb2.Append($"\r\nPending Operations: {PendingOperations}");
            sb2.Append(Active ? $", Motion: {LastMotion}" : $", Last Motion: {LastMotion}");
            if (LastMotion != ValveState.Unknown)
            {
                if (Active)
                    sb2.Append(StopRequested ? ", Stopping" : ", Active");
                else
                    sb2.Append(StopRequested ? ", Stopped" : ActionSucceeded ? ", Succeeded" : ", Failed");
            }
            if (Operation != null)
            {
                var which = Active ? "Current" : "Prior";
                sb2.Append($"\r\n{which} Operation: \"{Operation.Name}\", Value: {Operation.Value}, Updates Received: {UpdatesReceived}");
                if (UpdatesReceived > 0)
                {
                    var si = Device.Settings.CurrentLimit > 0 ? $"Current: {Current} / {Device.Settings.CurrentLimit} mA" : "";
                    var st = Device.Settings.TimeLimit > 0 ? $"Time: {Elapsed} / {Device.Settings.TimeLimit} s" : "";
                    var slim0 = Device.Settings.Limit0Enabled ? $"Limit0: {LimitSwitch0Engaged.ToString("Engaged", "Enabled")}" : "";
                    var slim1 = Device.Settings.Limit1Enabled ? $"Limit1: {LimitSwitch1Engaged.ToString("Engaged", "Enabled")}" : "";
                    var all = string.Join(" ", si, st, slim0, slim1);
                    if (all.Length > 0)
                        sb2.Append($"\r\n{all}");
                }
            }
            if (Manager != null)
                sb2.Append($"\r\n{Manager.Name}[{Manager.Keys[this]}]");
            sb.Append(Utility.IndentLines(sb2.ToString()));
            return sb.ToString();
        }
    }
}
