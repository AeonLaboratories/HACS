using HACS.Core;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Text;

namespace HACS.Components
{
    public class AnalogInput : ManagedDevice, IAnalogInput, AnalogInput.IDevice, AnalogInput.IConfig
    {
        #region static

        public static implicit operator double(AnalogInput x)
        { return x?.Voltage ?? 0; }

        #endregion static


        #region Device interfaces
        public new interface IDevice : ManagedDevice.IDevice
        {
            new IDeviceManager Manager { get; set; }
            double Voltage { get; set; }
        }

        public new interface IConfig : ManagedDevice.IConfig
        {
            AnalogInputMode AnalogInputMode { get; }
            double MaximumVoltage { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        IDeviceManager IDevice.Manager { get => base.Device.Manager; set => base.Device.Manager = value; }

        public virtual double Voltage
        { 
            get => voltage;
            protected set => Set(ref voltage, value); // Set because repeats matter for filtering
        }
        double voltage;
        double IDevice.Voltage
        {
            get => Voltage;
            set => Voltage = value;
        }

        public AnalogInputMode AnalogInputMode
        {
            get => analogInputMode;
            set => Ensure(ref analogInputMode, value, NotifyConfigChanged);
        }
        [JsonProperty("AnalogInputMode"), DefaultValue(AnalogInputMode.SingleEnded)]
        AnalogInputMode analogInputMode = AnalogInputMode.SingleEnded;

        public virtual double MaximumVoltage
        {
            get => maximumVoltage;
            set => Ensure(ref maximumVoltage, value, NotifyConfigChanged);
        }
        [JsonProperty("MaximumVoltage"), DefaultValue(10.0)]
        double maximumVoltage = 10.0;

        public virtual double MinimumVoltage
        {
            get => minimumVoltage ?? -MaximumVoltage;
            set => Ensure(ref minimumVoltage, value);
        }
        double? minimumVoltage;

        public virtual bool OverRange => Voltage > MaximumVoltage;
        public virtual bool UnderRange => Voltage < MinimumVoltage;

        public AnalogInput(IHacsDevice d = null) : base(d) { }

        public override string ToString()
        {
            var sb = new StringBuilder($"{Name}: {Voltage:0.00} V");
            sb.Append(ManagerString(this));
            return sb.ToString();
        }
    }
}
