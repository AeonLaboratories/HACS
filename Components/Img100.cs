using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
    // TODO convert this class to the ManagedDevice pattern, make
    // Xgs600 DeviceManager
    public class Img100 : SwitchedManometer, IImg100, Img100.IDevice, Img100.IConfig
    {
        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            Controller = Find<Xgs600>(controllerName);
        }

        #endregion HacsComponent

        #region Device interfaces

        public new interface IDevice : SwitchedManometer.IDevice { }
        public new interface IConfig : SwitchedManometer.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces


        [JsonProperty("Controller")]
        string ControllerName { get => Controller?.Name; set => controllerName = value; }
        string controllerName;
        public Xgs600 Controller { get; set; }

        [JsonProperty]
        public string UserLabel { get; set; }

        void processResponse(string response)
        {
            if (response == "00")
                TurnOff();
			else if (response == "01")
                TurnOn();
            // else unrecognized response, generate error?
        }

        /// <summary>
        /// Turn the device on.
        /// </summary>
        public override bool TurnOn()
        {
            if (IsOn || MillisecondsInState <= MinimumMillisecondsOff) return false;
            Controller.TurnOn(UserLabel, processResponse);
            return true;
        }


        /// <summary>
        /// Turn the device off.
        /// </summary>
        public override bool TurnOff()
        {
            if (!IsOn) return false;
			Controller.TurnOff(UserLabel, processResponse);
            return true;
        }
    }
}
