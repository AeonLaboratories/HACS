using System;
using System.ComponentModel;

namespace HACS.Core
{
	public interface INamedObject : INotifyPropertyChanged
	{
		/// <summary>
		/// The searchable name of an object. Each instance of a class should have a unique name.
		/// Only the first of multiple NamedObjects with the same name is cataloged.
		/// </summary>
		string Name { get; set; }
	}

	public interface IHacsComponent : INamedObject
	{
		/// <summary>
		/// Whether the Connect stage of the HacsComponent life-cycle has been completed for this device.
		/// </summary>
		bool Connected { get; }

		/// <summary>
		/// Whether the Initialize stage of the HacsComponent life-cycle has been completed for this device.
		/// </summary>
		bool Initialized { get; }

		/// <summary>
		/// Whether the Start stage of the HacsComponent life-cycle has been completed for this device.
		/// </summary>
		bool Started { get; }

		/// <summary>
		/// Whether the Stop stage of the HacsComponent life-cycle has been completed for this device.
		/// </summary>
		bool Stopped { get; }
	}

	public interface IHacsBase : IHacsComponent
	{
		Action SaveSettings { get; set; }
		Action<string> SaveSettingsToFile { get; set; }
	}

	public interface IHacsUI
	{
		void Close();
	}

	public interface IHacsLog : INamedObject
	{
		Action Update { get; set; }
		string FileName { get; set; }
		string Header { get; set; }
		string TimeStampFormat { get; set; }
		bool ArchiveDaily { get; set; }
		long ElapsedMilliseconds { get; }
		string TimeStamp();
		void Write(string entry = "");
		void WriteLine(string entry = "");
		void Record(string entry = "");
		void LogParsimoniously(string entry = "");

	}

	public interface IValue { double Value { get; } }

	// Update(), in this interface, is for classes that provide
	// a Value that is in some way related to supplied number, but
	// the relationship is not necessarily a simple or direct
	// dependency. Value might have different units than the
	// supplied number, or it may also depend on prior values, 
	// or on conditions not related to the supplied value.
	// However, in all cases, the object's Value is updated 
	// when it receives a number.
	public interface IDoubleUpdatable : IValue
	{
		double Update(double value);
	}

	public interface IFilter : INamedObject, IDoubleUpdatable { }
	public interface IRateOfChange : INamedObject, IDoubleUpdatable { }


}
