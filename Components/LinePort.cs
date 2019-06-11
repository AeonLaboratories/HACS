using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class LinePort : Port
	{
		#region Component Implementation

		public static readonly new List<LinePort> List = new List<LinePort>();
		public static new LinePort Find(string name) { return List.Find(x => x?.Name == name); }

		public LinePort()
		{
			List.Add(this);
		}

		#endregion Component Implementation


		[XmlType(AnonymousType = true)]
		public enum States { Loaded, Prepared, InProcess, Complete }
		[JsonProperty]
		public States State { get; set; }
		[JsonProperty]
		public string Contents { get; set; }

		public override string ToString()
		{
			string s = Name + ": " + State.ToString();
			if (!string.IsNullOrEmpty(Contents))
				s += " (" + Contents + ")";
			return s;
		}
	}
}
