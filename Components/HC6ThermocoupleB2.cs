using HACS.Core;
using System.Text;
using Utilities;

namespace HACS.Components
{
    public class HC6ThermocoupleB2 : ManagedThermocouple, IHC6ThermocoupleB2,
        HC6ThermocoupleB2.IConfig, HC6ThermocoupleB2.IDevice
    {
        #region static

        public static implicit operator double(HC6ThermocoupleB2 x)
        { return x?.Temperature ?? 0; }

        #endregion static


        #region Device interfaces

        public new interface IDevice : ManagedThermocouple.IDevice
        {
            HC6ControllerB2.ErrorCodes Errors { get; set; }
        }

        public new interface IConfig : ManagedThermocouple.IConfig { }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces


        /// <summary>
        /// Error codes reported by the controller.
        /// </summary>
        public HC6ControllerB2.ErrorCodes Errors => errors;
        HC6ControllerB2.ErrorCodes IDevice.Errors
        {
            get => errors;
            set => Ensure(ref errors, value);
        }
        HC6ControllerB2.ErrorCodes errors;


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"{Name}:");
            if (Type != ThermocoupleType.None)
                sb.Append($" {Temperature:0.0} {UnitSymbol} (Type {Type})");

            sb.Append(ManagedDevice.ManagerString(this));

            if (Errors != 0)
            {
                var sb2 = new StringBuilder();
                sb2.Append($"\r\nError = {Errors}");
                sb.Append(Utility.IndentLines(sb2.ToString()));
            }
            return sb.ToString();
        }
    }
}
