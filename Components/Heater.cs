using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
    public class Heater : Oven, IHeater, Heater.IDevice, Heater.IConfig
    {
        #region static

        public static implicit operator double(Heater x)
        { return x?.Temperature ?? -999; }

        #endregion static

        #region Device interfaces

        public new interface IDevice : Oven.IDevice, AutoManual.IDevice { }
        public new interface IConfig : Oven.IConfig, AutoManual.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;


        #endregion Device interfaces

        #region AutoManual
        AutoManual.IDevice IAutoManual.Device => this;
        AutoManual.IConfig IAutoManual.Config => this;

        /// <summary>
        /// Operate the heater in manual mode
        /// </summary>
        public virtual bool ManualMode { get => AutoManual.ManualMode; set => TargetManualMode = value; }
        [JsonProperty("ManualMode")]
        bool TargetManualMode { get => AutoManual.Config.ManualMode; set => AutoManual.ManualMode = value; }
        bool AutoManual.IConfig.ManualMode => TargetManualMode;
        bool AutoManual.IDevice.ManualMode { get => AutoManual.Device.ManualMode; set => AutoManual.Device.ManualMode = value; }

        /// <summary>
        /// The manual mode power level.
        /// </summary>
        public virtual double PowerLevel { get => AutoManual.PowerLevel; set => TargetPowerLevel = value; }
        [JsonProperty("PowerLevel")]
        double TargetPowerLevel { get => AutoManual.Config.PowerLevel; set => AutoManual.PowerLevel = value; }
        double AutoManual.IConfig.PowerLevel => TargetPowerLevel;
        double AutoManual.IDevice.PowerLevel { get => AutoManual.Device.PowerLevel; set => AutoManual.Device.PowerLevel = value; }

        /// <summary>
        /// The maximum device power level. This value protects the device 
        /// from receiving potentially damaging excessive power.
        /// </summary>
        public virtual double MaximumPowerLevel { get => AutoManual.MaximumPowerLevel; set => TargetMaximumPowerLevel = value; }
        [JsonProperty("MaximumPowerLevel")]
        double TargetMaximumPowerLevel { get => AutoManual.Config.MaximumPowerLevel; set => AutoManual.MaximumPowerLevel = value; }
        double AutoManual.IConfig.MaximumPowerLevel { get => AutoManual.Config.MaximumPowerLevel; }
        double AutoManual.IDevice.MaximumPowerLevel { get => AutoManual.Device.MaximumPowerLevel; set => AutoManual.Device.MaximumPowerLevel = value; }


        public virtual void Auto() => AutoManual.Auto();
        public virtual void Manual() => AutoManual.Manual();
        public virtual void Manual(double powerLevel) => AutoManual.Manual(powerLevel);
        public virtual void Hold() => AutoManual.Hold();

        #endregion AutoManual

        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == AutoManual)
                NotifyPropertyChanged(e?.PropertyName);
            else
                base.OnPropertyChanged(sender, e);
        }

        public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == AutoManual)
                NotifyConfigChanged(e?.PropertyName);
            else
                base.OnConfigChanged(sender, e);
        }

        AutoManual AutoManual;
        public Heater(IHacsDevice d = null) : base(d)
        {
            AutoManual = new AutoManual(d ?? this);
        }
    }
}
