using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	public class OnOffDevice : Component
	{
		public static new List<OnOffDevice> List;
		public static new OnOffDevice Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlElement("Controller")]
		public string SwitchBankName { get; set; }
		SwitchBank Controller;

		public int Channel { get; set; }
		public bool IsOn { get; set; }

		[XmlIgnore] public Stopwatch sw = new Stopwatch();
		[XmlIgnore] public long MillisecondsOn { get { return IsOn ? MillisecondsInState : 0; } }
		[XmlIgnore] public long MillisecondsOff { get { return IsOn ? 0 : MillisecondsInState; } }
		[XmlIgnore] public long MillisecondsInState { get { return sw.ElapsedMilliseconds; } }

		public OnOffDevice() { }

		public override void Connect()
		{
			SwitchBank c = SwitchBank.Find(SwitchBankName);
			Connect(c);
		}

		public void Connect(SwitchBank c)
		{
			if (Controller != c)
			{
				Controller = c;

				if (Controller != null)
					Controller.Connect(this);
			}
		}

		public override void Initialize()
		{
			//ForceState(IsOn);
			if (IsOn) ForceOn();
			else ForceOff();

			Initialized = true;
		}

		void ForceState(bool onOff)
		{
			if (onOff)
				Controller.TurnOn(Channel);
			else
				Controller.TurnOff(Channel);

			sw.Restart();
		}

		void ForceOn()
		{
			Controller.TurnOn(Channel);
			sw.Restart();
		}

		void ForceOff()
		{
			Controller.TurnOff(Channel);
			sw.Restart();
		}

		public void TurnOn() { if (!IsOn) ForceOn(); }

		public void TurnOff() { if (IsOn) ForceOff(); }

		public override string ToString()
		{
			return Name + " (" + (IsOn ? "On" : "Off") + ")";
		}
	}
}
