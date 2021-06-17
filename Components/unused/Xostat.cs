using HACS.Core;
using System.ComponentModel;

namespace HACS.Components
{
	public class Xostat : BindableObject
    {
		/// <summary>
		/// The sensor to monitor for Switchpoint
		/// </summary>
		public ISensor Sensor
		{
			get => sensor;
			set
			{
				if (Ensure(ref sensor, value))
				{
					if (OnDetector != null)
						OnDetector.Sensor = Sensor;
					if (OffDetector != null)
						OffDetector.Sensor = Sensor;
				}
			}
		}
		ISensor sensor;

		/// <summary>
		/// The Switch to operate in accordance with the Switchpoint.
		/// </summary>
		ISwitch Switch
		{
			get => _switch;
			set
			{
				if (Ensure(ref _switch, value))
				{
					if (OnDetector != null)
						OnDetector.Switch = Switch;
					if (OffDetector != null)
						OffDetector.Switch = Switch;
				}
			}
		}
		ISwitch _switch;

		public double? OnSwitchpoint
		{
			get => onSwitchpoint;
			set
			{
				if (Ensure(ref onSwitchpoint, value))
				{
					if (OnDetector != null)
						OnDetector.Switchpoint = OnSwitchpoint;
					if (OffDetector != null)
						OffDetector.Switchpoint = OnSwitchpoint;
				}
			}
		}
		double? onSwitchpoint;

		public DetectorSwitch.RuleCode OnSwitchpointRule
		{
			get => onSwitchpointRule;
			set
			{
				if (Ensure(ref onSwitchpointRule, value))
				{
					if (OnDetector != null)
						OnDetector.SwitchpointRule = OnSwitchpointRule;
					if (OffDetector != null)
						OffDetector.SwitchpointRule = OnSwitchpointRule;
				}
			}
		}
		DetectorSwitch.RuleCode onSwitchpointRule;

		public double? OffSwitchpoint
		{
			get => offSwitchpoint;
			set
			{
				if (Ensure(ref offSwitchpoint, value))
				{
					if (OnDetector != null)
						OnDetector.Switchpoint = OffSwitchpoint;
					if (OffDetector != null)
						OffDetector.Switchpoint = OffSwitchpoint;
				}
			}
		}
		double? offSwitchpoint;

		public DetectorSwitch.RuleCode OffSwitchpointRule
		{
			get => offSwitchpointRule;
			set
			{
				if (Ensure(ref offSwitchpointRule, value))
				{
					if (OnDetector != null)
						OnDetector.SwitchpointRule = OffSwitchpointRule;
					if (OffDetector != null)
						OffDetector.SwitchpointRule = OffSwitchpointRule;
				}
			}
		}
		DetectorSwitch.RuleCode offSwitchpointRule;

		DetectorSwitch OnDetector
		{
			get => onDetector;
			set
			{
				onDetector.Switch = Switch;
				onDetector.Sensor = Sensor;
				onDetector.Switchpoint = OnSwitchpoint;
				onDetector.SwitchpointRule = OnSwitchpointRule;
				onDetector.DetectedState = OnOffState.On;
			}
		}
		DetectorSwitch onDetector = new DetectorSwitch();

		DetectorSwitch OffDetector
		{
			get => offDetector;
			set
			{
				offDetector.Switch = Switch;
				offDetector.Sensor = Sensor;
				offDetector.Switchpoint = OffSwitchpoint;
				offDetector.SwitchpointRule = OffSwitchpointRule;
				offDetector.DetectedState = OnOffState.Off;
			}
		}
		DetectorSwitch offDetector = new DetectorSwitch();

		protected virtual void OnPropertyChanged(object sender = null, PropertyChangedEventArgs e = null)
		{
			var propertyName = e?.PropertyName;
			if (sender == OnDetector)
			{
				if (propertyName == nameof(OnDetector.Switch))
					NotifyPropertyChanged(nameof(OnDetector));
			}
			else if (sender == OffDetector)
			{
				if (propertyName == nameof(OffDetector.Switch))
					NotifyPropertyChanged(nameof(OffDetector));
			}
		}
	}
}
