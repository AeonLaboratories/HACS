using HACS.Core;
using System.ComponentModel;
using System.Text;

namespace HACS.Components
{
	/// <summary>
	/// A OnOff signal provided by a DeviceManager.
	/// </summary>
	public class DigitalInput : OnOff, IDigitalInput, DigitalInput.IDevice, DigitalInput.IConfig
	{

		#region Device interfaces

		public new interface IDevice : OnOff.IDevice, ManagedDevice.IDevice { }
		public new interface IConfig : OnOff.IConfig, ManagedDevice.IConfig  { }

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
		public DigitalInput(IHacsDevice d = null) : base(d)
		{
			ManagedDevice = new ManagedDevice(d ?? this);
		}

		public override string ToString()
		{
			var sb = new StringBuilder(base.ToString());
			sb.Append(ManagedDevice.ManagerString(this));
			return sb.ToString();
		}
	}
}
