using Microsoft.EntityFrameworkCore;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Tailgrab.PlayerManagement
{
    // Virtualizing collection for Users
    public class UserVirtualizingCollection : System.Collections.IList, System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        private readonly ServiceRegistry _services;
        private readonly int _pageSize = 100;
        private readonly Dictionary<int, List<UserInfoViewModel>> _pages = new Dictionary<int, List<UserInfoViewModel>>();
        private int _count = -1;
        private string? _filterText;

        public UserVirtualizingCollection(ServiceRegistry services)
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
                var query = db.UserInfos.AsQueryable();

                if (!string.IsNullOrWhiteSpace(_filterText))
                {
                    if (_filterText.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(u => u.UserId == _filterText);
                    }
                    else
                    {
                        query = query.Where(u => EF.Functions.Like(u.DisplayName, $"%{_filterText}%"));
                    }
                }

                _count = query.Count();
            }
            catch
            {
                _count = 0;
            }
        }

        private UserInfoViewModel? LoadAtIndex(int index)
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
                    var query = db.UserInfos.AsQueryable();

                    if (!string.IsNullOrWhiteSpace(_filterText))
                    {
                        if (_filterText.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                        {
                            query = query.Where(u => u.UserId == _filterText);
                        }
                        else
                        {
                            query = query.Where(u => EF.Functions.Like(u.DisplayName, $"%{_filterText}%"));
                        }
                    }

                    var items = query.OrderBy(a => a.DisplayName).Skip(skip).Take(_pageSize).ToList();
                    list = items.Select(a =>
                    {
                        var vm = new UserInfoViewModel(a);

                        // Load ModerationInfo count for this user
                        vm.ModerationRecordCount = db.ModerationInfos
                            .Where(m => m.UserId == a.UserId)
                            .Count();

                        var moderationLast = db.ModerationInfos
                            .Where(m => m.UserId == a.UserId)
                            .OrderByDescending(m => m.EventDateTime)
                            .FirstOrDefault();

                        if (moderationLast != null)
                        {
                            vm.ModeratedLastDateTime = moderationLast.EventDateTime;
                        }

                        // Load ProfileEvaluation data for this user
                        var profileEval = db.ProfileEvaluations
                            .Where(p => p.Md5checksum == a.LastProfileChecksum)
                            .OrderByDescending(p => p.LastDateTime)
                            .FirstOrDefault();

                        if (profileEval != null)
                        {
                            vm.ProfileEvaluationDateTime = profileEval.LastDateTime;

                            // Extract first line from Evaluation byte array
                            if (profileEval.Evaluation != null && profileEval.Evaluation.Length > 0)
                            {
                                try
                                {
                                    string evaluationText = System.Text.Encoding.UTF8.GetString(profileEval.Evaluation);
                                    string firstLine = evaluationText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)[0];
                                    vm.EvaluationFirstLine = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                                }
                                catch
                                {
                                    vm.EvaluationFirstLine = null;
                                }
                            }
                        }

                        return vm;
                    }).ToList();
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
                    list = new List<UserInfoViewModel>();
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
            if (value is UserInfoViewModel vm) return this.Cast<UserInfoViewModel>().Any(x => x.UserId == vm.UserId);
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

    public class UserInfoViewModel : INotifyPropertyChanged
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public double ElapsedMinutes { get; set; }
        private int _isBos;
        public int IsBos
        {
            get => _isBos;
            set
            {
                if (_isBos != value)
                {
                    _isBos = value;
                    OnPropertyChanged(nameof(IsBos));
                }
            }
        }

        public DateTime UpdatedAt { get; set; }

        private int _moderationRecordCount;
        public int ModerationRecordCount
        {
            get => _moderationRecordCount;
            set
            {
                if (_moderationRecordCount != value)
                {
                    _moderationRecordCount = value;
                    OnPropertyChanged(nameof(ModerationRecordCount));
                }
            }
        }

        private DateTime? _profileEvaluationDateTime;
        public DateTime? ProfileEvaluationDateTime
        {
            get => _profileEvaluationDateTime;
            set
            {
                if (_profileEvaluationDateTime != value)
                {
                    _profileEvaluationDateTime = value;
                    OnPropertyChanged(nameof(ProfileEvaluationDateTime));
                }
            }
        }

        private string? _evaluationFirstLine;
        public string? EvaluationFirstLine
        {
            get => _evaluationFirstLine;
            set
            {
                if (_evaluationFirstLine != value)
                {
                    _evaluationFirstLine = value;
                    OnPropertyChanged(nameof(EvaluationFirstLine));
                }
            }
        }

        private DateTime? _moderatedLastDateTime;
        public DateTime? ModeratedLastDateTime
        {
            get => _moderatedLastDateTime;
            set
            {
                if (_moderatedLastDateTime != value)
                {
                    _moderatedLastDateTime = value;
                    OnPropertyChanged(nameof(ModeratedLastDateTime));
                }
            }
        }

        public UserInfoViewModel(Models.UserInfo u)
        {
            UserId = u.UserId;
            DisplayName = u.DisplayName;
            ElapsedMinutes = u.ElapsedMinutes;
            UpdatedAt = u.UpdatedAt;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
