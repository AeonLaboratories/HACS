using System.Collections.Generic;
using Utilities;
using System.Xml.Serialization;
using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
	public class Sample : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<Sample> List = new List<Sample>();
		public static new Sample Find(string name) { return List.Find(x => x?.Name == name); }

		public Sample()
		{
			List.Add(this);
		}

		#endregion Component Implementation

		[JsonProperty]
		public string ID = "";
		[JsonProperty]
		public HacsComponent<SampleSource> SampleSourceRef { get; set; }
        public SampleSource Source => SampleSourceRef?.Component;

		[JsonProperty]
		public string Process;
		[JsonProperty]
		public bool SulfurSuspected;
		[JsonProperty]
		public bool NotifyCC_S;
		[JsonProperty]
		public bool Take_d13C;
		[JsonProperty]
		public double grams;		// Sample size
		[XmlIgnore] public double milligrams { get { return grams * 1000; } set { grams = value / 1000; } }
		[XmlIgnore] public double micrograms { get { return grams * 1000000; } set { grams = value / 1000000; } }
		[JsonProperty]
		public double ugDC;			// micrograms of dilution (dead) carbon added
		[JsonProperty]
		public double ugC;			// total micrograms carbon (C from the sample + ugDC)
		[JsonProperty]
		public double d13C_ugC;
		[JsonProperty]
		public double d13C_ppm;
		[JsonProperty]
		public int nAliquots = 0;
		[JsonProperty]
		public List<Aliquot> Aliquots = new List<Aliquot>();

		/// <summary>
		/// Returns a clone of the given sample
		/// </summary>
		/// <param name="cloneMe">The sample to clone</param>
		public Sample(Sample cloneMe) : this()
		{
			Name = cloneMe.Name;
			ID = cloneMe.ID;
            SampleSourceRef = cloneMe.SampleSourceRef;
			Process = cloneMe.Process;
			SulfurSuspected = cloneMe.SulfurSuspected;
			NotifyCC_S = cloneMe.NotifyCC_S;
			Take_d13C = cloneMe.Take_d13C;
			grams = cloneMe.grams;
			ugDC = cloneMe.ugDC;
			ugC = cloneMe.ugC;
			d13C_ugC = cloneMe.d13C_ugC;
			d13C_ppm = cloneMe.d13C_ppm;
			nAliquots = cloneMe.nAliquots;
			foreach (Aliquot a in cloneMe.Aliquots)
			{
				Aliquot sa = new Aliquot(a)
				{
					Sample = this
				};
				Aliquots.Add(sa);
			}
		}
    }

	[JsonObject(MemberSerialization.OptIn)]
    public class Aliquot
    {
		[XmlAttribute]
		[JsonProperty]
		public virtual string Name { get; set; }
		[XmlIgnore] public Sample Sample { get; set; }
		[JsonProperty]
        public string GR { get; set; }
		[JsonProperty]
        public double ugC { get; set; }
		[JsonProperty]
        public double pH2Initial { get; set; }
		[JsonProperty]
        public double pH2Final { get; set; }
		[JsonProperty]
        public double ResidualExpected { get; set; }
		[JsonProperty]
        public double Residual { get; set; }
		[JsonProperty]
        public bool ResidualMeasured { get; set; }
		[JsonProperty]
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
}
