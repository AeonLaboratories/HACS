using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HACS.Components
{
    public class Xgs600 : SerialController, IXgs600
    {
        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            Gauges = FindAll<IMeter>(gaugeNames);
        }

        [HacsInitialize]
        protected virtual void Initialize()
        {
            if (LogEverything) Log.Record("Initializing...");
            SelectServiceHandler = SelectXGS600Service;
            ResponseProcessor = ValidateResponse;
            //LostConnection -= OnConnectionLost;
            //LostConnection += OnConnectionLost;
            if (LogEverything) Log.Record("Initialization complete");
        }

        #endregion HacsComponent

        #region Physical device constants
        public enum PressureUnits { Torr = 0x00, mBar = 0x01, Pascal = 0x02, Unknown = 0xFF }

        public enum Commands
        {
            ReadPressureDump = 0x0F,
            SetPressureUnitsTorr = 0x10, SetPressureUnitsmBar = 0x11, SetPressureUnitsPascal = 0x12,
            ReadPressureUnits = 0x13,
            SetEmissionOff = 0x30, SetEmissionOn = 0x31, ReadEmission = 0x32, Unknown = 0xFF
        }

        public Commands PressureUnitsCommand(PressureUnits units) =>
            (Commands)((int)Commands.SetPressureUnitsTorr + (int)units);

        const string InvalidCommand = "?FF";
        const string Address = "00"; // RS232 Communication
        const char ResponseStartChar = '>';
        const char TerminationChar = '\r';

        #endregion Physical device constants

        #region Class Interface Values - Check the device state using these properties and methods
        //
        // These properties expose the state of the physical
        // device to the class user.
        //

        [JsonProperty("Gauges")]
        List<string> GaugeNames { get => Gauges?.Names(); set => gaugeNames = value; }
        List<string> gaugeNames;
        public List<IMeter> Gauges { get; set; }

        public PressureUnits Units { get; protected set; } = PressureUnits.Unknown;

        [JsonProperty]
        public PressureUnits TargetUnits { get; set; }

        #endregion Class Interface Values

        #region Class Interface Methods -- Control the device using these functions
        //
        // These methods expose the functionality of the physical
        // device to the class user.
        //

        public void SetPressureUnits(PressureUnits pressureUnits) => TargetUnits = pressureUnits;

        public void TurnOn(string userLabel, Action<string> returnResponse = default) =>
            commandQ.Enqueue((userLabel, Commands.SetEmissionOn, returnResponse));

        public void TurnOff(string userLabel, Action<string> returnResponse = default) =>
            commandQ.Enqueue((userLabel, Commands.SetEmissionOff, returnResponse));

        #endregion Class Interface Methods

        #region main class

        string userLabel = default;
        Action<string> returnResponse;
        Commands commandCode = Commands.Unknown;
        string commandString = "";
        Commands priorCommand = Commands.Unknown;
        ConcurrentQueue<(string, Commands, Action<string>)> commandQ = new ConcurrentQueue<(string, Commands, Action<string>)>();

        #endregion


        #region State Manager

        protected virtual Command SelectXGS600Service()
        {
            bool hurry = true;

            // a response was received
            priorCommand = commandCode;

            if (Units == PressureUnits.Unknown ||
                    priorCommand == Commands.SetPressureUnitsTorr ||
                    priorCommand == Commands.SetPressureUnitsmBar ||
                    priorCommand == Commands.SetPressureUnitsPascal)
                setCommand(Commands.ReadPressureUnits);
            else if (Units != TargetUnits)
                setCommand(PressureUnitsCommand(TargetUnits));
            else if (priorCommand == Commands.SetEmissionOn || priorCommand == Commands.SetEmissionOff)
                setCommand(Commands.ReadEmission);
            else if (commandQ.TryDequeue(out (string label, Commands cmd, Action<string> response) t))
            {
                userLabel = t.label;
                returnResponse = t.response;
                setCommand(t.cmd);
            }
            else
            {
                setCommand(Commands.ReadPressureDump);
                hurry = false;
            }

            return commandString.IsBlank() ? DefaultCommand :
                new Command(commandString, 1, hurry);

        }

        #endregion

        #region Controller commands

        void setCommand(Commands code)
        {
            commandCode = code;
            commandString = formatCommand("");
        }

        void setCommand(Commands code, string userLabel)
        {
            commandCode = code;
            commandString = formatCommand($"U{userLabel}");
        }

        /// <summary>
        /// userLabelField must be empty or be a valid user label prepended with "U"
        /// </summary>
        /// <returns></returns>
        string formatCommand(string userLabelField) => 
            $"#{Address}{(int)commandCode:X2}{userLabelField}{TerminationChar}";

        #endregion

        #region Controller responses

        public bool ValidateResponse(string response, int which)
        {
            try
            {
                response = response.TrimEnd(TerminationChar);

                if (response == InvalidCommand)
                {
                    Log.Record($"Invalid Command: {commandCode} \"{commandString}\"");
                    return false;
                }

                response = response.TrimStart(ResponseStartChar);

                switch (commandCode)
                {
                    case Commands.ReadEmission:
                        returnResponse?.Invoke(response);
                        userLabel = default;
                        returnResponse = default;
                        break;
                    case Commands.ReadPressureDump:
                        string[] pressures = response.Split(',');
                        if (pressures.Length != Gauges.Count)
                            Log.Record($"Gauges:Pressures mismatch in XGS-600.cs ({Gauges.Count}:{pressures.Length})");
                        else
                        {
                            int i = 0;
                            Gauges?.ForEach(gauge =>
                            {
                                try { gauge?.Update(double.Parse(pressures[i])); }
                                catch { }
                                i++;
                            });
                        }
                        break;
                    case Commands.ReadPressureUnits:
                        Units = (PressureUnits)int.Parse(response);
                        break;
                    default:
                        // No response data
                        break;
                }
                return true;
            }
            catch (Exception e)
            {
                if (LogEverything) Log.Record(e.ToString());
                return false;
            }
        }

        #endregion

   }
}
