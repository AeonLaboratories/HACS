using HACS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	/// <summary>
	/// 
	/// </summary>
	public class DynamicQuantity : Component
	{
		public static new List<DynamicQuantity> List;
		public static new DynamicQuantity Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public static implicit operator double(DynamicQuantity dq)
		{
			if (dq == null)
				return 0;
			return dq.Value;
		}

		double _Value;
		[XmlIgnore] public double Value
		{
			get { return _Value; }
			set
			{
				_Value = value;
				if (Initialized)
					RoC?.Update(_Value);
			}
		}

		public RateOfChange RoC { get; set; }

		public override void Initialize()
		{
			RoC?.Initialize();

			base.Initialize();
		}
	}
}
