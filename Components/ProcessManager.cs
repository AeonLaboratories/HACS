using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using Utilities;
using static Utilities.Utility;

namespace HACS.Components
{
	public class ProcessManager : HacsBase, IProcessManager
    {
		#region HacsComponent

		public ProcessManager()
		{
            StepTracker.Default = ProcessSubStep;
			BuildProcessDictionary();
		}

		#endregion HacsComponent
		
        public enum ProcessStateCode { Ready, Busy, Finished }

        public virtual HacsLog EventLog
        { 
            get => Hacs.EventLog;
            set => Hacs.EventLog = value;
        }

        [JsonProperty(Order = -98)] 
        public AlertManager AlertManager
        {
            get => alertManager;
            set { Components.Alert.DefaultAlertManager = alertManager = value; }
        }
        AlertManager alertManager;

        /// <summary>
        /// Send a message to the remote operator.
        /// </summary>
        public virtual void Alert(string subject, string message) =>
            AlertManager.Send(subject, message);

        /// <summary>
        /// Dispatch a message to the remote operator and to the local user interface.
        /// The process is not paused.
        /// </summary>
        public virtual void Announce(string subject, string message) =>
            AlertManager.Announce(subject, message);

        /// <summary>
        /// Send the message to the remote operator, then pause, giving
        /// the local operator the option to continue.
        /// </summary>
        public virtual void Pause(string subject, string message) =>
            AlertManager.Pause(subject, message);

        /// <summary>
        /// Make an entry in the EventLog, pause and give the local operator 
        /// the option to continue. The notice is transmitted as a Warning.
        /// </summary>
        public virtual void Warn(string subject, string message) =>
            AlertManager.Warn(subject, message);

        #region process manager

        /// <summary>
        /// ProcessDictionary list group separators
        /// </summary>
        public List<int> Separators = new List<int>();

        public Dictionary<string, ThreadStart> ProcessDictionary { get; protected set; } = new Dictionary<string, ThreadStart>();
        public List<string> ProcessNames { get; protected set; }

        // The derived class should call its BuildProcessDictionary override in its own Connect() method.
        // The derived class' BuildProcessDictionary override should call base.BuildProcessDictionary() after populating the dictionary.
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

		[JsonProperty(Order = -98)]
		public Dictionary<string, ProcessSequence> ProcessSequences { get; set; } = new Dictionary<string, ProcessSequence>();

		public ProcessStateCode ProcessState
        {
            get => processState;
            protected set => Ensure(ref processState, value);
        }
        ProcessStateCode processState = ProcessStateCode.Ready;
        protected Thread ManagerThread { get; set; } = null;
        protected Thread ProcessThread { get; set; } = null;
        protected Stopwatch ProcessTimer { get; set; } = new Stopwatch();
        public TimeSpan ProcessTime => ProcessTimer.Elapsed;
        public StepTracker ProcessStep { get; protected set; } = new StepTracker("ProcessStep");
        public StepTracker ProcessSubStep { get; protected set; } = new StepTracker("ProcessSubStep");

        public virtual string ProcessToRun
        {
            get => processToRun;
            set => Ensure(ref processToRun, value);
        }
        string processToRun;
        public enum ProcessTypeCode { Simple, Sequence }
        public ProcessTypeCode ProcessType
        {
            get => processType;
            protected set => Ensure(ref processType, value);
        }
        ProcessTypeCode processType;
        public bool RunCompleted
        {
            get => runCompleted;
            protected set => Ensure(ref runCompleted, value);
        }
        bool runCompleted = false;

		public virtual bool Busy => ManagerThread?.IsAlive ?? false;
        public virtual bool ProcessSequenceIsRunning =>
            ProcessType == ProcessTypeCode.Sequence && !RunCompleted;

        public virtual void RunProcess(string processToRun)
        {
            if (ManagerThread?.IsAlive ?? false) return;         // silently fail, for now
				//throw new Exception($"Can't start [{processToRun}]. [{ProcessToRun}] is running.");

			ProcessToRun = processToRun;

			ProcessState = ProcessStateCode.Ready;
            lock(ProcessTimer)
                RunCompleted = false;
            ManagerThread = new Thread(ManageProcess) { Name = $"{Name} ProcessManager", IsBackground = true };
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
                        case ProcessStateCode.Ready:
                            if (!string.IsNullOrEmpty(ProcessToRun))
                            {
                                ProcessState = ProcessStateCode.Busy;
                                if (ProcessDictionary.TryGetValue(ProcessToRun, out ThreadStart process))
                                {
                                    ProcessType = ProcessTypeCode.Simple;
                                }
                                else
                                {
                                    ProcessType = ProcessTypeCode.Sequence;
                                    process = RunProcessSequence;
                                }

                                ProcessThread = new Thread(() => RunProcess(process))
                                {
                                    IsBackground = true,
                                    Name = $"{Name} RunProcess"
                                };
                                ProcessTimer.Restart();
                                EventLog?.Record("Process starting: " + ProcessToRun);
                                ProcessThread.Start();
                            }
                            break;
                        case ProcessStateCode.Busy:
                            if (!ProcessThread.IsAlive)
                            {
                                ProcessState = ProcessStateCode.Finished;
                            }
                            break;
                        case ProcessStateCode.Finished:
                            ProcessStep.Clear();
                            ProcessSubStep.Clear();
                            ProcessTimer.Stop();

							ProcessEnded();

							ProcessThread = null;
							ProcessToRun = null;
							ProcessState = ProcessStateCode.Ready;

							break;
                        default:
                            break;
                    }
                    if (priorState == ProcessStateCode.Finished)
                        break;
                    if (priorState == ProcessState)
                        Thread.Sleep(200);
                }
            }
            catch { }
        }

        void RunProcess(ThreadStart process)
        {
            process?.Invoke();
            lock (ProcessTimer)
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
			//TODO how was this done elsewhere (added Find(s) method to NamedObject class)
            ProcessSequence ps = ProcessSequences.Values.ToList().Find(x => x?.Name == ProcessToRun);

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
        { WaitMilliseconds("Wait " + MinutesString(minutes) + ".", minutes * 60000); }

        #endregion parameterized processes

        #endregion ProcessSequences

        #endregion process manager

        #region fundamental processes
        protected void Wait(int milliseconds) { Thread.Sleep(milliseconds); }
        /// <summary>
        /// Wait 35 milliseconds
        /// </summary>
        protected void Wait() { Wait(35); }

        protected void WaitSeconds(int seconds)
        { WaitMilliseconds("Wait " + SecondsString(seconds) + ".", seconds * 1000); }

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
                WaitMilliseconds("Wait for remainder of " + MinutesString(minutes) + ".", milliseconds);
        }

        #endregion fundamental processes
    }
}