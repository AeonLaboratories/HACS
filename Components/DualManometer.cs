using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Utilities;

namespace HACS.Components
{
	
	// vacuum system pressure monitor
	public class DualManometer : SwitchedManometer, IDualManometer, DualManometer.IDevice, DualManometer.IConfig
	{
        #region static

        public static implicit operator double(DualManometer x)
		{ return x?.Pressure ?? 0; }

		public static bool SignificantChange(double pFrom, double pTo)
		{
			if (pFrom <= 0 || pTo <= 0) return pFrom != pTo;
			var change = Math.Abs(pTo - pFrom);
			var scale = Math.Min(Math.Abs(pFrom), Math.Abs(pTo));
			double significant;

			// TODO: is this whole idea dumb? 
			// Is there a better way to characterize a signficant difference?
			if (scale >= 10)
//				significant = 1;                // 1 Torr (10% at 10; 1% at 100; 0.1% at 1000)
				significant = 0.03 * scale;     // 3%)
			else if (scale >= 0.01)
				significant = 0.05 * scale;     // 5%
			else
				significant = 0.02 * scale;     // 2%

			return change >= significant;
		}

		#endregion static

        #region HacsComponent

        [HacsConnect]
		protected virtual void Connect()
        {
			HighPressureManometer = Find<IManometer>(highPressureManometerName);
			LowPressureManometer = Find<ISwitchedManometer>(lowPressureManometerName);
			Switch = LowPressureManometer;
		}

		#endregion HacsComponent

		#region Device interfaces

		public new interface IDevice : SwitchedManometer.IDevice { }
		public new interface IConfig : SwitchedManometer.IConfig { }
		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		// for pressures > high vacuum
		[JsonProperty("HighPressureManometer")]
		string HighPressureManometerName { get => HighPressureManometer?.Name; set => highPressureManometerName = value; }
		string highPressureManometerName;
        public IManometer HighPressureManometer
		{
			get => highPressureManometer;
			set => Ensure(ref highPressureManometer, value, OnPropertyChanged);
		}
		IManometer highPressureManometer;

		// for pressures <= "high vacuum"
		[JsonProperty("LowPressureManometer")]
		string LowPressureManometerName { get => LowPressureManometer?.Name; set => lowPressureManometerName = value; }
		string lowPressureManometerName;
        public ISwitchedManometer LowPressureManometer
		{
			get => lowPressureManometer;
			set
			{
				if (Ensure(ref lowPressureManometer, value, OnPropertyChanged))
				{
					if (lowPressureManometer != null)
					{
						base.StopAction = lowPressureManometer.StopAction;
						base.MillisecondsToValid = lowPressureManometer.MillisecondsToValid;
						base.MinimumMillisecondsOff = lowPressureManometer.MinimumMillisecondsOff;
					}
				}
			}
		}
		ISwitchedManometer lowPressureManometer;

		[JsonProperty]
		public double MaximumLowPressure { get; set; }
		[JsonProperty]
		public double MinimumHighPressure { get; set; }
		[JsonProperty]
		public double SwitchpointPressure { get; set; }

		public override StopAction StopAction
		{ 
			get => base.StopAction;
			set
			{
				if (LowPressureManometer != null)
					LowPressureManometer.StopAction = value;
				base.StopAction = value;
			}
		}

		public override int MillisecondsToValid
		{ 
			get => base.MillisecondsToValid;
			set
			{
				if (LowPressureManometer != null)
					LowPressureManometer.MillisecondsToValid = value;
				base.MillisecondsToValid = value;
			}
		}

		public override int MinimumMillisecondsOff
		{ 
			get => base.MinimumMillisecondsOff;
			set
			{
				if (LowPressureManometer != null)
					LowPressureManometer.MinimumMillisecondsOff = value;
				base.MinimumMillisecondsOff = value;
			}
		}


		// not a HacsComponent Update operation
		// triggered by change in either component manometer;
		// might be called twice for a single DAQ read...
		public void UpdatePressure()
		{
			if (!Initialized) return;

			double pressure;
			double pHP = Math.Max(HighPressureManometer.Pressure, HighPressureManometer.Sensitivity);
			double pLP = Math.Max(LowPressureManometer.Pressure, LowPressureManometer.Sensitivity);

			if (pHP > MinimumHighPressure || !LowPressureManometer.Valid)
				pressure = pHP;
			else if (pLP < MaximumLowPressure)
				pressure = pLP;
			else if (pLP > pHP)
				pressure = pHP;
			else    // MaximumLowPressure <= pLP <= pHP <= MinimumHighPressure
			{
				// high pressure reading weight coefficient
				double whp = (pHP - MaximumLowPressure) / (MinimumHighPressure - MaximumLowPressure);
				pressure = whp * pHP + (1 - whp) * pLP;
			}

			if (pressure < 0) pressure = 0;         // this should never happen
			Device.Pressure = pressure;
            ManageSwitch();
		}

		/// <summary>
		/// Automatically manage the LowPressureManometer's on/off state 
		/// based on the pressure and settings.
		/// </summary>
		public bool ManualMode
		{
			get => manualMode;
			set => Ensure(ref manualMode, value);
		}
		bool manualMode = false;

		public override bool TurnOn()
		{
			ManualMode = true;
			return Switch.TurnOn();
		}
		public override bool TurnOff()
		{
			ManualMode = true;
			return Switch.TurnOff();
		}

		void ManageSwitch()
        {
			if (ManualMode) return;

            bool pressureHigh = HighPressureManometer.Pressure > SwitchpointPressure;
            if (pressureHigh && !Switch.IsOff)
				Switch.TurnOff();
            else if (!pressureHigh && !Switch.IsOn)
				Switch.TurnOn();
        }

		public override void OnPropertyChanged(object sender = null, PropertyChangedEventArgs e = null)
		{
			// Both manometers trigger this method on every DAQ scan, but the 
			// pressure should only be updated once per scan, in case there is a 
			// filter, which potentially depends on the DAQ scan frequency.
			// Ideally, UpdatePressure should be called when the second of the two
			// events occurs, but there currently is no way to be sure.
			var propertyName = e?.PropertyName;
			if (sender == LowPressureManometer || sender == HighPressureManometer)
			{
				if (propertyName == nameof(IValue.Value))
					UpdatePressure();
				else
					NotifyPropertyChanged(propertyName);
			}
		}

		public DualManometer(IHacsDevice d = null) : base(d) { }

		public override string ToString()
        {
			return ManometerString() +
				Utility.IndentLines(
					$"\r\n{LowPressureManometer}" +
					$"\r\n{HighPressureManometer}");
        }
	}
}
