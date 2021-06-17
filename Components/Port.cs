using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
	public class Port : Chamber, IPort
	{
		#region HacsComponent

		protected override void Connect()
		{
			base.Connect();
			Valve = Find<IValve>(valveName);

			// TODO: is this "helper" a bad idea?
			if (Thermometer == null)
			{
				if (VTColdfinger != null)
					Thermometer = VTColdfinger.Heater;
				else if (Heater != null)
					Thermometer = Heater;
				else if (Coldfinger != null)
					Thermometer = Coldfinger.LevelSensor;
			}
		}

		#endregion HacsComponent

		[JsonProperty("Valve")]
		string ValveName { get => Valve?.Name; set => valveName = value; }
		string valveName;
        public IValve Valve
		{
			get => valve;
			set => Ensure(ref valve, value);
		}
		IValve valve;
		public void Open() => Valve?.OpenWait();
		public void Close() => Valve?.CloseWait();
		public bool IsOpened => Valve?.IsOpened ?? true;
		public bool IsClosed => Valve?.IsClosed ?? false;
    }
}