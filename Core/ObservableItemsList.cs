using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HACS.Core
{
	public class ObservableItemsList<T> : ObservableList<T> where T : INotifyPropertyChanged
	{
		public new void Add(T item)
		{
			base.Add(item);
			if (item != null)
				item.PropertyChanged += NotifyPropertyChanged;
		}

		public new void Remove(T item)
		{
			if (item != null)
				item.PropertyChanged -= NotifyPropertyChanged;
			base.Remove(item);
		}
	}
}
