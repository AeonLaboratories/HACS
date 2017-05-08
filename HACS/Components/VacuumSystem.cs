using HACS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HACS.Components
{
	public class VacuumSystem : Component
	{
		public static new List<VacuumSystem> List;
		public static new VacuumSystem Find(string name)
		{ return List?.Find(x => x.Name == name); }
	}
}
