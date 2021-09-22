using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace G3Demo
{
    public class ThrottlingObservableCollection<T> : ObservableCollection<T>
    {
        public void RemoveFirst()
        {
            RemoveAt(0);
        }
    }
/*
    public class ThrottlingObservableCollection<T> : ICollection<T>, INotifyCollectionChanged
    {
        private readonly Queue<T> _list = new Queue<T>();
        private readonly List<T> _removed = new List<T>();
        private readonly Stopwatch _notifyTimer = Stopwatch.StartNew();
        private readonly List<T> _added = new List<T>();
        private bool _notificationsEnabled = true;


        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            _list.Enqueue(item);
            _added.Add(item);
            Notify();
        }

        public void Clear()
        {
            _removed.AddRange(_list);
            _list.Clear();
            Notify();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            // this is a linear operation on a queue
            // dequeue each item in the queue,
            // enqueue it again if it is not the removed item
            var res = false;
            var count = _list.Count;
            for (int x = 0; x < count; x++)
            {
                var i = _list.Dequeue();
                if (!i.Equals(item))
                    _list.Enqueue(i);
                else
                {
                    res = true;
                    _removed.Add(i);
                }
            }

            if (res)
            {
                Notify();
            }

            return res;
        }

        private void Notify()
        {
            if (!NotificationsEnabled)
            {
                _removed.Clear();
                _added.Clear();
            }

            if (_notifyTimer.ElapsedMilliseconds < 100)
                return;

            if (_removed.Any())
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove, _removed));
                _removed.Clear();
            }

            if (_added.Any())
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, _added));
                _added.Clear();
            }
            _notifyTimer.Restart();
        }

        public int Count => _list.Count;
        public bool IsReadOnly => false;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void RemoveFirst()
        {
            var item = _list.Dequeue();
            _removed.Add(item);
            Notify();
        }

        public bool NotificationsEnabled
        {
            get => _notificationsEnabled;
            set
            {
                if (value != _notificationsEnabled)
                    return;
                _notificationsEnabled = value;
                if (_notificationsEnabled)
                {
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    // CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, _list.ToList()));
                }
                else
                {
                    _removed.Clear();
                    _added.Clear();
                }
            }
        }
    }
*/
}