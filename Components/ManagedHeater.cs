using System.ComponentModel;
using System.Text;

namespace HACS.Components
{
	/// <summary>
	/// A Heater that is operated by a DeviceManager.
	/// </summary>
	public class ManagedHeater : Heater, IManagedHeater, ManagedHeater.IDevice, ManagedHeater.IConfig
	{
		#region static

		public static implicit operator double(ManagedHeater x)
		{ return x?.Temperature ?? 0; }

		#endregion static


		#region Device interfaces

		public new interface IDevice : Heater.IDevice, ManagedDevice.IDevice { }
		public new interface IConfig : Heater.IConfig, ManagedDevice.IConfig { }
		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		#region ManagedDevice
		ManagedDevice.IDevice IManagedDevice.Device => this;
		ManagedDevice.IConfig IManagedDevice.Config => this;

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
		public ManagedHeater(IHacsDevice d = null) : base(d)
		{
			ManagedDevice = new ManagedDevice(d ?? this);
		}

		public override string ToString()
		{
			var sb = new StringBuilder(base.ToString());
			sb.Append(Components.ManagedDevice.ManagerString(this));
			return sb.ToString();
		}
	}
}
