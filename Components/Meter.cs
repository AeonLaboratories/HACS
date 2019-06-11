using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using Utilities;
using HACS.Core;
using System.ComponentModel;
using Newtonsoft.Json;

namespace HACS.Components
{
	// converts a LabJack analog input voltage to a meaningful value
	[XmlInclude(typeof(IonGauge))]
	public class Meter : DynamicQuantity
	{
		#region Component Implementation

		public static readonly new List<Meter> List = new List<Meter>();
		public static new Meter Find(string name) { return List.Find(x => x?.Name == name); }

		protected virtual void Connect()
        {
            LabJack?.Connect(this);
        }

		protected override void PostInitialize()
		{
			// LabJack.ScanFreq requires LabJack to be streaming
			if (Filter is ButterworthFilter f)
				f.SamplingFrequency = LabJack.ScanFreq;
			base.PostInitialize();
		}

		public Meter()
		{
			List.Add(this);
			OnConnect += Connect;
		}

		#endregion Component Implementation

		[JsonProperty]
		public HacsComponent<LabJackDaq> LabJackRef { get; set; }
        LabJackDaq LabJack => LabJackRef?.Component;
		[JsonProperty]
		public int Channel { get; set; }
		[JsonProperty]//, DefaultValue(LabJackDaq.AiModeType.SingleEnded)]
        public LabJackDaq.AiModeType AiMode { get; set; } = LabJackDaq.AiModeType.SingleEnded;

        double _Voltage;
		[XmlIgnore] public double Voltage
		{
			get { return _Voltage; }
			set
			{
                if (!Initialized)
				{
					_Voltage = value;
					return;
				}

				double priorVoltage = _Voltage;		// needed to decide whether to cancel Zeroing

				Update(value);  // update Meter's Value
				_Voltage = Filter?.Value ?? value;

				// does this code properly belong in the DynamicQuantity class?
				// abort zeroing if Value changed too much
				if (Zeroing)
				{
					double toomuch = 10 * Resolution;	// Resolution can be 0
					if (0 < toomuch && toomuch < Math.Abs(_Voltage - priorVoltage))
						Zeroing = false;
					else
					{
						zerosNeeded--;
						zero += _Voltage;
						if (zerosNeeded <= 0)
							offset(zero / zerosToAverage);
					}
				}
			}
		}

		double _MaxVoltage = 10;
		[XmlIgnore] public double MaxVoltage
		{
			get { return _MaxVoltage; }
			set
			{
				foreach (double maxV in LabJackDaq.MaxVoltages)
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

		public override string ToString()
		{
            return base.ToString() + $"\r\n   ({Voltage:0.0000} V)";
        }

        #region Zeroing
        // does this region properly belong in the DynamicQuantity class?

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

		public void ZeroNow()
		{ if (!Zeroing) Zeroing = true; }

		void offset(double offset)
		{
            var firstOp = Conversion.Operations.FirstOrDefault();
			while (firstOp is OperationSet os)
				firstOp = os.Operations.FirstOrDefault();

			bool insert = false;
			if (firstOp is Arithmetic firstArithmetic)
			{
				if (firstArithmetic.Operator == Arithmetic.Operators.Subtract)
					firstArithmetic.Operand = offset;
				else if (firstArithmetic.Operator == Arithmetic.Operators.Add)
					firstArithmetic.Operand = -offset;
				else
					insert = true;
			}
			else
				insert = true;

            if (insert)
                Conversion.Operations.Insert(0, new Arithmetic("x-" + offset.ToString()));

        }

        #endregion Zeroing

    }
}
