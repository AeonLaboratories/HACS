using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
	public class Thermocouple : Thermometer, IThermocouple, Thermocouple.IDevice, Thermocouple.IConfig
    {
		#region static

		public static implicit operator double(Thermocouple x)
		{ return x?.Temperature ?? 0; }

        #endregion static

        #region Device interfaces

        public new interface IDevice : Thermometer.IDevice
        {
            ThermocoupleType Type { get; set; }
        }

        public new interface IConfig : Thermometer.IConfig
        {
            ThermocoupleType Type { get; }
        }
        public new IConfig Config => this;

        public new IDevice Device => this;

        #endregion Device interfaces

        /// <summary>
        /// The Thermocouple's Type designation
        /// </summary>
        public virtual ThermocoupleType Type
        {
            get => type;
            set => Ensure(ref TargetType, value, NotifyConfigChanged, nameof(TargetType));
        }
        [JsonProperty("Type")]
        ThermocoupleType TargetType;
        ThermocoupleType IConfig.Type => TargetType;
        ThermocoupleType IDevice.Type
        {
            get => type;
            set => Ensure(ref type, value);
        }
        ThermocoupleType type;

        public Thermocouple(IHacsDevice d = null) : base(d) { }

    }
}
