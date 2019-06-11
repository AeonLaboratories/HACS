using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace HACS.Components
{
	public class Chamber : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<Chamber> List = new List<Chamber>();
		public static new Chamber Find(string name) { return List.Find(x => x?.Name == name); }

		public Chamber()
		{
			List.Add(this);
		}

		#endregion Component Implementation

		/// <summary>
		/// The chamber volume in milliliters / cubic centimeters
		/// </summary>
		[JsonProperty]
		public double MilliLiters { get; set; }
    }
}