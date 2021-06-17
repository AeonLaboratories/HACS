using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
	// TODO Get magic numbers into settings file
	// TODO consider deriving from a new class that wraps Chamber or Section and IStateManager (and get rid of Update())
	// TODO incorporate post-Update() logic from CEGS.cs into this class
	public class GraphiteReactor : Port, IGraphiteReactor
	{
		#region HacsComponent
		[HacsConnect]
		protected override void Connect()
		{
			base.Connect();
			Sample = Find<ISample>(sampleName);
		}

		#endregion HacsComponent

		public enum States { InProcess, Start, WaitTemp, WaitFalling, WaitFinish, Stop, WaitService, WaitPrep, Prepared, Disabled }

		public enum Sizes { Standard, Small }

		[JsonProperty]
		public States State
		{
			get => state;
			set
			{
				Ensure(ref state, value, OnPropertyChanged);
				if (Initialized) StateStopwatch.Reset();
			}
		}
		States state = States.WaitService;

		[JsonProperty]
		public Sizes Size
		{
			get => size;
			set => Ensure(ref size, value);
		}
		Sizes size = Sizes.Standard;

		[JsonProperty("Sample")]
		string SampleName { get => Sample?.Name; set => sampleName = value; }
		string sampleName;
		public ISample Sample
		{
			get => sample;
			set => Ensure(ref sample, value, OnPropertyChanged);
		}
		ISample sample;

		[JsonProperty("Aliquot"), DefaultValue(0)]
		int AliquotIndex
		{
			get => aliquotIndex;
			set => Ensure(ref aliquotIndex, value, OnPropertyChanged);
		}
		int aliquotIndex = 0;

		public IAliquot Aliquot
		{
			get
			{
				if (Sample?.Aliquots != null && Sample.Aliquots.Count > AliquotIndex)
					return Sample.Aliquots[AliquotIndex];
				else
					return null;
			}
			set
			{
				Sample = value?.Sample;
				AliquotIndex = Sample?.AliquotIndex(value) ?? 0;
				NotifyPropertyChanged(nameof(Contents));
			}
		}

		public string Contents => Aliquot?.Name ?? "";

		[JsonProperty, DefaultValue(580)]
		public int GraphitizingTemperature
		{
			get => graphitizingTemperature;
			set => Ensure(ref graphitizingTemperature, value);
		}
		int graphitizingTemperature = 580;

		[JsonProperty, DefaultValue(0)]
		public int SampleTemperatureOffset
		{
			get => sampleTemperatureOffset;
			set => Ensure(ref sampleTemperatureOffset, value);
		}
		int sampleTemperatureOffset = 0;

		[JsonProperty]
		public Stopwatch StateStopwatch { get; protected set; } = new Stopwatch();
		[JsonProperty]
		public Stopwatch ProgressStopwatch { get; protected set; } = new Stopwatch();

		[JsonProperty, DefaultValue(2000)]
		public double PressureMinimum
		{
			get => pressureMinimum;
			set => Ensure(ref pressureMinimum, value);
		}
		double pressureMinimum = 2000;

		[JsonProperty, DefaultValue(0)]
		public int PressurePeak
		{
			get => pressurePeak;
			set => Ensure(ref pressurePeak, value);
		}
		int pressurePeak = 0; // clips (double) Pressure to detect only significant (1 Torr) change


		public bool Busy => state < States.WaitService;
		public bool Prepared => state == States.Prepared;

		public double HeaterTemperature => Heater.Temperature;
		public double ColdfingerTemperature => Coldfinger.Temperature;

		// Error conditions (note magic numbers)
		// Use AND'd error coding system? List<>? Throw exceptions?
		public bool FurnaceUnresponsive =>
			state == States.WaitTemp && StateStopwatch.Elapsed.TotalMinutes > 10;

		public bool ReactionNotStarting =>
			state == States.WaitFalling && StateStopwatch.Elapsed.TotalMinutes > 45;

		public bool ReactionNotFinishing =>
			state == States.WaitFinish && StateStopwatch.Elapsed.TotalMinutes > 4 * 60;

		public void Start() { if (Aliquot != null) Aliquot.Tries++; State = States.Start; }
		public void Stop() => State = States.Stop;
		public void Reserve(IAliquot aliquot) { Aliquot = aliquot; State = States.InProcess; }
		public void Reserve(string contents)
		{
			var s = new Sample() { AliquotIds = new List<string>() { contents } };
			Reserve(s.Aliquots[0]);
			//var a = new Aliquot(contents) { ResidualMeasured = true };
			//new Sample() { Aliquots = new List<IAliquot>() { a } };
			//Reserve(a);
		}
		public void ServiceComplete() { Aliquot = null; State = States.WaitPrep; }
		public void PreparationComplete() => State = States.Prepared;

		public void TurnOn(double sampleSetpoint) =>
			Heater.TurnOn(sampleSetpoint + SampleTemperatureOffset);
		public void TurnOff() => Heater.TurnOff();

		/// <summary>
		/// Estimated sample temperature.
		/// </summary>
		public double SampleTemperature
		{
			get
			{
				var temperature = Heater.Temperature;
				if (temperature > 100)
					temperature -= SampleTemperatureOffset;
				return temperature;
			}
		}

		public void Update()
		{
			switch (state)
			{
				case States.Prepared:
				case States.InProcess:
					break;
				case States.Start:
					TurnOn(GraphitizingTemperature);
					State = States.WaitTemp;
					break;
				case States.WaitTemp:
					if (!Heater.IsOn) state = States.Start;
					if (!StateStopwatch.IsRunning) StateStopwatch.Restart();
					if (Math.Abs(GraphitizingTemperature - SampleTemperature) < 10)
						State = States.WaitFalling;
					break;
				case States.WaitFalling:	// wait for 15 minutes past the end of the peak pressure
					if (!StateStopwatch.IsRunning)
					{
						StateStopwatch.Restart();	// mark start of WaitFalling
						PressurePeak = (int)Pressure;
						ProgressStopwatch.Restart();	// mark pPeak updated
					}
					else if (Pressure >= PressurePeak)
					{
						PressurePeak = (int)Pressure;
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
						PressureMinimum = Pressure;
						ProgressStopwatch.Restart();	// mark pMin updated
					}
					else if (Pressure < PressureMinimum)
					{
						PressureMinimum = Pressure;
						ProgressStopwatch.Restart();	// mark pMin updated
					}
					else if (ProgressStopwatch.Elapsed.TotalMinutes > 5 || Pressure > PressureMinimum + 5)
						State = States.Stop;
					break;
				case States.Stop:
					Heater.TurnOff();
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

			if (Busy)
			{
				// graphitization is in progress
				if (Coldfinger.IsActivelyCooling && state >= States.Start)     // don't wait for graphitizing temp
					Coldfinger.Thaw();
				else if (Coldfinger.Thawing && Coldfinger.IsNearAirTemperature)
					Coldfinger.Standby();

				if (FurnaceUnresponsive)
					Alert.Send("GraphiteReactor warning!",
						$"{Name} furnace is unresponsive.");

				if (ReactionNotStarting)
					Alert.Send("Graphite reaction warning!",
						$"{Name} reaction hasn't started.\r\n" +
							"Is the furnace in place?");

				if (ReactionNotFinishing)
				{
					Alert.Send("Graphite reaction warning!",
						$"{Name} reaction hasn't finished.");
					State = GraphiteReactor.States.WaitFalling;  // reset the timer
				}
			}
		}

		protected override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var propertyName = e?.PropertyName;
			if (propertyName == nameof(Sample) || propertyName == nameof(AliquotIndex))
				NotifyPropertyChanged();
			else if (propertyName == nameof(State))
            {
				NotifyPropertyChanged(nameof(Busy));
				NotifyPropertyChanged(nameof(Prepared));
			}
			else
				base.OnPropertyChanged(sender, e);

			if (sender == Heater && e?.PropertyName == nameof(Heater.Temperature))
			{
				NotifyPropertyChanged(nameof(HeaterTemperature));
				NotifyPropertyChanged(nameof(SampleTemperature));
			}
			if (sender == Coldfinger && e?.PropertyName == nameof(Coldfinger.Temperature))
			{
				NotifyPropertyChanged(nameof(ColdfingerTemperature));
			}

		}

		public override string ToString()
		{
			var sb = new StringBuilder($"{Name}: {Contents} ({State}), {Pressure:0} {Manometer?.UnitSymbol}, {Thermometer?.Value:0} {Thermometer?.UnitSymbol}");
			if (StateStopwatch.IsRunning)
			{
				sb.Append($" ({StateStopwatch.Elapsed:h':'mm':'ss}");
				if (ProgressStopwatch.IsRunning)
					sb.Append($", {ProgressStopwatch.Elapsed:h':'mm':'ss}");
				sb.Append(")");
			}
            var sb2 = new StringBuilder();
            sb2.Append($"\r\n{Manometer}");
            sb2.Append($"\r\n{Heater}");
            sb2.Append($"\r\n{Coldfinger}");
            sb.Append(Utility.IndentLines(sb2.ToString()));
			return sb.ToString();
		}
	}
}