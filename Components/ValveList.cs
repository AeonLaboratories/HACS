using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static HACS.Core.NamedObject;

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
	public static class ValveList
	{
		static List<IVacuumSystem> vsList;

		// If v is the v_HighVacuum or v_LowVacuum of a VacuumSystem, return the VacuumSystem; otherwise null.
		static IVacuumSystem vacuumSystem(IValve v)
		{
			if (vsList == null) vsList = CachedList<IVacuumSystem>();
			return vsList.Find(vs => vs.HighVacuumValve == v || vs.LowVacuumValve == v);
		}

		/// <summary>
		/// Open the valve v. If v is a VacuumSystem HV or LV, VacuumSystem.Evacuate() instead.
		/// </summary>
		public static void Open<T>(this IEnumerable<T> valves, IValve v) where T : IValve
		{
			if (vacuumSystem(v) is VacuumSystem vs)
			{
				vs.Evacuate();      // don't directly control v_HV or v_LV
				while (vs.State != VacuumSystem.StateCode.Roughing &&
					   vs.State != VacuumSystem.StateCode.HighVacuum)
					Thread.Sleep(35);
			}
			else
				v?.OpenWait();
		}

		/// <summary>
		/// Close the valve. If v is a VacuumSystem HV or LV, VacuumSystem.Isolate() instead.
		/// </summary>
		/// <param name="v">the valve</param>
		/// <param name="wait">optionally, wait for valve motion to finish</param>
		public static void Close<T>(this IEnumerable<T> valves, IValve v, bool wait = false) where T : IValve
		{
			if (vacuumSystem(v) is VacuumSystem vs)
			{
				vs.Isolate();      // don't directly control v_HV or v_LV
				while (vs.State != VacuumSystem.StateCode.Isolated)
					Thread.Sleep(35);
			}
			else
				v?.Close();
			if (wait) v?.WaitForIdle();
		}

		/// <summary>
		/// Open the last valve on the list.
		/// </summary>
		public static void OpenLast<T>(this IEnumerable<T> valves) where T : IValve =>
			valves.Open(valves.Last());

		/// <summary>
		/// Close the last valve on the list.
		/// </summary>
		public static void CloseLast<T>(this IEnumerable<T> valves) where T : IValve =>
			valves.Close(valves.Last(), true);

		/// <summary>
		/// Open all the valves on the list.
		/// </summary>
		public static void Open<T>(this IEnumerable<T> valves) where T : IValve
		{
			foreach (var v in valves)
				valves.Open(v);
			valves.Last()?.WaitForIdle();
		}

		/// <summary>
		/// Close all the valves on the list.
		/// </summary>
		public static void Close<T>(this IEnumerable<T> valves) where T : IValve
		{
			if (valves == null) return;
			foreach (var v in valves)
				valves.Close(v);
			if (valves.Any())
				valves.Last()?.WaitForIdle();
		}

		public static void OpenExcept<T>(this IEnumerable<T> valves, IEnumerable<T> these) where T : IValve
			=> valves.SafeExcept(these).Open();


		public static void CloseExcept<T>(this IEnumerable<T> valves, IEnumerable<T> these) where T : IValve
			=> valves.SafeExcept(these).Close();


		/// <summary>
		/// True if all of the valves in the list are Opened
		/// </summary>
		public static bool IsOpened<T>(this IEnumerable<T> valves) where T : IValve => valves?.Any(v => !v.IsOpened) ?? false ? false : true;

		/// <summary>
		/// True if all of the valves in the list are Closed
		/// </summary>
		public static bool IsClosed<T>(this IEnumerable<T> valves) where T : IValve => valves?.Any(v => !v.IsClosed) ?? false ? false : true;
	}

}