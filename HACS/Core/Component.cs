using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Core
{
	public class Component : FindableObject
	{
		public static new List<Component> List { get; set; }
		public static new Component Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlIgnore]
		public virtual bool Initialized { get; protected set; }

		public virtual void Connect() { }

		public virtual void Initialize() { Initialized = true; }
	}
}
