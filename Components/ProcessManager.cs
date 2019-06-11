using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using System.Threading;
using Newtonsoft.Json;

namespace HACS.Components
{
    [XmlInclude(typeof(CEGS))]
    [XmlInclude(typeof(ExtractionLine))]
    public class ProcessManager : HacsBase
    {
		#region Component Implementation

		public static readonly new List<ProcessManager> List = new List<ProcessManager>();
		public static new ProcessManager Find(string name) { return List.Find(x => x?.Name == name); }

		public ProcessManager()
		{
			List.Add(this);
			BuildProcessDictionary();
		}

		#endregion Component Implementation
		
		[XmlType(AnonymousType = true)]
        public enum ProcessStates { Ready, Busy, Finished }

        [XmlIgnore] public Action<ProcessManager> ShowProcessSequenceEditor;
        [XmlIgnore] public HacsLog EventLog;
		[JsonProperty(Order = -98)] public AlertManager AlertManager { get; set; }

		#region process manager
		// The derived class should call it's BuildProcessDictionary override in its own Connect() method.
		// The derived class's BuildProcessDictionary call this base class after populating the dictionary.
		[XmlIgnore] public Dictionary<string, ThreadStart> ProcessDictionary = new Dictionary<string, ThreadStart>();
        [XmlIgnore] public List<string> ProcessNames;
        protected virtual void BuildProcessDictionary()
        {
            if (ProcessDictionary.Any())
            {
                var list = new List<string>();
                foreach (var kvpair in ProcessDictionary)
                    list.Add(kvpair.Key);
                ProcessNames = list;
            }
        }

		[JsonProperty(Order = -98)] public List<ProcessSequence> ProcessSequences { get; set; }

        [XmlIgnore] public ProcessStates ProcessState = ProcessStates.Ready;
        [XmlIgnore] public Thread ManagerThread = null;
        [XmlIgnore] public Thread ProcessThread = null;
        [XmlIgnore] public Stopwatch ProcessTime { get; set; } = new Stopwatch();
        [XmlIgnore] public StepTracker ProcessStep { get; set; } = new StepTracker("ProcessStep");
        [XmlIgnore] public StepTracker ProcessSubStep { get; set; } = new StepTracker("ProcessSubStep");
        [XmlIgnore] public virtual string ProcessToRun { get; set; }
        public enum ProcessTypes { Simple, Sequence }
        [XmlIgnore] public ProcessTypes ProcessType;
        [XmlIgnore] public bool RunCompleted = false;

		public bool Busy => ManagerThread?.IsAlive ?? false;

		public virtual void RunProcess(string processToRun)
        {
            if (Busy) return;         // silently fail, for now
				//throw new Exception($"Can't start [{processToRun}]. [{ProcessToRun}] is running.");

			ProcessToRun = processToRun;

			ProcessState = ProcessStates.Ready;
            lock(ProcessTime)
                RunCompleted = false;
            ManagerThread = new Thread(ManageProcess)
            {
                Name = $"{Name} ProcessManager",
                IsBackground = true
            };
            ManagerThread.Start();
        }

        public virtual void AbortRunningProcess()
        {
			if (ProcessThread?.IsAlive ?? false)
                ProcessThread.Abort();
        }
		
        // A Process runs in its own thread.
        // Only one Process can be executing at a time.
        protected void ManageProcess()
        {
            try
            {
                while (true)
                {
                    var priorState = ProcessState;
                    switch (ProcessState)
                    {
                        case ProcessStates.Ready:
                            if (!string.IsNullOrEmpty(ProcessToRun))
                            {
                                ProcessState = ProcessStates.Busy;
                                if (ProcessDictionary.TryGetValue(ProcessToRun, out ThreadStart process))
                                {
                                    ProcessType = ProcessTypes.Simple;
                                }
                                else
                                {
                                    ProcessType = ProcessTypes.Sequence;
                                    process = RunProcessSequence;
                                }

                                ProcessThread = new Thread(() => RunProcess(process))
                                {
                                    IsBackground = true,
                                    Name = $"{Name} RunProcess"
                                };
                                ProcessTime.Restart();
                                EventLog?.Record("Process starting: " + ProcessToRun);
                                ProcessThread.Start();
                            }
                            break;
                        case ProcessStates.Busy:
                            if (!ProcessThread.IsAlive)
                            {
                                ProcessState = ProcessStates.Finished;
                            }
                            break;
                        case ProcessStates.Finished:
                            ProcessStep.Clear();
                            ProcessSubStep.Clear();
                            ProcessTime.Stop();

							ProcessEnded();

							ProcessThread = null;
							ProcessToRun = null;
							ProcessState = ProcessStates.Ready;

							break;
                        default:
                            break;
                    }
                    if (priorState == ProcessStates.Finished)
                        break;
                    if (priorState == ProcessState)
                        Thread.Sleep(200);
                }
            }
            catch { }
        }

