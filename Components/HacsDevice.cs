using HACS.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HACS.Components
{
	/// <summary>
	/// Base implementation for a device class with data that is 
	/// updated asynchronously; i.e., the updates occur, but not 
	/// as an immediate result of an action taken by the class.
	/// The counter, which is usually incremented by update provider, 
	/// enables the class and its clients to monitor and handle the 
	/// updates (and/or their absence) asynchronously.
	/// </summary>
	public class HacsDevice : HacsComponent, IHacsDevice, HacsDevice.IDevice, HacsDevice.IConfig
	{
		/// <summary>
		/// Implementations of these properties are usually declared explicitly 
		/// in order to use the same name as the class' public get-only value.
		/// </summary>
		public interface IDevice
		{
			/// <summary>
			/// This counter is usually incremented by update provider.
			/// It enables the class and its clients to monitor and 
			/// handle data updates (and/or their absence) asynchronously.
			/// </summary>
			long UpdatesReceived { get; set; }
		}

		/// <summary>
		/// This property encapsulates configuration values, generally
		/// used by the IDevice source.
		/// </summary>
		public interface IConfig { }

		/// <summary>
		/// This property provides access to device property values 
		/// which are generally provided to the class from another source 
		/// (e.g., as a result of a hadware device input).
		/// </summary>
		public virtual IDevice Device { get; private set; }

		/// <summary>
		/// These configuration properties are generally intended for
		/// classes which can influence the IDevice property values,
		/// e.g. a device controller.
		/// </summary>
		// The values of these properties are often produced, sometimes 
		// indirectly, from configuration inputs to the class.
		// Within the class, changes to these properties should raise both 
		// the ConfigChanged and PropertyChanged events. The PropertyName for 
		// the PropertyChanged event should be "Target<propertyName>" 
		// to avoid confusion with events raised by the same-named property in 
		// the class that exposes the (actual, IDevice) property value.
		public virtual IConfig Config { get; private set; }

		/// <summary>
		/// These event handlers are invoked whenever the desired device
		/// configuration changes. EventArgs.PropertyName is usually the 
		/// name of an updated configuration property, but it may be null,
		/// or a generalized indication of the reason the event was raised, 
		/// such as &quot;{Init}&quot;.
		/// </summary>
		public virtual PropertyChangedEventHandler ConfigChanged { get; set; }

		/// <summary>
		/// Raises the ConfigChanged event.
		/// </summary>
		public virtual void NotifyConfigChanged(object sender, PropertyChangedEventArgs e) =>
			ConfigChanged?.Invoke(sender, e);

		/// <summary>
		/// Raises the ConfigChanged event.
		/// </summary>
		/// <param name="senderName"></param>
		public virtual void NotifyConfigChanged([CallerMemberName] string senderName = default) =>
			NotifyConfigChanged(this, PropertyChangedEventArgs(senderName));

		public virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e) =>
			NotifyPropertyChanged(e.PropertyName);

		public virtual void OnConfigChanged(object sender, PropertyChangedEventArgs e) =>
			NotifyConfigChanged(this, e);


		// TODO: make sure this setter lock can't cause a deadlock
		/// <summary>
		/// The number of status updates received.
		/// </summary>
		public virtual long UpdatesReceived
		{
			get => updatesReceived;
			protected set { lock (this) Ensure(ref updatesReceived, value); }
		}
		long updatesReceived;
		long IDevice.UpdatesReceived
		{
			get => UpdatesReceived;
			set => UpdatesReceived = value;
		}

		public HacsDevice(IHacsDevice d = null)
		{
			Device = d?.Device ?? this;
			Config = d?.Config ?? this;
			if (d != null)
			{
				PropertyChanged -= d.OnPropertyChanged;
				PropertyChanged += d.OnPropertyChanged;
				ConfigChanged -= d.OnConfigChanged;
				ConfigChanged += d.OnConfigChanged;
			}
		}
	}
}
