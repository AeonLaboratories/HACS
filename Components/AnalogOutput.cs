using HACS.Core;
using Newtonsoft.Json;
using System.Text;
using Utilities;

namespace HACS.Components
{
    public class AnalogOutput : ManagedDevice, IAnalogOutput, AnalogOutput.IDevice, AnalogOutput.IConfig
    {
        #region static

        public static implicit operator double(AnalogOutput x)
        { return x?.Voltage ?? 0; }

        #endregion static

        #region HacsComponent
        [HacsPostStart]
        protected virtual void PostStart()
        {
            NotifyConfigChanged(nameof(Voltage));
        }
        #endregion HacsComponent


        #region Device interfaces
        public new interface IDevice : ManagedDevice.IDevice
        { 
            double Voltage { get; set; }
        }

        public new interface IConfig : ManagedDevice.IConfig
        {
            double Voltage { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        /// <summary>
        /// The output voltage.
        /// </summary>
        public double Voltage
        {
            get => voltage;
            set => Ensure(ref TargetVoltage, value, NotifyConfigChanged, nameof(TargetVoltage));
        }
        [JsonProperty("OutputVoltage")]
        double TargetVoltage;
        double IConfig.Voltage => TargetVoltage;
        double IDevice.Voltage
        {
            get => voltage;
            set
            {
                Ensure(ref voltage, value);
                sw.Restart();
            }
        }
        double voltage;

        Stopwatch sw = new Stopwatch();
        public long MillisecondsInState => sw.ElapsedMilliseconds;

        public AnalogOutput(IHacsDevice d = null) : base(d) { }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"{Name}: {Voltage:0.00} V");
            sb.Append(ManagerString(this));
            return sb.ToString();
        }
    }
}
