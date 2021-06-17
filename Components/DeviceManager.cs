using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Utilities;

namespace HACS.Components
{
    public class DeviceManager : ManagedDevice, IDeviceManager, DeviceManager.IDevice, DeviceManager.IConfig
    {
        #region static

        public static readonly string InitServiceRequest = "{Init}";
        public static readonly PropertyChangedEventArgs InitArg = PropertyChangedEventArgs(InitServiceRequest);

        #endregion static

        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            // Warning: After the following assignment,
            //      Find<IStateManager>(Name)
            // might return either this or this.StateManager.
            // The assignment is made so the Log file will be named
            // for the DeviceManager.
            StateManager.Name = Name;

            if (LogEverything) Log.Record($"DeviceManager {Name} Connecting...");

            Devices = new Dictionary<string, IManagedDevice>();
            Keys = new Dictionary<IManagedDevice, string>();
            foreach (var x in deviceKeysNames)
            {
                if (FindSupportedDevice(x.Value) is IManagedDevice d)
                    Connect(d, x.Key);
                else
                    LogMessage($"DeviceManager {Name} can't find a supported device with name \"{x.Value}\".\r\n");
            }
            if (LogEverything) Log.Record($"...DeviceManager {Name} Connected.");
        }

        #endregion HacsComponent

        #region Class interface properties and methods

        #region Device Interfaces
        public new interface IDevice : ManagedDevice.IDevice { }
        public new interface IConfig : ManagedDevice.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;
        #endregion Device Interfaces


        #region IDeviceManager

        /// <summary>
        /// Finds the first device with the specified name that is
        /// a type (class) supported by this DeviceManager.
        /// The base implementation supports any IManagedDevice. Override this
        /// method to limit Devices to specific IManagedDevice classes.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected virtual IManagedDevice FindSupportedDevice(string name) =>
            Find<IManagedDevice>(name);

        public virtual bool IsSupported(IManagedDevice d, string key) =>
            !key.IsBlank() && d != null;

        public virtual void Connect(IManagedDevice d, string key)
        {
            if (!IsSupported(d, key)) return;

            if (Devices.ContainsKey(key))
            {
                var old = Devices[key];
                LogMessage($"DeviceManager {Name}: Replacing {old.Name} on {key} with {d.Name}");
                Disconnect(old);
            }

            Devices[key] = d;
            Keys[d] = key;
            d.Device.Manager = this;
            d.ConfigChanged += DeviceConfigChanged;
            ScheduleInitialService(d);
        }

        public virtual void Disconnect(IManagedDevice device)
        {
            if (device is IManagedDevice d && Keys.ContainsKey(d) && Keys[d] is string key)
            {
                d.Device.Manager = null;
                d.Device.UpdatesReceived = 0;
                device.ConfigChanged -= DeviceConfigChanged;
                Keys.Remove(device);
                Devices.Remove(key);
            }
        }

        /// <summary>
        /// Returns the positive integer found at the end of the 
        /// given key. Returns -1 on failure.
        /// </summary>
        protected virtual int ExtractChannelNumber(string key)
        {
            while (key.Length > 0)
            {
                if (int.TryParse(key, out int ch))
                    return ch;
                key = key.Substring(1, key.Length - 1);
            }
            return -1;
        }

        /// <summary>
        /// Returns true if the provided key is valid; 
        /// logs an error message and returns false if not.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        /// <param name="prefix"></param>
        /// <param name="maxIndex"></param>
        /// <returns></returns>
        protected virtual bool ValidKey(string name, string key, string prefix, int maxIndex)
        {
            if (IsValidKey(key, prefix, maxIndex)) 
                return true;
            LogMessage($"DeviceManager {Name} can't connect {name} with invalid key \"{key}\".\r\n" +
                $"\t\tKey must match template \"{prefix}#\" where # is in 0..{maxIndex}");
            return false;
        }

