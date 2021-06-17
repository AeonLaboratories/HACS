using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using Utilities;

namespace HACS.Components
{
    public class SerialDeviceManager : DeviceManager, ISerialDeviceManager,
        SerialDeviceManager.IConfig, SerialDeviceManager.IDevice
    {

        #region Physical device constants
        #endregion Physical device constants

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : DeviceManager.IDevice { }
        public new interface IConfig : DeviceManager.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region IDeviceManager

        public override bool Ready =>
            base.Ready &&
            (SerialController?.Ready ?? false);

        public override bool HasWork =>
            base.HasWork ||
            !serviceQ.IsEmpty ||
            (SerialController?.Busy ?? false);

        #endregion IDeviceManager

        [JsonProperty]
        public SerialController SerialController
        {
            get => serialController;
            set
            {
                if (serialController != value)
                {
                    // TODO: if serialController isn't null, first remove (restore??) the following properties?
                    serialController = value;
                    if (serialController != null)
                    {
                        UpdateSerialControllerLog();
                        serialController.SelectServiceHandler = SelectService;
                        serialController.ResponseProcessor = ValidateResponse;
                        serialController.LostConnection -= OnControllerLost;
                        serialController.LostConnection += OnControllerLost;
                    }
                    NotifyPropertyChanged();
                }
            }
        }
        SerialController serialController;

        void UpdateSerialControllerLog()
        {
            if (SerialController == null) return;
            SerialController.Log = Log;
            SerialController.LogEverything = LogEverything;
        }

        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var propertyName = e?.PropertyName;
            if (propertyName == nameof(Log) || propertyName == nameof(LogEverything))
                UpdateSerialControllerLog();
            base.OnPropertyChanged(sender, e);
        }


        #endregion Class interface properties and methods


        ConcurrentQueue<ObjectPair> serviceQ = new ConcurrentQueue<ObjectPair>();
        protected IManagedDevice ServiceDevice = null;
        protected string ServiceRequest = "";
        protected string ServiceCommand = "";
        protected int ResponsesExpected = 0;

        /// <summary>
        /// Returns the positive integer found at the end of the current 
        /// ServiceDevice's key. Returns -1 on failure.
        /// </summary>
        protected virtual int ChannelNumber =>
            ExtractChannelNumber(Keys[ServiceDevice]);

        /// <summary>
        /// Enqueue a device for a service call.
        /// </summary>
        /// <param name="sender">The device needing service</param>
        protected override void DeviceConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is IManagedDevice d)
            {
                if (LogEverything)
                    Log.Record($"SerialDeviceManager {Name}: Noticed {d.Name}'s {e.PropertyName} event.");
                serviceQ.Enqueue(new ObjectPair(d, e.PropertyName));
                if (SerialController != null)
                    SerialController.Hurry = true;
                StopWaiting();
            }
        }

        /// <summary>
        /// Based on the current value of ServiceDevice, determines what
        /// command to issue, if any, and if so, use setServiceValues() 
        /// to select the command and set the expected number of responses.
        /// </summary>
        protected virtual void SelectDeviceService()
        {
            if (LogEverything) Log.Record($"SerialDeviceManager {Name}.SelectDeviceService does nothing.");
            SetServiceValues("");
        }

        /// <summary>
        /// Sets the command string to send to the controller, and number of 
        /// responses to expect as a result.
        /// </summary>
        /// <param name="serviceCommand">The command string to transmit to the controller.</param>
        /// <param name="responsesExpected">The number of responses to expect, based on the command.</param>
        protected void SetServiceValues(string serviceCommand, int responsesExpected = 0)
        {
            ServiceCommand = serviceCommand;
            ResponsesExpected = responsesExpected;
        }

        #region State Management

        /// <summary>
        /// State is invalid if it is inconsistent with the desired Configuration, 
        /// or if the State doesn't fully and accurately represent the state of 
        /// the controller.
        /// </summary>
        protected virtual bool StateInvalid => UpdatesReceived < 1;

        #endregion State management

        #region Controller interactions

        /// <summary>
        /// Selects the ServiceDevice, calls SelectDeviceService to set
        /// ServiceCommand and ExpectedResponses, and determines
        /// whether to Hurry.
        /// </summary>
        protected virtual SerialController.Command SelectService()
        {
            bool hurry;
            if (StateInvalid)
            {
                ServiceDevice = this;
                ServiceRequest = "{config self}";
                SelectDeviceService();
            }
            else
            {
                if (ServiceDevice == this)
                    SetServiceValues("");
                else if (ServiceDevice != null)
                    SelectDeviceService();

                ObjectPair request;
                while (ServiceCommand.IsBlank() && serviceQ.TryDequeue(out request))
                {
                    ServiceDevice = request.x as IManagedDevice;
                    ServiceRequest = request.y as string;
                    if (LogEverything)
                    {
                        var o = request.x as NamedObject;
                        Log.Record($"SerialDeviceManager {Name}: Dequeued {o.GetType()} {o.Name} for service \"{request.y}\".");
                        if (ServiceDevice == null)
                            Log.Record($"SerialDeviceManager {o.Name} is not {nameof(IManagedDevice)}.");
                    }
                    SelectDeviceService();
                }
            }

            if (ServiceCommand.IsBlank())
            {
                ServiceDevice = this;
                ServiceRequest = "{idle}";
                SelectDeviceService();
                hurry = false;
            }
            else
                hurry = true;

            if (LogEverything) Log.Record($"SerialDeviceManager {Name}: ServiceCommand = \"{SerialController.Escape(ServiceCommand)}\", ResponsesExpected = {ResponsesExpected}, Hurry = {hurry}");
            return new SerialController.Command(ServiceCommand, ResponsesExpected, hurry);
        }

        /// <summary>
        /// What to do if the SerialController loses contact with
        /// the hardware.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnControllerLost(object sender, EventArgs e)
        {
            if (LogEverything) Log.Record($"SerialDeviceManager {Name}: {SerialController?.Name} lost connection");
            if (UpdatesReceived > 0)
            {
                foreach (var d in Devices.Values)
                    ScheduleInitialService(d);

                // indicate device initialization is scheduled
                Device.UpdatesReceived = 0;
            }
        }

        /// <summary>
        /// Accepts a response string from the SerialController
        /// and returns whether it is a valid response or not.
        /// The default implementation ignores the response
        /// and always returns true.
        /// </summary>
        /// <param name="response">The response string</param>
        /// <param name="which">In case multiple responses were returned for the command, this says which one. Starts at 0.</param>
        /// <returns>true if the response is valid</returns>
        protected virtual bool ValidateResponse(string response, int which)
        {
            return true;
        }

        #endregion Controller interactions
    }
}