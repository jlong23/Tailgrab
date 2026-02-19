using Microsoft.Win32;
using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Tailgrab.Common;
using Tailgrab.Clients.VRChat;
using Tailgrab.Models;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{
    public partial class TailgrabPanel : Window, IDisposable, INotifyPropertyChanged
    {
        private readonly DispatcherTimer fallbackTimer;
        private readonly DispatcherTimer statusBarTimer;

        public ObservableCollection<PlayerViewModel> ActivePlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> PastPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> PrintPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> EmojiPlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public AvatarVirtualizingCollection AvatarDbItems { get; private set; }
        public GroupVirtualizingCollection GroupDbItems { get; private set; }
        public UserVirtualizingCollection UserDbItems { get; private set; }

        public ICollectionView AvatarDbView { get; }
        public ICollectionView ActiveView { get; }
        public ICollectionView GroupDbView { get; }
        public ICollectionView UserDbView { get; }
        public ICollectionView PastView { get; }
        public ICollectionView PrintView { get; }
        public ICollectionView EmojiView { get; }


        private PlayerViewModel? _selectedActive;
        public PlayerViewModel? SelectedActive 
        { 
            get => _selectedActive;
            set
            {
                if (_selectedActive != value)
                {
                    _selectedActive = value;
                    OnPropertyChanged(nameof(SelectedActive));
                }
            }
        }

        private int _avatarQueueLength;
        public int AvatarQueueLength
        {
            get => _avatarQueueLength;
            set
            {
                if (_avatarQueueLength != value)
                {
                    _avatarQueueLength = value;
                    OnPropertyChanged(nameof(AvatarQueueLength));
                }
            }
        }

        private int _ollamaQueueLength;
        public int OllamaQueueLength
        {
            get => _ollamaQueueLength;
            set
            {
                if (_ollamaQueueLength != value)
                {
                    _ollamaQueueLength = value;
                    OnPropertyChanged(nameof(OllamaQueueLength));
                }
            }
        }

        private string _worldId = string.Empty;
        public string WorldId
        {
            get => _worldId;
            set
            {
                if (_worldId != value)
                {
                    _worldId = value;
                    OnPropertyChanged(nameof(WorldId));
                }
            }
        }

        private string _instanceId = string.Empty;
        public string InstanceId
        {
            get => _instanceId;
            set
            {
                if (_instanceId != value)
                {
                    _instanceId = value;
                    OnPropertyChanged(nameof(InstanceId));
                }
            }
        }

        private string _elapsedTime = "00:00:00";
        public string ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                if (_elapsedTime != value)
                {
                    _elapsedTime = value;
                    OnPropertyChanged(nameof(ElapsedTime));
                }
            }
        }

        public PlayerViewModel? SelectedPast { get; set; }
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public List<KeyValuePair<string, bool>> IsBosOptions { get; } = new List<KeyValuePair<string, bool>>
        {
            new KeyValuePair<string,bool>("YES", true),
            new KeyValuePair<string,bool>("NO", false)
        };

        public List<KeyValuePair<string, AlertTypeEnum>> AlertTypeOptions { get; } = new List<KeyValuePair<string, AlertTypeEnum>>
        {
            new KeyValuePair<string, AlertTypeEnum>("None", AlertTypeEnum.None),
            new KeyValuePair<string, AlertTypeEnum>("Watch", AlertTypeEnum.Watch),
            new KeyValuePair<string, AlertTypeEnum>("Nuisance", AlertTypeEnum.Nuisance),
            new KeyValuePair<string, AlertTypeEnum>("Crasher", AlertTypeEnum.Crasher)
        };


        public List<KeyValuePair<string, string>> AlertSoundOptions { get; set; } = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> AlertColorOptions { get; } = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("*NONE", "Normal"),
            new KeyValuePair<string, string>("Yellow", "Yellow"),
            new KeyValuePair<string, string>("Purple", "Purple"),
            new KeyValuePair<string, string>("Red", "Red"),
        };

        protected ServiceRegistry _serviceRegistry;

        public TailgrabPanel(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
            InitializeComponent();
            DataContext = this;

            // Set window title with version
            Title = $"Tailgrab {BuildInfo.GetInformationalVersion()}";

            ActiveView = CollectionViewSource.GetDefaultView(ActivePlayers);
            ActiveView.SortDescriptions.Add(new SortDescription("InstanceStartTime", ListSortDirection.Descending));
            UpdateHeaderSortIndicator(ActivePlayerInstanceStart, ActiveView, "InstanceStartTime");

            PastView = CollectionViewSource.GetDefaultView(PastPlayers);
            PastView.SortDescriptions.Add(new SortDescription("InstanceEndTime", ListSortDirection.Descending));
            UpdateHeaderSortIndicator(PastPlayerInstanceEnd, PastView, "InstanceEndTime");

            PrintView = CollectionViewSource.GetDefaultView(PrintPlayers);
            EmojiView = CollectionViewSource.GetDefaultView(EmojiPlayers);

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

            #region Secret Config Load            
            // Load saved secrets into UI fields if desired (not displayed in this view directly)
            var vrUser = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_VRChat_Web_UserName);
            var vrPass = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_VRChat_Web_Password);
            var vr2fa = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_VRChat_Web_2FactorKey);
            var ollamaKey = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Key);
            var ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint) ?? Tailgrab.Common.CommonConst.Default_Ollama_API_Endpoint;
            var ollamaProfilePrompt = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt) ?? Tailgrab.Common.CommonConst.Default_Ollama_API_Prompt;
            var ollamaImagePrompt = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt) ?? Tailgrab.Common.CommonConst.Default_Ollama_API_Image_Prompt;
            var ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model) ?? Tailgrab.Common.CommonConst.Default_Ollama_API_Model;
            var avatarGistUri = ConfigStore.GetStoredKeyString(Tailgrab.Common.CommonConst.Registry_Avatar_Gist);
            var groupGistUri = ConfigStore.GetStoredKeyString(Tailgrab.Common.CommonConst.Registry_Group_Gist);

            // Populate UI boxes but do not reveal secrets
            if (!string.IsNullOrEmpty(vrUser)) VrUserBox.Text = vrUser;
            if (!string.IsNullOrEmpty(vrPass)) VrPassBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(vr2fa)) Vr2FaBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(ollamaKey)) VrOllamaBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(ollamaEndpoint)) VrOllamaEndpointBox.Text = ollamaEndpoint;
            if (!string.IsNullOrEmpty(ollamaModel)) VrOllamaModelBox.Text = ollamaModel;
            if (!string.IsNullOrEmpty(ollamaProfilePrompt)) VrOllamaPromptBox.Text = ollamaProfilePrompt;
            if (!string.IsNullOrEmpty(ollamaImagePrompt)) VrOllamaImagePromptBox.Text = ollamaImagePrompt;

            if (!string.IsNullOrEmpty(avatarGistUri)) avatarGistUrl.Text = avatarGistUri;
            if (!string.IsNullOrEmpty(groupGistUri)) groupGistUrl.Text = groupGistUri;

            // Populate sound combo boxes
            try
            {
                var sounds = Tailgrab.Common.SoundManager.GetAvailableSounds();
                AlertSoundOptions = sounds.Select(s => new KeyValuePair<string, string>(s, s)).ToList();

                AvatarWarnSound.SelectedValue = "*NONE";
                AvatarWarnColor.SelectedValue = "Normal";
                AvatarNuisenceSound.SelectedValue = "ICQ_Uh_Oh";
                AvatarNuisenceColor.SelectedValue = "Yellow";
                AvatarCrasherSound.SelectedValue = "Police_Double_Chirping";
                AvatarCrasherColor.SelectedValue = "Red";

                GroupWarnSound.SelectedValue = "*NONE";
                GroupWarnColor.SelectedValue = "Normal";
                GroupNuisenceSound.SelectedValue = "ICQ_Uh_Oh";
                GroupNuisenceColor.SelectedValue = "Yellow";
                GroupCrasherSound.SelectedValue = "Police_Double_Chirping";
                GroupCrasherColor.SelectedValue = "Red";

                ProfileWarnSound.SelectedValue = "*NONE";
                ProfileWarnColor.SelectedValue = "Normal";
                ProfileNuisenceSound.SelectedValue = "ICQ_Uh_Oh";
                ProfileNuisenceColor.SelectedValue = "Yellow";
                ProfileCrasherSound.SelectedValue = "Police_Double_Chirping";
                ProfileCrasherColor.SelectedValue = "Red";
            }
            catch { }
            #endregion

            // Initial load of Avatars, Groups and Users
            RefreshAvatarDb();
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

            // Status bar timer to update queue lengths
            statusBarTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            statusBarTimer.Tick += StatusBarTimer_Tick;
            statusBarTimer.Start();

            this.Closed += (s, e) => Dispose();
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save to registry protected store
                ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_UserName, VrUserBox.Text ?? string.Empty);
                if (!string.IsNullOrEmpty(VrPassBox.Password)) ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_Password, VrPassBox.Password);
                if (!string.IsNullOrEmpty(Vr2FaBox.Password)) ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_2FactorKey, Vr2FaBox.Password);
                if (!string.IsNullOrEmpty(VrOllamaBox.Password)) ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Key, VrOllamaBox.Password);
                ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Endpoint, VrOllamaEndpointBox.Text ?? CommonConst.Default_Ollama_API_Endpoint);
                ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Prompt, VrOllamaPromptBox.Text ?? CommonConst.Default_Ollama_API_Prompt);
                ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Image_Prompt, VrOllamaImagePromptBox.Text ?? CommonConst.Default_Ollama_API_Image_Prompt);
                ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Model, VrOllamaModelBox.Text ?? CommonConst.Default_Ollama_API_Model);

                ConfigStore.PutStoredKeyString(Common.CommonConst.Registry_Avatar_Gist, avatarGistUrl.Text);
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Group_Gist, groupGistUrl.Text);

                System.Windows.MessageBox.Show("Configuration saved. Restart the Applicaton for all changes to take affect.", "Config", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SaveAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Avatar Alerts
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key, (string)AvatarWarnSound.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key, (string)AvatarNuisenceSound.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key, (string)AvatarCrasherSound.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key, (string)AvatarWarnColor.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key, (string)AvatarNuisenceColor.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key, (string)AvatarCrasherColor.SelectedValue);

                // Group Alerts
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key, (string)GroupWarnSound.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key, (string)GroupNuisenceSound.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key, (string)GroupCrasherSound.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key, (string)GroupWarnColor.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key, (string)GroupNuisenceColor.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key, (string)GroupCrasherColor.SelectedValue);

                // Group Alerts
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key, (string)ProfileWarnSound.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key, (string)ProfileNuisenceSound.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key, (string)ProfileCrasherSound.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key, (string)ProfileWarnColor.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key, (string)ProfileNuisenceColor.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key, (string)ProfileCrasherColor.SelectedValue);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetAlertKeyString(string alertKey, AlertTypeEnum alertType, string subType, object value)
        { 
            string key = CommonConst.ConfigRegistryPath + "\\" + alertKey + "\\" + alertType.ToString();

            if (value is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                ConfigStore.PutStoredKeyString(key, subType, stringValue);
            }
            else
            {
                ConfigStore.RemoveStoredKeyString(key, subType);
            }

        }


        private void StatusBarTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var avatarManager = _serviceRegistry.GetAvatarManager();
                var ollamaClient = _serviceRegistry.GetOllamaAPIClient();

                AvatarQueueLength = avatarManager?.GetQueueCount() ?? 0;
                OllamaQueueLength = ollamaClient?.GetQueueSize() ?? 0;

                // Update session info
                var currentSession = PlayerManager.CurrentSession;
                if (currentSession != null)
                {
                    // Check if session changed
                    if (WorldId != currentSession.WorldId || InstanceId != currentSession.InstanceId)
                    {
                        WorldId = currentSession.WorldId ?? string.Empty;
                        InstanceId = currentSession.InstanceId ?? string.Empty;                       
                    }

                    // Update elapsed time
                    var elapsed = DateTime.Now - currentSession.startDateTime;
                    int hours = (int)elapsed.TotalHours;
                    int minutes = elapsed.Minutes;
                    int seconds = elapsed.Seconds;
                    ElapsedTime = $"{hours:D3}:{minutes:D2}:{seconds:D2}";
                } 
                else
                {
                    ElapsedTime = "000:00:00";
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating status bar");
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
                    PrintPlayers.Clear();
                    EmojiPlayers.Clear();
                    break;
            }
        }

        private void AddOrUpdatePlayer(Player p)
        {
            PlayerViewModel? vm = ActivePlayers.FirstOrDefault(x => x.UserId == p.UserId);
            PlayerViewModel? vmPast = PastPlayers.FirstOrDefault(x => x.UserId == p.UserId);
            PlayerViewModel? vmPrint = PrintPlayers.FirstOrDefault(x => x.UserId == p.UserId);
            PlayerViewModel? vmEmoji = EmojiPlayers.FirstOrDefault(x => x.UserId == p.UserId);

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

                // If player has Emojis, add/update emoji list
                if (p.Inventory != null && p.Inventory.Count > 0)
                {
                    if (vmEmoji != null)
                    {
                        vmEmoji.UpdateFrom(p);
                    }
                    else
                    {
                        EmojiPlayers.Add(new PlayerViewModel(p));
                    }
                }
            }
            else
            {
                if (vm != null)
                {
                    PastPlayers.Add(vm);
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

            var olderThan = DateTime.Now.AddMinutes(-15);
            List<PlayerViewModel> toRemove = new List<PlayerViewModel>();
            foreach (PlayerViewModel oldPlayer in PastPlayers)
            {
                if (!string.IsNullOrEmpty(oldPlayer.InstanceEndTime))
                {
                    if (DateTime.TryParseExact(oldPlayer.InstanceEndTime, "u", null, System.Globalization.DateTimeStyles.None, out var endTime))
                    {
                        if (endTime < olderThan)
                        {
                            logger.Debug($"Removing old past player {oldPlayer.DisplayName} as {endTime} is older than cutoff {olderThan}");
                            toRemove.Add(oldPlayer);
                        }
                    }
                }
            }
            foreach (PlayerViewModel oldPlayer in toRemove)
            {
                PastPlayers.Remove(oldPlayer);
                PrintPlayers.Remove(oldPlayer);
                EmojiPlayers.Remove(oldPlayer);
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
                    sb.AppendLine($"Evaluation of Profile:\n");
                    sb.AppendLine($"{pvm.AIEval}");

                    System.Windows.Clipboard.SetText(text);
                    return;
                }

                System.Windows.Clipboard.SetText(player.ToString());
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
            ListSortDirection newDir;

            if (!existing.Equals(default(SortDescription)))
            {
                newDir = existing.Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(property, newDir));
            }
            else
            {
                newDir = ListSortDirection.Ascending;
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(property, newDir));
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

            // Get the current sort direction for the property
            var sortDesc = view.SortDescriptions.FirstOrDefault(sd => sd.PropertyName == property);

            // Clear all sort indicators first
            foreach (var col in gridView.Columns)
            {
                if (col.Header is GridViewColumnHeader hdr)
                {
                    // Remove any existing sort indicator from the content
                    string content = hdr.Content?.ToString() ?? string.Empty;
                    content = content.Replace(" ▲", "").Replace(" ▼", "").Trim();
                    hdr.Content = content;
                    hdr.Cursor = null;
                }
            }

            // Add sort indicator to the clicked header
            if (!sortDesc.Equals(default(SortDescription)))
            {
                string headerText = clickedHeader.Content?.ToString() ?? string.Empty;
                headerText = headerText.Replace(" ▲", "").Replace(" ▼", "").Trim();

                string indicator = sortDesc.Direction == ListSortDirection.Ascending ? " ▲" : " ▼";
                clickedHeader.Content = headerText + indicator;
                clickedHeader.Cursor = System.Windows.Input.Cursors.Hand;        
            }
        }

        //
        // Active Handlers
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

        private void ReportPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                string userId = pvm.UserId;
                try
                {
                    ShowProfileReportOverlay(userId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to open Report Profile overlay");
                    System.Windows.MessageBox.Show($"Failed to open Report Profile overlay: {ex.Message}",
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ShowProfileReportOverlay(string userId)
        {
            // Populate the overlay fields
            OverlayProfileReportUserIdTextBox.Text = userId.Trim();

            // Setup report reasons for profile (includes Child Exploitation)
            var profileReportReasons = new List<ReportReasonItem>
            {
                new ReportReasonItem("Sexual Content", "sexual"),
                new ReportReasonItem("Hateful Content", "hateful"),
                new ReportReasonItem("Gore and Violence", "gore"),
                new ReportReasonItem("Child Exploitation", "child"),
                new ReportReasonItem("Other", "other")
            };

            OverlayProfileReportReasonComboBox.ItemsSource = profileReportReasons;
            OverlayProfileReportReasonComboBox.SelectedIndex = 0;

            if (string.IsNullOrEmpty(userId))
            {
                OverlayProfileReportDescriptionTextBox.Text = string.Empty;
            }
            else
            {
                try
                {
                    // Get the player from PlayerManager
                    Player? player = _serviceRegistry.GetPlayerManager().GetPlayerByUserId(userId);

                    if (player != null && !string.IsNullOrEmpty(player.AIEval))
                    {
                        OverlayProfileReportDescriptionTextBox.Text = player.AIEval;
                        logger.Debug($"Loaded AI evaluation for user: {userId}");
                    }
                    else
                    {
                        OverlayProfileReportDescriptionTextBox.Text = "No AI evaluation available for this user.";
                        logger.Debug($"No AI evaluation found for user: {userId}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error loading AI evaluation for user: {userId}");
                    OverlayProfileReportDescriptionTextBox.Text = $"Error loading AI evaluation: {ex.Message}";
                }
            }

            // Clear any validation errors
            ClearProfileReportValidationErrors();

            // Show the overlay
            OverlayProfileReport.Visibility = Visibility.Visible;
        }

        private void ClearProfileReportValidationErrors()
        {
            // Reset UserID field
            OverlayProfileReportUserIdTextBox.BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
            OverlayProfileReportUserIdTextBox.BorderThickness = new Thickness(1);
            OverlayProfileReportUserIdError.Visibility = Visibility.Collapsed;
        }

        private bool ValidateProfileReportFields()
        {
            bool isValid = true;
            
            // Clear any previous validation errors first
            ClearProfileReportValidationErrors();
            
            // Validate User ID
            if (string.IsNullOrWhiteSpace(OverlayProfileReportUserIdTextBox.Text))
            {
                OverlayProfileReportUserIdTextBox.BorderBrush = new SolidColorBrush(Colors.Yellow);
                OverlayProfileReportUserIdTextBox.BorderThickness = new Thickness(3);
                OverlayProfileReportUserIdError.Visibility = Visibility.Visible;
                isValid = false;
            }
            
            return isValid;
        }

        private void OverlayProfileReportCancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            OverlayProfileReport.Visibility = Visibility.Collapsed;
            
            // Clear the fields
            OverlayProfileReportUserIdTextBox.Text = string.Empty;
            OverlayProfileReportDescriptionTextBox.Text = string.Empty;
            
            // Clear validation errors
            ClearProfileReportValidationErrors();
        }

        private async void OverlayProfileReportSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (!ValidateProfileReportFields())
                {
                    return;
                }

                string userId = OverlayProfileReportUserIdTextBox.Text.Trim();
                string category = OverlayProfileReportCategoryTextBox.Text;
                string reportReason = OverlayProfileReportReasonComboBox.SelectedValue?.ToString() ?? string.Empty;
                string reportDescription = OverlayProfileReportDescriptionTextBox.Text;

                // Disable the submit button to prevent double-submission
                OverlayProfileReportSubmitButton.IsEnabled = false;

                // Call the method that will handle the future web service call
                bool success = await SubmitProfileReport(userId, category, reportReason, reportDescription);

                // Show success message
                if (!success)                
                {
                    System.Windows.MessageBox.Show("Failed to submit report. Please try again later.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    OverlayProfileReportSubmitButton.IsEnabled = true;
                    return;
                }

                // Hide the overlay
                OverlayProfileReport.Visibility = Visibility.Collapsed;

                // Clear the fields
                OverlayProfileReportUserIdTextBox.Text = string.Empty;
                OverlayProfileReportDescriptionTextBox.Text = string.Empty;

                // Clear validation errors
                ClearProfileReportValidationErrors();

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to submit profile report");
                System.Windows.MessageBox.Show($"Failed to submit report: {ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                OverlayProfileReportSubmitButton.IsEnabled = true;
            }
        }

        private async Task<bool> SubmitProfileReport(string userId, string category, string reportReason, string reportDescription)
        {
            ModerationReportPayload rpt = new ModerationReportPayload();
            rpt.Type = "user";
            rpt.Category = "profile";
            rpt.Reason = reportReason;
            rpt.ContentId = userId;
            rpt.Description = reportDescription;

            ModerationReportDetails rptDtls = new ModerationReportDetails();
            rptDtls.InstanceType = "Group Public";
            rptDtls.InstanceAgeGated = false;
            rpt.Details = new List<ModerationReportDetails>() { rptDtls };

            bool success = await _serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (success)
            {
                logger.Info($"Profile Report submitted - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            else
            {
                logger.Warn($"Failed to submit profile report - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            return success;
        }
        #endregion

        //
        // Past Handlers
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

        //
        // Print Handlers
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
                logger.Error(ex, "Failed to open print URL");
            }
            e.Handled = true;
        }

        private void ReportPrintInventory_Click(object sender, RoutedEventArgs e)
        {
            PrintOverlayUserIdTextBox.Text = string.Empty;
            PrintOverlayInventoryIdTextBox.Text = string.Empty;
            PrintOverlayCategoryTextBox.Text = string.Empty;
            PrintOverlayReportDescriptionTextBox.Text = string.Empty;
            PrintOverlayReportReasonComboBox.ItemsSource = ReportReasons;

            // Clear any validation errors
            ClearPrintOverlayValidationErrors();

            // Show the overlay
            ReportPrintInventoryOverlay.Visibility = Visibility.Visible;
        }

        private void ReportPrintInventoryItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is PrintInfoViewModel print)
                {
                    ShowReportPrintOverlay(print);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open Report Print Item overlay");
                System.Windows.MessageBox.Show($"Failed to open Report Print Item overlay: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ShowReportPrintOverlay(PrintInfoViewModel print)
        {
            // Populate the overlay fields
            PrintOverlayUserIdTextBox.Text = print.OwnerId;
            PrintOverlayInventoryIdTextBox.Text = print.PrintId;
            PrintOverlayCategoryTextBox.Text = "print";
            PrintOverlayReportDescriptionTextBox.Text = print.AIEvaluation ?? string.Empty;

            PrintOverlayReportReasonComboBox.ItemsSource = ReportReasons;
            PrintOverlayReportReasonComboBox.SelectedIndex = 0;

            // Load the image
            if (!string.IsNullOrEmpty(print.PrintUrl))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(print.PrintUrl);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    PrintOverlayInventoryImagePreview.Source = bitmap;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to load Print image");
                }
            }

            // Clear any validation errors
            ClearPrintOverlayValidationErrors();

            // Show the overlay
            ReportPrintInventoryOverlay.Visibility = Visibility.Visible;
        }

        private void PrintOverlayCancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            ReportPrintInventoryOverlay.Visibility = Visibility.Collapsed;

            // Clear the fields
            PrintOverlayUserIdTextBox.Text = string.Empty;
            PrintOverlayInventoryIdTextBox.Text = string.Empty;
            PrintOverlayCategoryTextBox.Text = string.Empty;
            PrintOverlayReportDescriptionTextBox.Text = string.Empty;
            PrintOverlayInventoryImagePreview.Source = null;

            // Clear validation errors
            ClearPrintOverlayValidationErrors();
        }

        private void ClearPrintOverlayValidationErrors()
        {
            // Reset UserID field
            PrintOverlayUserIdTextBox.BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
            PrintOverlayUserIdTextBox.BorderThickness = new Thickness(1);
            PrintOverlayUserIdError.Visibility = Visibility.Collapsed;

            // Reset InventoryID field
            PrintOverlayInventoryIdTextBox.BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
            PrintOverlayInventoryIdTextBox.BorderThickness = new Thickness(1);
            PrintOverlayInventoryIdError.Visibility = Visibility.Collapsed;
        }

        private bool ValidatePrintOverlayFields()
        {
            bool isValid = true;

            // Clear any previous validation errors first
            ClearPrintOverlayValidationErrors();

            // Validate User ID
            if (string.IsNullOrWhiteSpace(PrintOverlayUserIdTextBox.Text))
            {
                PrintOverlayUserIdTextBox.BorderBrush = new SolidColorBrush(Colors.Yellow);
                PrintOverlayUserIdTextBox.BorderThickness = new Thickness(3);
                PrintOverlayUserIdError.Visibility = Visibility.Visible;
                isValid = false;
            }

            // Validate Inventory ID
            if (string.IsNullOrWhiteSpace(PrintOverlayInventoryIdTextBox.Text))
            {
                PrintOverlayInventoryIdTextBox.BorderBrush = new SolidColorBrush(Colors.Yellow);
                PrintOverlayInventoryIdTextBox.BorderThickness = new Thickness(3);
                PrintOverlayInventoryIdError.Visibility = Visibility.Visible;
                isValid = false;
            }

            return isValid;
        }

        private async void PrintOverlaySubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (!ValidatePrintOverlayFields())
                {
                    return;
                }

                var userId = PrintOverlayUserIdTextBox.Text;
                var category = PrintOverlayCategoryTextBox.Text;
                var inventoryId = PrintOverlayInventoryIdTextBox.Text;
                var reason = PrintOverlayReportReasonComboBox.SelectedValue?.ToString() ?? string.Empty;
                var description = PrintOverlayReportDescriptionTextBox.Text;

                // Call the method that will handle the future web service call
                bool success = await SubmitPrintReport(userId, inventoryId, category, reason, description);

                // Show success message
                if (!success)
                {
                    System.Windows.MessageBox.Show("Failed to submit report. Please try again later.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    PrintOverlaySubmitButton.IsEnabled = true;
                    return;
                }

                // Disable the submit button to prevent double-submission
                PrintOverlaySubmitButton.IsEnabled = false;

                // Hide the overlay
                ReportPrintInventoryOverlay.Visibility = Visibility.Collapsed;

                // Clear the fields
                PrintOverlayUserIdTextBox.Text = string.Empty;
                PrintOverlayInventoryIdTextBox.Text = string.Empty;
                PrintOverlayCategoryTextBox.Text = string.Empty;
                PrintOverlayReportDescriptionTextBox.Text = string.Empty;
                PrintOverlayInventoryImagePreview.Source = null;

                // Clear validation errors
                ClearPrintOverlayValidationErrors();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to submit print report");
                System.Windows.MessageBox.Show($"Failed to submit report: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                PrintOverlaySubmitButton.IsEnabled = true;
            }
        }

        private async Task<bool> SubmitPrintReport(string userId, string printId, string category, string reportReason, string reportDescription)
        {
            ModerationReportPayload rpt = new ModerationReportPayload();
            rpt.Type = category;
            rpt.Category = category;
            rpt.Reason = reportReason;
            rpt.ContentId = printId;
            rpt.Description = reportDescription;

            ModerationReportDetails rptDtls = new ModerationReportDetails();
            rptDtls.InstanceType = "Group Public";
            rptDtls.InstanceAgeGated = false;
            rptDtls.HolderId = userId;
            rpt.Details = new List<ModerationReportDetails>() { rptDtls };

            bool success = await _serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (success)
            {
                logger.Info($"Print Report submitted - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            else
            {
                logger.Warn($"Failed to submit Print Report - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            return success;
        }

        private async void OverlayPrintOnInputFieldChanged(object sender, TextChangedEventArgs e)
        {
            string inventoryId = PrintOverlayInventoryIdTextBox.Text.Trim();

            if (string.IsNullOrEmpty(inventoryId))
            {
                return;
            }

            try
            {
                // Clear the fields
                PrintOverlayCategoryTextBox.Text = "Print";
                PrintOverlayUserIdTextBox.Text = string.Empty;
                PrintOverlayReportDescriptionTextBox.Text = "...Checking Print Record";
                PrintOverlayInventoryImagePreview.Source = null;

                Print? printInfo = _serviceRegistry.GetVRChatAPIClient().GetPrintInfo(inventoryId);
                if (printInfo != null)
                {
                    PrintOverlayUserIdTextBox.Text = printInfo.OwnerId;
                    PrintOverlayInventoryIdTextBox.Text = printInfo.Id;

                    List<string> imageUrls = new List<string>();

                    if (!string.IsNullOrEmpty(printInfo.Files.Image))
                    {
                        imageUrls.Add(printInfo.Files.Image);
                    }

                    if (imageUrls.Count > 0)
                    {
                        // Load and display the image
                        LoadPrintImage(imageUrls.First());

                        await LoadPrintEvaluation(printInfo.Id, printInfo.OwnerId, imageUrls);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error retrieving Print info for Print ID: {inventoryId}");
            }
        }

        private async Task LoadPrintEvaluation(string inventoryId, string userId, List<string> imageUrlList)
        {
            try
            {
                // Check if evaluation already exists in the database
                TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                ImageEvaluation? imageEvaluation = dbContext.ImageEvaluations.Find(inventoryId);

                if (imageEvaluation != null)
                {
                    // Load existing evaluation
                    PrintOverlayReportDescriptionTextBox.Text = System.Text.Encoding.UTF8.GetString(imageEvaluation.Evaluation);
                    logger.Debug($"Loaded existing image evaluation for inventory ID: {inventoryId}");
                }
                else
                {
                    // Call Ollama to classify the image
                    PrintOverlayReportDescriptionTextBox.Text = "Loading AI evaluation...";

                    string? classification = await _serviceRegistry.GetOllamaAPIClient().ClassifyImageList(userId, inventoryId, imageUrlList);

                    if (!string.IsNullOrEmpty(classification))
                    {
                        PrintOverlayReportDescriptionTextBox.Text = classification;
                        logger.Debug($"Generated new image evaluation for inventory ID: {inventoryId}");
                    }
                    else
                    {
                        PrintOverlayReportDescriptionTextBox.Text = "Failed to generate AI evaluation.";
                        logger.Warn($"Failed to generate AI evaluation for inventory ID: {inventoryId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error loading image evaluation for inventory ID: {inventoryId}");
                PrintOverlayReportDescriptionTextBox.Text = $"Error: {ex.Message}";
            }
        }

        private void LoadPrintImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    PrintOverlayInventoryImagePreview.Source = null;
                    return;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.DecodePixelWidth = 200;
                bitmap.DecodePixelHeight = 200;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                //bitmap.Freeze();

                PrintOverlayInventoryImagePreview.Source = bitmap;
                logger.Debug($"Loaded image from URL: {imageUrl}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load image from URL: {imageUrl}");
                PrintOverlayInventoryImagePreview.Source = null;
            }
        }
        #endregion

        //
        // Emoji Handlers
        #region Emoji handlers
        public List<ReportReasonItem> ReportReasons { get; } = new List<ReportReasonItem>
        {
            new ReportReasonItem("Sexual Content", "sexual"),
            new ReportReasonItem("Hateful Content", "hateful"),
            new ReportReasonItem("Gore and Violence", "gore"),
            new ReportReasonItem("Other", "other")
        };

        private void EmojiApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter(EmojiView, EmojiFilterBox.Text);
        }

        private void EmojiClearFilter_Click(object sender, RoutedEventArgs e)
        {
            EmojiFilterBox.Text = string.Empty;
            ApplyFilter(EmojiView, string.Empty);
        }

        private void EmojiFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                EmojiFilterBox.Text = SelectedPast.DisplayName;
                ApplyFilter(EmojiView, PastFilterBox.Text);
            }
        }

        private void ReportInventory_Click(object sender, RoutedEventArgs e)
        {
            OverlayUserIdTextBox.Text = string.Empty;
            OverlayInventoryIdTextBox.Text = string.Empty;
            OverlayCategoryTextBox.Text = string.Empty;
            OverlayReportDescriptionTextBox.Text = string.Empty;
            OverlayReportReasonComboBox.ItemsSource = ReportReasons;

            // Clear any validation errors
            ClearOverlayValidationErrors();

            // Show the overlay
            ReportInventoryOverlay.Visibility = Visibility.Visible;
        }

        private void ReportInventoryItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is EmojiInfoViewModel emoji)
                {
                    ShowReportInventoryOverlay(emoji);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open Report Inventory Item overlay");
                System.Windows.MessageBox.Show($"Failed to open Report Inventory Item overlay: {ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ShowReportInventoryOverlay(EmojiInfoViewModel emoji)
        {
            // Populate the overlay fields
            OverlayUserIdTextBox.Text = emoji.UserId;
            OverlayInventoryIdTextBox.Text = emoji.InventoryId;
            OverlayCategoryTextBox.Text = emoji.InventoryType;
            OverlayReportDescriptionTextBox.Text = emoji.AIEvalutation ?? string.Empty;

            OverlayReportReasonComboBox.ItemsSource = ReportReasons;
            OverlayReportReasonComboBox.SelectedIndex = 0;

            // Load the image
            if (!string.IsNullOrEmpty(emoji.ImageUrl))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(emoji.ImageUrl);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    OverlayInventoryImagePreview.Source = bitmap;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to load inventory image");
                }
            }

            // Clear any validation errors
            ClearOverlayValidationErrors();

            // Show the overlay
            ReportInventoryOverlay.Visibility = Visibility.Visible;
        }

        private void OverlayCancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            ReportInventoryOverlay.Visibility = Visibility.Collapsed;
            
            // Clear the fields
            OverlayUserIdTextBox.Text = string.Empty;
            OverlayInventoryIdTextBox.Text = string.Empty;
            OverlayCategoryTextBox.Text = string.Empty;
            OverlayReportDescriptionTextBox.Text = string.Empty;
            OverlayInventoryImagePreview.Source = null;
            
            // Clear validation errors
            ClearOverlayValidationErrors();
        }

        private void ClearOverlayValidationErrors()
        {
            // Reset UserID field
            OverlayUserIdTextBox.BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
            OverlayUserIdTextBox.BorderThickness = new Thickness(1);
            OverlayUserIdError.Visibility = Visibility.Collapsed;
            
            // Reset InventoryID field
            OverlayInventoryIdTextBox.BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
            OverlayInventoryIdTextBox.BorderThickness = new Thickness(1);
            OverlayInventoryIdError.Visibility = Visibility.Collapsed;
        }

        private bool ValidateOverlayFields()
        {
            bool isValid = true;
            
            // Clear any previous validation errors first
            ClearOverlayValidationErrors();
            
            // Validate User ID
            if (string.IsNullOrWhiteSpace(OverlayUserIdTextBox.Text))
            {
                OverlayUserIdTextBox.BorderBrush = new SolidColorBrush(Colors.Yellow);
                OverlayUserIdTextBox.BorderThickness = new Thickness(3);
                OverlayUserIdError.Visibility = Visibility.Visible;
                isValid = false;
            }
            
            // Validate Inventory ID
            if (string.IsNullOrWhiteSpace(OverlayInventoryIdTextBox.Text))
            {
                OverlayInventoryIdTextBox.BorderBrush = new SolidColorBrush(Colors.Yellow);
                OverlayInventoryIdTextBox.BorderThickness = new Thickness(3);
                OverlayInventoryIdError.Visibility = Visibility.Visible;
                isValid = false;
            }
            
            return isValid;
        }

        private async void OverlaySubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (!ValidateOverlayFields())
                {
                    return;
                }

                var userId = OverlayUserIdTextBox.Text;
                var category = OverlayCategoryTextBox.Text;
                var inventoryId = OverlayInventoryIdTextBox.Text;
                var reason = OverlayReportReasonComboBox.SelectedValue?.ToString() ?? string.Empty;
                var description = OverlayReportDescriptionTextBox.Text;

                // Call the method that will handle the future web service call
                bool success = await SubmitInventoryReport(userId, inventoryId, category, reason, description);

                // Show success message
                if (!success)
                {
                    System.Windows.MessageBox.Show("Failed to submit report. Please try again later.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    OverlaySubmitButton.IsEnabled = true;
                    return;
                }

                // Disable the submit button to prevent double-submission
                OverlaySubmitButton.IsEnabled = false;

                // Hide the overlay
                ReportInventoryOverlay.Visibility = Visibility.Collapsed;

                // Clear the fields
                OverlayUserIdTextBox.Text = string.Empty;
                OverlayInventoryIdTextBox.Text = string.Empty;
                OverlayCategoryTextBox.Text = string.Empty;
                OverlayReportDescriptionTextBox.Text = string.Empty;
                OverlayInventoryImagePreview.Source = null;
                
                // Clear validation errors
                ClearOverlayValidationErrors();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to submit inventory report");
                System.Windows.MessageBox.Show($"Failed to submit report: {ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                OverlaySubmitButton.IsEnabled = true;
            }
        }

        private async Task<bool> SubmitInventoryReport(string userId, string inventoryId, string category, string reportReason, string reportDescription)
        {
            ModerationReportPayload rpt = new ModerationReportPayload();
            rpt.Type = category.ToLower();
            rpt.Category = category.ToLower();
            rpt.Reason = reportReason;
            rpt.ContentId = inventoryId;
            rpt.Description = reportDescription;

            ModerationReportDetails rptDtls = new ModerationReportDetails();
            rptDtls.InstanceType = "Group Public";
            rptDtls.InstanceAgeGated = false;
            rptDtls.HolderId = userId;
            rpt.Details = new List<ModerationReportDetails>() { rptDtls };

            bool success = await _serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (success)
            {
                logger.Info($"Inventory Report submitted - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            else
            {
                logger.Warn($"Failed to submit Inventory Report - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            return success;
        }

        private async void OverlayOnInputFieldChanged(object sender, TextChangedEventArgs e)
        {
            string userId = OverlayUserIdTextBox.Text.Trim();
            string inventoryId = OverlayInventoryIdTextBox.Text.Trim();

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(inventoryId))
            {
                return;
            }

            try
            {
                VRChatInventoryItem? inventoryItem = await _serviceRegistry.GetVRChatAPIClient().GetUserInventoryItem(userId, inventoryId);
                if (inventoryItem != null)
                {
                    if (inventoryItem.ItemTypeLabel.Equals("Emoji", StringComparison.OrdinalIgnoreCase))
                    {
                        OverlayCategoryTextBox.Text = "Emoji";
                    }
                    else if (inventoryItem.ItemTypeLabel.Equals("Sticker", StringComparison.OrdinalIgnoreCase))
                    {
                        OverlayCategoryTextBox.Text = "Sticker";
                    }
                    else
                    {
                        OverlayCategoryTextBox.Text = "Unknown";
                    }

                    List<string> imageUrls = new List<string>();

                    if (!string.IsNullOrEmpty(inventoryItem.Metadata?.ImageUrl))
                    {
                        imageUrls.Add(inventoryItem.Metadata.ImageUrl);
                    }

                    if ( !string.IsNullOrEmpty(inventoryItem.ImageUrl))
                    {
                        imageUrls.Add(inventoryItem.ImageUrl);
                    }

                    if( imageUrls.Count > 0)
                    {
                        // Load and display the image
                        LoadImage(imageUrls.First());

                        await LoadImageEvaluation(inventoryId, userId, imageUrls );
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error retrieving inventory item for User ID: {userId}, Inventory ID: {inventoryId}");
            }
        }

        private async Task LoadImageEvaluation(string inventoryId, string userId, List<string> imageUrlList)
        {
            try
            {
                // Check if evaluation already exists in the database
                TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                ImageEvaluation? imageEvaluation = dbContext.ImageEvaluations.Find(inventoryId);

                if (imageEvaluation != null)
                {
                    // Load existing evaluation
                    OverlayReportDescriptionTextBox.Text = System.Text.Encoding.UTF8.GetString(imageEvaluation.Evaluation);
                    logger.Debug($"Loaded existing image evaluation for inventory ID: {inventoryId}");
                }
                else
                {
                    // Call Ollama to classify the image
                    OverlayReportDescriptionTextBox.Text = "Loading AI evaluation...";

                    string? classification = await _serviceRegistry.GetOllamaAPIClient().ClassifyImageList(userId, inventoryId, imageUrlList);

                    if (!string.IsNullOrEmpty(classification))
                    {
                        OverlayReportDescriptionTextBox.Text = classification;
                        logger.Debug($"Generated new image evaluation for inventory ID: {inventoryId}");
                    }
                    else
                    {
                        OverlayReportDescriptionTextBox.Text = "Failed to generate AI evaluation.";
                        logger.Warn($"Failed to generate AI evaluation for inventory ID: {inventoryId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error loading image evaluation for inventory ID: {inventoryId}");
                OverlayReportDescriptionTextBox.Text = $"Error: {ex.Message}";
            }
        }

        private void LoadImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    OverlayInventoryImagePreview.Source = null;
                    return;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.DecodePixelWidth = 200;
                bitmap.DecodePixelHeight = 200;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                //bitmap.Freeze();

                OverlayInventoryImagePreview.Source = bitmap;
                logger.Debug($"Loaded image from URL: {imageUrl}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load image from URL: {imageUrl}");
                OverlayInventoryImagePreview.Source = null;
            }
        }

        private void EmojiHyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                logger.Info($"Opening group URL: {e.Uri}");
                var uri = new Uri($"{e.Uri}");
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
        #endregion

        //
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
            // Push filter to database for better performance
            if (string.IsNullOrWhiteSpace(filterText))
            {
                AvatarDbItems.SetFilter(null);
            }
            else
            {
                AvatarDbItems.SetFilter(filterText.Trim());
            }
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

        private async void AvatarDbGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is AvatarInfoViewModel vm)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();
                    var entity = db.AvatarInfos.Find(vm.AvatarId);
                    if (entity != null)
                    {
                        entity.AlertType = vm.AlertType;
                        entity.UpdatedAt = DateTime.UtcNow;
                        db.AvatarInfos.Update(entity);
                        db.SaveChanges();
                        vm.UpdatedAt = entity.UpdatedAt;

                        if( vm.AlertType >= AlertTypeEnum.Nuisance )
                        {
                            await _serviceRegistry.GetVRChatAPIClient().BlockAvatarGlobal(vm.AvatarId);
                        } else
                        {
                            await _serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(vm.AvatarId);
                        }
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
                            IsBos = false,
                            AlertType = AlertTypeEnum.None
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
        #endregion

        //
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
            // Push filter to database for better performance
            if (string.IsNullOrWhiteSpace(filterText))
            {
                GroupDbItems.SetFilter(null);
            }
            else
            {
                GroupDbItems.SetFilter(filterText.Trim());
            }
        }

        private void RefreshGroupDb()
        {
            try
            {
                GroupDbItems.Refresh();
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
                        entity.AlertType = vm.AlertType;
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

            GroupInfo? existing = _serviceRegistry.GetPlayerManager().AddUpdateGroupFromVRC(id);

            if (existing == null)
            {
                System.Windows.MessageBox.Show($"Group {id} not found via VRChat API.", "Fetch Group", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Filter the view to the fetched Group
                ApplyGroupDbFilter(GroupDbView, existing.GroupName ?? string.Empty);
                GroupIdBox.Text = string.Empty;
            }
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
        #endregion

        //
        // User DB UI handlers
        #region User DB handlers
        private void UserDbRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshUserDb();
        }

        private void RefreshUserDb()
        {
            try
            {
                UserDbItems?.Refresh();
            }
            catch { }
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
            // Push filter to database for better performance
            if (string.IsNullOrWhiteSpace(filterText))
            {
                UserDbItems.SetFilter(null);
            }
            else
            {
                UserDbItems.SetFilter(filterText.Trim());
            }
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            fallbackTimer.Stop();
            fallbackTimer.Tick -= FallbackTimer_Tick;

            statusBarTimer.Stop();
            statusBarTimer.Tick -= StatusBarTimer_Tick;

            PlayerManager.PlayerChanged -= PlayerManager_PlayerChanged;
        }
    }

    #region ViewModels
    public class PlayerViewModel : INotifyPropertyChanged
    {
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }
        public string AvatarName { get; private set; }
        public string PenActivity { get; private set; }
        public string? LastStickerUrl { get; private set; }
        public string? LastStickerImageUrl { get; private set; }
        public string InstanceStartTime { get; private set; }
        public string InstanceEndTime { get; private set; }
        public string Profile { get; private set; }
        public string AIEval { get; private set; }
        public bool IsWatched { get; set; } = false;
        public string History { get; set; } = string.Empty;
        public string AlertMessages { get; set; } = string.Empty;

        private string _AlertColor = "Normal";

        public ObservableCollection<PrintInfoViewModel> Prints { get; private set; } = new ObservableCollection<PrintInfoViewModel>();
        public ObservableCollection<EmojiInfoViewModel> Emojis { get; private set; } = new ObservableCollection<EmojiInfoViewModel>();

        public string HighlightClass
        {
            get
            {
                return _AlertColor;
            }
        }

        public PlayerViewModel(Player p)
        {
            UserId = p.UserId;
            DisplayName = p.DisplayName;
            AvatarName = p.AvatarName;
            PenActivity = p.PenActivity;
            LastStickerUrl = p.LastStickerUrl;
            LastStickerImageUrl = p.LastStickerUrl;
            InstanceStartTime = p.InstanceStartTime.ToString("u");
            InstanceEndTime = p.InstanceEndTime.HasValue ? p.InstanceEndTime.Value.ToString("u") : string.Empty;
            Profile = p.UserBio ?? string.Empty;
            AIEval = p.AIEval ?? "Not Evaluated";
            IsWatched = p.IsWatched;
            AlertMessages = string.Empty;            

            // populate prints
            if (p.PrintData != null)
            {
                foreach (var pr in p.PrintData.Values)
                {
                    Prints.Add(new PrintInfoViewModel(pr));
                }
            }

            if (p.Inventory != null)
            {
                foreach (var inv in p.Inventory)
                {
                    Emojis.Add(new EmojiInfoViewModel(p.UserId, inv));
                }
            }

            string history = string.Empty;
            foreach (var hist in p.Events)
            {
                history += $"({hist.EventTime:u} - {hist.EventDescription})\n";
            }
            History = history.TrimEnd();
        }

        public void UpdateFrom(Player p)
        {
            bool changed = false;

            if (UserId != p.UserId) { UserId = p.UserId; changed = true; }
            if (DisplayName != p.DisplayName) { DisplayName = p.DisplayName; changed = true; }
            if (AvatarName != p.AvatarName) { AvatarName = p.AvatarName; changed = true; }
            if (PenActivity != p.PenActivity) { PenActivity = p.PenActivity; changed = true; }
            if (LastStickerUrl != p.LastStickerUrl) { LastStickerUrl = p.LastStickerUrl; LastStickerImageUrl = p.LastStickerUrl; changed = true; }

            var start = p.InstanceStartTime.ToString("u");
            if (InstanceStartTime != start) { InstanceStartTime = start; changed = true; }

            var end = p.InstanceEndTime.HasValue ? p.InstanceEndTime.Value.ToString("u") : string.Empty;
            if (InstanceEndTime != end) { InstanceEndTime = end; changed = true; }
            if (Profile != (p.UserBio ?? string.Empty)) { Profile = p.UserBio ?? string.Empty; changed = true; }
            if (AIEval != (p.AIEval ?? "Not Evaluated")) { AIEval = p.AIEval ?? "Not Evaluated"; changed = true; }
            if (IsWatched != p.IsWatched) { IsWatched = p.IsWatched; changed = true; }
            if (AlertMessages != p.AlertMessage) { AlertMessages = p.AlertMessage ?? string.Empty; changed = true; }
            if (_AlertColor != p.AlertColor ) { _AlertColor = p.AlertColor; changed = true; }

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
            if (p.Inventory != null)
            {
                Emojis.Clear();
                foreach (var inv in p.Inventory)
                {
                    Emojis.Add(new EmojiInfoViewModel(p.UserId, inv));
                }
            }

            string history = string.Empty;
            foreach (var hist in p.Events)
            {
                history += $"({hist.EventTime:u} - {hist.EventDescription})\n";
            }
            History = history.TrimEnd();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PrintInfoViewModel : INotifyPropertyChanged
    {
        public string PrintId { get; set; }
        public string OwnerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime Timestamp { get; set; }
        public string PrintUrl { get; set; }
        public string AIEvaluation { get; set; }
        public string AIClass { get; set; }
        public string AuthorName { get; set; }
        public PrintInfoViewModel(PlayerPrint p )
        {
            PrintId = p.PrintId;
            OwnerId = p.OwnerId;
            CreatedAt = p.CreatedAt;
            Timestamp = p.Timestamp;
            PrintUrl = p.PrintUrl;
            AuthorName = p.AuthorName;
            AIEvaluation = p.AIEvaluation;
            AIClass = p.AIClass;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class EmojiInfoViewModel
    {
        public string UserId { get; set; }
        public string InventoryId { get; set; }
        public DateTime SpawnedAt { get; set; }
        public string ImageUrl { get; set; }       
        public string InventoryType { get; set; }
        public string AIEvalutation { get; set; }
        public EmojiInfoViewModel(string userId, PlayerInventory i)
        {
            UserId = userId;
            InventoryId = i.InventoryId;
            SpawnedAt = i.SpawnedAt;
            ImageUrl = i.ItemUrl;
            InventoryType = i.InventoryType;
            AIEvalutation = i.AIEvaluation;
        }
    }

    public class ReportReasonItem
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }

        public ReportReasonItem(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }
    #endregion
}