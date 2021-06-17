using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Utilities;

namespace HACS.Core
{
    // Computes a filtered (smoothed) rate of change.
    // Follows http://www.holoborodko.com/pavel
    // Longer filters provide increased noise suppression.
    [JsonObject(MemberSerialization.OptIn)]
	public class RateOfChange : NamedObject, IRateOfChange
	{
		public static implicit operator double(RateOfChange roc)
		{ return roc?.Value ?? 0; }

		#region Properties

		[JsonProperty]
		public int FilterLength
		{
			get => filterLength;
			set
			{
				if (value < 5) filterLength = 5;
				else if (value > 63) filterLength = 63;
				else if (value % 2 != 0) filterLength = value; 	// make sure length is odd
				else filterLength = value - 1;
				
				lastItem = filterLength-1;
				n = filterLength - 3;
				M = (filterLength - 1) / 2;
				divisor = Math.Pow(2, n);

				createFilter();
				NotifyPropertyChanged();
			}
		}
		
		int filterLength = 11;		// filter length == v.Length
		int lastItem = 10;
		int n = 8;					// 2m in Holoborodko
		int M = 5;					// middle position
		double divisor = 256;       // A common divisor distributed out of the 
									// polynomial terms.
									// This divisor incorporates a 2 from the irregularly-spaced
									// data version.
		double[] c;					// filter polynomial coefficients
		double[] v;					// history of values
		double[] t;					// times since prior values

		Stopwatch sw = new Stopwatch();

		[JsonProperty]
		public double MaxIntervalMilliseconds
		{
			get => maxIntervalMilliseconds;
			set => Ensure(ref maxIntervalMilliseconds, value);
		}
		double maxIntervalMilliseconds;

		[JsonProperty, DefaultValue(0.001)]
		public double Resolution
		{
			get => resolution;
			set => Ensure(ref resolution, value);
		}
		double resolution = 0.001;

		public double MillisecondsSinceLastUpdate
        {
			get => millisecondsSinceLastUpdate;
			protected set => Ensure(ref millisecondsSinceLastUpdate, value);
        }
		double millisecondsSinceLastUpdate;

		public double Value
		{
			get => value;
			protected set => Set(ref this.value, value);
		}
		double value;

		public RateOfChange RoC
		{
			get => roC;
			set => Set(ref roC, value, OnPropertyChanged);
		}
		RateOfChange roC;

		#endregion Properties

		public RateOfChange() { sw.Start(); }

		public RateOfChange(int filterLength) : this()
		{ FilterLength = filterLength; }

		// See http://www.holoborodko.com/pavel for theoretical basis.
		// See "holobordko filter.ods" for the analysis that led to this
		// algorithm and implementation.
		void createFilter()
		{		
			c = new double[M];
			double bL, bR, b0;
			bR = b0 = 0; 
			for (int i = 0; i < M; i++)
			{
				bL = Utility.binomCoeff(n, i);
				var k = M - i;				
				c[k - 1] = k * (bL - bR) / divisor;
				bR = b0;
				b0 = bL;
			}

			v = new double[filterLength];
			t = new double[filterLength];
		}

		bool initialized => t != null;
		void initialize() => createFilter();

		public double Update(double newValue)
		{
			if (!initialized) initialize();

			var elapsed = sw.Elapsed.TotalMilliseconds;
			var seconds = elapsed / 1000;
			if (elapsed >= MaxIntervalMilliseconds || Math.Abs(newValue - v[filterLength-2])/seconds >= Resolution)
			{
				MillisecondsSinceLastUpdate = elapsed; 
				sw.Restart();
				
				// store the new datapoint and the time elapsed since the prior one
				v[lastItem] = newValue;
				t[lastItem] = seconds;
			
				double sumT = 0;			// total time
				double sumPT = 0;		// sum of polynomial terms
				
				for (int i = 0, j = M + 1, j2 = M; i < M; i++, j++)
				{
					sumT += t[j] + t[j2];
					if (sumT > 0)
						sumPT += c[i] * (v[j] - v[--j2]) / sumT;
				}
				
				Value = sumPT;
				RoC?.Update(Value);
				
				// make space for next value
				for (int i = 0, j = 1; i < lastItem; i++, j++)
				{				
					v[i] = v[j];
					t[i] = t[j];
				}
			}
			return Value;
		}

		protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == this) return;
			if (sender == RoC)
				NotifyPropertyChanged(sender, PropertyChangedEventArgs(nameof(RoC)));
		}
	}
}
