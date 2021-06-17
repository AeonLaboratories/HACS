using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Utilities;

namespace HACS.Components
{
    public class Section : HacsComponent, ISection
	{
		#region static
		/// <summary>
		/// Creates a new (unnamed) Section by combining two previously defined Sections.
		/// Section a's VacuumSystem and PathToVacuum are used unless a.PathToVacuum
		/// is null, in which case Section b's are used instead.
		/// </summary>
		public static Section Combine(ISection a, ISection b)
		{
			var common = Connections(a, b);
			if (common == null) return null;

			var s = new Section();
			s.Chambers = a?.Chambers?.SafeUnion(b.Chambers);
			s.Ports = a?.Ports.SafeUnion(b.Ports);

			if (a.PathToVacuum != null)
			{
				s.VacuumSystem = a.VacuumSystem;
				s.PathToVacuum = a.PathToVacuum;
				s.PathToVacuumIsolation = a.PathToVacuumIsolation;
			}
			else
			{
				s.VacuumSystem = b.VacuumSystem;
				s.PathToVacuum = b.PathToVacuum;
				s.PathToVacuumIsolation = b.PathToVacuumIsolation;
			}

			s.InternalValves = a.InternalValves?.SafeUnion(b.InternalValves);
			s.InternalValves = s.InternalValves?.SafeUnion(common);

			s.Isolation = b.Isolation?.SafeUnion(a.Isolation);

			// remove all the internal valves from Isolation
			s.InternalValves?.ForEach(v => s.Isolation?.Remove(v));

			// remove all the common valves from PathToVacuum
			common.ForEach(v => s.PathToVacuum?.Remove(v));

			// remove all Isolation valves from PathToVacuumIsolation
			s.Isolation?.ForEach(v => s.PathToVacuumIsolation?.Remove(v));

			return s;
		}

		/// <summary>
		/// All of the valves that directly connect the two Sections.
		/// </summary>
		/// <returns>null if no connecting valves are found</returns>
		public static List<IValve> Connections(ISection a, ISection b)
		{
			// Find the valves that each Isolation list has in common
			// with the other's Isolation or InternalValves lists.
			var c1 = a?.Isolation?.SafeIntersect(b?.Isolation?.SafeUnion(b?.InternalValves));
			var c2 = b?.Isolation?.SafeIntersect(a?.Isolation?.SafeUnion(a?.InternalValves));
			var common = c1?.SafeUnion(c2);

			return common.Count > 0 ? common : null;
		}

		#endregion static

		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			Chambers = FindAll<IChamber>(chamberNames);
			Ports = FindAll<IPort>(portNames);
			VacuumSystem = Find<IVacuumSystem>(vacuumSystemName);
			Isolation = FindAll<IValve>(isolationValveNames);
			InternalValves = FindAll<IValve>(internalValveNames);
			PathToVacuum = FindAll<IValve>(pathToVacuumValveNames);
			PathToVacuumIsolation = FindAll<IValve>(pathToVacuumIsolationValveNames);
		}

		#endregion HacsComponent

		[JsonProperty("Chambers")]
		List<string> ChamberNames { get => Chambers?.Names(); set => chamberNames = value; }
		List<string> chamberNames;
		/// <summary>
		/// The Chambers that together make up the Section.
		/// </summary>
		// TODO: make this an ObervableItemList and update the chamber searches when
		// the list items change
		public List<IChamber> Chambers
		{
			get => chambers;
			set
			{
				Ensure(ref chambers, value, NotifyPropertyChanged);
				Manometer = chambers.Find(x => x.Manometer != null) is Chamber mc ? mc.Manometer : default;
				Thermometer = chambers.Find(x => x.Thermometer != null) is Chamber tc ? tc.Thermometer : default;
			}
		}
		List<IChamber> chambers;

		[JsonProperty("Ports")]
		List<string> PortNames { get => Ports?.Names(); set => portNames = value; }
		List<string> portNames;
		/// <summary>
		/// Any Ports connected to the Section.
		/// </summary>
		public List<IPort> Ports
		{
			get => ports;
			set => Ensure(ref ports, value, NotifyPropertyChanged);
		}
		List<IPort> ports;

