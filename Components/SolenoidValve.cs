using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using static Utilities.Utility;

namespace HACS.Components
{
    /// <summary>
    /// A Switchbank-controlled valve.
    /// </summary>
    public class SolenoidValve : ManagedSwitch, ISolenoidValve, SolenoidValve.IDevice, SolenoidValve.IConfig
    {
        #region Device interfaces

        public new interface IDevice : ManagedSwitch.IDevice, Valve.IDevice { }
        public new interface IConfig : ManagedSwitch.IConfig, Valve.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces


        #region Valve

        Valve.IDevice IValve.Device => this;
        Valve.IConfig IValve.Config => this;


        public virtual ValveState ValveState
        {
            get => presentState(IntendedState);
            protected set { }
        }
        ValveState Valve.IDevice.ValveState
        {
            get => ValveState;
            set => ValveState = value;
        }

        /// <summary>
        /// Absolute position "Value"
        /// </summary>
        public virtual int Position
        {
            get => 0;
            protected set { }
        }

        [JsonProperty, DefaultValue(0.0)]
        public virtual double OpenedVolumeDelta
        {
            get => openedVolumeDelta;
            set => Ensure(ref openedVolumeDelta, value);
        }
        double openedVolumeDelta = 0.0;

        public virtual bool Ready => Manager?.Ready ?? false;
        /// <summary>
        /// Whether this actuator currently has any actions pending
        /// </summary>
        public bool Idle => PendingOperations == 0;

        public bool IsOpened => ValveState == ValveState.Opened;
        public bool IsClosed => ValveState == ValveState.Closed;
        public void Open() => DoOperation("Open");
        public void Close() => DoOperation("Close");
        public void OpenWait() { Open(); WaitForIdle(); }
        public void CloseWait() { Close(); WaitForIdle(); }

        public void WaitForIdle() =>
            WaitForCondition(() => Idle, -1, 5);

        public void Exercise()
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



        /// <summary>
        /// The powered valve state; either Closed or Opened. A "Normally Closed"
        /// valve will have PoweredState == Opened;
        /// </summary>
        [JsonProperty, DefaultValue(ValveState.Opened)]
        public ValveState PoweredState { get; set; } = ValveState.Opened;

        /// <summary>
        /// The valve never takes longer than this to physically change states.
        /// With no means to check the physical valve state, this time is treated as
        /// if required to assure the requested state has been reached.
        /// </summary>
        [JsonProperty, DefaultValue(30)]
        public int MillisecondsToChangeState { get; set; } = 30;

        /// <summary>
        /// The valve could potentially be in motion if this value is true.
        /// </summary>
        public bool InMotion { get; protected set; }

        /// <summary>
        /// The valve motion state, i.e., "Opening" or "Closing", that produces the given valve state.
        /// </summary>
        /// <param name="vstate">Opened or Closed</param>
        ValveState motionState(ValveState vstate) =>
            vstate == ValveState.Opened ? ValveState.Opening :
            vstate == ValveState.Closed ? ValveState.Closing :
            vstate;

        /// <summary>
        /// The motion state that corresponds with vstate, if the valve is
        /// presently moving; otherwise, vstate is returned unchanged.
        /// </summary>
        /// <param name="vstate"></param>
        ValveState presentState(ValveState vstate) => InMotion ? motionState(vstate) : vstate;

        ValveState IntendedState => (PoweredState == ValveState.Opened) == Config.State.IsOn() ?
            ValveState.Opened : ValveState.Closed;

        public List<string> Operations { get; } = new List<string> { "Stop", "Close", "Open" };

        /// <summary>
        /// The number of operations which have been submitted 
        /// to the Controller for this valve, but which have
        /// not been completed.
        /// </summary>
        public int PendingOperations = 0;

        public void DoOperation(string operation)
        {
            if (operation == "Open")
                actuate(ValveState.Opened);
            else if (operation == "Close")
                actuate(ValveState.Closed);
            else if (operation == "Stop")
                Stop();
        }

        public override OnOffState OnOffState 
        { 
            get => base.OnOffState;
            protected set
            {
                InMotion = true;
                NotifyPropertyChanged(nameof(ValveState));
                WaitForCondition(() => MillisecondsInState >= MillisecondsToChangeState, MillisecondsToChangeState, 10);
                operationStopping();
                InMotion = false;
                base.OnOffState = value;
                NotifyPropertyChanged(nameof(ValveState));
            }
        }

        void operationStarting() { lock (this) PendingOperations++; }
        void operationStopping() { lock (this) if (PendingOperations > 0) PendingOperations--; }

        void actuate(ValveState state)
        {
            operationStarting();
            if (!TurnOnOff(PoweredState == state))
                operationStopping();    // already in state
        }
        public void Stop() => PendingOperations = 0;


        public SolenoidValve(IHacsDevice d = null) { }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"{Name}: {ValveState}");
            if (Manager != null)
            {
                StringBuilder sb2 = new StringBuilder();
                sb2.Append($"\r\n{Manager.Name}[{Manager.Keys[this]}]");
                sb.Append(IndentLines(sb2.ToString()));
            }
            return sb.ToString();
        }
    }
}
