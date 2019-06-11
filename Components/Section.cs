using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class Section : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<Section> List = new List<Section>();
		public static new Section Find(string name) { return List.Find(x => x?.Name == name); }

		protected virtual void Connect()
		{
            if (PathToVacuum != null) PathToVacuum.Name = Name + ".PathToVacuum";
			if (Isolation != null) Isolation.Name = Name + ".Isolation";
			if (InternalValves != null) InternalValves.Name = Name + ".InternalValves";
		}

		public Section()
		{
			List.Add(this);
			OnConnect += Connect;
		}

        #endregion Component Implementation

        [XmlArray("Chambers")]
        [XmlArrayItem("ChamberRef")]
		[JsonProperty("Chambers")]
		public List<HacsComponent<Chamber>> ChamberRefs { get; set; }  // = new List<HacsComponent<Chamber>>();
        [XmlIgnore] public List<Chamber> Chambers => ChamberRefs?.Select(cr => cr.Component).ToList();


        [XmlArray("Ports")]
		[XmlArrayItem("PortRef")]
		[JsonProperty("Ports")]
		public List<HacsComponent<Port>> PortRefs { get; set; }         // = new List<>();
        [XmlIgnore] public List<Port> Ports => PortRefs?.Select(pr => pr.Component).ToList();

		[JsonProperty]
		public HacsComponent<VacuumSystem> VacuumSystemRef { get; set; }
        public VacuumSystem VacuumSystem => VacuumSystemRef?.Component;

		/// <summary>
		/// An ordered list of valves from the Section to its VacuumSystem manifold
		/// </summary>
		[JsonProperty]
		public ValveList PathToVacuum { get; set; }

		/// <summary>
		/// An ordered list of valves that isolate the Section and define
		/// its volume perimeter. Also include any valves required to
		/// isolate the PathToVacuum.
		/// Often, port valves (IP, IP2, GRs, etc) should be omitted
		/// here (use the Ports list, instead). Valves listed
		/// here are always closed to isolate the section, whereas port
		/// valves are only operated explicitly as such, and otherwise
		/// can be omitted from or included in normal Section operations 
		/// by managing them in the calling code, depending on whether 
		/// any or all should be treated as part of the Section according
		/// to the needs of the caller.
		/// </summary>
		[JsonProperty]
		public ValveList Isolation { get; set; }

		/// <summary>
		/// An ordered list of valves that joins the Section chambers into a single volume.
		/// </summary>
		[JsonProperty]
		public ValveList InternalValves { get; set; }

		/// <summary>
		/// Creates a new Section by combining two previously defined Sections.
		/// Section a's VacuumSystem and PathToVacuum are used unless a.PathToVacuum
		/// is null, in which case Section b's are used instead.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static Section Combine(Section a, Section b)
		{
			// The two Sections Isolation lists must have at least one valve in common.
			// If the PathToVacuum isolation were separated, there should be exactly
			// one common isolation valve between two combinable sections.
			var common = a.Isolation.ValveRefs.Intersect(b.Isolation.ValveRefs).ToList();
			if (common.Count() < 1)
				return null;

			var c = new Section();
			c.ChamberRefs = a.ChamberRefs.Union(b.ChamberRefs).ToList();

			if (c.Chambers.Count < 2)
				c.Name = a.Name + "_" + b.Name;
			else
				c.Name = c.Chambers.First().Name + "_" + c.Chambers.Last().Name;

			c.PortRefs = a.PortRefs.Union(b.PortRefs).ToList();

			if (a.PathToVacuum != null)
			{
				c.VacuumSystemRef = a.VacuumSystemRef;
				c.PathToVacuum = a.PathToVacuum;
			}
			else
			{
				c.VacuumSystemRef = b.VacuumSystemRef;
				c.PathToVacuum = b.PathToVacuum;
			}

			// may also (unnecessarily) include the other PathToVacuum isolation
			// no Isolation should ever be empty or null
			c.Isolation = new ValveList();
			c.Isolation.ValveRefs = a.Isolation.ValveRefs.Union(b.Isolation.ValveRefs).ToList();

			c.InternalValves = new ValveList();
			c.InternalValves.ValveRefs = union(a.InternalValves?.ValveRefs, b.InternalValves?.ValveRefs);

			c.InternalValves.ValveRefs.Add(common.First());
			c.Isolation.ValveRefs.Remove(common.First());
			c.PathToVacuum.ValveRefs.Remove(common.First());

			return c;
		}

		static List<HacsComponent<HacsComponent>> union(List<HacsComponent<HacsComponent>> a, List<HacsComponent<HacsComponent>> b)
		{
			if (a == null)
			{
				if (b == null)
                    return new List<HacsComponent<HacsComponent>>();
                else
                    return new List<HacsComponent<HacsComponent>>(b);
			}
			else if (b == null)
                return new List<HacsComponent<HacsComponent>>(a);
            else
                return a.Union(b).ToList();
        }

        /// <summary>
        /// This is a fair approximation.
        /// The volume of a set of joined chambers can differ slightly
        /// from the sum of the chamber volumes, due to small movements
        /// of volumes within the valves themselves.
        /// </summary>
        public double MilliLiters => CurrentVolume();
		public double CurrentVolume() => CurrentVolume(false);
		public double CurrentVolume(bool includePorts)
		{
			double ml = 0;
			Chambers.ForEach(c => ml += c.MilliLiters);
			if (includePorts)
				Ports.ForEach(p => { if (p.IsOpened) ml += p.MilliLiters; });
			return ml;
		}

		/// <summary>
		/// Closes the valves that form the the section boundary.
		/// </summary>
		public void Isolate() { Isolation?.Close(); }

		/// <summary>
		/// Opens the section's internal valves (joins the Chambers).
		/// </summary>
		public void Open() { InternalValves?.Open(); }

		/// <summary>
		/// Closes the section's internal valves (separates the Chambers).
		/// </summary>
		public void Close() { InternalValves?.Close(); }

		/// <summary>
		/// Closes the PathToVacuum, or invokes VacuumSystem.Isolate(), if PathToVacuum is empty.
		/// </summary>
		public void IsolateFromVacuum()
        {
            if (PathToVacuum != null && PathToVacuum.Valves != null && PathToVacuum.Valves.Any())
                PathToVacuum.Close();
            else
                VacuumSystem.Isolate();
        }

		/// <summary>
		/// Opens the PathToVacuum, or invokes VacuumSystem.Evacuate(), if PathToVacuum is empty.
		/// Warning: No vacuum state or pressure checking is done.
		/// </summary>
		public void JoinToVacuum()
		{
			if (PathToVacuum != null && PathToVacuum.Valves != null && PathToVacuum.Valves.Any())
				PathToVacuum.Open();
			else
				VacuumSystem.Evacuate();
		}

		/// <summary>
		/// Isolates the section, joins all internal chambers together and evacuates them.
		/// Port valves are not moved.
		/// Waits 3 seconds after evacuation commences, then returns.
		/// </summary>
		public void OpenAndEvacuate() { OpenAndEvacuate(-1); }
		/// <summary>
		/// Isolates the section, joins all internal chambers together and evacuates them.
		/// Port valves are not moved.
		/// If pressure is 0, waits until pressure_baseline is reached.
		/// If pressure &lt; 0, waits 3 seconds after evacuation commences, then returns.
		/// Otherwise, waits until the given pressure is reached.
		/// </summary>
		/// <param name="pressure">-1 is the default if no pressure is provided</param>
		public void OpenAndEvacuate(double pressure)
		{
			Isolate();
			Open();
			Evacuate(pressure);
		}

		public void Evacuate() { Evacuate(-1); }

		public void Evacuate(double pressure)
        {
			Isolate();		// required to ensure PathToVacuum is isolated
			VacuumSystem.IsolateManifold();
			PathToVacuum?.Open();
            VacuumSystem.Evacuate(pressure);
        }

		/// <summary>
		/// Internal valves are opened and section is joined to the vacuum manifold.
		/// Note: the section need not be evacuated, nor evacuating, just connected 
		/// to the vacuum manifold.
		/// </summary>
		public bool IsOpened
		{
			get
			{
				return (InternalValves == null || InternalValves.IsOpened) && 
					(PathToVacuum == null || PathToVacuum.IsOpened);
			}
		}

		public void OpenPorts() { Ports?.ForEach(p => p.Open()); }
		public void ClosePorts() { Ports?.ForEach(p => p.Close()); }
    }
}