using Newtonsoft.Json;

namespace HACS.Components
{
    /// <summary>
    /// An automatic device that can be operated in manual mode.
    /// </summary>
    public class AutoManual : Auto, IAutoManual, AutoManual.IDevice, AutoManual.IConfig
    {
        #region Device interfaces

        public new interface IDevice : Auto.IDevice
        {
            bool ManualMode { get; set; }
            double PowerLevel { get; set; }
            double MaximumPowerLevel { get; set; }
        }

        public new interface IConfig : Auto.IConfig
        {
            bool ManualMode { get; }
            double PowerLevel { get; }
            double MaximumPowerLevel { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        /// <summary>
        /// True to operate the device in Manual mode. False for Auto mode.
        /// In Auto mode, SetSetpoint() provides a desired temperature (°C).
        /// In Manual mode, SetSetpoint()'s parameter is interpreted as
        /// a percent power level;
        /// </summary>
        public virtual bool ManualMode
        { 
            get => manualMode; 
            set => Ensure(ref TargetManualMode, value, NotifyConfigChanged, nameof(TargetManualMode)); 
        }
        [JsonProperty("ManualMode")]
        bool TargetManualMode;
        bool IConfig.ManualMode => TargetManualMode;
        bool IDevice.ManualMode
        {
            get => manualMode;
            set => Ensure(ref manualMode, value);
        }
        bool manualMode;


        /// <summary>
        /// The heater's output PowerLevel [0..100%]
        /// </summary>
        public virtual double PowerLevel
        {
            get => powerLevel;
            set => Ensure(ref TargetPowerLevel, value, NotifyConfigChanged, nameof(TargetPowerLevel));
        }
        [JsonProperty("PowerLevel")]
        double TargetPowerLevel;
        double IConfig.PowerLevel => TargetPowerLevel;
        double IDevice.PowerLevel
        {
            get => powerLevel;
            set
            {
                // TODO: should this throw an exception instead? or substitute the max?
                if (value <= MaximumPowerLevel)
                    Ensure(ref powerLevel, value);
            }
        }
        double powerLevel;


        /// <summary>
        /// The maximum allowable power level, or "Control Output" value
        /// [0..100%], to be delivered to this device. Note: The 
        /// percentage is of the Controller's output capability, not 
        /// the device's range of input power. MaximumPowerLevel is what prevents 
        /// the Controller from overpowering the device.
        /// </summary>
        public virtual double MaximumPowerLevel
        {
            get => maximumPowerLevel;
            set => Ensure(ref TargetMaximumPowerLevel, value, NotifyConfigChanged, nameof(TargetMaximumPowerLevel));
        }
        [JsonProperty("MaximumPowerLevel")]
        double TargetMaximumPowerLevel;
        double IConfig.MaximumPowerLevel => TargetMaximumPowerLevel;
        double IDevice.MaximumPowerLevel
        {
            get => maximumPowerLevel;
            set => Ensure(ref maximumPowerLevel, value);
        }
        double maximumPowerLevel;


        public virtual void Auto() => ManualMode = false;
        public virtual void Manual() => ManualMode = true;
        public virtual void Manual(double powerLevel)
        {
            Manual();
            PowerLevel = powerLevel;
        }

        public virtual void Hold() =>  Manual(PowerLevel);

        public AutoManual(IHacsDevice d = null) : base(d) { }

    }
}
