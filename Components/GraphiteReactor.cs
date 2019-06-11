using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
	public class GraphiteReactor : Port
	{
		#region Component Implementation

		public static readonly new List<GraphiteReactor> List = new List<GraphiteReactor>();
		public static new GraphiteReactor Find(string name) { return List.Find(x => x?.Name == name); }

		public GraphiteReactor()
		{
			List.Add(this);
		}

		#endregion Component Implementation


		[XmlType(AnonymousType = true)]
		public enum States { InProcess, Start, WaitTemp, WaitFalling, WaitFinish, Stop, WaitService, WaitPrep, Ready, Disabled }

		public bool isBusy => _State < States.WaitService;
		public bool isReady => _State == States.Ready;

		// Get magic numbers into settings; load from external source
		// in class initializer.

		States _State = States.WaitService;
		[JsonProperty]
		public States State
		{
			get { return _State; }
			set { if (Initialized) StateStopwatch.Reset(); _State = value; }
		}

		[JsonProperty]
		public Aliquot Aliquot { get; set; }
		public string Contents => Aliquot?.Name ?? "";
		[JsonProperty]//, DefaultValue(580)]
		public int GraphitizingTemp { get; set; } = 580;
		[JsonProperty]
        public Stopwatch StateStopwatch { get; set; } = new Stopwatch();
		[JsonProperty]
        public Stopwatch ProgressStopwatch { get; set; } = new Stopwatch();

		[JsonProperty]//, DefaultValue(2000)]
        public double pMin { get; set; } = 2000;
		[JsonProperty]//, DefaultValue(0)]
        public int pPeak { get; set; } = 0; // clips (double) Pressure to detect only significant (1 Torr) change

		[JsonProperty]
		public HacsComponent<Heater> FurnaceRef { get; set; }
		public Heater Furnace => FurnaceRef?.Component;

		[JsonProperty]
		public HacsComponent<Meter> PressureMeterRef { get; set; }
		public Meter PressureMeter => PressureMeterRef?.Component;

		[JsonProperty]
		public HacsComponent<FTColdfinger> ColdfingerRef { get; set; }
		public FTColdfinger Coldfinger => ColdfingerRef?.Component;

		public double Pressure => PressureMeter;
		public double FeTemperature => Furnace.Temperature;
		public double CFTemperature => Coldfinger.Temperature;

		// Error conditions (note magic numbers)
		// Use AND'd error coding system? List<>? Throw exceptions?
		public bool FurnaceUnresponsive =>
		    _State == States.WaitTemp && StateStopwatch.Elapsed.TotalMinutes > 10;

		public bool ReactionNotStarting =>
		    _State == States.WaitFalling && StateStopwatch.Elapsed.TotalMinutes > 45;

		public bool ReactionNotFinishing =>
		    _State == States.WaitFinish && StateStopwatch.Elapsed.TotalMinutes > 4 * 60;

		public void Start() { if (Aliquot != null) Aliquot.Tries++; State = States.Start; }
		public void Stop() { State = States.Stop; }
		public void Reserve(Aliquot aliquot) { Aliquot = aliquot; State = States.InProcess; }
		public void Reserve(string contents) { Reserve(new Aliquot(contents)); }
		public void ServiceComplete() { State = States.WaitPrep; Aliquot = null; }
		public void PreparationComplete() { State = States.Ready; }

		public void Update()
		{
			switch (_State)
			{
				case States.Ready:
				case States.InProcess:
					break;
				case States.Start:
					Furnace.TurnOn(GraphitizingTemp);
					State = States.WaitTemp;
					break;
				case States.WaitTemp:
					if (!Furnace.IsOn) _State = States.Start;
					if (!StateStopwatch.IsRunning) StateStopwatch.Restart();
					if (Math.Abs(GraphitizingTemp - Furnace.Temperature) < 10)
						State = States.WaitFalling;
					break;
				case States.WaitFalling:	// wait for 15 minutes past the end of the peak pressure
					if (!StateStopwatch.IsRunning)
					{
						StateStopwatch.Restart();	// mark start of WaitFalling
						pPeak = (int)Pressure;
						ProgressStopwatch.Restart();	// mark pPeak updated
					}
					else if (Pressure >= pPeak)
					{
						pPeak = (int)Pressure;
						ProgressStopwatch.Restart();	// mark pPeak updated
					}
					else if (ProgressStopwatch.Elapsed.TotalMinutes > 15)  // 15 min since pPeak
						State = States.WaitFinish;
					break;
				case States.WaitFinish:
					// Considers graphitization complete when the pressure
					// decline slows to between 0.6 and 0.8 Torr / min.
					// The AC cycle may perturb the pressure reading by
					// as much as +0.8 Torr/min over 3 minutes worst case.
					// +0.6 Torr/min over 6 minutes
					// +0.4 Torr/min over 10 minutes
					// +0.2 Torr/min over 20 minutes
					// In 3 minutes, with the worst-case AC cycle perturbation,
					// some decline should still be observed if the true
					// pressure has declined faster than 0.6 Torr/min.
					// In 6 minutes, with the worst-case AC cycle perturbation,
					// a decline >= 1.2 Torr should be observable.
					if (!StateStopwatch.IsRunning)
					{
						StateStopwatch.Restart();	// mark start of WaitFinish
						pMin = Pressure;
						ProgressStopwatch.Restart();	// mark pMin updated
					}
					else if (Pressure < pMin)
					{
						pMin = Pressure;
						ProgressStopwatch.Restart();	// mark pMin updated
					}
					else if (ProgressStopwatch.Elapsed.TotalMinutes > 5 || Pressure > pMin + 5)
						State = States.Stop;
					break;
				case States.Stop:
					Furnace.TurnOff();
					State = States.WaitService;
					break;
				case States.WaitService:
					// state changed by ServiceComplete();
					break;
				case States.WaitPrep:
				// changed by PreparationComplete();
				default:
					break;
			}

			if (isBusy)
			{
				switch (Coldfinger.State)
				{
					case FTColdfinger.States.Freeze:
					case FTColdfinger.States.Raise:
						if (_State >= States.Start)		// don't wait for graphitizing temp
							Coldfinger.Thaw();
						break;
					case FTColdfinger.States.Thaw:
						if (Coldfinger.isNearAirTemperature())
							Coldfinger.Stop();
						break;
					case FTColdfinger.States.Stop:
					case FTColdfinger.States.Standby:
					default:
						break;
				}
				//UpdateUI();
			}
		}

		public override string ToString()
		{
			string s = $"{Name}: {State}";
			if (StateStopwatch.IsRunning)
			{
				TimeSpan ts = StateStopwatch.Elapsed;
				s += " (" + ts.ToString(@"h\:mm\:ss");
				if (ProgressStopwatch.IsRunning)
				{
					ts = ProgressStopwatch.Elapsed;
					s += ", " + ts.ToString(@"h\:mm\:ss");
				}
				s += ")";
			}
			s += "\r\n" +
				Utility.IndentLines(
					PressureMeter?.ToString() + "\r\n" +
					Furnace?.ToString() + "\r\n" +
					Coldfinger?.ToString()
				);
			return s;
		}
	}
}