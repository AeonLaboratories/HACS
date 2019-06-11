using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Core
{
	/// <summary>
	/// Coordinates the startup and shutdown of a user interface and a hacs implementation
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class HACSBridge<T> where T : HacsBase
	{
		public IHacsUI _UserInterface;
		public IHacsUI UserInterface
		{
			get { return _UserInterface; }
			set { _UserInterface = value; Start(); }
		}
		public T HACSImplementation;
		public bool Initialized { get; set; }

		void Start()
		{
			LoadSettings("settings.xml");
			//LoadSettings("settings.json");
			if (HACSImplementation == null)
			{
				UserInterface.Close();
				return;
			}
			Hacs.Connect();
		}

		private void loadJson(string settingsFile)
		{
			using (var reader = new StreamReader(settingsFile))
				HACSImplementation = (T)(new JsonSerializer()
				{
					Converters = { new StringEnumConverter() },
					//DefaultValueHandling = DefaultValueHandling.Ignore, TODO: Enable this
					TypeNameHandling = TypeNameHandling.Auto
				}).Deserialize(reader, typeof(T));
		}

		private void loadXml(string settingsFile)
		{
			using (var reader = new StreamReader(settingsFile))
				HACSImplementation = (T)(new XmlSerializer(typeof(T))).Deserialize(reader);
		}

		protected virtual void LoadSettings(string settingsFile)
		{
			try
			{
				if (settingsFile.EndsWith(".xml"))
					loadXml(settingsFile);
				else
					loadJson(settingsFile);
			}
			catch (Exception e)
			{
				Notice.Send(e.ToString());
				HACSImplementation = default(T);
				return;
			}
			HACSImplementation.SettingsFilename = settingsFile;
		}

		public void UILoaded()
		{
			Hacs.Initialize();
		}

		public void UIShown()
		{
			Hacs.Start();
		}

		public void UIClosing()
		{
			Hacs.Stop();
		}
	}
}
