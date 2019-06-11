using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using HACS.Core;
using Newtonsoft.Json;
using Utilities;

namespace HACS.Components
{
    public class AnalogOutput : HacsComponent
    {
		#region Component Implementation

		public static readonly new List<AnalogOutput> List = new List<AnalogOutput>();
		public static new AnalogOutput Find(string name) { return List.Find(x => x?.Name == name); }

        protected void Initialize()
        {
            SetOutput(OutputVoltage);
        }

		public AnalogOutput()
		{
			List.Add(this);
			OnInitialize += Initialize;
		}

		#endregion Component Implementation


		[JsonProperty]
		public HacsComponent<LabJackDaq> LabJackRef { get; set; }
        LabJackDaq LabJack => LabJackRef?.Component;

		[JsonProperty]
		public int Channel { get; set; }

		[JsonProperty]
		public double OutputVoltage { get; set; }

        [XmlIgnore] public Stopwatch sw = new Stopwatch();
        public long MillisecondsInState => sw.ElapsedMilliseconds;

        public void SetOutput(double voltage)
        {
            if (Initialized && OutputVoltage == voltage) return;
            try
            {
                OutputVoltage = voltage;
                LabJack.SetAO(Channel, OutputVoltage);
                sw.Restart();
            }
            catch (Exception e) { Notice.Send(e.Message + ", " + e.ToString()); }
        }

        public override string ToString()
        {
            return $"{Name}: V = {OutputVoltage:1,2}, {MillisecondsInState/1000:0)} seconds";
        }
    }
}
