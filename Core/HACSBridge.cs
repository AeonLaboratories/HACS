using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Resources;
using Utilities;

namespace HACS.Core
{
    public abstract class HacsBridge
	{
		public static ResourceManager Resources { get; set; }
		public Action CloseUI;
		public abstract HacsBase GetHacs();
		public abstract void Start();
		public abstract void UILoaded();
		public abstract void UIShown();
		public abstract void UIClosing();
	}

	/// <summary>
	/// Coordinates the startup and shutdown of a user interface and a hacs implementation
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class HacsBridge<T> : HacsBridge where T : HacsBase, new()    // require parameterless constructor, needed for bootstrapping a new implementation
	{
		protected T HacsImplementation { get; set; }

		public override HacsBase GetHacs() => HacsImplementation;

		public bool Initialized { get; protected set; }

		protected JsonSerializer JsonSerializer { get; set; }

		public string SettingsFilename
		{
			get => settingsFilename;
			set
			{
				if (!value.IsBlank())
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

		bool BootstrapNewSystem = false;
		public HacsBridge() : this(false) { }

		public HacsBridge(bool bootstrapNewSystem)
		{
			BootstrapNewSystem = bootstrapNewSystem;

			if (bootstrapNewSystem)
			{
				JsonSerializer = new JsonSerializer()
				{
//					Converters = { new StringEnumConverter(), HideNameInDictionaryConverter.Default },
					Converters = { new StringEnumConverter() },
					DefaultValueHandling = DefaultValueHandling.Populate,
					Formatting = Formatting.Indented,
					NullValueHandling = NullValueHandling.Include,
					TypeNameHandling = TypeNameHandling.Auto
				};
			}
			else
			{
				JsonSerializer = new JsonSerializer()
				{
					//Converters = { new StringEnumConverter(), HideNameInDictionaryConverter.Default },
					Converters = { new StringEnumConverter() },
					//DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
					DefaultValueHandling = DefaultValueHandling.Populate,
					Formatting = Formatting.Indented,
					NullValueHandling = NullValueHandling.Include,
					//NullValueHandling = NullValueHandling.Ignore,
					TypeNameHandling = TypeNameHandling.Auto
				};
			}
		}

		public override void Start()
		{
			if (BootstrapNewSystem)
			{
				HacsImplementation = new T();
				SaveSettings("bootstrap.json");
				CloseUI();
				return;
			}
			else
			{
				LoadSettings(settingsFilename);
				if (HacsImplementation == null)
				{
					CloseUI();
					return;
				}
			}
			Hacs.Connect();
		}

		private void loadJson(string settingsFile)
		{
			using (var reader = new StreamReader(settingsFile))
				HacsImplementation = (T)JsonSerializer.Deserialize(reader, typeof(T));
		}

		protected virtual void LoadSettings(string settingsFile)
		{
			try
			{
				loadJson(settingsFile);
			}
			catch (Exception e)
			{
				Notice.Send(e.ToString());
				HacsImplementation = default(T);
				return;
			}
			HacsImplementation.SaveSettings = SaveSettings;
			HacsImplementation.SaveSettingsToFile = SaveSettings;
		}

		private void saveJson(string filename)
		{
			using (var stream = File.CreateText(filename))
				JsonSerializer?.Serialize(stream, HacsImplementation);
		}

		protected virtual void SaveSettings() { SaveSettings(SettingsFilename); }

		protected virtual void SaveSettings(string filename)
		{
			if (filename.IsBlank())
				throw new NullReferenceException("Settings filename can not be null or whitespace.");

			try
			{
				if (filename == SettingsFilename)
				{
					File.Delete(backupSettingsFilename);
					File.Move(settingsFilename, backupSettingsFilename);
				}

				saveJson(filename);
			}
			catch
			{
				// Typically a user has tried to reload settings.json just before a save.
				// Do we need to do anything here? The exception being caught is important
				// so the save loop doesn't break.
			}
        }

		public override void UILoaded()
		{
			Hacs.Initialize();
		}

		public override void UIShown()
		{
			Hacs.Start();
		}

		public override void UIClosing()
		{
			Hacs.Stop();
		}
	}
}