        /// <summary>
        /// Returns true if the key matches the pattern 
        /// &quot;&lt;prefix&gt;&lt;index&gt;&quot;,
        /// and index is in the range [0..maxIndex].
        /// </summary>
        /// <param name="key">The key to be tested</param>
        /// <param name="prefix">A valid prefix</param>
        /// <param name="maxIndex">The highest valid index</param>
        /// <returns></returns>
        protected virtual bool IsValidKey(string key, string prefix, int maxIndex)
        {
            var tidLen = prefix.Length;
            if (key.IsBlank())
                return false;
            if (key.Length < tidLen + 1)
                return false;
            if (key.Substring(0, tidLen) != prefix)
                return false;
            if (!int.TryParse(key.Substring(tidLen), out int channel))
                return false;
            if (channel < 0 || channel > maxIndex)
                return false;
            return true;
        }

        protected void ScheduleInitialService(IManagedDevice d)
        {
            d.Device.UpdatesReceived = 0;                  // indicates Device data is invalid
            DeviceConfigChanged(d, InitArg);        // schedule for initial service
        }

        /// <summary>
        /// Devices controlled by this Manager.
        /// </summary>
        public Dictionary<string, IManagedDevice> Devices
        {
            get => devices;
            set => Set(ref devices, value);
        }
        Dictionary<string, IManagedDevice> devices;

        [JsonProperty("Devices")]
        Dictionary<string, string> DeviceKeysNames { get => Devices?.KeysNames(); set => deviceKeysNames = value; }
        Dictionary<string, string> deviceKeysNames;

        /// <summary>
        /// Keys for the Devices controlled by this Manager;
        /// </summary>
        [Browsable(false)]
        public Dictionary<IManagedDevice, string> Keys { get; set; }

        #endregion IDeviceManager

        #region StateManager
        [JsonProperty]
        public virtual int IdleTimeout { get => StateManager.IdleTimeout; set => StateManager.IdleTimeout = value; }
        public virtual bool Ready => StateManager.Ready;
        public virtual bool HasWork => StateManager.HasWork;
        // Busy must be re-implemented here in case Ready or HasWork
        // are overridden.
        public virtual bool Busy => Ready && HasWork;
        public virtual bool Stopping => StateManager.Stopping;
        public new bool Stopped => StateManager.Stopped;
        public virtual LogFile Log { get => StateManager.Log; set => StateManager.Log = value; }
        [JsonProperty]
        public virtual bool LogEverything { get => StateManager.LogEverything; set => StateManager.LogEverything = value; }
        public virtual void LogMessage(string message) => StateManager.LogMessage(message);
        int IStateManager.StateLoopTimeout { get => StateManager.StateLoopTimeout; set => StateManager.StateLoopTimeout = value; }
        Action IStateManager.ManageState { get => StateManager.ManageState; set => StateManager.ManageState = value; }
        void IStateManager.StopWaiting() => StateManager.StopWaiting();

        protected virtual void ManageState() { }
        protected virtual void StopWaiting() => (this as IStateManager).StopWaiting();

        #endregion StateManager


        #endregion Class interface properties and methods

        #region State Management
        /// <summary>
        /// Handles ManagedDevice Configuration changes
        /// The default implementation does nothing.
        /// </summary>
        /// <param name="sender">The device needing service</param>
        /// <param name="e">e.PropertyName is repurposed to represent the requested service.</param>
        protected virtual void DeviceConfigChanged(object sender, PropertyChangedEventArgs e) { }

        #endregion State management


        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == StateManager)
                NotifyPropertyChanged(e?.PropertyName);
            else
                base.OnPropertyChanged(sender, e);
        }

        IStateManager StateManager;
        public DeviceManager(IHacsDevice d = null) : base(d)
        {
            StateManager = new StateManager();
            StateManager.PropertyChanged += OnPropertyChanged;
            (StateManager as IStateManager).ManageState = ManageState;
        }


        public override string ToString()
        {
            return $"{Name}";
        }
    }
}