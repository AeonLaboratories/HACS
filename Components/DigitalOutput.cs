namespace HACS.Components
{
    public class DigitalOutput : ManagedSwitch, IDigitalOutput, DigitalOutput.IDevice, DigitalOutput.IConfig
    {
        public new interface IDevice : ManagedSwitch.IDevice
        {
            new IDeviceManager Manager { get; set; }
        }
        public new interface IConfig : ManagedSwitch.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        public DigitalOutput(IHacsDevice d = null) : base(d) { }

        IDeviceManager IDevice.Manager { get => base.Device.Manager; set => base.Device.Manager = value; }

    }
}
