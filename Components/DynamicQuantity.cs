using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
    public class DynamicQuantity : HacsComponent
    {
		#region Component Implementation

		public static readonly new List<DynamicQuantity> List = new List<DynamicQuantity>();
		public static new DynamicQuantity Find(string name) { return List.Find(x => x?.Name == name); }

		protected virtual void PostInitialize()
		{
			Filter?.Initialize(Value);
            RoC?.Initialize();
        }

		public DynamicQuantity()
		{
			List.Add(this);
			OnPostInitialize += PostInitialize;
		}

		#endregion Component Implementation


		public static implicit operator double(DynamicQuantity x)
        { return x?.Value ?? 0; }

		[JsonProperty]
        public double Value
        {
            get { return _Value; }
            set { Update(value); }
		}
		double _Value;

		[JsonProperty]
		public string UnitSymbol { get; set; } = "";

        [XmlElement(typeof(AveragingFilter))]
        [XmlElement(typeof(ButterworthFilter))]
		[JsonProperty]
		public DigitalFilter Filter { get; set; }

		[JsonProperty]
		public OperationSet Conversion { get; set; }
		[JsonProperty]
		public double Sensitivity { get; set; } // the smallest meaningful (detectable) difference from zero
		[JsonProperty]
		public double Resolution { get; set; }  // the resolvable unit size (smaller Value differences are indistinguishable)

		[JsonProperty]
		public bool ResolutionIsProportional
        {
            get { return _ResolutionIsProportional; }
            set
            {
                _ResolutionIsProportional = value;
                if (value)
                    SignificantDigits = 1 + Utility.PowerOfTenCeiling(1 / Resolution);
            }
        }
        bool _ResolutionIsProportional = false;
        int SignificantDigits = 1;  // used when ResolutionIsProportional

		[JsonProperty]
		public RateOfChange RoC { get; set; }

		[JsonProperty]
		public double Stable { get; set; }
		public bool IsStable => RoC == null ? false : Math.Abs(RoC) <= Stable;

		[JsonProperty]
		public double Falling { get; set; }
		public bool IsFalling => RoC == null ? false : RoC <= Falling;

		[JsonProperty]
		public double Rising { get; set; }
		public bool IsRising => RoC == null ? false : RoC >= Rising;

		// add additional, similar RoC conditions for VTT and IP?

		public double Update(double value)
		{
			if (Initialized)
			{
				if (Filter != null && Filter.Initialized)
					value = Filter.Update(value);

				if (Conversion != null) value = Conversion.Execute(value);

				if (Resolution != 0)
                {
                    if (ResolutionIsProportional)
                        value = Utility.Significant(value, SignificantDigits);
                    else
                        value = Math.Round(value / Resolution) * Resolution;
                }

                if (Math.Abs(value) <= Sensitivity)
				{
					if (value < 0)
						value = -Sensitivity;
					else
						value = Sensitivity;
				}
				RoC?.Update(value);
			}

			_Value = value;
			StateChanged?.Invoke();
			return _Value;
		}

		/// <summary>
		/// Waits until Value has remained stable for the given number of seconds.
		/// </summary>
		/// <param name="seconds"></param>
		public void WaitForStable(int seconds = 5)
		{
			while (!IsStable)
				Thread.Sleep(50);
			Stopwatch sw = new Stopwatch();
			sw.Restart();
			while (sw.ElapsedMilliseconds < seconds * 1000)
			{
				Thread.Sleep(50);
				if (!IsStable)
					sw.Restart();
			}
		}

        public override string ToString()
        {
            var sb = new StringBuilder($"{Name}: {Value}");
            if (!string.IsNullOrEmpty(UnitSymbol))
                sb.Append($" {UnitSymbol}");
            if (RoC != null)
            {
                sb.Append($"\r\n   {RoC.Value:0.000}");
                if (!string.IsNullOrEmpty(UnitSymbol))
                    sb.Append($" {UnitSymbol}/s");
                else
                    sb.Append($" units/s");
            }
            return sb.ToString();
        }
    }
}