        void RunProcess(ThreadStart process)
        {
            process();
            lock (ProcessTime)
                RunCompleted = true;            // if the process is aborted, RunCompleted will not be set true;
        }

		protected virtual void ProcessEnded()
		{
			EventLog?.Record($"Process {(RunCompleted ? "completed" : "aborted")}: {ProcessToRun}");
		}

		#region ProcessSequences

		// The derived class can implement a Combust() process
		// (if it doesn't those steps won't do anything)
		protected virtual void Combust(int temperature, int minutes, bool admitO2, bool openLine, bool waitForSetpoint) { }

        void RunProcessSequence()
        {
            ProcessSequence ps = ProcessSequences.Find(x => x?.Name == ProcessToRun);

            if (ps == null)
                throw new Exception("No such Process Sequence: \"" + ProcessToRun + "\"");

            foreach (ProcessSequenceStep step in ps.Steps)
            {
                ProcessStep.Start(step.Name);
                if (step is CombustionStep cs)
                    Combust(cs.Temperature, cs.Minutes, cs.AdmitO2, cs.OpenLine, cs.WaitForSetpoint);
                else if (step is WaitMinutesStep wms)
                    WaitMinutes(wms.Minutes);       // this is provided by ProcessManager
                else
                    ProcessDictionary[step.Name]();
                ProcessStep.End();
            }
        }


        #region parameterized processes
        // these require added functionality in the ProcessSequenceStep class

        public void WaitMinutes(int minutes)
        { WaitMilliseconds("Wait " + min_string(minutes) + ".", minutes * 60000); }

        #endregion parameterized processes

        #endregion ProcessSequences

        #endregion process manager


        #region fundamental processes
        protected void Wait(int milliseconds) { Thread.Sleep(milliseconds); }
        protected void Wait() { Wait(35); }

        protected void WaitMilliseconds(string description, int milliseconds)
        {
            ProcessSubStep.Start(description);
            int elapsed = (int)ProcessSubStep.Elapsed.TotalMilliseconds;
            while (milliseconds > elapsed)
            {
                Wait((int)Math.Min(milliseconds - elapsed, 35));
                elapsed = (int)ProcessSubStep.Elapsed.TotalMilliseconds;
            }
            ProcessSubStep.End();
        }

        protected void WaitRemaining(int minutes)
        {
            int milliseconds = minutes * 60000 - (int)ProcessStep.Elapsed.TotalMilliseconds;
            if (milliseconds > 0)
                WaitMilliseconds("Wait for remainder of " + min_string(minutes) + ".", milliseconds);
        }

        #region MOVE THIS TO UTILITY
        // considers y always a consonant
        protected bool IsVowel(char c) { return "aeiou".Contains(c); }

        // Tries to guess the plural of a singular word.
        // Fails for words like deer, mouse, and ox.
        protected string Plural(string singular)
        {
            if (string.IsNullOrEmpty(singular)) return string.Empty;
            singular.TrimEnd();
            if (string.IsNullOrEmpty(singular)) return string.Empty;

            int slen = singular.Length;
            char ultimate = singular[slen - 1];
            if (slen == 1)
            {
                if (char.IsUpper(ultimate)) return singular + "s";
                return singular + "'s";
            }
            ultimate = char.ToLower(ultimate);
            char penultimate = char.ToLower(singular[slen - 2]);

            if (ultimate == 'y')
            {
                if (IsVowel(penultimate)) return singular + "s";
                return singular.Substring(0, slen - 1) + "ies";
            }
            if (ultimate == 'f')
                return singular.Substring(0, slen - 1) + "ves";
            if (penultimate == 'f' && ultimate == 'e')
                return singular.Substring(0, slen - 2) + "ves";
            if ((penultimate == 'c' && ultimate == 'h') ||
                (penultimate == 's' && ultimate == 'h') ||
                (penultimate == 's' && ultimate == 's') ||
                (ultimate == 'x') ||
                (ultimate == 'o' && !IsVowel(penultimate)))
                return singular + "es";
            return singular + "s";
        }

        /// <summary>
        /// Returns a string like "5.2 minutes" or "1 second".
        /// </summary>
        /// <param name="howmany"></param>
        /// <param name="singularUnit"></param>
        /// <returns></returns>
        protected string ToUnitsString(double howmany, string singularUnit)
        { return howmany.ToString() + " " + ((howmany == 1) ? singularUnit : Plural(singularUnit)); }

        protected string min_string(int minutes)
        { return ToUnitsString(minutes, "minute"); }

        #endregion MOVE THIS TO UTILITY
        #endregion fundamental processes

    }
}