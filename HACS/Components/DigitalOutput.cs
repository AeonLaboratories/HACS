using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using System.Windows.Forms;
using HACS.Core;

namespace HACS.Components
{
	public class DigitalOutput : Component
    {
		public static new List<DigitalOutput> List;
		public static new DigitalOutput Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlElement("LabJack")]
		public string LabJackName { get; set; }
		LabJackDaq LabJack;

		public LabJackDaq.DIO Dio { get; set; }
		public bool IsOn { get; set; }

        [XmlIgnore] public Stopwatch sw = new Stopwatch();
		[XmlIgnore] public long MillisecondsOn { get { return IsOn ? MillisecondsInState : 0; } }
		[XmlIgnore] public long MillisecondsOff { get { return IsOn ? 0 : MillisecondsInState; } }
		[XmlIgnore] public long MillisecondsInState { get { return sw.ElapsedMilliseconds; } }

		public DigitalOutput() { }

		public override void Connect()
		{
			LabJack = LabJackDaq.Find(LabJackName);
		}

		public override void Initialize()
		{
			SetOutput(IsOn);
			Initialized = true;
		}

        public void SetOutput(bool OnOff)
        {
			try
			{
				LabJack.SetDO(Dio, OnOff);
				IsOn = OnOff;
				sw.Restart();
			}
			catch (Exception e) { MessageBox.Show(e.Message + ", " + e.ToString()); }
        }

		public override string ToString()
		{
			return Name + " (" + (IsOn ? "On" : "Off") + "):\r\n" + 
				Utility.IndentLines(String.Format("Dio:{0} msInState:{1}", Dio, MillisecondsInState));
		}
    }
}
