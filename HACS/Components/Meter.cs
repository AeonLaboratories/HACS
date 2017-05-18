using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
    // converts a LabJack analog input voltage to a meaningful value
	public class Meter : Component
    {
		public static new List<Meter> List;
		public static new Meter Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public static implicit operator double(Meter m)
		{
			if (m == null) return 0;
			return m.Value;
		}

		public enum AiModeType { SingleEnded, Differential }
		public static double[] MaxVoltages = { 10, 1, 0.1, 0.01 };

		[XmlIgnore] public Action StateChanged;

		[XmlElement("LabJack")]
		public string LabJackName { get; set; }
		LabJackDaq LabJack;
		public int Channel { get; set; }
		public AiModeType AiMode { get; set; }
		public OperationSet Conversion { get; set; }
		
		[XmlElement(typeof(AveragingFilter))]
		[XmlElement(typeof(ButterworthFilter))]
		public DigitalFilter Filter { get; set; }

		public double Sensitivity { get; set; }
		public double Resolution { get; set; }
		public Utilities.RateOfChange RoC { get; set; }

		[XmlIgnore] public double Value { get; private set; }

		double _Voltage = 0;
		[XmlIgnore] public double Voltage
		{
			get { return _Voltage; }
			set
			{
				if (!Initialized) { _Voltage = value; return; }

				double oldValue = Value;		// N.B. 'Value' not 'value'

				if (first_reading)
				{
					_Voltage = value;

					if (Filter != null)
						Filter.Initialize(_Voltage);

					first_reading = false;
				}
				else
				{
					if (Filter == null)
						_Voltage = value;
					else
					{
						if (Resolution > 0 && Math.Abs(Filter.Value - value) > 50.0 * Resolution)
						{
							Filter.Initialize(value);		// reset filter on step change
							_Voltage = value;
						}
						else
							_Voltage = Filter.Update(value);
					}
				}

				double units = Conversion.Execute(Voltage);
                if (Resolution != 0)
                    units = Math.Round(units / Resolution) * Resolution;
                if (Math.Abs(units) <= Sensitivity)
                {
                    if (units < 0)
                        units = -Sensitivity;
                    else
                        units = Sensitivity;
                }
				Value = units;

				if (RoC != null) RoC.Update(Value);

				if (Zeroing)
				{
					// abort zeroing if the units changed too much
					double toomuch = 10 * Resolution;
					if (0 < toomuch && toomuch < Math.Abs(Value - oldValue))
						Zeroing = false;
					else
					{
                        zerosNeeded--;
                        zero += Value;
						if (zerosNeeded <= 0)
							offset(zero / zerosToAverage);
					}
				}
				
				StateChanged?.Invoke();
			}
		}

		double _MaxVoltage = 10;
		[XmlIgnore] public double MaxVoltage
        {
            get { return _MaxVoltage; }
            set
            {
				foreach (double maxV in MaxVoltages)
					if (value == maxV)
					{
						_MaxVoltage = maxV;
						if (!explicitMinVoltage) _MinVoltage = -MaxVoltage;
						return;
					}
				}
        }

		double _MinVoltage = -10;
		[XmlIgnore] public double MinVoltage
		{
			get { return _MinVoltage; }
			set
			{
				_MinVoltage = value;
				explicitMinVoltage = true;
			}
		}
		bool explicitMinVoltage = false;

		bool first_reading = true;		// avoid filtering the first value
		double zero;
		static int zerosToAverage = 25;
		int zerosNeeded = 0;
		[XmlIgnore] public bool Zeroing
		{
			get { return zerosNeeded > 0; }
			protected set
			{
				if (value)
				{
					zero = 0;
					zerosNeeded = zerosToAverage;
				}
				else
					zerosNeeded = 0;
			}
		}
		
		public override string ToString()
		{
			return Name + ": " + Voltage.ToString("0.0000 V");
		}

		public Meter() : this("") { }

		public Meter(string name) 
		{
			Name = name;
			//Converter = new VoltageConverter();
			AiMode = AiModeType.SingleEnded;
		}

		public override void Connect()
		{
			LabJack = LabJackDaq.Find(LabJackName);
			LabJack.ConnectAI(this);
		}

		public override void Initialize()
		{
			RoC?.Initialize();
			
			if (Filter is ButterworthFilter)
				(Filter as ButterworthFilter).SamplingFrequency = LabJack.ScanFreq;

			Initialized = true;
		}

        public void ZeroNow()
        { if (!Zeroing) Zeroing = true; }

		void offset(double offset)
		{
			 Arithmetic last = Conversion.Operations.Last() as Arithmetic;

			if (last != null && last.Operator == Arithmetic.Operators.Add)
				last.Operand -= offset;
			else
				Conversion.Operations.Add(new Arithmetic("x-" + offset.ToString()));
		}
    }

	public class EnabledMeter : Meter
	{

	}
}
