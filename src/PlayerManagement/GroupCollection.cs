using System.Collections.Specialized;
using System.ComponentModel;

namespace Tailgrab.PlayerManagement
{
    // Virtualizing collection for Groups similar to AvatarVirtualizingCollection
    public class GroupVirtualizingCollection : System.Collections.IList, System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        private readonly ServiceRegistry _services;
        private readonly int _pageSize = 100;
        private readonly Dictionary<int, List<GroupInfoViewModel>> _pages = new Dictionary<int, List<GroupInfoViewModel>>();
        private int _count = -1;

        public GroupVirtualizingCollection(ServiceRegistry services)
        {
            _services = services;
        }

        public void Refresh()
        {
            _pages.Clear();
            _count = -1;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void EnsureCount()
        {
            if (_count >= 0) return;
            try
            {
                var db = _services.GetDBContext();
                _count = db.GroupInfos.Count();
            }
            catch
            {
                _count = 0;
            }
        }

        private GroupInfoViewModel? LoadAtIndex(int index)
        {
            if (index < 0) return null;
            EnsureCount();
            if (index >= _count) return null;
            var page = index / _pageSize;
            if (!_pages.TryGetValue(page, out var list))
            {
                try
                {
                    var db = _services.GetDBContext();
                    var skip = page * _pageSize;
                    var items = db.GroupInfos.OrderBy(a => a.GroupName).Skip(skip).Take(_pageSize).ToList();
                    list = items.Select(a => new GroupInfoViewModel(a)).ToList();
                    _pages[page] = list;
                    var keep = new HashSet<int> { page, page - 1, page + 1 };
                    var keys = _pages.Keys.ToList();
                    foreach (var k in keys)
                    {
                        if (!keep.Contains(k)) _pages.Remove(k);
                    }
                }
                catch
                {
                    list = new List<GroupInfoViewModel>();
                }
            }
            var idxInPage = index % _pageSize;
            if (idxInPage < list.Count) return list[idxInPage];
            return null;
        }

        // IList implementation (read-only)
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value)
        {
            EnsureCount();
            if (value is GroupInfoViewModel vm) return this.Cast<GroupInfoViewModel>().Any(x => x.GroupId == vm.GroupId);
            return false;
        }
        public int IndexOf(object? value) => -1;
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;
        public object? this[int index]
        {
            get { return LoadAtIndex(index); }
            set => throw new NotSupportedException();
        }

        public void CopyTo(Array array, int index)
        {
            EnsureCount();
            for (int i = 0; i < _count; i++) array.SetValue(LoadAtIndex(i), index + i);
        }

        public int Count
        {
            get { EnsureCount(); return _count; }
        }

        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public System.Collections.IEnumerator GetEnumerator()
        {
            EnsureCount();
            for (int i = 0; i < _count; i++) yield return LoadAtIndex(i)!;
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
    }

    public class GroupInfoViewModel : INotifyPropertyChanged
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        private bool _isBos;
        public bool IsBos
        {
            get => _isBos;
            set
            {
                if (_isBos != value)
                {
                    _isBos = value;
                    IsBosText = BoolToYesNo(_isBos);
                    OnPropertyChanged(nameof(IsBos));
                    OnPropertyChanged(nameof(IsBosText));
                }
            }
        }

        public string IsBosText { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public GroupInfoViewModel(Tailgrab.Models.GroupInfo a)
        {
            GroupId = a.GroupId;
            GroupName = a.GroupName;
            IsBos = a.IsBos;
            UpdatedAt = a.UpdatedAt;
            IsBosText = BoolToYesNo(IsBos);
        }

        // Convert boolean to YES/NO string for display
        public static string BoolToYesNo(bool value) => value ? "YES" : "NO";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
