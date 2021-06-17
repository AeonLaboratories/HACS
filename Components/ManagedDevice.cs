using Utilities;

namespace HACS.Components
{
	public class ManagedDevice : HacsDevice, IManagedDevice, ManagedDevice.IDevice, ManagedDevice.IConfig
	{
		public static string ManagerString(IManagedDevice d)
		{
			if (d?.Manager is IDeviceManager m)
				return Utility.IndentLines($"\r\n{m.Name}[{m.Keys[d]}]");
			return "";
		}


		#region Device interfaces

		public new interface IDevice : HacsDevice.IDevice
		{
			/// <summary>
			/// The DeviceManager that operates or services the device.
			/// This property is populated by the DeviceManager when
			/// the IManagedDevice is added to the DeviceManager's
			/// Device collection.
			/// </summary>
			IDeviceManager Manager { get; set; }
		}

		/// <summary>
		/// Values used to configure the hardware.
		/// </summary>
		public new interface IConfig : HacsDevice.IConfig { }

		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		public virtual IDeviceManager Manager => manager;
		IDeviceManager IDevice.Manager
		{
			get => manager;
			set => Ensure(ref manager, value);
		}
		IDeviceManager manager;

		public ManagedDevice(IHacsDevice d = null) : base(d) { }

	}
}
