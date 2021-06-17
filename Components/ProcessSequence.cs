using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace HACS.Components
{
	public class ProcessSequence : HacsComponent, IProcessSequence
	{
		#region HacsComponent

		#endregion HacsComponent

		[JsonProperty] public InletPort.Type PortType
		{
			get => portType;
			set => Ensure(ref portType, value);
		}
		InletPort.Type portType;

		[JsonProperty] public List<string> CheckList
		{
			get => checkList;
			set => Ensure(ref checkList, value);
		}
		List<string> checkList;

		[JsonProperty] public List<ProcessSequenceStep> Steps
		{
			get => steps;
			set => Ensure(ref steps, value);
		}
		List<ProcessSequenceStep> steps;

		public ProcessSequence() { }

        public ProcessSequence(string name) : this(name, InletPort.Type.Combustion) { }

		public ProcessSequence(string name, InletPort.Type source)
		{
			Name = name;
			PortType = source;
			Steps = new List<ProcessSequenceStep>();
		}

		public ProcessSequence Clone()
		{
			ProcessSequence ps = new ProcessSequence(Name, PortType);
            Steps.ForEach(pss => ps.Steps.Add(pss.Clone()));
			return ps;
		}

        public override string ToString() => Name;
	}

    public class ProcessSequenceStep : NamedObject, IProcessSequenceStep
    {
        public ProcessSequenceStep() { }

		public ProcessSequenceStep(string name) { Name = name; }

		public virtual ProcessSequenceStep Clone() => new ProcessSequenceStep(Name);

		public override string ToString()
		{
			if (Name.EndsWith("_"))
				return Name.Substring(0, Name.Length - 1);
			return Name;
		}
	}

	public abstract class ParameterizedStep : ProcessSequenceStep { }

	[Description("Combust the sample")]
	public class CombustionStep : ParameterizedStep, ICombustionStep
	{
		[JsonProperty]
		public int Temperature { get; set; }
		[JsonProperty]
		public int Minutes { get; set; }
		[JsonProperty]
		public bool AdmitO2 { get; set; }
		[JsonProperty]
		public bool OpenLine { get; set; }
		[JsonProperty]
		public bool WaitForSetpoint { get; set; }

		public CombustionStep() : this(25, 0, false, false, false) { }

        public CombustionStep(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
            : this("Combust", temperature, minutes, admitO2, openLine, waitForSetpoint) { }

		public CombustionStep(string name, int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint)
		{
			Name = name;
			Temperature = temperature;
			Minutes = minutes;
			AdmitO2 = admitO2;
			OpenLine = openLine;
			WaitForSetpoint = waitForSetpoint;
		}

		public override ProcessSequenceStep Clone() =>
            new CombustionStep(Name, Temperature, Minutes, AdmitO2, OpenLine, WaitForSetpoint);

		public override string ToString()
		{
            var sb = new StringBuilder($"{Name} at {Temperature} for {Minutes} minutes.");
			if (AdmitO2)
				sb.Append(" Admit O2.");
			if (OpenLine)
				sb.Append(" Open Line.");
			if (WaitForSetpoint)
				sb.Append(" Wait For Setpoint.");
			return sb.ToString();
		}
	}

	public class WaitMinutesStep : ParameterizedStep, IWaitMinutesStep
	{
		public int Minutes { get; set; }

        public WaitMinutesStep() : this(0) { }

        public WaitMinutesStep(int minutes) : this("Wait Minutes", minutes) { }

		public WaitMinutesStep(string name, int minutes)
		{
			Name = name;
			Minutes = minutes;
		}

		public override ProcessSequenceStep Clone() => new WaitMinutesStep(Name, Minutes);

		public override string ToString() => $"Wait for {Minutes} minutes.";
	}
}