		[JsonProperty("VacuumSystem")]
		string VacuumSystemName { get => VacuumSystem?.Name; set => vacuumSystemName = value; }
		string vacuumSystemName;
		/// <summary>
		/// The VacuumSystem used to evacuate the Section.
		/// </summary>
		public IVacuumSystem VacuumSystem
		{
			get => vacuumSystem;
			set => Ensure(ref vacuumSystem, value, NotifyPropertyChanged);
		}
		IVacuumSystem vacuumSystem;

		[JsonProperty("Isolation")]
		List<string> IsolationValveNames { get => Isolation?.Names(); set => isolationValveNames = value; }
		List<string> isolationValveNames;
		/// <summary>
		/// The ordered list of valves that isolate the Section and define
		/// its volume perimeter.
		/// Usually, port valves should be omitted here (use the Ports list, 
		/// instead). Valves listed here are always closed to isolate the 
		/// section, whereas port valves are only operated explicitly as 
		/// such, and otherwise can be omitted from or included in normal 
		/// Section operations by managing them in the calling code, depending 
		/// on whether any or all should be treated as part of the Section 
		/// according to the needs of the caller.
		/// </summary>
		public List<IValve> Isolation
		{
			get => isolation;
			set => Ensure(ref isolation, value, NotifyPropertyChanged);
		}
		List<IValve> isolation;

		[JsonProperty("InternalValves")]
		List<string> InternalValveNames { get => InternalValves?.Names(); set => internalValveNames = value; }
		List<string> internalValveNames;
		/// <summary>
		/// The ordered list of valves that join the Section chambers into a single volume.
		/// </summary>
		public List<IValve> InternalValves
		{
			get => internalValves;
			set => Ensure(ref internalValves, value, NotifyPropertyChanged);
		}
		List<IValve> internalValves;

		[JsonProperty("PathToVacuum")]
		List<string> PathToVacuumValveNames { get => PathToVacuum?.Names(); set => pathToVacuumValveNames = value; }
		List<string> pathToVacuumValveNames;
		/// <summary>
		/// The ordered list of valves that join the Section to its VacuumSystem manifold.
		/// The last valve in the list is on the VacuumSystem manifold, and usually
		/// the first valve is the last one on Isolation.
		/// If VacuumSystem.VacuumManifold is part of the Section, PathToVacuum should be null.
		/// </summary>
		public List<IValve> PathToVacuum
		{
			get => pathToVacuum;
			set => Ensure(ref pathToVacuum, value, NotifyPropertyChanged);
		}
		List<IValve> pathToVacuum;

		[JsonProperty("PathToVacuumIsolation")]
		List<string> PathToVacuumIsolationValveNames { get => PathToVacuumIsolation?.Names(); set => pathToVacuumIsolationValveNames = value; }
		List<string> pathToVacuumIsolationValveNames;
		/// <summary>
		/// The ordered list of valves that isolate the PathToVacuum.
		/// </summary>
		public List<IValve> PathToVacuumIsolation
		{
			get => pathToVacuumIsolation;
			set => Ensure(ref pathToVacuumIsolation, value, NotifyPropertyChanged);
		}
		List<IValve> pathToVacuumIsolation;

		/// <summary>
		/// The measured volume of the joined chambers, or the
		/// CurrentVolume() if no measurement has been stored.
		/// The measured value may differ slightly from calculated
		/// sum of the individual chamber volumes, due to small
		/// movements of sub-volumes within the valves.
		/// </summary>
		public double MilliLiters
		{
			get => ((milliLiters ?? 0) == 0) ? CurrentVolume() : (double) milliLiters;
			set => Ensure(ref milliLiters, value);
		}
		[JsonProperty("MilliLiters")]
		double? milliLiters;

		/// <summary>
		/// An approximate volume of the section, the sum of its
		/// chamber volumes. This may differ slightly from the value
		/// measured for the joined chambers, due to small movements
		/// of sub-volumes within the valves.
		/// </summary>
		/// <returns>MilliLiters</returns>
		public double CurrentVolume() => CurrentVolume(false);

