using HACS.Core;
using System.Collections.Generic;

namespace HACS.Components
{
	public class VacuumSystem : Component
	{
		public static new List<VacuumSystem> List;
		public static new VacuumSystem Find(string name)
		{ return List?.Find(x => x.Name == name); }
	}
}
