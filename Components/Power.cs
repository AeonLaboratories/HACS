using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Utilities;

namespace HACS.Components
{
	public class Power : HacsComponent, IPower
	{
		#region HacsComponent
		[HacsConnect]
		protected virtual void Connect()
		{
			DC5V = Find<IVoltmeter>(dc5VName);
			MainsDetect = Find<IVoltmeter>(mainsDetectName);
		}

		#endregion HacsComponent

		[JsonProperty("DC5V")]
		string Dc5VName { get => DC5V?.Name; set => dc5VName = value; }
		string dc5VName;
		public IVoltmeter DC5V
		{ 
			get => dc5V;
			set => Ensure(ref dc5V, value, NotifyPropertyChanged);
		}
		IVoltmeter dc5V;

		[JsonProperty("MainsDetect")]
		string MainsDetectName { get => MainsDetect?.Name; set => mainsDetectName = value; }
		string mainsDetectName;
		public IVoltmeter MainsDetect
		{
			get => mainsDetect;
			set => Ensure(ref mainsDetect, value, NotifyPropertyChanged);
		}
		IVoltmeter mainsDetect;

		[JsonProperty, DefaultValue(4.0)]
		public double MainsDetectMinimumVoltage
		{
			get => mainsDetectMinimumVoltage;
			set => Ensure(ref mainsDetectMinimumVoltage, value);
		}
		double mainsDetectMinimumVoltage = 4.0;

		public bool MainsIsDown => MainsDetect.Voltage < MainsDetectMinimumVoltage;
		public Stopwatch MainsDownTimer = new Stopwatch();

		[JsonProperty]
		int MilliSecondsMainsDownLimit
		{
			get => milliSecondsMainsDownLimit;
			set => Ensure(ref milliSecondsMainsDownLimit, value);
		}
		int milliSecondsMainsDownLimit;

		public bool MainsHasFailed => MainsDownTimer.ElapsedMilliseconds > MilliSecondsMainsDownLimit;

		// TODO: should these be NotifyPropertyChanged() instead?
		public Action MainsDown { get; set; }
		public Action MainsRestored { get; set; }
		public Action MainsFailed { get; set; }

        bool failureHandled = false;
		public void Update()
		{
			// Power monitoring is not possible if we can't read voltages.
			// TODO: make a Daq base class, and derive LabJackU6 from it.
			if (MainsDetect is IManagedDevice d && d.Manager is LabJackU6 lj && !lj.IsUp)
				return;

			if (MainsIsDown)
			{
				if (!MainsDownTimer.IsRunning)
				{
					MainsDownTimer.Restart();
					MainsDown?.Invoke();
                    failureHandled = false;
				}
				else if (MainsHasFailed && !failureHandled)
				{
					MainsFailed?.Invoke();
                    failureHandled = true;
				}
			}
			else if (MainsDownTimer.IsRunning)
			{
				MainsDownTimer.Stop();
				MainsDownTimer.Reset();
				MainsRestored?.Invoke();
			}
		}
	}
}