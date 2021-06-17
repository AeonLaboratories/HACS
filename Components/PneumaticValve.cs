namespace HACS.Components
{
    /// <summary>
    /// A valve moved pneumatically via an air solenoid valve,
    /// controlled by a Switchbank.
    /// In instances of this class, the PoweredState property must reflect 
    /// the valve state of the pneumatic valve when the solenoid valve is
    /// powered, and not the valve state of the solenoid valve (i.e., regardless
    /// of whether the powered solenoid valve is opened or closed, the consequent
    /// state of the pneumatic valve is what matters).
    /// </summary>
    public class PneumaticValve : SolenoidValve, IPneumaticValve, PneumaticValve.IDevice, PneumaticValve.IConfig
    {
        #region Device interfaces

        public new interface IDevice : SolenoidValve.IDevice { }
        public new interface IConfig : SolenoidValve.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;
        Valve.IDevice IValve.Device => this;
        Valve.IConfig IValve.Config => this;

        #endregion Device interfaces

    }
}
