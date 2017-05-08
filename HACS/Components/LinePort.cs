using HACS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class LinePort : Component
	{
		public static new List<LinePort> List;
		public static new LinePort Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType = true)]
		public enum States { Loaded, Prepared, InProcess, Complete }

		public States State { get; set; }
		public string Contents { get; set; }

		public LinePort() { }

		public LinePort(string name, States state, string contents)
		{
			Name = name;
			State = state;
			Contents = contents;
		}

		public override string ToString()
		{
			string s = Name + ": " + State.ToString();
			if (!string.IsNullOrEmpty(Contents))
				s += " (" + Contents + ")";
			return s;
		}
	}
}
