using HACS.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class ProcessSequence : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<ProcessSequence> List = new List<ProcessSequence>();
		public static new ProcessSequence Find(string name) { return List.Find(x => x?.Name == name); }

		public ProcessSequence()
		{
			List.Add(this);
		}

		#endregion Component Implementation
		
		[JsonProperty] public HacsComponent<SampleSource> SampleSourceRef { get; set; }
		public SampleSource SampleSource => SampleSourceRef?.Component;
		[JsonProperty] public List<ProcessSequenceStep> Steps;

		public ProcessSequence(string name) : this()
		{
			Name = name;
			Steps = new List<ProcessSequenceStep>();
		}

		public ProcessSequence(string name, SampleSource source)
		{
			Name = name;
			SampleSourceRef.Name = source.Name;
			Steps = new List<ProcessSequenceStep>();
		}

		public ProcessSequence Clone()
		{
			ProcessSequence ps = new ProcessSequence(Name, SampleSource);
			foreach (ProcessSequenceStep pss in Steps)
			{
				ps.Steps.Add(pss.Clone());
			}
			return ps;
		}

		public override string ToString()
		{
			return Name;
		}
	}

	[XmlInclude(typeof(CombustionStep))]
    [XmlInclude(typeof(WaitMinutesStep))]
    public class ProcessSequenceStep : INamedObject
    {
		[XmlAttribute]
		public virtual string Name { get; set; }

        public ProcessSequenceStep() { }

		public ProcessSequenceStep(string name)
		{
			Name = name;
		}

		public virtual ProcessSequenceStep Clone()
		{
			return new ProcessSequenceStep(Name);
		}

		public override string ToString()
		{
			if (Name.EndsWith("_"))
				return Name.Substring(0, Name.Length - 1);
			return Name;
		}
	}

	public class CombustionStep : ProcessSequenceStep
	{
		public int Temperature;
		public int Minutes;
		public bool AdmitO2;
		public bool OpenLine;
		public bool WaitForSetpoint;

		public CombustionStep()
		{
			Name = "Combust";
		}

		public CombustionStep(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
			: this()
		{
			Temperature = temperature;
			Minutes = minutes;
			AdmitO2 = admitO2;
			OpenLine = openLine;
			WaitForSetpoint = waitForSetpoint;
		}

		public CombustionStep(string name, int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
		{
			Name = name;
			Temperature = temperature;
			Minutes = minutes;
			AdmitO2 = admitO2;
			OpenLine = openLine;
			WaitForSetpoint = waitForSetpoint;
		}

		public override ProcessSequenceStep Clone()
		{
			return new CombustionStep(Name, Temperature, Minutes, AdmitO2, OpenLine, WaitForSetpoint);
		}

		public override string ToString()
		{
			string title = Name + " at " + Temperature + " for " + Minutes + " m.";
			if (AdmitO2)
				title += " Admit O2.";
			if (OpenLine)
				title += " Open Line.";
			if (WaitForSetpoint)
				title += " Wait For Setpoint.";
			return title;
		}
	}

	public class WaitMinutesStep : ProcessSequenceStep
	{
		public int Minutes;

		public WaitMinutesStep()
		{
			Name = "Wait Minutes";
		}

		public WaitMinutesStep(int minutes)
			: this()
		{
			Minutes = minutes;
		}

		public WaitMinutesStep(string name, int minutes)
		{
			Name = name;
			Minutes = minutes;
		}

		public override ProcessSequenceStep Clone()
		{
			return new WaitMinutesStep(Name, Minutes);
		}

		public override string ToString()
		{
			return "Wait for " + Minutes + " m.";
		}
	}
}
