using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace HACS.Core
{
    public interface IHacsComponent : INamedObject
    {
		bool Connected { get; }
        bool Initialized { get; }
        // These property names are likely to have a more valuable, different natural meaning
        // in the context of the derived class. How to handle this?
        //bool Started { get; }     
        //bool Stopped { get; }
        Action StateChanged { get; set; }
    }

	[JsonObject(MemberSerialization.OptIn)]
    public class HacsComponent : IHacsComponent
	{
        public static readonly List<HacsComponent> List = new List<HacsComponent>();
		public static HacsComponent Find(string name) { return List.Find(x => x?.Name == name); }

		[JsonProperty(Order = -99), JsonRequired]
        [XmlAttribute] public virtual string Name { get; set; }
        public virtual bool Connected => Hacs.Connected;
        public virtual bool Initialized => Hacs.Initialized;
        //public virtual bool Started => Hacs.Started;
        //public virtual bool Stopped => Hacs.Stopped;

        public static Action OnPreConnect;
		public static Action OnConnect;
		public static Action OnPostConnect;

		public static Action OnPreInitialize;
		public static Action OnInitialize;
		public static Action OnPostInitialize;

		public static Action OnPreStart;
		public static Action OnStart;
		public static Action OnPostStart;

		public static Action OnPreStop;
		public static Action OnStop;
		public static Action OnPostStop;

		[XmlIgnore] public Action StateChanged { get; set; }

		public HacsComponent() { List.Add(this); }

        public override string ToString() { return $"{Name}"; }
    }

	[JsonConverter(typeof(StringINamedObjectConverter))]
    public class HacsComponent<T> : INamedObject where T : IHacsComponent
    {
        [XmlAttribute]
        public string Name
        {
            get => Component?.Name ?? _Name;
            set
            {
                _Name = value;
                if (Hacs.Connected)
                    connect();
            }
        }
        string _Name;

        public T Component
        {
            get
            {
                if (_Component == null)
                    connect();
                return _Component;
            }
        }
        T _Component;

        private void connect()
        {
            _Component = HacsComponent.List.OfType<T>().Where(x => x.Name == _Name).First();
        }
    }
}
