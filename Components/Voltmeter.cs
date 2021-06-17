using Newtonsoft.Json;
using System.ComponentModel;
using Utilities;

namespace HACS.Components
{
	/// <summary>
	/// A device that converts a voltage into a meaningful value
	/// </summary>
	public class Voltmeter : Meter, IVoltmeter, Voltmeter.IDevice, Voltmeter.IConfig
	{
		#region static

		public static implicit operator double(Voltmeter x)
		{ return x?.Voltage ?? 0; }

		#endregion static

		#region Device interfaces

		public new interface IDevice : Meter.IDevice
		{
			double Voltage { get; set; }
			double MaximumVoltage { get; set; }
		}
		public new interface IConfig : Meter.IConfig
		{
			double MaximumVoltage { get; }
		}

		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces


		/// <summary>
		/// The meter's filtered input voltage.
		/// </summary>
		[JsonProperty]
		public virtual double Voltage
		{
			get => voltage;
			protected set => Ensure(ref voltage, value);
		}
		double voltage;

		double IDevice.Voltage
		{
			get => Voltage;
			set
			{
				Update(value);						// update the Meter with the new input
				Voltage = Filter?.Value ?? value;   // retrieve the filtered value
			}
		}

		[JsonProperty]
		public virtual double MinimumVoltage
		{
			get => minimumVoltage;
			set => Ensure(ref minimumVoltage, value);
		}
		double minimumVoltage = 0;

		public virtual double MaximumVoltage
		{
			get => maximumVoltage ?? 10.0;
			set => Ensure(ref TargetMaximumVoltage, value, NotifyConfigChanged, nameof(TargetMaximumVoltage));
		}
		[JsonProperty("MaximumVoltage"), DefaultValue(10.0)]
		double TargetMaximumVoltage = 10.0;
		double IConfig.MaximumVoltage => TargetMaximumVoltage;
		double IDevice.MaximumVoltage
		{
			get => maximumVoltage ?? TargetMaximumVoltage;
			set => Ensure(ref maximumVoltage, value); 
		}
		double? maximumVoltage;

		public virtual new bool OverRange => base.OverRange || Voltage > MaximumVoltage;
		public virtual new bool UnderRange => base.OverRange || Voltage < MinimumVoltage;

		public Voltmeter(IHacsDevice d = null) : base(d) { }

		public override string ToString()
		{
			return base.ToString() + 
				Utility.IndentLines($"\r\n({Voltage:0.0000} V)");
        }
	}
}
