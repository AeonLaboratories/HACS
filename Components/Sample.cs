using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static HACS.Components.CegsPreferences;

namespace HACS.Components
{
    public class Sample : HacsComponent, ISample
	{
        #region static
		/// <summary>
		/// Generates a new unique Sample Name.
		/// </summary>
		public static string GenerateSampleName => $"{SampleCounter++}";

		#endregion static

		#region HacsComponent
		[HacsConnect]
		protected virtual void Connect()
		{
			InletPort = Find<IInletPort>(inletPortName);
		}

		#endregion HacsComponent

		/// <summary>
		/// Typically assigned by the laboratory to identify and track the sample.
		/// </summary>
		[JsonProperty]
		public string LabId
		{
			get => labId;
			set => Ensure(ref labId, value);
		}
		string labId;


		[JsonProperty("InletPort")]
		string InletPortName { get => InletPort?.Name ?? ""; set => inletPortName = value; }
		string inletPortName;
		public IInletPort InletPort
		{
			get => inletPort;
			set => Ensure(ref inletPort, value);
		}
		IInletPort inletPort;


		[JsonProperty]
		public string Process
		{
			get => process;
			set => Ensure(ref process, value);
		}
		string process;

		[JsonProperty]
		public bool SulfurSuspected
		{
			get => sulfurSuspected;
			set => Ensure(ref sulfurSuspected, value);
		}
		bool sulfurSuspected;

		[JsonProperty]
		public bool Take_d13C
		{
			get => take_d13C;
			set => Ensure(ref take_d13C, value);
		}
		bool take_d13C;

		/// <summary>
		/// Sample size
		/// </summary>
		[JsonProperty]		
		public double Grams
		{
			get => grams;
			set => Ensure(ref grams, value, OnPropertyChanged);
		}
		double grams;

		public double Milligrams
		{
			get => Grams * 1000;
			set => Grams = value / 1000;
		}

		public double Micrograms
		{ 
			get => Grams * 1000000;
			set => Grams = value / 1000000;
		}

		/// <summary>
		/// The initial sample mass, expressed as micromoles; 
		/// intended to be used with pure gas samples like CO2 or CH4 that
		/// have one carbon atom per particle.
		/// Perhaps this should be renamed to avoid confusion with
		/// the other similarly-named properties ("xxCarbon"), which refer 
		/// to the extracted CO2.
		/// </summary>
		public double Micromoles
        {
			get => Micrograms / GramsCarbonPerMole;
			set => Micrograms = value * GramsCarbonPerMole;
        }

		/// <summary>
		/// micrograms of dilution (dead) carbon added
		/// </summary>
		[JsonProperty]
		public double MicrogramsDilutionCarbon
		{
			get => microgramsDilutionCarbon;
			set => Ensure(ref microgramsDilutionCarbon, value);
		}
		double microgramsDilutionCarbon;

		/// <summary>
		/// total micrograms carbon from the sample
		/// </summary>
		[JsonProperty]
		public double TotalMicrogramsCarbon
		{
			get => totalMicrogramsCarbon;
			set => Ensure(ref totalMicrogramsCarbon, value, OnPropertyChanged);
		}
		double totalMicrogramsCarbon;

		public double TotalMicromolesCarbon
		{
			get => TotalMicrogramsCarbon / GramsCarbonPerMole;
			set => TotalMicrogramsCarbon = value * GramsCarbonPerMole;
		}

		/// <summary>
		/// micrograms carbon (C from the sample + ugDC) selected for analysis
		/// </summary>
		[JsonProperty]
		public double SelectedMicrogramsCarbon
		{
			get => selectedMicrogramsCarbon;
			set => Ensure(ref selectedMicrogramsCarbon, value, OnPropertyChanged);
		}
		double selectedMicrogramsCarbon;

		public double SelectedMicromolesCarbon
		{
			get => SelectedMicrogramsCarbon / GramsCarbonPerMole;
			set => SelectedMicrogramsCarbon = value * GramsCarbonPerMole;
		}

		[JsonProperty]
		public double Micrograms_d13C
		{
			get => micrograms_d13C;
			set => Ensure(ref micrograms_d13C, value);
		}
		double micrograms_d13C;

		[JsonProperty]
		public double d13CPartsPerMillion
		{
			get => _d13CPartsPerMillion;
			set => Ensure(ref _d13CPartsPerMillion, value);
		}
		double _d13CPartsPerMillion;


		public List<string> AliquotIds
		{
			get => Aliquots.Select(a => a.Name).ToList();
			set
			{
				// allow blank Aliquot IDs; automatically generate them later
				// silently delete extraneous values
				while (value.Count > MaximumAliquotsPerSample)
					value.RemoveAt(MaximumAliquotsPerSample);

				for (int i = 0; i < value.Count; ++i)
                {
					if (AliquotsCount < i + 1)
						Aliquots.Add(new Aliquot() { Sample = this });
					Aliquots[i].Name = value[i];
				}
			}
		}

		public int AliquotsCount
		{
			get => Aliquots.Count;		// It is an error for Aliquots to be null
			set
			{
				if (value < 0) value = 0;
				if (value > MaximumAliquotsPerSample)
					value = MaximumAliquotsPerSample;
				if (Aliquots.Count < value)
				{
					for (int i = Aliquots.Count; i < value; ++i)
						Aliquots.Add(new Aliquot() { Sample = this });
				}
				else if (Aliquots.Count > value)
				{
					while (Aliquots.Count > value)
						Aliquots.RemoveAt(value);
				}
			}
		}


		[JsonProperty]
		public List<IAliquot> Aliquots
		{
			get => aliquots;
			set => Ensure(ref aliquots, value);
		}
		List<IAliquot> aliquots = new List<IAliquot>(); // Aliquots is never null

		protected void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var property = e?.PropertyName;
			if (property == nameof(Grams))
            {
				NotifyPropertyChanged(nameof(Milligrams));
				NotifyPropertyChanged(nameof(Micrograms));
				NotifyPropertyChanged(nameof(Micromoles));
			}
			else if (property == nameof(TotalMicrogramsCarbon))
            {
				NotifyPropertyChanged(nameof(TotalMicromolesCarbon));
            }
			else if (property == nameof(SelectedMicrogramsCarbon))
			{
				NotifyPropertyChanged(nameof(SelectedMicromolesCarbon));
			}
		}


		public Sample()
		{
			Name = GenerateSampleName;
		}

		public int AliquotIndex(IAliquot aliquot)
		{
			for (int i = 0; i < Aliquots.Count; ++i)
				if (Aliquots[i] == aliquot)
					return i;
			return -1;
		}

		public override string ToString()
        {
            return $"{LabId} [{InletPort?.Name ?? "---"}] {{{Name}}}";
        }
    }
}