		/// <summary>
		/// The sum of the Chamber volumes and optionally the volumes of opened Ports.
		/// </summary>
		/// <param name="includePorts">include the opened port volumes?</param>
		public double CurrentVolume(bool includePorts)
		{
			double ml = 0;
			Chambers.ForEach(c => ml += c.MilliLiters);
			InternalValves?.ForEach(v => { if (v.IsOpened) ml += v.OpenedVolumeDelta; });
			if (includePorts)
				Ports?.ForEach(p => { if (p.IsOpened) ml += p.MilliLiters + p.Valve.OpenedVolumeDelta; });
			return ml;
		}

		/// <summary>
		/// The Pressure (sensor) of the first Chamber that has one.
		/// </summary>
		public IManometer Manometer
		{
			get => manometer ?? (Chambers.Find(x => x.Manometer != null) is IChamber c ? c.Manometer : null);
			set => Ensure(ref manometer, value, NotifyPropertyChanged);
		}
		IManometer manometer;
		public double Pressure => Manometer?.Pressure ?? 0;

		/// <summary>
		/// The Temperature (sensor) of the first Chamber that has one.
		/// </summary>
		public IThermometer Thermometer
		{
			get => thermometer ?? (Chambers.Find(x => x.Thermometer != null) is IChamber c ? c.Thermometer : null);
			set => Ensure(ref thermometer, value, NotifyPropertyChanged);
		}
		IThermometer thermometer;
		public double Temperature => Thermometer?.Temperature ?? 0;

		/// <summary>
		/// If the Section comprises a single FlowChamber, this is its flow valve.
		/// </summary>
		public IRxValve FlowValve =>
			Chambers.Count == 1 && Chambers[0] is IFlowChamber c ? c.FlowValve : default;

		/// <summary>
		/// If the Section comprises a single FlowChamber, this is its FlowManager.
		/// </summary>
		public IFlowManager FlowManager
		{
			get => flowManager ?? (Chambers.Count == 1 && Chambers[0] is IFlowChamber c ? c.FlowManager : default);
			set => Ensure(ref flowManager, value);
		}
		IFlowManager flowManager;

		/// <summary>
		/// Gets whether any of the Chambers are Dirty, or sets
		/// them all Dirty.
		/// </summary>
		public bool Dirty
		{
			get => Chambers.Find(x => x.Dirty) is IChamber c;
			set
			{
				Chambers.ForEach(x => x.Dirty = value);
				NotifyPropertyChanged();
			}
		}

		/// <summary>
		/// A method for cleaning the section;
		/// </summary>
		public virtual Action Clean { get; set; }

		/// <summary>
		/// The Heater of the first Chamber that has one.
		/// </summary>
		public IHeater Heater
		{
			get => heater ?? (Chambers.Find(x => x.Heater != null) is IChamber c ? c.Heater : null);
			set => Ensure(ref heater, value);
		}
		IHeater heater;

		/// <summary>
		/// The Coldfinger of the first Chamber that has one.
		/// </summary>
		public IColdfinger Coldfinger
		{
			get => coldfinger ?? (Chambers.Find(x => x.Coldfinger != null) is IChamber c ? c.Coldfinger : null);
			set => Ensure(ref coldfinger, value);
		}
		IColdfinger coldfinger;

		/// <summary>
		/// The Variable-Temperature Coldfinger of the first Chamber that has one.
		/// </summary>
		public IVTColdfinger VTColdfinger
		{
			get => vtColdfinger ?? (Chambers.Find(x => x.VTColdfinger != null) is IChamber c ? c.VTColdfinger : null);
			set => Ensure(ref vtColdfinger, value);
		}
		IVTColdfinger vtColdfinger;

		/// <summary>
		/// Close the valves that form the Section boundary.
		/// </summary>
		public void Isolate() => Isolation?.Close();

		/// <summary>
		/// Close the valves that form the Section boundary except for the given list.
		/// </summary>
		/// <param name="valves"></param>
		public void IsolateExcept(IEnumerable<IValve> valves) =>
			Isolation?.CloseExcept(valves);


