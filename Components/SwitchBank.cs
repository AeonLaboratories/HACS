using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Text;
using Utilities;
using static Utilities.Utility;

namespace HACS.Components
{
    public class SwitchBank : SerialDeviceManager, ISwitchBank,
        SwitchBank.IConfig, SwitchBank.IDevice
	{
        #region HacsComponent
        #endregion HacsComponent

        #region Device constants
        /// <summary>
        /// Error codes which may be reported by this device's Controller
        /// </summary>
        [Flags]
        public enum ErrorCodes
        {
            /// <summary>
            /// No error, status is ok
            /// </summary>
			None = 0,
            /// <summary>
            /// RS232 input buffer overflow; commands are too frequent
            /// </summary>
			RxBufferOverflow = 1,
            /// <summary>
            /// RS232 CRC error (cyclical redundancy check failed)
            /// </summary>
			CRC = 2,
            /// <summary>
            /// Unrecognized command
            /// </summary>
			BadCommand = 4,
            /// <summary>
            /// Invalid channel
            /// </summary>
			BadChannel = 8,
        }

        #endregion Device constants

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : SerialDeviceManager.IDevice
        {
            string Model { get; set; }
            string Firmware { get; set; }

            int SelectedSwitch { get; set; }
            ErrorCodes Errors { get; set; }
        }
        public new interface IConfig : SerialDeviceManager.IConfig { }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region IDeviceManager

        public override bool IsSupported(IManagedDevice d, string key)
        {
            if (IsValidKey(key, "", Channels - 1) && d is IManagedSwitch)
                return true;

            Log.Record($"Connect: {d.Name}'s key \"{key}\" and type ({d.GetType()}) are not supported together." +
                $"\r\n\tOne of them is invalid or they are not compatible.");
            return false;
        }

        #endregion IDeviceManager

        #region Settings

        [JsonProperty]
        public int Channels
        {
            get => channels;
            set => Ensure(ref channels, value);
        }
        int channels;
        #endregion Settings

        #region Retrieved device values

        /// <summary>
        /// The device model identifier.
        /// </summary>
        public string Model => model;
        string IDevice.Model
        {
            get => model;
            set => Set(ref model, value);
        }
        string model;

        /// <summary>
        /// The firmware revision identifier.
        /// </summary>
        public string Firmware => firmware;
        string IDevice.Firmware
        {
            get => firmware;
            set => Set(ref firmware, value);
        }
        string firmware;

        /// <summary>
        /// The channel number of the currently selected switch.
        /// </summary>
        public int SelectedSwitch => selectedSwitch;
        int IDevice.SelectedSwitch
        {
            get => selectedSwitch;
            set => Set(ref selectedSwitch, value);
        }
        int selectedSwitch;

        /// <summary>
        /// Error codes reported by the controller.
        /// </summary>
        public ErrorCodes Errors => errors;
        ErrorCodes IDevice.Errors
        {
            get => errors;
            set => Ensure(ref errors, value);
        }
        ErrorCodes errors;

        #endregion Retrieved device values

        public override string ToString()
        {
            var sb = new StringBuilder(base.ToString());
            if (Devices.Count > 0)
            {
                var sb2 = new StringBuilder();
                foreach (var d in Devices.Values)
                    if (d is ManagedSwitch s)
                        sb2.Append($"\r\n{s}".Replace($"\r\n   ({Name} ", "("));
                sb.Append(Utility.IndentLines(sb2.ToString()));
            }
            return sb.ToString();
        }

        #endregion Class interface properties and methods

        #region IDeviceManager

        protected override IManagedDevice FindSupportedDevice(string name)
        {
            if (Find<ManagedSwitch>(name) is ManagedSwitch d) return d;
            return null;
        }

        #endregion IDeviceManager

        #region State Management
        #endregion State Management

        #region Controller commands

        string ControllerDataCommand => "z";
        string ControllerResetCommand => "x";
        bool ActionNeeded(ISwitch s) =>
            s.OnOffState.IsUnknown() || s.OnOffState.IsOn() != s.Config.State.IsOn();
        string OneZero(ISwitch s) => s.Config.State.IsOn().OneZero();

        #endregion Controller commands

        #region Controller interactions

        protected override void SelectDeviceService()
        {
            if (LogEverything)
                Log.Record($"SelectDeviceService: Device = {ServiceDevice?.Name}, Request = \"{ServiceRequest}\"");
            SetServiceValues("");       // default to nothing needed

            if (ServiceDevice == this)
            {
                if (Device.UpdatesReceived == 0)
                    SetServiceValues(ControllerDataCommand, 1);
            }
            else if (ServiceDevice is IManagedSwitch s)
            {
                if (s.UpdatesReceived == 0 || s.Device.OnOffState.IsUnknown())
                    SetServiceValues($"n{ChannelNumber} r", 1);
                else if (ActionNeeded(s))
                    SetServiceValues($"n{ChannelNumber} {OneZero(s)} r", 1);
            }
            else
            {
                Log.Record($"{ServiceDevice?.Name}'s device type ({ServiceDevice?.GetType()}) is not supported.");
            }
            if (LogEverything)
                Log.Record($"ServiceDevice = {ServiceDevice?.Name}, ServiceCommand = \"{ServiceCommand}\", ResponsesExpected = {ResponsesExpected}");
        }


        #region helper properties and methods for controller responses

        // TODO: These two helpers are all over the place. fix this
        bool ErrorCheck(bool errorCondition, string errorMessage)
        {
            if (errorCondition)
            {
                if (LogEverything)
                    Log.Record($"{errorMessage}");
                return true;
            }
            return false;
        }
        bool LengthError(object[] elements, int nExpected, string elementDescription = "value", string where = "")
        {
            var n = elements.Length;
            if (!where.IsBlank()) where = $" {where}";
            return ErrorCheck(n != nExpected,
                $"Expected {ToUnitsString(nExpected, elementDescription)}{where}, not {n}.");
        }

        #endregion helper properties and methods for controller responses


        protected override bool ValidateResponse(string response, int which)
        {
            try
            {
                var lines = response.GetLines();
                if (lines.Length == 0) return false;
                var values = lines[0].GetValues();
                var n = values.Length;

                if (SerialController.CommandMessage[0] == ControllerDataCommand[0])       // Controller data
                {
                    if (LengthError(lines, 1, "controller data line"))
                        return false;

                    if (LengthError(values, 4, "value", "on controller data line 1"))
                        return false;

                    Device.Model = values[2];
                    Device.Firmware = values[3];

                    Device.UpdatesReceived++;
                }
                else if (SerialController.CommandMessage[0] == 'r')       // report
                {
                    if (LengthError(lines, 1, "status report line"))
                        return false;

                    if (LengthError(values, 3, "report value"))
                        return false;

                    var i = int.Parse(values[0]);
                    if (ErrorCheck(i < 0 || i >= Channels,
                            $"Invalid channel in status report: {i}"))
                        return false;
                    Device.SelectedSwitch = i;

                    var key = $"{i}";
                    if (ErrorCheck(!Devices.ContainsKey(key),
                            $"Report received, but no device is assigned to channel {i}"))
                        return false;

                    var d = Devices[key] as ManagedSwitch;
                    if (ErrorCheck(d == null,
                            $"The device at {key} isn't a {typeof(ManagedSwitch)}"))
                        return false;

                    d.Device.OnOffState = (values[1][0] == '1').ToOnOffState();
                    Device.Errors = (ErrorCodes)int.Parse(values[2]);
                    d.Device.UpdatesReceived++;
                }
                else
                {
                    if (LogEverything)
                        Log.Record($"Unrecognized response");
                    return false;       // unrecognized response
                }
                if (LogEverything)
                    Log.Record($"Response successfully decoded");
                return true;
            }
            catch (Exception e)
            {
                //if (LogEverything)
                Log.Record($"{e}");
                return false;
            }
        }

        #endregion Controller interactions

	}
}
