using HACS.Core;
using Newtonsoft.Json;
using static HACS.Components.CegsPreferences;


namespace HACS.Components
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Aliquot : NamedObject, IAliquot
    {
        [JsonProperty("Sample")]
        string SampleName
        {
            get => sample?.Name ?? sampleName;
            set => sampleName = value;
        }
        string sampleName;
        
        public ISample Sample 
        {
            get => sample ??= Find<Sample>(sampleName);
            set => Ensure(ref sample, value);
        }
        ISample sample;

        [JsonProperty]
        public string GraphiteReactor { get; set; }
        [JsonProperty]
        public double MicrogramsCarbon { get; set; }
        public double MicromolesCarbon => MicrogramsCarbon / GramsCarbonPerMole;
        [JsonProperty]
        public double InitialGmH2Pressure { get; set; }
        [JsonProperty]
        public double FinalGmH2Pressure { get; set; }
        [JsonProperty]
        public double H2CO2PressureRatio { get; set; }
        [JsonProperty]
        public double ExpectedResidualPressure { get; set; }
        [JsonProperty]
        public double ResidualPressure { get; set; }
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
            GraphiteReactor = cloneMe.GraphiteReactor;
            MicrogramsCarbon = cloneMe.MicrogramsCarbon;
            H2CO2PressureRatio = cloneMe.H2CO2PressureRatio;
            ExpectedResidualPressure = cloneMe.ExpectedResidualPressure;
            ResidualPressure = cloneMe.ResidualPressure;
            ResidualMeasured = cloneMe.ResidualMeasured;
            Tries = cloneMe.Tries;
        }

        public Aliquot()
        {
            Name = "";
            Sample = null;
            GraphiteReactor = "";
            MicrogramsCarbon = 0;
            InitialGmH2Pressure = 0;
            FinalGmH2Pressure = 0;
            H2CO2PressureRatio = 0;
            ExpectedResidualPressure = 0;
            ResidualPressure = 0;
            ResidualMeasured = false;
            Tries = 0;
        }

        public Aliquot(string name) : this()
        {
            Name = name;
        }
    }
}
