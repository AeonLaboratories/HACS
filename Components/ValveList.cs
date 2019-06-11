using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using HACS.Core;
using System.Threading;
using Newtonsoft.Json;

namespace HACS.Components
{
	//Whether, where, when certain valves should appear in ValveLists.
	//
	// In general:
	//
	// If HV or LV appears and ValveList.Close() or .Open() is invoked, the vacuum
	// system state is changed to Isolate or Evacuate instead of opening or closing
	// that valve directly.
	//
	// Exclude (physically present) port valves (at the IP, VP, GRs, etc.) to have
	// them ignored by general-purpose ValveList methods. This enables and requires
	// their inclusion or exclusion to be controlled by the code that invokes those
	// methods, which is normally the desired functionality.
	//
	// Gas supply valves usually may be included or omitted without affecting general-
	// purpose functionality. It normally doesn't hurt to include them, but their 
	// absence normally does not matter, either, because they are essentially always
	// closed (except briefly to admit gas). If a gas supply valve is included
	// in an Isolation list (i.e., a volume perimeter), and that list is ever 
	// Open()ed, the gas supply valve will be opened along with everything else. This
	// most probably would be undesired; however, Isolation lists are never Open()ed
	// as a whole in the general-purpose code.

	public class ValveList : HacsComponent
    {
		#region Component Implementation

		public static readonly new List<ValveList> List = new List<ValveList>();
		public static new ValveList Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
        {
			if (Valves.Any())
				List.Add(this);         // list only nonempty ValveLists
			else
				Name = null;			// un-name empty valve lists
		}

		public ValveList()
		{
			OnConnect += Connect;
		}

		#endregion Component Implementation


		// TODO: is there any way to "promote" this so the individual <Valve>s
		// appear directly under <ValveList> ?
		[XmlArray("Valves")]
        [XmlArrayItem("ValveRef")]
		[JsonProperty("Valves")]
        public List<HacsComponent<HacsComponent>> ValveRefs { get; set; }
        [XmlIgnore]public List<IValve> Valves => ValveRefs?.Select(vr => vr.Component as IValve).ToList();
        public IValve Last => Valves.Last();


		// If v is the v_HighVacuum or v_LowVacuum of a VacuumSystem, return the VacuumSystem; otherwise null.
		VacuumSystem vacuumSystem(IValve v) => VacuumSystem.List.Find(vs => vs.v_HighVacuum == v || vs.v_LowVacuum == v);


		/// <summary>
		/// Open the valve. If v is a VacuumSystem HV or LV, VacuumSystem.Evacuate() instead.
		/// </summary>
		/// <param name="v">the valve</param>
		/// <param name="wait">optionally, wait for valve motion to finish</param>
		public void Open(IValve v, bool wait = false)
		{
			if (vacuumSystem(v) is VacuumSystem vs)
			{
				vs.Evacuate();      // don't directly control v_HV or v_LV
				while (vs.State != VacuumSystem.States.Roughing &&
					   vs.State != VacuumSystem.States.HighVacuum)
					Thread.Sleep(35);
			}
			else
				v?.Open();

			if (wait) v?.WaitForIdle();
		}

		/// <summary>
		/// Close the valve. If v is a VacuumSystem HV or LV, VacuumSystem.Isolate() instead.
		/// </summary>
		/// <param name="v">the valve</param>
		/// <param name="wait">optionally, wait for valve motion to finish</param>
		public void Close(IValve v, bool wait = false)
		{
			if (vacuumSystem(v) is VacuumSystem vs)
			{
				vs.Isolate();      // don't directly control v_HV or v_LV
				while (vs.State != VacuumSystem.States.Isolated)
					Thread.Sleep(35);
			}
			else
				v?.Close();
			if (wait) v?.WaitForIdle();
		}

		/// <summary>
		/// Open the last valve on the list.
		/// </summary>
		public void OpenLast() => Open(Last, true);

		/// <summary>
		/// Close the last valve on the list.
		/// </summary>
		public void CloseLast() => Close(Last, true);

		/// <summary>
		/// Open all the valves on the list.
		/// </summary>
		public void Open()
        {
			Valves?.ForEach(v => Open(v));
            Last?.WaitForIdle();
        }

		/// <summary>
		/// Close all the valves on the list.
		/// </summary>
		public void Close()
        {
			Valves?.ForEach(v => Close(v));
            Last?.WaitForIdle();
        }

        public bool IsOpened => Valves?.Any(v => !v.IsOpened) ?? false ? false : true;

		public bool IsClosed => Valves?.Any(v => !v.IsClosed) ?? false ? false : true;

	}
}