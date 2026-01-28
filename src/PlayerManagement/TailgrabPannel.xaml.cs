using NLog;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Tailgrab.Clients.VRChat;
using Tailgrab.Config;
using Tailgrab.Models;
using VRChat.API.Model;

namespace Tailgrab.PlayerManagement
{
    public partial class TailgrabPannel : Window, IDisposable
    {
        private readonly DispatcherTimer fallbackTimer;

        public ObservableCollection<PlayerViewModel> ActivePlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> PastPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> StickerPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> PrintPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public AvatarVirtualizingCollection AvatarDbItems { get; private set; }
        public GroupVirtualizingCollection GroupDbItems { get; private set; }
        public UserVirtualizingCollection UserDbItems { get; private set; }

        public ICollectionView AvatarDbView { get; }
        public ICollectionView ActiveView { get; }
        public ICollectionView GroupDbView { get; }
        public ICollectionView UserDbView { get; }
        public ICollectionView PastView { get; }
        public ICollectionView StickerView { get; }
        public ICollectionView PrintView { get; }

        public PlayerViewModel? SelectedActive { get; set; }
        public PlayerViewModel? SelectedPast { get; set; }
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public List<KeyValuePair<string, bool>> IsBosOptions { get; set; }

        protected ServiceRegistry _serviceRegistry;

        public TailgrabPannel(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
            InitializeComponent();
            DataContext = this;

            ActiveView = CollectionViewSource.GetDefaultView(ActivePlayers);
            ActiveView.SortDescriptions.Add(new SortDescription("InstanceStartTime", ListSortDirection.Descending));

            PastView = CollectionViewSource.GetDefaultView(PastPlayers);
            PastView.SortDescriptions.Add(new SortDescription("InstanceEndTime", ListSortDirection.Descending));

            StickerView = CollectionViewSource.GetDefaultView(StickerPlayers);

            PrintView = CollectionViewSource.GetDefaultView(PrintPlayers);

            AvatarDbItems = new AvatarVirtualizingCollection(_serviceRegistry);
            AvatarDbView = CollectionViewSource.GetDefaultView(AvatarDbItems);
            // The virtualizing collection returns items ordered by AvatarName already.

            GroupDbItems = new GroupVirtualizingCollection(_serviceRegistry);
            GroupDbView = CollectionViewSource.GetDefaultView(GroupDbItems);

            // Group collection is ordered by GroupName at source
            UserDbItems = new UserVirtualizingCollection(_serviceRegistry);
            UserDbView = CollectionViewSource.GetDefaultView(UserDbItems);

            // User collection ordered by DisplayName at source
            UserDbView.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));

            // Options for the IsBOS combo column
            IsBosOptions = new List<KeyValuePair<string, bool>>
            {
                new KeyValuePair<string,bool>("YES", true),
                new KeyValuePair<string,bool>("NO", false)
            };

            // Initial load of avatars
            RefreshAvatarDb();

