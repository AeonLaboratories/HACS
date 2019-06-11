using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
    // wrapper for Utility.LogFile class so logs can be operated as Hacs.Component
	public class HacsLog : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<HacsLog> List = new List<HacsLog>();
		public static new HacsLog Find(string name) { return List.Find(x => x?.Name == name); }

		// Open logs early (in Connect() instead of in Start()) so they are
		// available to record anything interesting during startup
		protected void Connect()
        {
			Log = new LogFile(FileName)
            {
                LogFolder = LogFolder,
                ArchiveFolder = ArchiveFolder,
            };
            if (!string.IsNullOrEmpty(_Header))
                Log.Header = _Header;
            if (!string.IsNullOrEmpty(_TimeStampFormat))
                Log.TimeStampFormat = _TimeStampFormat;
            Log.ArchiveDaily = _ArchiveDaily;
        }

		// Close logs late so they can record shutdown activity
		protected void PostStop()
        {
            Log?.Close();
        }

		public HacsLog()
		{
			List.Add(this);
			OnConnect += Connect;
			OnPostStop += PostStop;
		}

		#endregion Component Implementation

		public static void UpdateAll() { List?.ForEach(x => x?.Update?.Invoke()); }
		[XmlIgnore] public Action Update;       // to be implemented by caller

		public static string LogFolder = @".\";
        public static string ArchiveFolder = @"archive\";


        LogFile Log;

		[JsonProperty]
        public string FileName
        {
            get { return Log?.FileName ?? _FileName; }
            set { _FileName = value; }
        }
        string _FileName = "Log.txt";

		[JsonProperty]
        public string Header
        {
            get { return Log?.Header ?? _Header; }
            set { _Header = value; if (Log != null) Log.Header = value; }
        }
        string _Header = "";

		[JsonProperty]
        public string TimeStampFormat
        {
            get { return Log?.TimeStampFormat ?? _TimeStampFormat; }
            set { _TimeStampFormat = value; if (Log != null) Log.TimeStampFormat = value; }
        }
        string _TimeStampFormat = "";

		[JsonProperty]
        public bool ArchiveDaily
        {
            get { return Log?.ArchiveDaily ?? _ArchiveDaily; }
            set { _ArchiveDaily = value; if (Log != null) Log.ArchiveDaily = value; }
        }
        bool _ArchiveDaily;


        public long ElapsedMilliseconds => Log?.ElapsedMilliseconds ?? 0;

        public string TimeStamp()
        { return Log?.TimeStamp() ?? ""; }

        public HacsLog(string filename, bool archiveDaily = true) : this()
        {
            FileName = filename;
            ArchiveDaily = archiveDaily;
        }


        /// <summary>
        /// Write the entry into the log file with no newline added.
        /// </summary>
        /// <param name="entry"></param>
        public void Write(string entry = "") { Log?.Write(entry); }

        /// <summary>
        /// Writes a one-line entry into the logfile (adds "\r\n").
        /// </summary>
        /// <param name="entry"></param>
        public void WriteLine(string entry = "") { Log?.WriteLine(entry); }

        /// <summary>
        /// Writes a one-line timestamp and entry to the log file
        /// </summary>
        /// <param name="entry">Text to appear after the time stamp</param>
        public void Record(string entry = "") { Log?.Record(entry); }

        /// <summary>
        /// Writes a one-line timestamp and entry to the log file if
        /// the entry is not the same as the last entry.
        /// </summary>
        /// <param name="entry">Text to appear after the time stamp</param>
        public void LogParsimoniously(string entry = "") { Log?.LogParsimoniously(entry); }

        public override string ToString()
        {
            return $"{Name}: \"{FileName}\"";
        }
    }
}
