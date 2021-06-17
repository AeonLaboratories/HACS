using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
	/// <summary>
	/// A thermometer attached to a DAQ analog input.
	/// </summary>
	public class AIThermometer : AIVoltmeter, IAIThermometer, AIThermometer.IDevice, AIThermometer.IConfig
	{
		#region static

		public static implicit operator double(AIThermometer x)
		{ return x?.Temperature ?? 0; }

		#endregion static

		#region Device interfaces

		public new interface IDevice : AIVoltmeter.IDevice, Thermometer.IDevice { }
		public new interface IConfig : AIVoltmeter.IConfig, Thermometer.IConfig { }

		public new IDevice Device => this;
		public new IConfig Config => this;
		Thermometer.IDevice IThermometer.Device => this;
		Thermometer.IConfig IThermometer.Config => this;

		#endregion Device interfaces

		public override double Value
		{
			get => base.Value;
			protected set
			{
				base.Value = value;
				Thermometer.Device.Temperature = base.Value;
			}
		}

		[JsonProperty("Temperature")]
		double Thermometer.IDevice.Temperature { get => Thermometer.Device.Temperature; set => Thermometer.Device.Temperature = value; }
		public double Temperature => Thermometer.Temperature;

		// Spoof only those Properties of the embedded Thermometer that serve as the implementations
		// of Properties for this class instance.
		// "Spoof" means that this instance is given as the sender of the PropertyChanged event
		// raised by the embedded object's changed Property.
		// Note in particular that Value is a property of AIThermometer, so Thermometer.Value should
		// not raise an AIThermometer.Value PropertyChanged event.
		public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var propertyName = e?.PropertyName;
			if (sender == Thermometer)
			{ 
				if (propertyName == nameof(IThermometer.Temperature))
					NotifyPropertyChanged(propertyName);
			}
			else
				base.OnPropertyChanged(sender, e);
		}

		public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == Thermometer)
				NotifyConfigChanged(e?.PropertyName);
			else
				base.OnConfigChanged(sender, e);
		}

		Thermometer Thermometer;
		public AIThermometer(IHacsDevice d = null) : base(d)
		{
			Thermometer = new Thermometer(d ?? this);
		}
	}
}
