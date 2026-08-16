using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace TimeTracker.Infrastructure.Collections;

/// <summary>
/// Кастомная реализация ObservableCollection. 
/// Позволяет избежать шторма обновлений UI при добавлении списков и контролировать операции с памятью.
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceRange(IEnumerable<T> collection)
    {
        Items.Clear();
        foreach (var item in collection)
        {
            Items.Add(item);
        }
        // Стреляем ивентом только один раз для всего списка операций
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