		/// <summary>
		/// Open the Section's internal valves (join all the Chambers).
		/// </summary>
		public void Open() => InternalValves?.Open();

		/// <summary>
		/// Close the Section's internal valves (separate all the Chambers).
		/// </summary>
		public void Close() => InternalValves?.Close();

        /// <summary>
        /// Close PathToVacuum. If PathToVacuum is null, invoke VacuumSystem.Isolate() instead.
        /// </summary>
        public void IsolateFromVacuum()
        {
            if (PathToVacuum != null && PathToVacuum.Any())
                PathToVacuum.Close();
            else
                VacuumSystem.Isolate();
        }


		/// <summary>
		/// Open PathToVacuum. If PathToVacuum is null, invoke VacuumSystem.Evacuate() instead.
		/// Warning: No vacuum state or pressure checking is done.
		/// </summary>
		public void JoinToVacuum()
		{
			PathToVacuumIsolation?.Close();
			if (PathToVacuum != null && PathToVacuum.Any())
				PathToVacuum.Open();
			else
				VacuumSystem.Evacuate();
		}

		/// <summary>
		/// Isolate the section and connect it to the Vacuum Manifold
		/// if possible. If there is no PathToVacuum, isolate the
		/// section and the VacuumSystem Manifold.
		/// </summary>
		public void IsolateAndJoinToVacuum()
		{
			var toBeOpened = PathToVacuum;
			var toBeClosed = Isolation.SafeUnion(PathToVacuumIsolation);

			var firstIsolateVacuumSystem = toBeOpened != null && toBeOpened.Any();
			if (firstIsolateVacuumSystem)
				VacuumSystem.Isolate();

			VacuumSystem.IsolateExcept(toBeOpened);
			toBeClosed?.CloseExcept(toBeOpened);

			toBeOpened?.Open();
		}

		/// <summary>
		/// Isolate the section, join all chambers together, and evacuate them.
		/// Wait 3 seconds after evacuation commences.
		/// Port valves are not moved.
		/// </summary>
		public void OpenAndEvacuate() { OpenAndEvacuate(-1); }

		/// <summary>
		/// Isolate the section, join all chambers together, and evacuate them.
		/// No port valves are moved if no port is specified. If a port is given,
		/// it is opened and all others are closed.
		/// If pressure is 0, wait until pressure_baseline is reached.
		/// If pressure &lt; 0, wait 3 seconds after evacuation commences.
		/// Otherwise, wait until the given pressure is reached.
		/// </summary>
		/// <param name="pressure">-1 is the default if no pressure is provided</param>
		/// <param name="port">also evacuate this port and no others</param>
		public void OpenAndEvacuate(double pressure = -1, IPort port = null)
        {
			var toBeOpened = InternalValves.SafeUnion(PathToVacuum);
			var toBeClosed = Isolation.SafeUnion(PathToVacuumIsolation);

			var firstIsolateVacuumSystem = toBeOpened != null && toBeOpened.Any();
			if (firstIsolateVacuumSystem)
				VacuumSystem.Isolate();

			VacuumSystem.IsolateExcept(toBeOpened);
			toBeClosed?.CloseExcept(toBeOpened);

			if (port != null)
			{
				ClosePortsExcept(port);
				port.Open();
			}

			toBeOpened?.Open();

			VacuumSystem.Evacuate(pressure);
		}

		/// <summary>
		/// Isolate the section, join all chambers together, open all ports,
		/// and evacuate them.
		/// WARNING: Do not use this method if any of the ports might be
		/// open to atmosphere or otherwise exposed to an essentially infinite 
		/// supply of gas.
		/// If pressure is 0, wait until pressure_baseline is reached.
		/// If pressure &lt; 0, wait 3 seconds after evacuation commences.
		/// Otherwise, wait until the given pressure is reached.
		/// </summary>
		/// <param name="pressure">-1 is the default if no pressure is provided</param>
		public void OpenAndEvacuateAll(double pressure = -1)
		{
			var toBeOpened = InternalValves.SafeUnion(PathToVacuum);
			var toBeClosed = Isolation.SafeUnion(PathToVacuumIsolation);

			// TODO: consider skipping this if all of toBeOpened
			// are actually already opened.
			if (toBeOpened != null && toBeOpened.Any())
				VacuumSystem.Isolate();

			VacuumSystem.IsolateExcept(toBeOpened);
			toBeClosed?.CloseExcept(toBeOpened);

			OpenPorts();
			toBeOpened?.Open();

			// TODO: add some safety code to time out, isolate the vacuum
			// system, issue a Warning, etc., in case the pressure cannot
			// be reached.
			VacuumSystem.Evacuate(pressure);
		}



