using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using static Utilities.Utility;


namespace HACS.Components
{
    /// <summary>
    /// A valve whose Open and Close operations are implemented by 
    /// two distinct (usually pneumatic) valves.
    /// </summary>
    public class DualActionValve : HacsDevice, IDualActionValve, DualActionValve.IDevice, DualActionValve.IConfig
    {
        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            OpenValve = Find<IValve>(openValveName);
            CloseValve = Find<Valve>(closeValveName);
        }

        [HacsInitialize]
        protected virtual void Initialize()
        {
            NotifyPropertyChanged();
        }

        #endregion HacsComponent

        #region Device interfaces

        public new interface IDevice : HacsDevice.IDevice, Valve.IDevice { }
        public new interface IConfig : HacsDevice.IConfig, Valve.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;
        Valve.IDevice IValve.Device => this;
        Valve.IConfig IValve.Config => this;

        #endregion Device interfaces

        [JsonProperty("OpenValve")]
        string OpenValveName { get => OpenValve?.Name; set => openValveName = value; }
        string openValveName;
        // The valve whose fluid powers the DualActionValve's Open operation.
        public IValve OpenValve 
        {
            get => openValve;
            set => Ensure(ref openValve, value, NotifyPropertyChanged);
        }
        IValve openValve;

        [JsonProperty("CloseValve")]
        string CloseValveName { get => CloseValve?.Name; set => closeValveName = value; }
        string closeValveName;
        // The valve whose fluid powers the DualActionValve's Close operation.
        public IValve CloseValve { 
            get => closeValve;
            set => Ensure(ref closeValve, value, NotifyPropertyChanged); 
        }
        IValve closeValve;

        public List<string> Operations { get; } = new List<string> { "Stop", "Close", "Open" };

        public void DoOperation(string operation)
        {
            if (operation == "Open")
                Open();
            else if (operation == "Close")
                Close();
            else if (operation == "Stop")
                Stop();
        }

        /// <summary>
        /// The state of the valve (e.g., Opened, Closed, Unknown, etc.).
        /// </summary>
        public ValveState ValveState
        {
            get => valveState;
            set => Ensure(ref valveState, value);
        }
        [JsonProperty("ValveState"), DefaultValue(ValveState.Unknown)]
        ValveState valveState = ValveState.Unknown;

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


        /// <summary>
        /// The valve's controller is ready to accept commands.
        /// </summary>
        public bool Ready => (OpenValve?.Ready ?? false) && (CloseValve?.Ready ?? false);

        /// <summary>
        /// The valve is fully open.
        /// </summary>
        public bool IsOpened => ValveState == ValveState.Opened;

        /// <summary>
        /// The valve is fully closed.
        /// </summary>
        public bool IsClosed => ValveState == ValveState.Closed;

        /// <summary>
        /// Issue an open command for the valve.
        /// </summary>
        public void Open() => OpenWait();

        /// <summary>
        /// Issue a close command for the valve.
        /// </summary>
        public void Close() => CloseWait();
        public void Stop()
        {
            OpenValve.Stop();
            CloseValve.Stop();
        }

        public bool Idle => OpenValve.Idle && CloseValve.Idle;

        /// <summary>
        /// Wait until the valve has no pending operations.
        /// </summary>
        public void WaitForIdle() =>
            WaitForCondition(() => Idle, -1, 5);

        /// <summary>
        /// Open the valve and wait for the operation to complete.
        /// </summary>
        public void OpenWait()
        {
            ValveState = ValveState.Opening;
            CloseValve.CloseWait();
            OpenValve.OpenWait();
            ValveState = ValveState.Opened;
            OpenValve.CloseWait();
        }

        /// <summary>
        /// Close the valve and wait for the operation to complete.
        /// </summary>
        public void CloseWait()
        {
            ValveState = ValveState.Closing;
            OpenValve.CloseWait();
            CloseValve.OpenWait();
            ValveState = ValveState.Closed;
            CloseValve.CloseWait();
        }

        /// <summary>
        /// Does nothing; method is present to comply with IValve interface.
        /// </summary>
        public void Exercise()
        {
            // do nothing
        }
    }
}
