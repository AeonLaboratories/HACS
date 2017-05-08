using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utilities;
using System.Xml.Serialization;

namespace HACS.Components
{
	public enum SampleSources { TubeFurnace, InletPortCombustion, InletPortNeedle, InletPortBreakseal, CO2Supply }
	public class Aliquot : NamedObject
    {
		[XmlIgnore] public Sample Sample { get; set; }
		public string GR { get; set; }
		public double ugC { get; set; }
		public double pH2Initial { get; set; }
		public double pH2Final { get; set; }
		public double ResidualExpected { get; set; }
		public double Residual { get; set; }
		public bool ResidualMeasured { get; set; }
		public int Tries { get; set; }

		/// <summary>
		/// Returns a clone of the given aliquot
		/// </summary>
		/// <param name="cloneMe">The aliquot to clone</param>
		public Aliquot(Aliquot cloneMe)
		{
			Name = cloneMe.Name;
			Sample = cloneMe.Sample;
			GR = cloneMe.GR;
			ugC = cloneMe.ugC;
			pH2Initial = cloneMe.pH2Initial;
			pH2Final = cloneMe.pH2Final;
			ResidualExpected = cloneMe.ResidualExpected;
			Residual = cloneMe.Residual;
			ResidualMeasured = cloneMe.ResidualMeasured;
			Tries = cloneMe.Tries;
		}

		public Aliquot()
		{
			Name = "";
			Sample = null;
			GR = "";
			ugC = 0;
			pH2Initial = 0;
			pH2Final = 0;
			ResidualExpected = 0;
			Residual = 0;
			ResidualMeasured = false;
			Tries = 0;
		} 

        public Aliquot(string name) : this()
		{ 
			Name = name;
		}
    }

    public class Sample : NamedObject
    {
		public string ID;
		public SampleSources Source;
		public string Process;
		public int Filtrations;
		public bool SulfurSuspected;
		public bool NotifyCC_S;
		public bool Take_d13C;
		public bool Only_d13C;
		public double grams;		// Sample size
		[XmlIgnore] public double milligrams { get { return grams * 1000; } set { grams = value / 1000; } }
		[XmlIgnore] public double micrograms { get { return grams * 1000000; } set { grams = value / 1000000; } }
		public double ugDC;			// micrograms of dilution (dead) carbon added
		public double ugC;			// total micrograms carbon (C from the sample + ugDC)
        public double d13C_ugC;
        public double d13C_ppm;
		public int nAliquots = 0;
        public List<Aliquot> Aliquots;

        public Sample()
		{
			Name = "";
			ID = "";
			Aliquots = new List<Aliquot>();
		}

		public Sample(string name) : this()
		{
			Name = name;
		}

		/// <summary>
		/// Returns a clone of the given sample
		/// </summary>
		/// <param name="cloneMe">The sample to clone</param>
		public Sample(Sample cloneMe) : this()
		{
			Name = cloneMe.Name;
			ID = cloneMe.ID;
			Source = cloneMe.Source;
			Process = cloneMe.Process;
			Filtrations = cloneMe.Filtrations;
			SulfurSuspected = cloneMe.SulfurSuspected;
			NotifyCC_S = cloneMe.NotifyCC_S;
			Take_d13C = cloneMe.Take_d13C;
			Only_d13C = cloneMe.Only_d13C;
			grams = cloneMe.grams;
			ugDC = cloneMe.ugDC;
			ugC = cloneMe.ugC;
			d13C_ugC = cloneMe.d13C_ugC;
			d13C_ppm = cloneMe.d13C_ppm;
			nAliquots = cloneMe.nAliquots;
			foreach (Aliquot a in cloneMe.Aliquots)
			{
				Aliquot sa = new Aliquot(a);
				sa.Sample = this;
				Aliquots.Add(sa);
			}
		}
    }
}
