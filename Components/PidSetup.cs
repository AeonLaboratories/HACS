using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
    public class PidSetup : HacsComponent, IPidSetup
    {
        #region static
        public static int DefaultGainPrecision = 2;
        public static int DefaultIntegralPrecision = 5;
        public static int DefaultDerivativePrecision = 1;
        public static int DefaultPresetPrecision = 3;

        static List<PidSetup> setups = CachedList<PidSetup>();
        public static PidSetup Find(int gain, int integral, int derivative, int preset)
        {
            foreach (var setup in setups)
            {
                if (setup.encodedGain == gain &&
                    setup.encodedIntegral == integral &&
                    setup.encodedDerivative == derivative &&
                    setup.encodedPreset == preset)
                    return setup;
            }
            return null;
        }
        #endregion static

        /// <summary>
        /// The number [0..15] of fractional digits of the PidGain value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 2.
        /// </summary>
        public int GainPrecision
        {
            get => gainPrecision;
            set => Ensure(ref gainPrecision, value);
        }
        int gainPrecision = DefaultGainPrecision;

        /// <summary>
        /// The number [0..15] of fractional digits of the PidIntegral value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 5.
        /// </summary>
        public int IntegralPrecision
        {
            get => integralPrecision;
            set => Ensure(ref integralPrecision, value);
        }
        int integralPrecision = DefaultIntegralPrecision;

        /// <summary>
        /// The number [0..15] of fractional digits of the PidDerivative value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 1.
        /// </summary>
        public int DerivativePrecision
        {
            get => derivativePrecision;
            set => Ensure(ref derivativePrecision, value);
        }
        int derivativePrecision = DefaultDerivativePrecision;

        /// <summary>
        /// The number [0..15] of fractional digits of the PidPreset value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 3.
        /// </summary>
        public int PresetPrecision
        {
            get => presetPrecision;
            set => Ensure(ref presetPrecision, value);
        }
        int presetPrecision = DefaultPresetPrecision;


        /// <summary>
        /// Controller gain: Kc
        /// </summary>
        [JsonProperty]
        public double Gain
        {
            get => encodedGain.DoubleValue;
            set => encodedGain.DoubleValue = value;
        }
        IntEncodableDouble encodedGain;
        public int EncodedGain
        {
            get => encodedGain.IntValue;
            set => encodedGain.IntValue = value;
        }


        /// <summary>
        /// Integral coefficient: Ci = 1 / Ti, 
        /// where Ti is the process time constant "dead time plus lag"
        /// in units of the control output (power level) update period.
        /// Note that this coefficient does not depend on the process 
        /// gain term Kc.
        /// </summary>
        [JsonProperty]
        public double Integral
        {
            get => encodedIntegral.DoubleValue;
            set => encodedIntegral.DoubleValue = value;
        }
        IntEncodableDouble encodedIntegral;
        public int EncodedIntegral
        {
            get => encodedIntegral.IntValue;
            set => encodedIntegral.IntValue = value;
        }


        /// <summary>
        /// Derivative coefficient: Cd = Kc * Td,
        /// where Kc is the controller gain and Td is the dead time
        /// in units of the control output (power level) update period.
        /// Note that this coefficient includes the process gain factor.
        /// </summary>
        [JsonProperty]
        public double Derivative
        {
            get => encodedDerivative.DoubleValue;
            set => encodedDerivative.DoubleValue = value;
        }
        IntEncodableDouble encodedDerivative;
        public int EncodedDerivative
        {
            get => encodedDerivative.IntValue;
            set => encodedDerivative.IntValue = value;
        }


        /// <summary>
        /// Preset coefficient: Cpr = 1 / gp
        /// where the process gain gp is usually dPV/dCO where dPV and
        /// dCO are typical changes in the process variable (e.g., 
        /// temperature) and control output (e.g., power level) over 
        /// a representative step test.
        /// </summary>
        [JsonProperty]
        public double Preset
        {
            get => encodedPreset.DoubleValue;
            set => encodedPreset.DoubleValue = value;
        }
        IntEncodableDouble encodedPreset;
        public int EncodedPreset
        {
            get => encodedPreset.IntValue;
            set => encodedPreset.IntValue = value;
        }


        public PidSetup()
        {
            encodedGain = new IntEncodableDouble(GainPrecision);
            encodedIntegral = new IntEncodableDouble(IntegralPrecision);
            encodedDerivative = new IntEncodableDouble(DerivativePrecision);
            encodedPreset = new IntEncodableDouble(PresetPrecision);
            encodedGain.PropertyChanged += OnPropertyChanged;
            encodedIntegral.PropertyChanged += OnPropertyChanged;
            encodedDerivative.PropertyChanged += OnPropertyChanged;
            encodedPreset.PropertyChanged += OnPropertyChanged;
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is IntEncodableDouble d)
            {
                if (d == encodedGain)
                    NotifyPropertyChanged("Gain");
                else if (d == encodedIntegral)
                    NotifyPropertyChanged("Integral");
                else if (d == encodedDerivative)
                    NotifyPropertyChanged("Derivative");
                else if (d == encodedPreset)
                    NotifyPropertyChanged("Preset");
            }
        }

        /// <summary>
        /// Creates a new instance with the specified PidSetup precisions.
        /// </summary>
        /// <param name="gainPrecision">The number [0..15] of fractional digits of the PidGain value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 2.</param>
        /// <param name="integralPrecision">The number [0..15] of fractional digits of the PidIntegral value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 5.</param>
        /// <param name="derivativePrecision">The number [0..15] of fractional digits of the PidDerivative value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 1.</param>
        /// <param name="presetPrecision">The number [0..15] of fractional digits of the PidPreset value
        /// to be encoded as whole numbers in a scaled integer representation.
        /// The default is 3.</param>
        public PidSetup(int gainPrecision, int integralPrecision, int derivativePrecision, int presetPrecision)
        {
            encodedGain = new IntEncodableDouble(gainPrecision);
            encodedIntegral = new IntEncodableDouble(integralPrecision);
            encodedDerivative = new IntEncodableDouble(derivativePrecision);
            encodedPreset = new IntEncodableDouble(presetPrecision);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"{Name}");
            StringBuilder sb2 = new StringBuilder();
            sb2.Append($"\r\nGain: {encodedGain.IntValue}");
            sb2.Append($"\r\nIntegral: {encodedIntegral.IntValue}");
            sb2.Append($"\r\nDerivative: {encodedDerivative.IntValue}");
            sb2.Append($"\r\nPreset: {encodedPreset.IntValue}");
            sb.Append(Utility.IndentLines(sb2.ToString()));
            return sb.ToString();
        }
    }
}
