using System;
using System.ComponentModel;
using System.Windows.Forms;
using Utilities;

namespace HACS.Components.Controls
{
    /// <summary>
    /// An Indicator for displaying a numeric value from a device.
    /// </summary>
    [
        Description("Gauge"),
        //ToolboxBitmap(typeof(Gauge), "Gauge.png"),
        DefaultEvent("Click")
    ]
    public partial class Gauge : Label, IDeviceDisplay
    {
        #region UI Context
        // These properties and methods are for use in the UI thread. They should not be
        // accessed/invoked from the Device Context thread.

		public override string ToString()
		{
			return DeviceState;
		}

        [Category("Gauge Events")]
		[Description("Provides a facility for altering the gauge appearance based on its updated value.")]
		public event EventHandler Decorate;

		public void UpdateDisplay()
		{
			Text = valueToText(_DisplayValue);
			Decorate?.Invoke(this, null);
		}

		public void Connect(Meter d)
		{ Device = d; if (d != null) { d.StateChanged += StateChanged; StateChanged(); } }

		public void Connect(Heater d)
		{ Device = d; if (d != null) { d.StateChanged = StateChanged; StateChanged(); } }

		public void Connect(TempSensor d)
		{ Device = d; if (d != null) { d.StateChanged = StateChanged; StateChanged(); } }

        public void Connect(EurothermFurnace d)
		{ Device = d; if (d != null) { d.StateChanged = StateChanged; StateChanged(); } }

        #endregion UI Context

        #region Device Context
        // These properties and methods must not alter UI elements.
        // In particular, event handlers in this section must not run in the UI thread,
        // or the UI will interfere with device operations.

        [Category("Gauge Events")]
        public event EventHandler DisplayValueChanged;

        [Category("Gauge Events")]
        public event EventHandler DeviceError;

        [Category("Gauge Properties")]
		[Description("Gets or sets the device that provides the display value")]
		public object Device { get; set; }

        [Category("Gauge Properties")]
		[Description("Associates another control with the gauge")]
		public Control LinkedControl { get; set; }

        [Category("Gauge Properties")]
		[Description("Gets the value to be displayed")]
		public double DisplayValue
        {
            get { return _DisplayValue; }
            private set
            {
                if (_DisplayValue != value || firstValue)
                {
                    _DisplayValue = value;
					firstValue = false;
                    if (DisplayValueChanged != null)
                        DisplayValueChanged(this, null);
                }
            }
        }
        double _DisplayValue;
		bool firstValue = true;

		public void SetDisplayValue(double value)
		{ DisplayValue = value; }

        [Category("Gauge Properties")]
		[Description("Text to prepended to the DisplayValue")]
		public string Prefix { get; set; }

        [Category("Gauge Properties")]
		[Description("Text to appended to the DisplayValue")]
		public string Suffix { get; set; }

        [Category("Gauge Properties")]
		[Description("A string indicating the state of Device")]
		public string DeviceState { get; set; }

        [Category("Gauge Properties")]
		[Description("Whether Device is currently on")]
		public bool DeviceOn { get; set; }

        [Category("Gauge Properties")]
		[Description("True when a meter's Voltage exceeds its MaxVoltage")]
		public bool OverRange { get; set; }

		[Category("Gauge Properties")]
		[Description("True when a meter's Voltage is less than its MinVoltage")]
		public bool UnderRange { get; set; }
		
		[Category("Gauge Properties")]
		[Description("Device's error code")]
		public int Error
        {
            get { return _Error; }
            set
            {
                if (_Error != value)
                {
                    _Error = value;
                    if (DeviceError != null)
                        DeviceError(this, null);
                }
            }
        }
        int _Error;

        [Category("Gauge Properties")]
		[Description("This format string defines how DisplayValue should be shown")]
        public string DisplayFormat { get; set; }

        [Category("Gauge Properties")]
		[Description("If DisplayFormat is empty, DisplayValue is shown in scientific notation with this many significant digits. Ignored if DisplayFormat is non-empty.")]
		public int SignificantDigits { get; set; }

		[Category("Gauge Properties")]
		[Description("Whether to limit DisplayValue to Maximum")]
		public bool ClipMaximum { get; set; }

		[Category("Gauge Properties")]
		[Description("If ClipMaximum is true, DisplayValue will not exceed this number")]
		public double Maximum { get; set; }

		[Category("Gauge Properties")]
		[Description("Whether to limit DisplayValue to Minimum")]
		public bool ClipMinimum { get; set; }

		[Category("Gauge Properties")]
		[Description("If ClipMinimum is true, DisplayValue will never be less than this number")]
		public double Minimum { get; set; }


		double clipMinMax(double value)
		{
			if (ClipMinimum && value < Minimum)
			{
				UnderRange = true;
				value = Minimum;
			}
			else
				UnderRange = false;

			if (ClipMaximum && value > Maximum)
			{
				OverRange = true;
				value = Maximum;
			}
			else
				OverRange = false;
			return value;
		}
		
		double checkMeter(Meter m)
		{
			double value = clipMinMax(m);
            if (!UnderRange && !OverRange)
            {
                if (m.Voltage > m.MaxVoltage)
                    OverRange = true;
                else if (m.Voltage < m.MinVoltage)
                    UnderRange = true;

                if (m.Sensitivity > 0 && value <= m.Sensitivity)
                {
                    UnderRange = true;
                    value = m.Sensitivity;
                }
            }

			return value;
		}

		// take a reading (called by the connected device)
		public void StateChanged()
		{
			DeviceState = Device.ToString();
			if (Device is Meter)
			{
				Meter m = Device as Meter;
				DisplayValue = checkMeter(m);
			}
			else if (Device is Heater)
			{
				Heater h = Device as Heater;
				Error = h.Errors;
				DeviceOn = h.IsOn;
				DisplayValue = clipMinMax(h.Temperature);
			}
			else if (Device is TempSensor)
			{
				TempSensor ts = Device as TempSensor;
				DeviceState = ts.ToString();
				DisplayValue = clipMinMax(ts.Temperature);
			}
            else if (Device is EurothermFurnace)
            {
                EurothermFurnace tf = Device as EurothermFurnace;
                DeviceOn = tf.IsOn;
				DisplayValue = clipMinMax(tf.Temperature);
			}
		}

		#endregion Device Context

		bool looksLikeZero(string s)
		{
			foreach (char c in s)
				if (c > '0' && c <= '9')
					return false;
			return true;
		}

		string valueToText(double value)
        {
			string valueString;
			if (DisplayFormat == "")
            {
                if (SignificantDigits < 2 || SignificantDigits > 6)
                    SignificantDigits = 3;
                valueString = Utility.sigDigitsString(value, SignificantDigits);
            }
            else
                valueString = value.ToString(DisplayFormat);

			string rangeSymbol = "";
			if (Device is Meter && !looksLikeZero(valueString))
			{
				if (OverRange) rangeSymbol = ">";
				else if (UnderRange) rangeSymbol = "<";
			}

			return Prefix + rangeSymbol + valueString + Suffix;
        }

    }
}
