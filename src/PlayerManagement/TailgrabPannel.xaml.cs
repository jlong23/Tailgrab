using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Tailgrab.Config;

namespace Tailgrab.PlayerManagement
{
    public partial class TailgrabPannel : Window, IDisposable
    {
        private readonly DispatcherTimer fallbackTimer;

        public ObservableCollection<PlayerViewModel> ActivePlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> PastPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> StickerPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<AvatarInfoViewModel> AvatarDbItems { get; } = new ObservableCollection<AvatarInfoViewModel>();
        public ObservableCollection<GroupInfoViewModel> GroupDbItems { get; } = new ObservableCollection<GroupInfoViewModel>();

        public ICollectionView AvatarDbView { get; }
        public ICollectionView ActiveView { get; }
        public ICollectionView GroupDbView { get; }
        public ICollectionView PastView { get; }
        public ICollectionView StickerView { get; }

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

            AvatarDbView = CollectionViewSource.GetDefaultView(AvatarDbItems);
            AvatarDbView.SortDescriptions.Add(new SortDescription("AvatarName", ListSortDirection.Ascending));

            GroupDbView = CollectionViewSource.GetDefaultView(GroupDbItems);
            GroupDbView.SortDescriptions.Add(new SortDescription("GroupName", ListSortDirection.Ascending));

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

            // Initial load of Groups
            RefreshGroupDb();

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
                var db = _serviceRegistry.GetDBContext();
                var avatars = db.AvatarInfos.OrderBy(a => a.AvatarName).ToList();
                AvatarDbItems.Clear();
                foreach (var a in avatars)
                {
                    AvatarDbItems.Add(new AvatarInfoViewModel(a));
                }
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
                var db = _serviceRegistry.GetDBContext();
                var groups = db.GroupInfos.OrderBy(a => a.GroupName).ToList();
                GroupDbItems.Clear();
                foreach (var g in groups)
                {
                    GroupDbItems.Add(new GroupInfoViewModel(g));
                }
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

        public string HighlightClass
        {
            get
            {
                if(IsWatched)
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
            if( IsWatched != p.IsWatched) { IsWatched = p.IsWatched; changed = true; }
            if( WatchCode != p.WatchCode) { WatchCode = p.WatchCode; changed = true; }

            if (changed) OnPropertyChanged(string.Empty);
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
