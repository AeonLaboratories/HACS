using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace HACS.Core
{
	public class BindableObject : INotifyPropertyChanged
	{
		private static ConcurrentQueue<Action> notificationQ = new ConcurrentQueue<Action>();
		private static ManualResetEvent notificationSignal = new ManualResetEvent(false);
		static Counter QCounter { get; set; }

		class Counter : HacsComponent
        {
			Stopwatch sw = new Stopwatch();
			public int Count
            {
				get => count;
                set
                {
					count = value;
					Elapsed = sw.Elapsed.TotalMilliseconds;
					sw.Restart();
					PropertyChanged?.Invoke(this, PropertyChangedEventArgs(nameof(Count)));
					PropertyChanged?.Invoke(this, PropertyChangedEventArgs(nameof(Elapsed)));
                }
            }
			int count;

			public double Elapsed { get; set; }
        }

		static BindableObject()
		{
//			QCounter = new Counter() { Name = "QCount" };
			Task.Factory.StartNew(ManageQ, TaskCreationOptions.LongRunning);
        }

		static void ManageQ()
        {
			while (!Hacs.Stopping)
            {
//				int i = 0;
				while (notificationQ.TryDequeue(out Action notify) && !Hacs.Stopping)
				{
					notify();
//					++i;
				}
//				QCounter.Count = i;
				if (!Hacs.Stopping)
				{
					notificationSignal.Reset();
					notificationSignal.WaitOne(100);
				}
            }
        }

		/// <summary>
		/// The event handlers to be invoked whenever the property changes.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Retrieves the cached PropertyChangedEventArgs with the given propertyName,
		/// or creates and stores it, if it doesn't already exist.
		/// </summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public static PropertyChangedEventArgs PropertyChangedEventArgs(string propertyName = "")
		{
			if (!CachedEventArgs.TryGetValue(propertyName, out PropertyChangedEventArgs args))
				args = CachedEventArgs[propertyName] = new PropertyChangedEventArgs(propertyName);
			return args;
		}
		protected static ConcurrentDictionary<string, PropertyChangedEventArgs> CachedEventArgs = new ConcurrentDictionary<string, PropertyChangedEventArgs>();

		/// <summary>
		/// A method intended to be used as a class property setter, to
		/// (perhaps conditionally) assign a value to the property, and 
		/// if appropriate, raise a PropertyChanged event.
		/// </summary>
		/// <typeparam name="T">The property's type</typeparam>
		/// <param name="field">A reference to the property's backing variable, usually a field</param>
		/// <param name="value">The value to be assigned</param>
		/// <param name="propertyName">The name of the property</param>
		public delegate bool PropertySetter<T>(ref T field, T value, [CallerMemberName] string propertyName = default);


		/// <summary>
		/// Set the property backing variable to the specified value and raise the PropertyChanged event.
		/// </summary>
		/// <typeparam name="T">The property's type</typeparam>
		/// <param name="field">A reference to the property's backing variable, usually a field</param>
		/// <param name="value">The value to be assigned</param>
		/// <param name="propertyName">The name of the property</param>
		/// <returns>true</returns>
		protected virtual bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = default)
		{
			field = value;
			NotifyPropertyChanged(propertyName);
			return true;
		}


		/// <summary>
		/// If the present value of the property does not equal the specified value, 
		/// set the property backing variable to the specified value and raise the PropertyChanged event.
		/// </summary>
		/// <typeparam name="T">The property's type</typeparam>
		/// <param name="field">A reference to the property's backing variable, usually a field</param>
		/// <param name="value">The value to be assigned</param>
		/// <param name="propertyName">The name of the property</param>
		/// <returns>true if the field was updated to value</returns>
		protected virtual bool Ensure<T>(ref T field, T value, [CallerMemberName] string propertyName = default)
		{
			if (!field?.Equals(value) ?? value != null)
				return Set(ref field, value, propertyName);
			return false;
		}


		/// <summary>
		/// Set the property backing variable to the specified value and raise the PropertyChanged event.
		/// </summary>
		/// <typeparam name="T">The property's type</typeparam>
		/// <param name="field">A reference to the property's backing variable, usually a field</param>
		/// <param name="value">The value to be assigned</param>
		/// <param name="handler">A PropertyChanged handler for the property</param>
		/// <param name="propertyName">The name of the property</param>
		/// <returns>true</returns>
		protected virtual bool Set<T>(ref T field, T value, PropertyChangedEventHandler handler, [CallerMemberName] string propertyName = default)
		{
			if (field is INotifyPropertyChanged fold)
				fold.PropertyChanged -= handler;
			field = value;
			if (field is INotifyPropertyChanged fnew)
			{
				fnew.PropertyChanged += handler;
				handler?.Invoke(field, PropertyChangedEventArgs(propertyName));
			}
			else
			{
				handler?.Invoke(this, PropertyChangedEventArgs(propertyName));
			}

			NotifyPropertyChanged(propertyName);
			return true;
		}

		/// <summary>
		/// If the specified value is different from that of the referenced field, 
		/// set the field to the new value, move the specified PropertyChanged handler 
		/// from from the old object to the new one and invoke it, and finally raise 
		/// the PropertyChanged event.
		/// </summary>
		/// <typeparam name="T">The property's type</typeparam>
		/// <param name="field">A reference to the property's backing variable, usually a field</param>
		/// <param name="value">The value to be assigned</param>
		/// <param name="handler">A PropertyChanged handler for the property</param>
		/// <param name="propertyName">The name of the property</param>
		/// <returns>true</returns>
		protected virtual bool Ensure<T>(ref T field, T value, PropertyChangedEventHandler handler, [CallerMemberName] string propertyName = default)
		{
			if (!field?.Equals(value) ?? value != null)
				return Set(ref field, value, handler, propertyName);
			return false;
		}


		/// <summary>
		/// Raises the PropertyChanged event.
		/// </summary>
		/// <param name="propertyName"></param>
		protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = "") =>
			NotifyPropertyChanged(this, PropertyChangedEventArgs(propertyName));

		/// <summary>
		/// Raises the PropertyChanged event.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected virtual void NotifyPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			notificationQ.Enqueue(() => PropertyChanged?.Invoke(sender, e));
			notificationSignal.Set();
		}
	}
}
