using HACS.Core;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
	/// <summary>
	/// An analog input voltmeter
	/// </summary>
	public class AIVoltmeter : Voltmeter, IAIVoltmeter, AIVoltmeter.IDevice, AIVoltmeter.IConfig
	{
		#region static

		public static implicit operator double(AIVoltmeter x)
		{ return x?.Voltage ?? 0; }

		#endregion static

		#region Device interfaces

		public new interface IDevice : Voltmeter.IDevice, AnalogInput.IDevice { }
		public new interface IConfig : Voltmeter.IDevice, AnalogInput.IConfig { }

		public new IDevice Device => this;
		public new IConfig Config => this;

		AnalogInput.IDevice IAnalogInput.Device => this;
		AnalogInput.IConfig IAnalogInput.Config => this;
		ManagedDevice.IDevice IManagedDevice.Device => this;
		ManagedDevice.IConfig IManagedDevice.Config => this;

		#endregion Device interfaces

		#region AnalogInput

		[JsonProperty]
		public AnalogInputMode AnalogInputMode { get => AnalogInput.AnalogInputMode; set => AnalogInput.AnalogInputMode = value; }

		public IDeviceManager Manager => Device.Manager;
		IDeviceManager ManagedDevice.IDevice.Manager { get => Device.Manager; set => Device.Manager = value; }
		IDeviceManager AnalogInput.IDevice.Manager { get => AnalogInput.Device.Manager; set => AnalogInput.Device.Manager = value; }


        public override double Voltage
		{
			get => voltage;
			protected set { voltage = value; ; }
		}
		double voltage;

        double AnalogInput.IDevice.Voltage
		{ 
			get => AnalogInput.Device.Voltage;
			set
			{
				AnalogInput.Device.Voltage = value;
				(this as IVoltmeter).Device.Voltage = value;
			}
		}

		#endregion AnalogInput

		public override double MaximumVoltage
		{ 
			get => base.MaximumVoltage;
			set
			{
				base.MaximumVoltage = value;
				AnalogInput.MaximumVoltage = value;
			}
		}

		public override double MinimumVoltage
		{ 
			get => base.MinimumVoltage;
			set
			{
				base.MinimumVoltage = value;
				AnalogInput.MinimumVoltage = value;
			}
		}

		public override bool OverRange => base.OverRange || AnalogInput.OverRange;
		public override bool UnderRange => base.UnderRange || AnalogInput.UnderRange;

		public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var propertyName = e?.PropertyName;
			if (sender == AnalogInput)
			{
				if (propertyName == nameof(IAnalogInput.Voltage))
					NotifyPropertyChanged(e?.PropertyName);
			}
			else
				base.OnPropertyChanged(sender, e);
		}

		public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == AnalogInput)
				NotifyConfigChanged(e?.PropertyName);
			else
				base.OnConfigChanged(sender, e);
		}


		AnalogInput AnalogInput;
		public AIVoltmeter(IHacsDevice d = null) : base(d)
		{
			AnalogInput = new AnalogInput(d ?? this);
		}

		public override string ToString()
		{
			var sb = new StringBuilder(base.ToString());
			sb.Append(ManagedDevice.ManagerString(this));
			return sb.ToString();
		}
	}
}
