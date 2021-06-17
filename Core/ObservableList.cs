using System.Collections.Generic;
using System.ComponentModel;

namespace HACS.Core
{
	public class ObservableList<T> : List<T>, INotifyPropertyChanged
	{
		#region static
		static PropertyChangedEventArgs DefaultArgs;		
		static ObservableList()
		{
			DefaultArgs = 
				BindableObject.PropertyChangedEventArgs(string.Empty);
		}

        #endregion static

		public event PropertyChangedEventHandler PropertyChanged;

		public new void Add(T item)
		{
			base.Add(item);
			NotifyPropertyChanged(this, DefaultArgs);
		}

		public new void Remove(T item)
		{
			base.Remove(item);
			NotifyPropertyChanged(this, DefaultArgs);
		}

		protected virtual void NotifyPropertyChanged(object sender, PropertyChangedEventArgs e) =>
			PropertyChanged?.Invoke(sender, e);
	}
}
