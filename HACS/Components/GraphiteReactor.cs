using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	public class GraphiteReactor : Component
    {
		public static new List<GraphiteReactor> List;
		public static new GraphiteReactor Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType = true)]
		public enum States { InProcess, Start, WaitTemp, WaitFalling, WaitFinish, Stop, WaitService, WaitPrep, Ready, Disabled }

        public bool isBusy { get { return _State < States.WaitService; } }
        public bool isReady { get { return _State == States.Ready; } }

		// Get magic numbers into settings; load from external source
		// in class initializer.

		States _State;
		public States State
		{
			get { return _State; }
			set { if (Initialized) StateStopwatch.Reset(); _State = value; } 
		}

		public Aliquot Aliquot { get; set; }
		public string Contents { get { return (Aliquot == null) ? "" : Aliquot.Name; } }
		public int GraphitizingTemp { get; set; }
		public Stopwatch StateStopwatch { get; set; }
		public Stopwatch ProgressStopwatch { get; set; }
	
		[XmlIgnore] public double MilliLitersVolume;
		public double pMin { get; set; }
		public int pPeak { get; set; }	// clips (double) Pressure to detect only significant (1 Torr) change

		[XmlElement("Furnace")]
		public string FurnaceName { get; set; }
		[XmlIgnore] public Heater Furnace;

		[XmlElement("PressureMeter")]
		public string PressureMeterName { get; set; }
		[XmlIgnore] public Meter PressureMeter;

		[XmlElement("Coldfinger")]
		public string ColdfingerName { get; set; }
		[XmlIgnore] public FTColdfinger Coldfinger;

		[XmlElement("GMValve")]
		public string GMValveName { get; set; }
		[XmlIgnore] public Valve GMValve;

		public double Pressure { get { return PressureMeter; } }
		public double FeTemperature { get { return Furnace.Temperature; } }
		public double CFTemperature { get { return Coldfinger.Temperature; } }

		// Error conditions (note magic numbers)
		// Use AND'd error coding system? List<>? Throw exceptions?
		public bool FurnaceUnresponsive
		{ get { return _State == States.WaitTemp && StateStopwatch.Elapsed.TotalMinutes > 10; } }

		public bool ReactionNotStarting
		{ get { return _State == States.WaitFalling && StateStopwatch.Elapsed.TotalMinutes > 45; } }

		public bool ReactionNotFinishing
		{ get { return _State == States.WaitFinish && StateStopwatch.Elapsed.TotalMinutes > 4 * 60; } }

		public GraphiteReactor()
		{
			State = States.WaitService;
			GraphitizingTemp = 580;
			StateStopwatch = new Stopwatch();
			ProgressStopwatch = new Stopwatch();
			pMin = 2000;
			pPeak = 0;
		}

		public GraphiteReactor(string name)
			: this()
		{ Name = name; }

		public override void Connect()
		{
			Furnace = Heater.Find(FurnaceName);
			PressureMeter = Meter.Find(PressureMeterName);
			Coldfinger = FTColdfinger.Find(ColdfingerName);
			GMValve = Valve.Find(GMValveName);
		}

		public void Connect(Heater furnace, Meter pressureMeter, FTColdfinger ftColdfinger, Valve gmValve)
		{
			Furnace = furnace;
			PressureMeter = pressureMeter;
			Coldfinger = ftColdfinger;
			GMValve = gmValve;
		}

		public override void Initialize() { Initialized = true; }

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
				case States.WaitFalling:     // wait for five minutes past the end of the peak pressure
					if (!StateStopwatch.IsRunning)
					{
						StateStopwatch.Restart();       // mark start of WaitFalling
						pPeak = (int)Pressure;
						ProgressStopwatch.Restart();      // mark pPeak updated
					}
					else if (Pressure >= pPeak)
					{
						pPeak = (int)Pressure;
						ProgressStopwatch.Restart();      // mark pPeak updated
					}
					else if (ProgressStopwatch.Elapsed.TotalMinutes > 5)  // 5 min since pPeak
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
						StateStopwatch.Restart();       // mark start of WaitFinish
						pMin = Pressure;
						ProgressStopwatch.Restart();      // mark pMin updated
					}
					else if (Pressure < pMin)
					{
						pMin = Pressure;
						ProgressStopwatch.Restart();      // mark pMin updated
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
			string s = Name + ": " + State.ToString();
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
					PressureMeter.ToString() + "\r\n" +
					Furnace.ToString() + "\r\n" +
					Coldfinger.ToString()
				);
			return s;
		}
	}
}