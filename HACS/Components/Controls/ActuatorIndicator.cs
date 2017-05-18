using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace HACS.Components.Controls
{
    public class ActuatorIndicator : PictureBox
    {
        [ReadOnly(true)] public override string Text { get { return ""; } }
        [ReadOnly(true)] public override bool AutoSize { get { return false; } }

        public event EventHandler DeviceStateChanged;
        public Actuator Actuator { get; set; }

		public void UpdateUI()
		{
			if (DeviceStateChanged != null)
				DeviceStateChanged(this, null);
		}
		
		public void Connect(Actuator a)
        { Actuator = a; a.StateChanged = UpdateUI; }
    }

    [
        Description("Valve Indicator"),
        DefaultEvent("Click")
    ]
    public class ValveIndicator : ActuatorIndicator
    {
        public ValveIndicator()
        {
            Width = 18;
            Height = 18;
            BorderStyle = BorderStyle.FixedSingle;
        }
    }
}
