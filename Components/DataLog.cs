using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace HACS.Components
{
	public class DataLog : HacsLog, IDataLog
	{
        #region static

        public static bool DefaultChanged(Column col) =>
			col.Resolution < 0 ? 
				false :
			col.PriorValue is double p && col.Source?.Value is double v ?
				Math.Abs(p - col.Source.Value) >= col.Resolution :
			true;

		[JsonObject(MemberSerialization.OptIn)]
		public class Column
		{
			/// <summary>
			/// The Column name; normally the name of a NamedObject that implments IValue
			/// </summary>
			[JsonProperty]
			public string Name;

			/// <summary>
			/// When the magnitude of the difference between the current value and 
			/// the prior recorded value exceeds Resolution, a new log entry is recorded. 
			/// If Resolution is less than zero, this column will never trigger a log entry.
			/// </summary>
			[JsonProperty]
			public double Resolution;

			/// <summary>
			/// The Column data is formatted according to this string.
			/// </summary>
			[JsonProperty]
			public string Format;

			/// <summary>
			/// The IValue source for the Column data
			/// </summary>
			public IValue Source;

			/// <summary>
			/// The most recent data value recorded for this Column
			/// </summary>
			public double? PriorValue;
		}

		#endregion static

		#region HacsComponent
		[HacsConnect]
		protected virtual void Connect()
		{
			SetSources();
			SetHeader();
		}
		#endregion HacsComponent

		[JsonIgnore]
		public override string Header { get => base.Header; set => base.Header = value; }

		[JsonProperty]
		public virtual ObservableList<Column> Columns
		{
			get => columns;
			set => Ensure(ref columns, value, OnColumnsChanged);
		}
		ObservableList<Column> columns;
		protected virtual void OnColumnsChanged(object sender = null, PropertyChangedEventArgs e = null)
		{
			if (sender == Columns)
			{
				if (Connected)
				{
					SetSources();
					SetHeader();
				}
			}
		}

		void SetSources()
		{
			foreach (var col in Columns)
				col.Source = (IValue)FirstOrDefault<NamedObject>(x => x.Name == col.Name && x is IValue);
		}

		void SetHeader()
		{
			if (columns != null && columns.Count > 0)
			{
				var sb = new StringBuilder(columns[0].Name);
				for (int i = 1; i < columns.Count; ++i)
					sb.Append("\t" + columns[i].Name);
				Header = sb.ToString();
			}
		}

		public void AddNewValue(string name, double resolution, string format, Func<double> getValue)
		{
			if (!(Columns.Find(x => x.Name == name) is Column c))
				c = new Column() { Name = name };
			c.Resolution = resolution;
			c.Format = format;
			c.Source = new NamedValue(name, getValue);
		}


		public DataLog(string fileName, bool archiveDaily = true) : base(fileName, archiveDaily)
		{
			Update = Report;
		}

		public virtual Func<Column, bool> Changed { get; set; } = DefaultChanged;

		bool AnyChanged() => Columns.Any(c => Changed(c));

		double[] currentValues;
		string value(int index)
		{
			var col = Columns[index];
			var v = col?.Source?.Value ?? Double.NaN;
			currentValues[index] = v;
			return v.ToString(col.Format);
		}

		[JsonProperty, DefaultValue(30000)]
		public virtual long ChangeTimeoutMilliseconds { get; set; } = 30000;
		[JsonProperty, DefaultValue(false)]
		public virtual bool OnlyLogWhenChanged { get; set; } = false;

		StringBuilder entryBuilder = new StringBuilder();
		string GenerateEntry()
		{
			entryBuilder.Clear();
			currentValues = new double[Columns.Count];
			entryBuilder.Append(value(0));
			for (int i = 1; i < Columns.Count; ++i)
				entryBuilder.Append("\t" + value(i));
			return entryBuilder.ToString();
		}

		string skippedTimeStamp;
		string skippedEntry = "";
		void Skip(string entry)
		{
			skippedTimeStamp = TimeStamp();
			skippedEntry = entry;
		}

		void WriteSkippedEntry()
        {
			WriteLine(skippedTimeStamp + skippedEntry);
			skippedEntry = "";
		}

		void WriteLog(string entry)
		{
			if (OnlyLogWhenChanged)
				LogParsimoniously(entry);
			else
			{
				if (!skippedEntry.IsBlank() && skippedEntry != entry)
					WriteSkippedEntry();
				Record(entry);
			}
			for (int i = 0; i < Columns.Count; ++i)
				Columns[i].PriorValue = currentValues[i];
		}

		protected virtual void Report()
		{
			if (Columns == null || Columns.Count == 0) return;

			var entry = GenerateEntry();
			if (AnyChanged() || ElapsedMilliseconds >= ChangeTimeoutMilliseconds)
				WriteLog(entry);
			else
				Skip(entry);
		}
	}
}