		/// <summary>
		/// Isolate the Section and begin evacuating it. All other 
		/// valves on the VacuumSystem manifold are closed first.
		/// Wait three seconds after evacuation commences, then return.
		/// </summary>
		public void Evacuate() { Evacuate(-1); }

        /// <summary>
        /// Isolate the Section and evacuate it to the given pressure. All other 
        /// valves on the VacuumSystem manifold are closed first.
        /// </summary>
        /// <param name="pressure">wait until this pressure is reached</param>
		public void Evacuate(double pressure)
        {
			IsolateAndJoinToVacuum();
			VacuumSystem.Evacuate(pressure);
        }

		/// <summary>
		/// All internal valves are opened and the section is joined to the vacuum manifold.
		/// Note: the section need not be evacuated, nor evacuating, just connected 
		/// to the vacuum manifold.
		/// </summary>
		public bool IsOpened
		{
			get
			{
				return (InternalValves == null || InternalValves.IsOpened()) && 
					(PathToVacuum == null || PathToVacuum.IsOpened());
			}
		}

        /// <summary>
        /// Open all of the Ports on the Section.
        /// </summary>
		public void OpenPorts() { Ports?.ForEach(p => p.Open()); }

		/// <summary>
		/// Close all of the Ports on the Section.
		/// </summary>
		public void ClosePorts() => Ports?.ForEach(p => p.Close());

		/// <summary>
		/// Close all of the Ports except the given one.
		/// </summary>
		public void ClosePortsExcept(IPort port) =>
			Ports?.ForEach(p => { if (p != port) p.Close(); });

		/// <summary>
		/// A list of all valves that directly connect this Section to the given Section.
		/// </summary>
		/// <returns>null if no connecting valves are found</returns>
		public List<IValve> Connections(ISection s) =>
			Connections(this, s);

		/// <summary>
		/// Joins the given Section to this one by opening
		/// all valves between them.
		/// </summary>
		/// <returns>true if successful, false if no joining valve was found</returns>
		public bool JoinTo(ISection s)
		{
			if (Connections(s) is List<IValve> connections)
			{
				connections.Open();
				return connections.Count > 0;
			}
			return false;
		}

		/// <summary>
		/// Isolates the given Section from this one by closing all
		/// valves between them.
		/// </summary>
		/// <returns>true if successful, false if no joining valves were found</returns>
		public bool IsolateFrom(ISection s)
		{
			if (Connections(s).SafeExcept(InternalValves) is List<IValve> connections &&
				connections.Count > 0)
			{
				connections.Close();
				return true;
			}
			return false;
		}


		/// <summary>
		/// A clone of this Section, but without a Name.
		/// </summary>
		public Section Clone()
        {
			var s = new Section();
			s.Chambers = Chambers?.ToList();
			s.Ports = Ports?.ToList();
			s.VacuumSystem = VacuumSystem;
			s.PathToVacuum = PathToVacuum?.ToList();
			s.Isolation = Isolation?.ToList();
			s.InternalValves = InternalValves?.ToList();
			return s;
		}


		public override string ToString()
        {
			//TODO flesh out
			var sb = new StringBuilder();
			sb.Append($"{Name}{(Dirty ? " (Dirty)" : "")}");
			if (Manometer != null)
				sb.Append(Environment.NewLine + Utility.IndentLines(Manometer.ToString()));
			if (Thermometer != null)
				sb.Append(Environment.NewLine + Utility.IndentLines(Thermometer.ToString()));

			return sb.ToString();
        }
    }
}