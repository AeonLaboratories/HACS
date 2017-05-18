namespace HACS.Components.Controls
{
	public interface IDeviceDisplay
	{
		object Device { get; set; }
		void StateChanged();
	}
}
