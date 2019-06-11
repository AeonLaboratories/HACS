using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Xml.Serialization;

namespace HACS.Core
{
	public abstract class HacsBase : HacsComponent
	{
		[XmlIgnore] public virtual bool Started { get; protected set; } = false;

		protected JsonSerializer JsonSerializer { get; set; }
		protected XmlSerializer XmlSerializer { get; set; }

		[XmlIgnore]
		public string SettingsFilename
		{
			get => settingsFilename;
			set
			{
				if (!String.IsNullOrWhiteSpace(value))
				{
					settingsFilename = value;
					int period = settingsFilename.LastIndexOf('.');
					if (period < 0) period = settingsFilename.Length;
					backupSettingsFilename = settingsFilename.Insert(period, ".backup");
				}
			}
		}
		string settingsFilename = "settings.json";
		string backupSettingsFilename = "settings.backup.json";

		public HacsBase()
		{
			JsonSerializer = new JsonSerializer()
			{
				Converters = { new StringEnumConverter() },
				//DefaultValueHandling = DefaultValueHandling.Ignore, TODO: Enable this
				Formatting = Formatting.Indented,
				NullValueHandling = NullValueHandling.Ignore,
				TypeNameHandling = TypeNameHandling.Auto
			};

			XmlSerializer = new XmlSerializer(this.GetType());
		}

		private void saveJson(string filename)
		{
			using (var stream = File.CreateText(filename))
				JsonSerializer?.Serialize(stream, this);
		}

		private void saveXml(string filename)
		{
			using (var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
				XmlSerializer.Serialize(stream, this);
		}

		protected virtual void SaveSettings() { SaveSettings(SettingsFilename); }

		protected virtual void SaveSettings(string filename)
		{
			if (!Started) return;

			if (String.IsNullOrWhiteSpace(filename))
				throw new NullReferenceException("Settings filename can not be null or whitespace.");

			try
			{
				if (filename == SettingsFilename)
				{
					File.Delete(backupSettingsFilename);
					File.Move(settingsFilename, backupSettingsFilename);
				}

				if (filename.EndsWith(".xml"))
					saveXml(filename);
				else
					saveJson(filename);
			}
			catch (Exception e)
			{
				throw (e);
			}
		}
	}
}
