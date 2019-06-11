using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using System.Windows.Forms;
using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
	public class DigitalOutput : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<DigitalOutput> List = new List<DigitalOutput>();
		public static new DigitalOutput Find(string name) { return List.Find(x => x?.Name == name); }

        protected void Initialize()
        {
            SetOutput(IsOn);
        }

		public DigitalOutput()
		{
			List.Add(this);
			OnInitialize += Initialize;
		}

		#endregion Component Implementation
		
		[JsonProperty]
		public HacsComponent<LabJackDaq> LabJackRef { get; set; }
        LabJackDaq LabJack => LabJackRef?.Component;

		[JsonProperty]
		public LabJackDaq.DIO Dio { get; set; }
		[JsonProperty]
		public bool IsOn { get; set; }

		[XmlIgnore] public Stopwatch sw = new Stopwatch();
		public long MillisecondsOn => IsOn ? MillisecondsInState : 0;
		public long MillisecondsOff => IsOn ? 0 : MillisecondsInState;
		public long MillisecondsInState => sw.ElapsedMilliseconds;

		public void SetOutput(bool OnOff)
		{
			try
			{
				LabJack.SetDO(Dio, OnOff);
				IsOn = OnOff;
				sw.Restart();
			}
			catch (Exception e) { Notice.Send(e.Message + ", " + e.ToString()); }
		}

		public override string ToString()
		{
			return Name + " (" + (IsOn ? "On" : "Off") + "):\r\n" + 
				Utility.IndentLines(String.Format("Dio:{0} msInState:{1}", Dio, MillisecondsInState));
		}
	}
}
