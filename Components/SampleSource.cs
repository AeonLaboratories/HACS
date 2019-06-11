using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class SampleSource : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<SampleSource> List = new List<SampleSource>();
		public static new SampleSource Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
		{
			if (PathToVTT != null) PathToVTT.Name = Name + ".PathToVTT";
		}

		public SampleSource()
		{
			List.Add(this);
			OnConnect += Connect;
		}

		#endregion Component Implementation


		public static SampleSource Default => List?[0];

		[JsonProperty]
		public HacsComponent<LinePort> LinePortRef { get; set; }
        public LinePort LinePort => LinePortRef?.Component;

		[JsonProperty]
		public HacsComponent<Sample> SampleRef { get; set; }
        public Sample Sample => SampleRef?.Component;

		[JsonProperty]
        public ValveList PathToVTT { get; set; }

    }
}
