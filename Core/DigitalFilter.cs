using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Utilities;

namespace HACS.Core
{
	[JsonObject(MemberSerialization.OptIn)]
	public class DigitalFilter : NamedObject, IFilter
	{
		public static implicit operator double(DigitalFilter f)
		{ return f?.Value ?? 0; }
		public static double WeightedUpdate(double x, double oldValue, double stability)
		{
			if (stability > 0 && stability <= 1)
				return oldValue * stability + x * (1 - stability);
			else
				return x;
		}

		// Reset the filter if x differs from Value by StepChange or more.
		// Set higher than noise floor, at least. 0 disables the filter.
		[JsonProperty]
		public double StepChange
		{
			get => stepChange;
			set => Ensure(ref stepChange, value);
		}
		double stepChange = double.PositiveInfinity;
		public bool IsStepChange(double x) { return double.IsNaN(Value) || double.IsNaN(x) || Math.Abs(x - Value) >= StepChange; }

		public virtual double Value
		{
			get => value;
			set => Set(ref this.value, value);
		}
		double value;

		public virtual bool Initialized
		{
			get => initialized;
			protected set => Ensure(ref initialized, value);
		}
		bool initialized = false;

		public virtual double Initialize(double value) { Value = value; Initialized = true; return Value; }
		public virtual double Filter(double value) { Value = value; return Value; }
		public virtual double Update(double value)
		{
			if (!Initialized || IsStepChange(value))
				return Initialize(value);
			else
				return Filter(value);
		}
	}

	public class AveragingFilter : DigitalFilter
	{
		protected double oldCoeff, newCoeff;
		[JsonProperty]
		public double Stability
		{
			get => oldCoeff;
			set 
			{ 
				if (value >= 0 && value <= 1)
				{
					oldCoeff = value; 
					newCoeff = 1 - value;
					NotifyPropertyChanged();
				}
				else
					throw new Exception("AveragingFilter: Stability out of range.");
			}
		}

		public AveragingFilter() : this(0) { }

		public AveragingFilter(double stability)
		{ Stability = stability; }

		public override double Filter(double value)
		{ return Value = Value * oldCoeff + value * newCoeff; }
	}

	public class ButterworthFilter : DigitalFilter
	{
		static double TWOPI = 2.0 * Math.PI;

		bool isOdd(int i) { return (i & 1) != 0; }

		[JsonProperty]
		public int Order
		{ 
			get => order; 
			set => Ensure(ref order, value, ConfigurationChanged);
		}
		int order;

		public double SamplingFrequency
		{
			get => samplingFrequency;
			set => Ensure(ref samplingFrequency, value, ConfigurationChanged);
		}
		double samplingFrequency;

		[JsonProperty]
		public double CutoffFrequency
		{
			get => cutoffFrequency;
			set => Ensure(ref cutoffFrequency, value, ConfigurationChanged);
		}
		double cutoffFrequency;

		double[] Cx;	// filter coefficients
		double[] Cy;
		double Gain;

		double[] X;		// ring buffers for filter history
		double[] Y;
		int lastIndex;	// the last index used in Updating the Value
		int next;		// pointer to X and Y ring buffers

		public ButterworthFilter() : this(2, 25, 0.5) { }

		// alpha represents the corner frequency as a fraction of sampling rate;
		// it must be > 0 and < 0.5
		public ButterworthFilter(int order, double sampling_frequency, double cutoff_frequency)
		{
			Order = order;
			SamplingFrequency = sampling_frequency;
			CutoffFrequency = cutoff_frequency;
		}

		public override double Initialize(double value)
		{
			if (SamplingFrequency <= 0)
			{
				Value = value;
				return value;
				// Initialized is NOT set to true;
			}

			double alpha = CutoffFrequency / SamplingFrequency;
			var s_poles = find_stable_poles(alpha);
			var z_zeros = all_minus_ones(s_poles.Count);
			var z_poles = bilinear_transform(s_poles);

			Cx = polyCoefficients(z_zeros);
			Cy = Utility.Negate(polyCoefficients(z_poles));
			Gain = -1.0 / evaluate_ratio(Cx, Cy, 1.0);

			X = new double[Order];
			Y = new double[Order];
			lastIndex = Order - 1;
			next = lastIndex;

			double x = Gain * value;
			for (int i = 0; i < Order; ++i)
			{
				X[i] = x;
				Y[i] = value;
			}

			return base.Initialize(value);
		}

		List<Complex> all_minus_ones(int n)
		{
			var z_zeros = new List<Complex>();
			for (int i = 0; i < n; ++i) z_zeros.Add(-1.0);	// why do it this way?
			return z_zeros;
		}
	
		// On entering this function, next points to latest (most recently
		// received) x and y values. Decrementing next <Order> times will 
		// leave next pointing to the oldest values.
		public override double Filter(double x)
		{
			x *= Gain;
			double filtered = Cx[0] * x;
			for (int ci = lastIndex; ci >= 0; --ci)
			{
				filtered += Cx[ci] * X[next] + Cy[ci] * Y[next];
				if (ci > 0 && --next < 0) next = lastIndex;
			}

			// now, next still points to oldest x and y values
			X[next] = x;
			Y[next] = filtered;
			// now, next points to latest x and y values

			Value = filtered;
			return Value;
		}


		// find the stable poles in the S-plane
		List<Complex> find_stable_poles(double alpha)
		{
			List<Complex> poles = new List<Complex>();

			double warped_alpha = Math.Tan(Math.PI * alpha) / Math.PI;
			double w1 = TWOPI * warped_alpha;

			double k = Math.PI / Order;
			for (double theta = isOdd(order) ? 0 : k / 2; theta < TWOPI; theta += k)
			{
				Complex pole = Complex.FromPolarCoordinates(1, theta);
				if (pole.Real < 0)		// it's in the stable region
					poles.Add(pole * w1);
			}
			return poles;
		}


		// Map the S-plane poles & zeros onto the Z-plane, 
		// using a bilinear transform
		List<Complex> bilinear_transform(List<Complex> spoles) 
		{
			var poles = new List<Complex>();
			foreach (Complex pole in spoles)
				poles.Add(bilinear_transform(pole));
			return poles;
		}

		Complex bilinear_transform(Complex point)
		{ return (2.0 + point) / (2.0 - point); }


		// generate polynomial coefficients
		double[] polyCoefficients(List<Complex> points)
		{
			var ccoeffs = new Complex[points.Count + 1];
			ccoeffs[0] = 1;
			for (int i = 1; i < points.Count; ++i)
				ccoeffs[i] = 0;

			foreach (Complex point in points)
				multin(point, ccoeffs);

			double[] coeffs = new double[ccoeffs.Length];
			for (int i = 0; i < ccoeffs.Length; ++i)
				coeffs[i] = ccoeffs[i].Real;

			return coeffs;
		}

		// multiply factor (z-w) into the coefficients
		void multin(Complex w, Complex[] coeffs)
		{
			Complex nw = -w;
			for (int i = coeffs.Length - 1; i >= 1; --i)
				coeffs[i] = (nw * coeffs[i]) + coeffs[i - 1];
			coeffs[0] = nw * coeffs[0];
		}

		// evaluate response at z
		double evaluate_ratio(double[] topcoeffs, double[] botcoeffs, double x)
		{ return Utility.EvaluatePolynomial(topcoeffs, x) / Utility.EvaluatePolynomial(botcoeffs, x); }

		protected virtual void ConfigurationChanged(object sender, PropertyChangedEventArgs e) =>
			Initialized = false;
	}
}
