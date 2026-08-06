using Microsoft.EntityFrameworkCore;
using System.Collections.Specialized;
using System.ComponentModel;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement
{
    public class ModerationVirtualizingCollection : System.Collections.IList, System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        private readonly ServiceRegistry _services;
        private readonly int _pageSize = 100;
        private readonly Dictionary<int, List<ModerationInfoViewModel>> _pages = new Dictionary<int, List<ModerationInfoViewModel>>();
        private int _count = -1;
        private string? _userIdFilter;
        private string? _contentIdFilter;

        public ModerationVirtualizingCollection(ServiceRegistry services)
        {
            _services = services;
        }

        public void SetFilter(string? userIdFilter, string? contentIdFilter)
        {
            if (_userIdFilter != userIdFilter || _contentIdFilter != contentIdFilter)
            {
                _userIdFilter = userIdFilter;
                _contentIdFilter = contentIdFilter;
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
                var query = db.ModerationInfos.AsQueryable();

                if (!string.IsNullOrWhiteSpace(_userIdFilter))
                {
                    query = query.Where(m => EF.Functions.Like(m.UserId, $"%{_userIdFilter}%"));
                }

                if (!string.IsNullOrWhiteSpace(_contentIdFilter))
                {
                    query = query.Where(m => EF.Functions.Like(m.ContentId, $"%{_contentIdFilter}%"));
                }

                _count = query.Count();
            }
            catch
            {
                _count = 0;
            }
        }

        private ModerationInfoViewModel? LoadAtIndex(int index)
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
                    var query = db.ModerationInfos.AsQueryable();

                    if (!string.IsNullOrWhiteSpace(_userIdFilter))
                    {
                        query = query.Where(m => EF.Functions.Like(m.UserId, $"%{_userIdFilter}%"));
                    }

                    if (!string.IsNullOrWhiteSpace(_contentIdFilter))
                    {
                        query = query.Where(m => EF.Functions.Like(m.ContentId, $"%{_contentIdFilter}%"));
                    }

                    var items = query.OrderByDescending(m => m.EventDateTime).Skip(skip).Take(_pageSize).ToList();
                    list = items.Select(m => new ModerationInfoViewModel(m)).ToList();
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
                    list = new List<ModerationInfoViewModel>();
                }
            }
            var idxInPage = index % _pageSize;
            if (idxInPage < list.Count) return list[idxInPage];
            return null;
        }

        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value)
        {
            EnsureCount();
            if (value is ModerationInfoViewModel vm) return this.Cast<ModerationInfoViewModel>().Any(x => x.Id == vm.Id);
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

    public class ModerationInfoViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string ContentType { get; set; }
        public string UserId { get; set; }
        public string ContentId { get; set; }
        public string ContentName { get; set; }
        public string Thumbnail { get; set; }
        public DateTime EventDateTime { get; set; }
        public DateTime? CloseDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool IsClosed { get; set; }
        public bool IsDeleted { get; set; }

        public string Report {  get; set; }

        public ModerationInfoViewModel(Tailgrab.Models.ModerationInfo m)
        {
            Id = m.Id ?? "Unknown";
            ContentType = m.ContentType ?? "Unknown";
            UserId = m.UserId ?? "Unknown";
            ContentId = m.ContentId ?? "Unknown";
            ContentName = m.ContentName ?? "Unknown";
            Thumbnail = m.Thumbnail ?? string.Empty;
            EventDateTime = m.EventDateTime;
            CloseDate = m.ClosedDate;
            DeletedDate = m.DeletedDate;
            IsClosed = m.IsClosed;
            IsDeleted = m.IsDeleted;
            Report = m.Report != null ? System.Text.Encoding.UTF8.GetString(m.Report) : string.Empty;   
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
