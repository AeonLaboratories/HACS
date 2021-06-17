using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
	/// <summary>
	/// A manometer attached to a DAQ analog input.
	/// </summary>
	public class AIManometer : AIVoltmeter, IAIManometer, AIManometer.IDevice, AIManometer.IConfig
	{
		#region static

		public static implicit operator double(AIManometer x)
		{ return x?.Pressure ?? 0; }

		#endregion static

		#region Device interfaces

		public new interface IDevice : AIVoltmeter.IDevice, Manometer.IDevice { }
		public new interface IConfig : AIVoltmeter.IConfig, Manometer.IConfig { }

		public new IDevice Device => this;
		public new IConfig Config => this;
		Manometer.IDevice IManometer.Device => this;
		Manometer.IConfig IManometer.Config => this;

		#endregion Device interfaces

		public override double Value
		{
			get => base.Value;
			protected set
			{
				base.Value = value;
				Manometer.Device.Pressure = base.Value;
			}
		}

		[JsonProperty("Pressure")]
		double Manometer.IDevice.Pressure { get => Manometer.Device.Pressure; set => Manometer.Device.Pressure = value; }
		public double Pressure => Manometer.Pressure;

		public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var propertyName = e?.PropertyName;
			if (sender == Manometer)
			{
				if (propertyName == nameof(IManometer.Pressure))
					NotifyPropertyChanged(propertyName);
			}
			else
				base.OnPropertyChanged(sender, e);
		}

		public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == Manometer)
				NotifyConfigChanged(e?.PropertyName);
			else
				base.OnConfigChanged(sender, e);
		}

		Manometer Manometer;
		public AIManometer(IHacsDevice d = null) : base(d)
		{
			Manometer = new Manometer(d ?? this);
		}
	}
}
