using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace HACS.Core
{
	public class HacsLog : HacsComponent, IHacsLog
	{
		#region static
		public static List<HacsLog> List
		{
			get
			{
				if (list == null) list = CachedList<HacsLog>();
				return list;
			}
		}
		static List<HacsLog> list;
		public static void UpdateAll() { List?.ForEach(x => x?.Update?.Invoke()); }

		public static string LogFolder
		{
			get => logFolder;
			set { if (ValidateFolder(value)) logFolder = value; }
		}
		static string logFolder = @".\log\";

		public static string ArchiveFolder
		{
			get => archiveFolder;
			set { if (ValidateFolder(value)) archiveFolder = value; }
		}
		static string archiveFolder = @".\log\archive\";

		public static bool ValidateFolder(string path)
		{
			try
			{
				var fullPath = Path.GetFullPath(path);
				if (File.Exists(fullPath)) return false;
				Directory.CreateDirectory(fullPath);
				return true;
			}
			catch { return false; }
		}

		#endregion static

		public Action Update
		{
			get => update;
			set => Ensure(ref update, value);
		}
		Action update;

		LogFile Log;
		public void Close() => Log?.Close();
		
		[JsonProperty]
		public string FileName
		{
			get => Log?.FileName ?? fileName;
			set
			{
				if (Ensure(ref fileName, value))
				{
                    if (Log == null)
                    {
                        Log = new LogFile(fileName)
                        {
                            LogFolder = LogFolder,
                            ArchiveFolder = ArchiveFolder
                        };
                        if (!header.IsBlank())
                            Log.Header = header;
                        if (!timeStampFormat.IsBlank())
                            Log.TimeStampFormat = timeStampFormat;
                        Log.ArchiveDaily = archiveDaily;
                    }
                    else
                        Log.FileName = value;
                }
			}
		}
		string fileName = "Log.txt";

		[JsonProperty]
		public virtual string Header
		{
			get => Log?.Header ?? header;
			set
			{
				if (Ensure(ref header, value) && Log != null)
					Log.Header = value;
			}
		}
		string header = "";

		[JsonProperty]
		public string TimeStampFormat
		{
			get => Log?.TimeStampFormat ?? timeStampFormat;
			set
			{
				if (Ensure(ref timeStampFormat, value) && Log != null)
					Log.TimeStampFormat = value;
			}
		}
		string timeStampFormat = "";

		[JsonProperty]
		public bool ArchiveDaily
		{
			get => Log?.ArchiveDaily ?? archiveDaily;
			set
			{
				if (Ensure(ref archiveDaily, value) && Log != null)
					Log.ArchiveDaily = value;
			}
		}
		bool archiveDaily;

		public long ElapsedMilliseconds => Log?.ElapsedMilliseconds ?? 0;

		public string TimeStamp()
		{ return Log?.TimeStamp() ?? ""; }

		public HacsLog(string filename, bool archiveDaily = true)
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
