using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class Port : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<Port> List = new List<Port>();
		public static new Port Find(string name) { return List.Find(x => x?.Name == name); }

		public Port()
		{
			List.Add(this);
		}

		#endregion Component Implementation

		[JsonProperty]
		public HacsComponent<HacsComponent> ValveRef { get; set; }
        public IValve Valve => ValveRef?.Component as IValve;

		[JsonProperty]
		public HacsComponent<Chamber> ChamberRef { get; set; }
        public Chamber Chamber => ChamberRef?.Component;

		public double MilliLiters => Chamber?.MilliLiters ?? 0;
		public void Open() => Valve?.OpenWait();
		public void Close() => Valve?.CloseWait();
		public bool IsOpened => Valve?.IsOpened ?? true;
		public bool IsClosed => Valve?.IsClosed ?? false;
    }
}