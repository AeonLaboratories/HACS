using Newtonsoft.Json;

namespace HACS.Components
{
    public class Thermometer : Meter, IThermometer, Thermometer.IDevice, Thermometer.IConfig
    {
        #region static

        public static implicit operator double(Thermometer x)
        { return x?.Temperature ?? 0; }

        #endregion static

        #region Device interfaces

        public new interface IDevice : Meter.IDevice
        {
            double Temperature { get; set; }
        }
        public new interface IConfig : Meter.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        [JsonProperty]
        public virtual double Temperature
        {
            get => Value;
            protected set { if (Value != Update(value)) NotifyPropertyChanged(); }
        }
        double IDevice.Temperature
        {
            get => Temperature;
            set => Temperature = value;
        }
        //public double Voltage => (this as IVoltmeter)?.Voltage ?? 0;

        public Thermometer(IHacsDevice d = null) : base(d) { }

    }
}
