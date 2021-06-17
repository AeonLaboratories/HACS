using System.ComponentModel;

namespace HACS.Components
{

    public class ManagedValve : Valve, IManagedValve, ManagedValve.IDevice, ManagedValve.IConfig
    {

        #region Device interfaces

        public new interface IDevice : Valve.IDevice, ManagedDevice.IDevice { }
        public new interface IConfig : Valve.IConfig, ManagedDevice.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;
        ManagedDevice.IDevice IManagedDevice.Device => this;
        ManagedDevice.IConfig IManagedDevice.Config => this;

        #endregion Device interfaces

        #region ManagedDevice
        public IDeviceManager Manager => ManagedDevice.Manager;
        IDeviceManager ManagedDevice.IDevice.Manager { get => ManagedDevice.Manager; set => ManagedDevice.Device.Manager = value; }

        #endregion ManagedDevice

        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == ManagedDevice)
                NotifyPropertyChanged(e?.PropertyName);
            else
                base.OnPropertyChanged(sender, e);
        }

        public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == ManagedDevice)
                NotifyConfigChanged(e?.PropertyName);
            else
                base.OnConfigChanged(sender, e);
        }


        ManagedDevice ManagedDevice;
        public ManagedValve(IHacsDevice d = null) : base(d)
        {
            ManagedDevice = new ManagedDevice(d ?? this);
        }
    }
}