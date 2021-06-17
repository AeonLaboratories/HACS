using System.ComponentModel;
using System.Text;

namespace HACS.Components
{
	/// <summary>
	/// A Thermocouple that is operated by a DeviceManager.
	/// </summary>
	public class ManagedThermocouple : Thermocouple, IManagedThermocouple, ManagedThermocouple.IDevice, ManagedThermocouple.IConfig
	{
		#region static

		public static implicit operator double(ManagedThermocouple x)
		{ return x?.Temperature ?? 0; }

		#endregion static

		#region Device interfaces

		public new interface IDevice : Thermocouple.IDevice, ManagedDevice.IDevice { }
		public new interface IConfig : Thermocouple.IConfig, ManagedDevice.IConfig { }
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
		public ManagedThermocouple(IHacsDevice d = null) : base(d)
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