            // Load saved secrets into UI fields if desired (not displayed in this view directly)
            var vrUser = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_VRChat_Web_UserName);
            var vrPass = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_VRChat_Web_Password);
            var vr2fa = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_VRChat_Web_2FactorKey);
            var ollamaKey = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Key);
            var ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Endpoint) ?? Tailgrab.Common.Common.Default_Ollama_API_Endpoint;
            var ollamaPrompt = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Prompt) ?? Tailgrab.Common.Common.Default_Ollama_API_Prompt;
            var ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Model) ?? Tailgrab.Common.Common.Default_Ollama_API_Model;

            // Populate UI boxes but do not reveal secrets
            if (!string.IsNullOrEmpty(vrUser)) VrUserBox.Text = vrUser;
            if (!string.IsNullOrEmpty(vrPass)) VrPassBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(vr2fa)) Vr2FaBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(ollamaKey)) VrOllamaBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(ollamaEndpoint)) VrOllamaEndpointBox.Text = ollamaEndpoint;
            if (!string.IsNullOrEmpty(ollamaModel)) VrOllamaModelBox.Text = ollamaModel;
            if (!string.IsNullOrEmpty(ollamaPrompt)) VrOllamaPromptBox.Text = ollamaPrompt;

            // Initial load of Groups and Users
            RefreshGroupDb();
            RefreshUserDb();

            // Subscribe to PlayerManager events for reactive updates
            PlayerManager.PlayerChanged += PlayerManager_PlayerChanged;

            // Fallback timer to ensure eventual sync (in case of missed events)
            fallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            fallbackTimer.Tick += FallbackTimer_Tick;
            fallbackTimer.Start();

            this.Closed += (s, e) => Dispose();
        }

        public class PrintInfoViewModel : INotifyPropertyChanged
        {
            public string Id { get; set; }
            public string AuthorName { get; set; }
            public string OwnerId { get; set; }
            public DateTime Timestamp { get; set; }
            public string Url { get; set; }

            public PrintInfoViewModel(VRChat.API.Model.Print p)
            {
                Id = p.Id ?? string.Empty;
                AuthorName = p.AuthorName ?? string.Empty;
                OwnerId = p.OwnerId ?? string.Empty;
                Timestamp = p.Timestamp;
                Url = p.Files.Image ?? string.Empty;
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
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

            public UserInfoViewModel(Tailgrab.Models.UserInfo u)
            {
                UserId = u.UserId;
                DisplayName = u.DisplayName;
                ElapsedMinutes = u.ElapsedMinutes;
                IsBos = u.IsBos;
                UpdatedAt = u.UpdatedAt;
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void UserHyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                logger.Info($"Opening User URL: {e.Uri}");
                var uri = new Uri($"https://vrchat.com/home/user/{e.Uri}");
                var psi = new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to open user URL");
            }
            e.Handled = true;
        }

        // User DB UI handlers
        private void UserDbRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshUserDb();
        }

        private void UserDbApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyUserDbFilter(UserDbView, UserDbFilterBox.Text);
        }

        private void UserDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            UserDbFilterBox.Text = string.Empty;
            ApplyUserDbFilter(UserDbView, string.Empty);
        }

        private void ApplyUserDbFilter(ICollectionView view, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                view.Filter = null;
                view.Refresh();
                return;
            }

            string ft = filterText.Trim();
            view.Filter = obj =>
            {
                if (obj is UserInfoViewModel vm)
                {
                    return vm.DisplayName?.IndexOf(ft, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
                return false;
            };
            view.Refresh();
        }

        private void UserDbGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is UserInfoViewModel vm)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();
                    var entity = db.UserInfos.Find(vm.UserId);
                    if (entity != null)
                    {
                        entity.IsBos = vm.IsBos;
                        entity.UpdatedAt = DateTime.UtcNow;
                        db.UserInfos.Update(entity);
                        db.SaveChanges();
                        vm.UpdatedAt = entity.UpdatedAt;
                    }
                }
                catch { }
            }
        }

        // Virtualizing collection for Users
        public class UserVirtualizingCollection : System.Collections.IList, System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
        {
            private readonly ServiceRegistry _services;
            private readonly int _pageSize = 100;
            private readonly Dictionary<int, List<UserInfoViewModel>> _pages = new Dictionary<int, List<UserInfoViewModel>>();
            private int _count = -1;

            public UserVirtualizingCollection(ServiceRegistry services)
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
                    _count = db.UserInfos.Count();
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
                        var items = db.UserInfos.OrderBy(a => a.DisplayName).Skip(skip).Take(_pageSize).ToList();
                        list = items.Select(a => new UserInfoViewModel(a)).ToList();
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

        // Lightweight virtualizing collection for Avatar DB. It only fetches items on demand
        // and holds a small cache to limit memory usage. It queries the EF DB context for
        // counts and pages of avatars ordered by AvatarName.
        public class AvatarVirtualizingCollection : System.Collections.IList, System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
        {
            private readonly ServiceRegistry _services;
            private readonly int _pageSize = 100;
            private readonly Dictionary<int, List<AvatarInfoViewModel>> _pages = new Dictionary<int, List<AvatarInfoViewModel>>();
            private int _count = -1;

            public AvatarVirtualizingCollection(ServiceRegistry services)
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
                    _count = db.AvatarInfos.Count();
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
                        var items = db.AvatarInfos.OrderBy(a => a.AvatarName).Skip(skip).Take(_pageSize).ToList();
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

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save to registry protected store
                ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_VRChat_Web_UserName, VrUserBox.Text ?? string.Empty);
                if (!string.IsNullOrEmpty(VrPassBox.Password)) ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_VRChat_Web_Password, VrPassBox.Password);
                if (!string.IsNullOrEmpty(Vr2FaBox.Password)) ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_VRChat_Web_2FactorKey, Vr2FaBox.Password);
                if (!string.IsNullOrEmpty(VrOllamaBox.Password)) ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_Ollama_API_Key, VrOllamaBox.Password);
                ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_Ollama_API_Endpoint, VrOllamaEndpointBox.Text ?? Tailgrab.Common.Common.Default_Ollama_API_Endpoint);
                ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_Ollama_API_Prompt, VrOllamaPromptBox.Text ?? Tailgrab.Common.Common.Default_Ollama_API_Prompt);
                ConfigStore.SaveSecret(Tailgrab.Common.Common.Registry_Ollama_API_Model, VrOllamaModelBox.Text ?? Tailgrab.Common.Common.Default_Ollama_API_Model);

                System.Windows.MessageBox.Show("Configuration saved. Restart the Applicaton for all changes to take affect.", "Config", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FallbackTimer_Tick(object? sender, EventArgs e)
        {
            // Ensure collections reflect PlayerManager state
            try
            {
                var players = _serviceRegistry.GetPlayerManager().GetAllPlayers().ToList();
                UpdateCollectionsFromSnapshot(players);
            }
            catch { }
        }

        private void PlayerManager_PlayerChanged(object? sender, PlayerChangedEventArgs e)
        {
            // Ensure UI thread
            Dispatcher.Invoke(() => HandlePlayerChange(e));
        }

        // Avatar DB UI handlers
        #region Avatar DB handlers
        private void AvatarDbRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAvatarDb();
        }

        private void AvatarDbApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyAvatarDbFilter(AvatarDbView, AvatarDbFilterBox.Text);
        }

        private void AvatarDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            AvatarDbFilterBox.Text = string.Empty;
            ApplyAvatarDbFilter(AvatarDbView, string.Empty);
        }

        private void ApplyAvatarDbFilter(ICollectionView view, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                view.Filter = null;
                view.Refresh();
                return;
            }

            string ft = filterText.Trim();
            view.Filter = obj =>
            {
                if (obj is AvatarInfoViewModel vm)
                {
                    return vm.AvatarName?.IndexOf(ft, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
                return false;
            };
            view.Refresh();
        }

        private void RefreshAvatarDb()
        {
            try
            {
                // Refresh virtualized collection which will clear caches and re-query counts
                AvatarDbItems.Refresh();
            }
            catch { }
        }

        private void AvatarDbGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is AvatarInfoViewModel vm)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();
                    var entity = db.AvatarInfos.Find(vm.AvatarId);
                    if (entity != null)
                    {
                        entity.IsBos = vm.IsBos;
                        entity.UpdatedAt = DateTime.UtcNow;
                        db.AvatarInfos.Update(entity);
                        db.SaveChanges();
                        vm.UpdatedAt = entity.UpdatedAt;
                    }
                }
                catch { }
            }
        }

        private void AvatarFetch_Click(object sender, RoutedEventArgs e)
        {
            string? id = AvatarIdBox.Text?.Trim();
            if (string.IsNullOrEmpty(id)) return;

            try
            {
                VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                Avatar? avatar = vrcClient.GetAvatarById(id);
                if (avatar != null)
                {
                    TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                    AvatarInfo? existing = dbContext.AvatarInfos.Find(avatar.Id);
                    if (existing == null)
                    {
                        var newEntity = new Tailgrab.Models.AvatarInfo
                        {
                            AvatarId = avatar.Id,
                            UserId = avatar.AuthorId ?? string.Empty,
                            AvatarName = avatar.Name ?? string.Empty,
                            ImageUrl = avatar.ImageUrl ?? string.Empty,
                            CreatedAt = avatar.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            IsBos = false
                        };
                        dbContext.AvatarInfos.Add(newEntity);
                        dbContext.SaveChanges();
                    }
                    else
                    {
                        existing.UserId = avatar.AuthorId ?? string.Empty;
                        existing.AvatarName = avatar.Name ?? string.Empty;
                        existing.ImageUrl = avatar.ImageUrl ?? string.Empty;
                        existing.CreatedAt = avatar.CreatedAt;
                        existing.UpdatedAt = DateTime.UtcNow;
                        dbContext.AvatarInfos.Update(existing);
                        dbContext.SaveChanges();
                    }

                    // Filter the view to the fetched avatar
                    ApplyAvatarDbFilter(AvatarDbView, avatar.Name ?? string.Empty);
                    AvatarIdBox.Text = string.Empty;
                }
                else
                {
                    System.Windows.MessageBox.Show($"Avatar {id} not found via VRChat API.", "Fetch Avatar", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to fetch avatar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion


        // Group DB UI handlers
        #region Group DB handlers
        private void GroupDbRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGroupDb();
        }

        private void GroupDbApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyGroupDbFilter(GroupDbView, GroupDbFilterBox.Text);
        }

        private void GroupDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            GroupDbFilterBox.Text = string.Empty;
            ApplyGroupDbFilter(GroupDbView, string.Empty);
        }

        private void ApplyGroupDbFilter(ICollectionView view, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                view.Filter = null;
                view.Refresh();
                return;
            }

            string ft = filterText.Trim();
            view.Filter = obj =>
            {
                if (obj is GroupInfoViewModel vm)
                {
                    return vm.GroupName?.IndexOf(ft, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
                return false;
            };
            view.Refresh();
        }

        private void RefreshGroupDb()
        {
            try
            {
                GroupDbItems.Refresh();
            }
            catch { }
        }

        private void RefreshUserDb()
        {
            try
            {
                UserDbItems?.Refresh();
            }
            catch { }
        }

        private void GroupDbGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is GroupInfoViewModel vm)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();
                    var entity = db.GroupInfos.Find(vm.GroupId);
                    if (entity != null)
                    {
                        entity.IsBos = vm.IsBos;
                        entity.UpdatedAt = DateTime.UtcNow;
                        db.GroupInfos.Update(entity);
                        db.SaveChanges();
                        vm.UpdatedAt = entity.UpdatedAt;
                    }
                }
                catch { }
            }
        }

        private void GroupFetch_Click(object sender, RoutedEventArgs e)
        {
            string? id = GroupIdBox.Text?.Trim();
            if (string.IsNullOrEmpty(id)) return;

            try
            {
                VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                VRChat.API.Model.Group? group = vrcClient.getGroupById(id);
                if (group != null)
                {
                    TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                    GroupInfo? existing = dbContext.GroupInfos.Find(group.Id);
                    if (existing == null)
                    {
                        GroupInfo newEntity = new GroupInfo
                        {
                            GroupId = group.Id,
                            GroupName = group.Name ?? string.Empty,
                            CreatedAt = group.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            IsBos = false
                        };
                        
                        dbContext.GroupInfos.Add(newEntity);
                        dbContext.SaveChanges();
                    }
                    else
                    {
                        existing.GroupId = group.Id;
                        existing.GroupName = group.Name ?? string.Empty;
                        existing.CreatedAt = group.CreatedAt;
                        existing.UpdatedAt = DateTime.UtcNow;
                        dbContext.GroupInfos.Update(existing);
                        dbContext.SaveChanges();
                    }

                    // Filter the view to the fetched Group
                    ApplyGroupDbFilter(GroupDbView, group.Name ?? string.Empty);
                    GroupIdBox.Text = string.Empty;
                }
                else
                {
                    System.Windows.MessageBox.Show($"Group {id} not found via VRChat API.", "Fetch Group", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to fetch Group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private void HandlePlayerChange(PlayerChangedEventArgs e)
        {
            switch (e.Type)
            {
                case PlayerChangedEventArgs.ChangeType.Added:
                    AddOrUpdatePlayer(e.Player);
                    break;
                case PlayerChangedEventArgs.ChangeType.Updated:
                    AddOrUpdatePlayer(e.Player);
                    break;
                case PlayerChangedEventArgs.ChangeType.Removed:
                    // PlayerLeft sets InstanceEndTime before raising Removed, so move to past list
                    MoveToPast(e.Player);
                    break;
                case PlayerChangedEventArgs.ChangeType.Cleared:
                    ActivePlayers.Clear();
                    PastPlayers.Clear();
                    StickerPlayers.Clear();
                    break;
            }
        }

        private void AddOrUpdatePlayer(Player p)
        {
            var vm = ActivePlayers.FirstOrDefault(x => x.UserId == p.UserId);
            var vmPast = PastPlayers.FirstOrDefault(x => x.UserId == p.UserId);
            var vmSticker = StickerPlayers.FirstOrDefault(x => x.UserId == p.UserId);
            var vmPrint = PrintPlayers.FirstOrDefault(x => x.UserId == p.UserId);

            if (p.InstanceEndTime == null)
            {
                if (vm != null)
                {
                    vm.UpdateFrom(p);
                }
                else
                {
                    // Not in active
                    var newVm = new PlayerViewModel(p);
                    ActivePlayers.Add(newVm);
                }

                // Ensure not in past or sticker
                if (vmPast != null)
                {
                    PastPlayers.Remove(vmPast);
                }
                if (vmSticker != null)
                {
                    StickerPlayers.Remove(vmSticker);
                }

                // If player has a sticker URL, add/update sticker list
                if (!string.IsNullOrEmpty(p.LastStickerUrl))
                {
                    if (vmSticker != null)
                    {
                        vmSticker.UpdateFrom(p);
                    }
                    else
                    {
                        StickerPlayers.Add(new PlayerViewModel(p));
                    }
                }

                // If player has prints, add/update print list
                if (p.PrintData != null && p.PrintData.Count > 0)
                {
                    if (vmPrint != null)
                    {
                        vmPrint.UpdateFrom(p);
                    }
                    else
                    {
                        PrintPlayers.Add(new PlayerViewModel(p));
                    }
                }
            }
            else
            {
                // Past player
                if (vmPast != null)
                {
                    vmPast.UpdateFrom(p);
                }
                else
                {
                    var newVm = new PlayerViewModel(p);
                    PastPlayers.Add(newVm);
                }

                if (vm != null)
                {
                    ActivePlayers.Remove(vm);
                }
                // move prints if present
                if (vmPrint != null)
                {
                    PrintPlayers.Remove(vmPrint);
                }
            }
        }

        private void MoveToPast(Player p)
        {
            // If already present in Active, remove and add to Past
            var activeVm = ActivePlayers.FirstOrDefault(x => x.UserId == p.UserId);
            if (activeVm != null)
            {
                ActivePlayers.Remove(activeVm);
            }

            var pastVm = PastPlayers.FirstOrDefault(x => x.UserId == p.UserId);
            if (pastVm != null)
            {
                pastVm.UpdateFrom(p);
            }
            else
            {
                PastPlayers.Add(new PlayerViewModel(p));
            }

            var olderThan = DateTime.UtcNow.AddMinutes(-10);
            var toRemove = new List<PlayerViewModel>();
            foreach (var oldPlayer in PastPlayers)
            {
                if (!string.IsNullOrEmpty(oldPlayer.InstanceEndTime))
                {
                    if (DateTime.TryParseExact(oldPlayer.InstanceEndTime, "u", null, System.Globalization.DateTimeStyles.None, out var endTime))
                    {
                        if (olderThan < endTime)
                        {
                            logger.Debug($"Removing old past player {oldPlayer.DisplayName} as {olderThan} < {endTime}");
                            toRemove.Add(oldPlayer);
                        }
                    }
                }
            }
            foreach (var oldPlayer in toRemove)
            {
                PastPlayers.Remove(oldPlayer);
            }
        }

        private void UpdateCollectionsFromSnapshot(System.Collections.Generic.List<Player> players)
        {
            // Add or update current players
            foreach (var p in players)
            {
                AddOrUpdatePlayer(p);
            }

            // Remove any that no longer exist in source
            var userIds = players.Select(x => x.UserId).ToHashSet();
            var toRemoveActive = ActivePlayers.Where(x => !userIds.Contains(x.UserId)).ToList();
            foreach (var rm in toRemoveActive) ActivePlayers.Remove(rm);

            var toRemovePast = PastPlayers.Where(x => !userIds.Contains(x.UserId) && string.IsNullOrEmpty(x.InstanceEndTime)).ToList();
            foreach (var rm in toRemovePast) PastPlayers.Remove(rm);

            var toRemoveSticker = StickerPlayers.Where(x => !userIds.Contains(x.UserId)).ToList();
            foreach (var rm in toRemoveSticker) StickerPlayers.Remove(rm);

            // Rebuild sticker list for players who have LastStickerUrl
            foreach (var player in players)
            {
                if (!string.IsNullOrEmpty(player.LastStickerUrl))
                {
                    var existing = StickerPlayers.FirstOrDefault(x => x.UserId == player.UserId);
                    if (existing != null)
                    {
                        existing.UpdateFrom(player);
                    }
                    else
                    {
                        StickerPlayers.Add(new PlayerViewModel(player));
                    }
                }
            }
        }

        // Column header click sorting ------------------------------------------------

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not GridViewColumnHeader header) return;
            if (header.Tag is not string property) return;

            // Determine which ListView this header belongs to
            var lv = FindAncestor<System.Windows.Controls.ListView>(header);
            if (lv == null) return;

            var view = lv == ActiveListView ? ActiveView : PastView;
            ToggleSort(view, property);

            // Update visual indicator
            UpdateHeaderSortIndicator(header, view, property);
        }

        private void CopyPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                // Try to find the underlying Player by UserId
                var player = _serviceRegistry.GetPlayerManager().GetPlayerByUserId(pvm.UserId);
                if (player == null)
                {
                    // Build formatted string from the viewmodel alone
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"DisplayName: {pvm.DisplayName}");
                    sb.AppendLine($"UserId: {pvm.UserId}");
                    sb.AppendLine($"GroupName: {pvm.AvatarName}");
                    sb.AppendLine($"InstanceStart: {pvm.InstanceStartTime}");
                    sb.AppendLine($"InstanceEnd: {pvm.InstanceEndTime}");
                    sb.AppendLine($"WorldId: {PlayerManager.CurrentSession?.WorldId}");
                    sb.AppendLine($"InstanceId: {PlayerManager.CurrentSession?.InstanceId}");

                    sb.AppendLine($"User Profile at Instance Start:\n");
                    sb.AppendLine($"{pvm.Profile}");
                    var text = sb.ToString();
                    System.Windows.Clipboard.SetText(text);
                    return;
                }

                System.Windows.Clipboard.SetText(player.ToString());
            }
        }

        private void EvalPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                // Try to find the underlying Player by UserId
                var player = _serviceRegistry.GetPlayerManager().GetPlayerByUserId(pvm.UserId);
                if (player != null)
                {
                    // Build formatted string from the viewmodel alone
                    var sb = new System.Text.StringBuilder();

                    sb.AppendLine($"DisplayName: {pvm.DisplayName}");
                    sb.AppendLine($"UserId: {pvm.UserId}");

                    sb.AppendLine($"Evaluation of Profile:\n");
                    sb.AppendLine($"{pvm.AIEval}");
                    var text = sb.ToString();
                    System.Windows.Clipboard.SetText(text);
                    return;
                }

                System.Windows.Clipboard.SetText("");
            }
        }

        private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void ToggleSort(ICollectionView view, string property)
        {
            // If already sorted on property, flip direction. Otherwise set ascending.
            var existing = view.SortDescriptions.FirstOrDefault(sd => sd.PropertyName == property);
            if (!existing.Equals(default(SortDescription)))
            {
                var newDir = existing.Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(property, newDir));
            }
            else
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Ascending));
            }
            view.Refresh();
        }

        private void UpdateHeaderSortIndicator(GridViewColumnHeader clickedHeader, ICollectionView view, string property)
        {
            // Clear indicators on sibling headers in same ListView
            var lv = FindAncestor<System.Windows.Controls.ListView>(clickedHeader);
            if (lv == null) return;

            var gridView = lv.View as GridView;
            if (gridView == null) return;

            foreach (var col in gridView.Columns)
            {
                if (col.Header is GridViewColumnHeader hdr && hdr != clickedHeader)
                {
                    hdr.ClearValue(GridViewColumnHeader.CursorProperty);
                }
            }

            // Set cursor as simple visual indicator on clicked header
            clickedHeader.Cursor = System.Windows.Input.Cursors.Hand;
        }

        // Filters and existing handlers ------------------------------------------------

        #region Active handlers

        private void ActiveApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(ActiveView, ActiveFilterBox.Text);
        }

        private void ActiveClearFilter_Click(object sender, RoutedEventArgs e)
        {
            ActiveFilterBox.Text = string.Empty;
            ApplyFilter(ActiveView, string.Empty);
        }

        private void ActiveFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedActive != null)
            {
                ActiveFilterBox.Text = SelectedActive.DisplayName;
                ApplyFilter(ActiveView, ActiveFilterBox.Text);
            }
        }

        #endregion

        #region Past handlers

        private void PastApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(PastView, PastFilterBox.Text);
        }

        private void PastClearFilter_Click(object sender, RoutedEventArgs e)
        {
            PastFilterBox.Text = string.Empty;
            ApplyFilter(PastView, string.Empty);
        }

        private void PastFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                PastFilterBox.Text = SelectedPast.DisplayName;
                ApplyFilter(PastView, PastFilterBox.Text);
            }
        }

        #endregion

        #region Sticker handlers

        private void StickersApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(StickerView, StickersFilterBox.Text);
        }

        private void StickersClearFilter_Click(object sender, RoutedEventArgs e)
        {
            StickersFilterBox.Text = string.Empty;
            ApplyFilter(StickerView, string.Empty);
        }

        private void StickerFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                StickersFilterBox.Text = SelectedPast.DisplayName;
                ApplyFilter(StickerView, PastFilterBox.Text);
            }
        }

        #endregion

        #region Print handlers

        private void PrintApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(PrintView, PrintFilterBox.Text);
        }

        private void PrintClearFilter_Click(object sender, RoutedEventArgs e)
        {
            PrintFilterBox.Text = string.Empty;
            ApplyFilter(PrintView, string.Empty);
        }

        private void PrintFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                PrintFilterBox.Text = SelectedPast.DisplayName;
                ApplyFilter(PrintView, PastFilterBox.Text);
            }
        }

        #endregion

        private void PrintHyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                logger.Info($"Opening print URL: {e.Uri}");
                var psi = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to open print URL");
            }
            e.Handled = true;
        }

        private void ApplyFilter(ICollectionView view, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                view.Filter = null;
                view.Refresh();
                return;
            }

            string ft = filterText.Trim();
            view.Filter = obj =>
            {
                if (obj is PlayerViewModel pvm)
                {
                    return pvm.DisplayName?.IndexOf(ft, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
                return false;
            };
            view.Refresh();
        }

        public void Dispose()
        {
            fallbackTimer.Stop();
            fallbackTimer.Tick -= FallbackTimer_Tick;
            PlayerManager.PlayerChanged -= PlayerManager_PlayerChanged;
        }

        private void GroupHyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                logger.Info($"Opening group URL: {e.Uri}");
                var uri = new Uri($"https://vrchat.com/home/group/{e.Uri}");
                var psi = new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to open group URL");
            }
            e.Handled = true;
        }
        private void AvatarHyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                logger.Info($"Opening Avatar URL: {e.Uri}");
                var uri = new Uri($"https://vrchat.com/home/avatar/{e.Uri}");
                var psi = new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to open group URL");
            }
            e.Handled = true;
        }
    }

    public class PlayerViewModel : INotifyPropertyChanged
    {
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }
        public string AvatarName { get; private set; }
        public string PenActivity { get; private set; }
        public string? LastStickerUrl { get; private set; }
        public System.Windows.Media.ImageSource? LastStickerImage { get; private set; }
        public string InstanceStartTime { get; private set; }
        public string InstanceEndTime { get; private set; }
        public string Profile { get; private set; }
        public string AIEval { get; private set; }
        public bool IsWatched { get; set; } = false;
        public string WatchCode { get; private set; } = string.Empty;
        public ObservableCollection<PrintInfoViewModel> Prints { get; private set; } = new ObservableCollection<PrintInfoViewModel>();

        public string HighlightClass
        {
            get
            {
                if (IsWatched)
                {
                    return "Alert";
                }

                return "Normal";
            }
        }

        public PlayerViewModel(Player p)
        {
            UserId = p.UserId;
            DisplayName = p.DisplayName;
            AvatarName = p.AvatarName;
            PenActivity = p.PenActivity;
            LastStickerUrl = p.LastStickerUrl;
            LastStickerImage = LoadImageFromUrl(p.LastStickerUrl);
            InstanceStartTime = p.InstanceStartTime.ToString("u");
            InstanceEndTime = p.InstanceEndTime.HasValue ? p.InstanceEndTime.Value.ToString("u") : string.Empty;
            Profile = p.UserBio ?? string.Empty;
            AIEval = p.AIEval ?? "Not Evaluated";
            IsWatched = p.IsWatched;
            WatchCode = p.WatchCode;
            // populate prints
            if (p.PrintData != null)
            {
                foreach (var pr in p.PrintData.Values)
                {
                    Prints.Add(new PrintInfoViewModel(pr));
                }
            }
        }

        public void UpdateFrom(Player p)
        {
            bool changed = false;

            if (UserId != p.UserId) { UserId = p.UserId; changed = true; }
            if (DisplayName != p.DisplayName) { DisplayName = p.DisplayName; changed = true; }
            if (AvatarName != p.AvatarName) { AvatarName = p.AvatarName; changed = true; }
            if (PenActivity != p.PenActivity) { PenActivity = p.PenActivity; changed = true; }
            if (LastStickerUrl != p.LastStickerUrl) { LastStickerUrl = p.LastStickerUrl; LastStickerImage = LoadImageFromUrl(p.LastStickerUrl); changed = true; }

            var start = p.InstanceStartTime.ToString("u");
            if (InstanceStartTime != start) { InstanceStartTime = start; changed = true; }

            var end = p.InstanceEndTime.HasValue ? p.InstanceEndTime.Value.ToString("u") : string.Empty;
            if (InstanceEndTime != end) { InstanceEndTime = end; changed = true; }
            if (Profile != (p.UserBio ?? string.Empty)) { Profile = p.UserBio ?? string.Empty; changed = true; }
            if (AIEval != (p.AIEval ?? "Not Evaluated")) { AIEval = p.AIEval ?? "Not Evaluated"; changed = true; }
            if (IsWatched != p.IsWatched) { IsWatched = p.IsWatched; changed = true; }
            if (WatchCode != p.WatchCode) { WatchCode = p.WatchCode; changed = true; }

            if (changed) OnPropertyChanged(string.Empty);
            // update prints collection
            if (p.PrintData != null)
            {
                // simple replace strategy
                Prints.Clear();
                foreach (var pr in p.PrintData.Values)
                {
                    Prints.Add(new PrintInfoViewModel(pr));
                }
            }
        }

        private System.Windows.Media.ImageSource? LoadImageFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url);
                bitmap.DecodePixelWidth = 200;
                bitmap.DecodePixelHeight = 200;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class PrintInfoViewModel
    {
        public string PrintId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PrintUrl { get; set; }
        public PrintInfoViewModel(VRChat.API.Model.Print p)
        {
            PrintId = p.Id;
            CreatedAt = p.CreatedAt;
            PrintUrl = p.Files.Image;
        }
    }


    public class AvatarInfoViewModel : INotifyPropertyChanged
    {
        public string AvatarId { get; set; }
        public string AvatarName { get; set; }
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

        public AvatarInfoViewModel(Tailgrab.Models.AvatarInfo a)
        {
            AvatarId = a.AvatarId;
            AvatarName = a.AvatarName;
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
