using Newtonsoft.Json;

namespace HACS.Components
{
    public class Manometer : Meter, IManometer, Manometer.IDevice, Manometer.IConfig
    {
        #region static

        public static implicit operator double(Manometer x)
        { return x?.Pressure ?? 0; }

        #endregion static

        #region Device interfaces

        public new interface IDevice : Meter.IDevice
        {
            double Pressure { get; set; }
        }
        public new interface IConfig : Meter.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        [JsonProperty]
        public virtual double Pressure
        {
            get => Value;
            protected set { if (Value != Update(value)) NotifyPropertyChanged(); }
        }
        double IDevice.Pressure
        {
            get => Pressure;
            set => Pressure = value;
        }
        //public double Voltage => (this as IVoltmeter)?.Voltage ?? 0;

        public Manometer(IHacsDevice d = null) : base(d) { }

    }
}
