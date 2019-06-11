using System;
using System.Collections.Generic;
using Utilities;

namespace HACS.Core
{
	public class FindableObject : NamedObject
	{
		public static List<FindableObject> List;
		public static FindableObject Find(string name) { return List?.Find(x => x?.Name == name); }
	}
}
