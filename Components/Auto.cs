using Newtonsoft.Json;

namespace HACS.Components
{
    /// <summary>
    /// A device that autonomously controls an output to maintain a sensed value at a given setpoint.
    /// </summary>
    public class Auto : Switch, IAuto, Auto.IDevice, Auto.IConfig
    {
        #region Device interfaces

        public new interface IDevice : Switch.IDevice
        { 
            double Setpoint { get; set; }
        }

        public new interface IConfig : Switch.IConfig
        {
            double Setpoint { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        /// <summary>
        /// The Setpoint value. Default -999.
        /// </summary>
        public virtual double Setpoint
        {
            get => setpoint;
            set => Ensure(ref TargetSetpoint, value, NotifyConfigChanged, nameof(TargetSetpoint));
        }
        [JsonProperty("Setpoint")]
        double TargetSetpoint;
        double IConfig.Setpoint => TargetSetpoint;

        double IDevice.Setpoint
        {
            get => setpoint;
            set => Ensure(ref setpoint, value);
        }
        double setpoint = -999;


        public virtual void TurnOn(double setpoint)
        {
            Setpoint = setpoint;
            TurnOn();
        }

        public Auto(IHacsDevice d = null) : base(d) { }
    }
}
