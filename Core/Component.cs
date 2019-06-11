using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using HACS.Components;

namespace HACS.Core
{
    // TODO: consider renaming this class "HacsComponent"
    //  and the methods HacsComponentStart(), etc.
    // The issues are
    //    1. Component is used elsewhere in the Windows system.
    //    2. Simply-named methods like Start() and Close() often have other, more natural meanings
    //       within the derived Component's own functionality.
	public class Component : FindableObject
	{
		public static new List<Component> List { get; set; } = new List<Component>();
		public static new Component Find(string name) { return List?.Find(x => x?.Name == name); }

		// If there are[XmlInclude]'s at the top of a Component-derived class
		//    the class must check for derived types in ConnectAll() and InitializeAll()
		//    but NOT in Connect() nor Initialize(), in case derived version calls base version
		// Every Component-derived class must "new" ConnectAll() if it overrides Connect()
		//    likewise with Initialize()
		public static void ConnectAll() { List?.ForEach(x => x?.Connect()); }
        public static void InitializeAll() { List?.ForEach(x => x?.Initialize()); }
        public static void StartAll() { List?.ForEach(x => x?.ComponentStart()); }
        public static void UpdateAll() { List?.ForEach(x => x?.ComponentUpdate()); }
		public static void StopAll(List<Component> ExceptThese = null)
		{
			if (ExceptThese == null)
				List?.ForEach(x => x?.ComponentStop());
			else
				List?.ForEach(x => 
				{
					if (ExceptThese.Find(e => e == x) == null)
						x.ComponentStop();
				});
		}

		[XmlIgnore] public Action StateChanged;

		public Component()
		{
			List?.Add(this);
		}



        // TODO: consider renaming all below to "Initialized", "Component.Connect()", etc ???

        [XmlIgnore] public virtual bool Initialized { get; protected set; } = false;
        //[XmlIgnore] public virtual bool ComponentStarted { get; protected set; } = false;
        //[XmlIgnore] public virtual bool ComponentStopping { get; protected set; } = false;
        //[XmlIgnore] public virtual bool ComponentStopped { get; protected set; } = false;

        /// <summary>
        /// Connect must require nothing of any referred objects except instantiation.
        /// Connect should always succeed even if a referred object is not itself Connect()ed.
        /// </summary>
        public virtual void Connect()
        {
            //Initialized = false;
            //ComponentStarted = false;
            //ComponentStopping = false;
            //ComponentStopped = false;
            /* do stuff */
        }

        /// <summary>
        ///  Initialize can assume all referred objects are Connected, but they might not
        ///  be Initialized.
        /// </summary>
        public virtual void Initialize()
        {
            /* do stuff */
            Initialized = true;
        }

        public virtual void ComponentStart()
        {
        //    ComponentStopping = false;
        //    /* do stuff */
        //    ComponentStopped = false;
        //    ComponentStarted = true;
        }

        public virtual void ComponentUpdate() { }

        public virtual void ComponentStop()
        {
        //    ComponentStopping = true;
        //    /* do stuff */
        //    ComponentStopped = true;
        }
	}
}
