using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement
{
    // Lightweight virtualizing collection for Avatar DB. It only fetches items on demand
    // and holds a small cache to limit memory usage. It queries the EF DB context for
    // counts and pages of avatars ordered by AvatarName.
    public class AvatarVirtualizingCollection : System.Collections.IList, System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        private readonly ServiceRegistry _services;
        private readonly int _pageSize = 100;
        private readonly Dictionary<int, List<AvatarInfoViewModel>> _pages = new Dictionary<int, List<AvatarInfoViewModel>>();
        private int _count = -1;
        private string? _filterText;

        public AvatarVirtualizingCollection(ServiceRegistry services)
        {
            _services = services;
        }

        public void SetFilter(string? filterText)
        {
            if (_filterText != filterText)
            {
                _filterText = filterText;
                Refresh();
            }
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
                var query = db.AvatarInfos.AsQueryable();
                
                if (!string.IsNullOrWhiteSpace(_filterText))
                {
                    if (_filterText.StartsWith("avtr_", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(a => a.AvatarId == _filterText);
                    }
                    else
                    {
                        query = query.Where(a => EF.Functions.Like(a.AvatarName, $"%{_filterText}%"));
                    }
                }
                
                _count = query.Count();
            }
            catch
            {
                _count = 0;
            }
        }

        private AvatarInfoViewModel? LoadAtIndex(int index)
        {
            if (index < 0) return null;
            EnsureCount();
            if (index >= _count) return null;
            var page = index / _pageSize;
            if (!_pages.TryGetValue(page, out var list))
            {
                // load this page
                try
                {
                    var db = _services.GetDBContext();
                    var skip = page * _pageSize;
                    var query = db.AvatarInfos.AsQueryable();
                    
                    if (!string.IsNullOrWhiteSpace(_filterText))
                    {
                        var filterLower = _filterText.ToLower();
                        query = query.Where(a => a.AvatarName.ToLower().Contains(filterLower));
                    }
                    
                    var items = query.OrderBy(a => a.AvatarName).Skip(skip).Take(_pageSize).ToList();
                    list = items.Select(a => new AvatarInfoViewModel(a)).ToList();
                    _pages[page] = list;
                    // Keep only a couple pages in memory (current, prev, next)
                    var keep = new HashSet<int> { page, page - 1, page + 1 };
                    var keys = _pages.Keys.ToList();
                    foreach (var k in keys)
                    {
                        if (!keep.Contains(k)) _pages.Remove(k);
                    }
                }
                catch
                {
                    list = new List<AvatarInfoViewModel>();
                }
            }
            var idxInPage = index % _pageSize;
            if (idxInPage < list.Count) return list[idxInPage];
            return null;
        }

        // IList implementation (read-only for UI)
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value)
        {
            EnsureCount();
            if (value is AvatarInfoViewModel vm) return this.Cast<AvatarInfoViewModel>().Any(x => x.AvatarId == vm.AvatarId);
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

        // Collection changed event for WPF to react to resets
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
    }

    public class AvatarInfoViewModel : INotifyPropertyChanged
    {
        private AlertTypeEnum _alertType;

        public string AvatarId { get; set; }
        public string AvatarName { get; set; }
        
        public AlertTypeEnum AlertType
        {
            get => _alertType;
            set
            {
                if (_alertType != value)
                {
                    _alertType = value;
                    OnPropertyChanged(nameof(AlertType));
                }
            }
        }
        
        public DateTime? UpdatedAt { get; set; }

        public AvatarInfoViewModel(Tailgrab.Models.AvatarInfo a)
        {
            AvatarId = a.AvatarId;
            AvatarName = a.AvatarName;
            UpdatedAt = a.UpdatedAt;
            AlertType = a.AlertType;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
