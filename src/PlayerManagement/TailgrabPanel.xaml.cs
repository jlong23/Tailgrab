using Microsoft.Win32;
using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
using Tailgrab.Common;
using Tailgrab.Models;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{
    public partial class TailgrabPanel : Window, IDisposable, INotifyPropertyChanged
    {
        public static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected ServiceRegistry _serviceRegistry;

        private readonly DispatcherTimer fallbackTimer;
        private readonly DispatcherTimer statusBarTimer;

        public ObservableCollection<PlayerViewModel> ActivePlayers { get; } = [];
        public ObservableCollection<PlayerViewModel> PastPlayers { get; } = [];
        public ObservableCollection<PlayerViewModel> PrintPlayers { get; } = [];
        public ObservableCollection<PlayerViewModel> EmojiPlayers { get; } = [];
        public ObservableCollection<TailTaskViewModel> OpenLogs { get; } = [];
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
        public ICollectionView OpenLogsView { get; }


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

        private PlayerViewModel? _selectedPast;
        public PlayerViewModel? SelectedPast
        {
            get => _selectedPast;
            set
            {
                if (_selectedPast != value)
                {
                    logger.Debug($"SelectedPast changed to: {value?.ToString()}");
                    _selectedPast = value;
                    OnPropertyChanged(nameof(SelectedPast));
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

        public List<KeyValuePair<string, AlertTypeEnum>> AlertTypeOptions { get; } =
        [
            new KeyValuePair<string, AlertTypeEnum>("None", AlertTypeEnum.None),
            new KeyValuePair<string, AlertTypeEnum>("Watch", AlertTypeEnum.Watch),
            new KeyValuePair<string, AlertTypeEnum>("Nuisance", AlertTypeEnum.Nuisance),
            new KeyValuePair<string, AlertTypeEnum>("Crasher", AlertTypeEnum.Crasher)
        ];

        private ObservableCollection<AlertColorOption> _alertColorOptions = [];
        public ObservableCollection<AlertColorOption> AlertColorOptions 
        {
            get => _alertColorOptions;
            set
            {
                _alertColorOptions = value;
                OnPropertyChanged(nameof(AlertColorOptions));
            }
        }

        public List<KeyValuePair<string, string>> AlertSoundOptions { get; set; } = [];

        private List<KeyValuePair<string, string>> _ollamaModelOptions = [];
        public List<KeyValuePair<string, string>> OllamaModelOptions
        {
            get => _ollamaModelOptions;
            set
            {
                _ollamaModelOptions = value;
                OnPropertyChanged(nameof(OllamaModelOptions));
            }
        }

        private bool _canTestProfilePrompt;
        public bool CanTestProfilePrompt
        {
            get => _canTestProfilePrompt;
            set
            {
                if (_canTestProfilePrompt != value)
                {
                    _canTestProfilePrompt = value;
                    OnPropertyChanged(nameof(CanTestProfilePrompt));
                }
            }
        }

        public ObservableCollection<Models.TestImageAIEvalItem> TestImageAIEvalItems { get; } = [];

        public List<ReportReasonItem> ProfileReportReasonsOptions =
        [
            new ReportReasonItem("Sexual Content", "sexual"),
            new ReportReasonItem("Hateful Content", "hateful"),
            new ReportReasonItem("Gore and Violence", "gore"),
            new ReportReasonItem("Child Exploitation", "child"),
            new ReportReasonItem("Other", "other")
        ];

        // Color options for selection
        public List<ColorOption> ColorOptions { get; } =
        [
            new ColorOption("Dark Gray", "#FF1E1E1E"),
            new ColorOption("Light Gray", "#FFE6E6E6"),
            new ColorOption("Black", "Black"),
            new ColorOption("White", "White"),
            new ColorOption("Red", "Red"),
            new ColorOption("Yellow", "Yellow"),
            new ColorOption("Green", "Green"),
            new ColorOption("Blue", "Blue"),
            new ColorOption("Purple", "Purple"),
            new ColorOption("Orange", "Orange"),
            new ColorOption("Pink", "Pink"),
            new ColorOption("Light Green", "LightGreen"),
            new ColorOption("Light Blue", "LightBlue"),
            new ColorOption("Light Pink", "LightPink"),
            new ColorOption("Dark Blue", "#FF1d1db3"),
            new ColorOption("Bright Yellow", "#FFFFFF00"),
            new ColorOption("Cyan", "Cyan"),
            new ColorOption("Magenta", "Magenta"),
            new ColorOption("Lime", "Lime"),
            new ColorOption("Brown", "Brown"),
            new ColorOption("Navy", "Navy"),
            new ColorOption("Teal", "Teal"),
            new ColorOption("Maroon", "Maroon"),
            new ColorOption("Olive", "Olive"),
            new ColorOption("Silver", "Silver"),
            new ColorOption("Gold", "Gold"),
        ];

        // Highlight class color properties with backing fields
        private System.Windows.Media.Brush _normalBackground = null!;
        public System.Windows.Media.Brush NormalBackground
        {
            get => _normalBackground;
            set { _normalBackground = value; OnPropertyChanged(nameof(NormalBackground)); }
        }

        private System.Windows.Media.Brush _normalForeground = null!;
        public System.Windows.Media.Brush NormalForeground
        {
            get => _normalForeground;
            set { _normalForeground = value; OnPropertyChanged(nameof(NormalForeground)); }
        }

        private System.Windows.Media.Brush _friendBackground = null!;
        public System.Windows.Media.Brush FriendBackground
        {
            get => _friendBackground;
            set { _friendBackground = value; OnPropertyChanged(nameof(FriendBackground)); }
        }

        private System.Windows.Media.Brush _friendForeground = null!;
        public System.Windows.Media.Brush FriendForeground
        {
            get => _friendForeground;
            set { _friendForeground = value; OnPropertyChanged(nameof(FriendForeground)); }
        }

        private System.Windows.Media.Brush _Class01Background = null!;
        public System.Windows.Media.Brush Class01Background
        {
            get => _Class01Background;
            set { _Class01Background = value; OnPropertyChanged(nameof(Class01Background)); }
        }

        private System.Windows.Media.Brush _Class01Foreground = null!;
        public System.Windows.Media.Brush Class01Foreground
        {
            get => _Class01Foreground;
            set { _Class01Foreground = value; OnPropertyChanged(nameof(Class01Foreground)); }
        }

        private System.Windows.Media.Brush _Class02Background = null!;
        public System.Windows.Media.Brush Class02Background
        {
            get => _Class02Background;
            set { _Class02Background = value; OnPropertyChanged(nameof(Class02Background)); }
        }

        private System.Windows.Media.Brush _Class02Foreground = null!;
        public System.Windows.Media.Brush Class02Foreground
        {
            get => _Class02Foreground;
            set { _Class02Foreground = value; OnPropertyChanged(nameof(Class02Foreground)); }
        }

        private System.Windows.Media.Brush _Class03Background = null!;
        public System.Windows.Media.Brush Class03Background
        {
            get => _Class03Background;
            set { _Class03Background = value; OnPropertyChanged(nameof(Class03Background)); }
        }

        private System.Windows.Media.Brush _Class03Foreground = null!;
        public System.Windows.Media.Brush Class03Foreground
        {
            get => _Class03Foreground;
            set { _Class03Foreground = value; OnPropertyChanged(nameof(Class03Foreground)); }
        }

        private System.Windows.Media.Brush _Class04Background = null!;
        public System.Windows.Media.Brush Class04Background
        {
            get => _Class04Background;
            set { _Class04Background = value; OnPropertyChanged(nameof(Class04Background)); }
        }

        private System.Windows.Media.Brush _Class04Foreground = null!;
        public System.Windows.Media.Brush Class04Foreground
        {
            get => _Class04Foreground;
            set { _Class04Foreground = value; OnPropertyChanged(nameof(Class04Foreground)); }
        }

        private System.Windows.Media.Brush _selectedBackground = null!;
        public System.Windows.Media.Brush SelectedBackground
        {
            get => _selectedBackground;
            set { _selectedBackground = value; OnPropertyChanged(nameof(SelectedBackground)); }
        }

        private System.Windows.Media.Brush _selectedForeground = null!;
        public System.Windows.Media.Brush SelectedForeground
        {
            get => _selectedForeground;
            set { _selectedForeground = value; OnPropertyChanged(nameof(SelectedForeground)); }
        }

        private System.Windows.Media.Brush _mouseOverBackground = null!;
        public System.Windows.Media.Brush MouseOverBackground
        {
            get => _mouseOverBackground;
            set { _mouseOverBackground = value; OnPropertyChanged(nameof(MouseOverBackground)); }
        }

        private System.Windows.Media.Brush _mouseOverForeground = null!;
        public System.Windows.Media.Brush MouseOverForeground
        {
            get => _mouseOverForeground;
            set { _mouseOverForeground = value; OnPropertyChanged(nameof(MouseOverForeground)); }
        }

        // Selected color options for ComboBoxes
        private ColorOption? _selectedNormalBackground;
        public ColorOption? SelectedNormalBackground
        {
            get => _selectedNormalBackground;
            set
            {
                _selectedNormalBackground = value;
                if (value != null) NormalBackground = value.Brush;
                OnPropertyChanged(nameof(SelectedNormalBackground));
            }
        }

        private ColorOption? _selectedNormalForeground;
        public ColorOption? SelectedNormalForeground
        {
            get => _selectedNormalForeground;
            set
            {
                _selectedNormalForeground = value;
                if (value != null) NormalForeground = value.Brush;
                OnPropertyChanged(nameof(SelectedNormalForeground));
            }
        }

        private ColorOption? _selectedFriendBackground;
        public ColorOption? SelectedFriendBackground
        {
            get => _selectedFriendBackground;
            set
            {
                _selectedFriendBackground = value;
                if (value != null) FriendBackground = value.Brush;
                OnPropertyChanged(nameof(SelectedFriendBackground));
            }
        }

        private ColorOption? _selectedFriendForeground;
        public ColorOption? SelectedFriendForeground
        {
            get => _selectedFriendForeground;
            set
            {
                _selectedFriendForeground = value;
                if (value != null) FriendForeground = value.Brush;
                OnPropertyChanged(nameof(SelectedFriendForeground));
            }
        }

        private ColorOption? _selectedClass01Background;
        public ColorOption? SelectedClass01Background
        {
            get => _selectedClass01Background;
            set
            {
                _selectedClass01Background = value;
                if (value != null) Class01Background = value.Brush;
                OnPropertyChanged(nameof(SelectedClass01Background));
            }
        }

        private ColorOption? _selectedClass01Foreground;
        public ColorOption? SelectedClass01Foreground
        {
            get => _selectedClass01Foreground;
            set
            {
                _selectedClass01Foreground = value;
                if (value != null) Class01Foreground = value.Brush;
                OnPropertyChanged(nameof(SelectedClass01Foreground));
            }
        }

        private ColorOption? _selectedClass02Background;
        public ColorOption? SelectedClass02Background
        {
            get => _selectedClass02Background;
            set
            {
                _selectedClass02Background = value;
                if (value != null) Class02Background = value.Brush;
                OnPropertyChanged(nameof(SelectedClass02Background));
            }
        }

        private ColorOption? _selectedClass02Foreground;
        public ColorOption? SelectedClass02Foreground
        {
            get => _selectedClass02Foreground;
            set
            {
                _selectedClass02Foreground = value;
                if (value != null) Class02Foreground = value.Brush;
                OnPropertyChanged(nameof(SelectedClass02Foreground));
            }
        }

        private ColorOption? _selectedClass03Background;
        public ColorOption? SelectedClass03Background
        {
            get => _selectedClass03Background;
            set
            {
                _selectedClass03Background = value;
                if (value != null) Class03Background = value.Brush;
                OnPropertyChanged(nameof(SelectedClass03Background));
            }
        }

        private ColorOption? _selectedClass03Foreground;
        public ColorOption? SelectedClass03Foreground
        {
            get => _selectedClass03Foreground;
            set
            {
                _selectedClass03Foreground = value;
                if (value != null) Class04Foreground = value.Brush;
                OnPropertyChanged(nameof(SelectedClass03Foreground));
            }
        }

        private ColorOption? _selectedClass04Background;
        public ColorOption? SelectedClass04Background
        {
            get => _selectedClass04Background;
            set
            {
                _selectedClass04Background = value;
                if (value != null) Class04Background = value.Brush;
                OnPropertyChanged(nameof(SelectedClass04Background));
            }
        }

        private ColorOption? _selectedClass04Foreground;
        public ColorOption? SelectedClass04Foreground
        {
            get => _selectedClass04Foreground;
            set
            {
                _selectedClass04Foreground = value;
                if (value != null) Class04Foreground = value.Brush;
                OnPropertyChanged(nameof(SelectedClass04Foreground));
            }
        }

        private ColorOption? _selectedSelectedBackground;
        public ColorOption? SelectedSelectedBackground
        {
            get => _selectedSelectedBackground;
            set
            {
                _selectedSelectedBackground = value;
                if (value != null) SelectedBackground = value.Brush;
                OnPropertyChanged(nameof(SelectedSelectedBackground));
            }
        }

        private ColorOption? _selectedSelectedForeground;
        public ColorOption? SelectedSelectedForeground
        {
            get => _selectedSelectedForeground;
            set
            {
                _selectedSelectedForeground = value;
                if (value != null) SelectedForeground = value.Brush;
                OnPropertyChanged(nameof(SelectedSelectedForeground));
            }
        }


        public TailgrabPanel(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
            InitializeComponent();
            DataContext = this;

            // Load highlight colors from registry BEFORE setting SelectedValue on color ComboBoxes
            // This ensures AlertColorOptions is populated when WPF binding resolves
            LoadHighlightColors();

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

            OpenLogsView = CollectionViewSource.GetDefaultView(OpenLogs);
            OpenLogsView.SortDescriptions.Add(new SortDescription("StartTime", ListSortDirection.Descending));

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
            var vrUser = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName);
            var vrPass = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_Password);
            var vr2fa = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_2FactorKey);
            var ollamaKey = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key);
            var ollamaEndpoint = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;
            var ollamaModel = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
            var ollamaProfilePrompt = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Prompt) ?? CommonConst.Default_Ollama_API_Prompt;
            var ollamaImagePrompt = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Image_Prompt) ?? CommonConst.Default_Ollama_API_Image_Prompt;
            var avatarGistUri = ConfigStore.GetStoredKeyString(CommonConst.Registry_Avatar_Gist);
            var groupGistUri = ConfigStore.GetStoredKeyString(CommonConst.Registry_Group_Gist);

            // Populate UI boxes but do not reveal secrets
            if (!string.IsNullOrEmpty(vrUser)) VrUserBox.Text = vrUser;
            if (!string.IsNullOrEmpty(vrPass)) VrPassBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(vr2fa)) Vr2FaBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(ollamaKey)) VrOllamaBox.ToolTip = "Stored (hidden)";
            if (!string.IsNullOrEmpty(ollamaEndpoint)) VrOllamaEndpointBox.Text = ollamaEndpoint;
            if (!string.IsNullOrEmpty(ollamaModel)) VrOllamaModelBox.SelectedValue = ollamaModel;
            if (!string.IsNullOrEmpty(ollamaProfilePrompt)) VrOllamaPromptBox.Text = ollamaProfilePrompt;
            if (!string.IsNullOrEmpty(ollamaImagePrompt)) VrOllamaImagePromptBox.Text = ollamaImagePrompt;

            if (!string.IsNullOrEmpty(avatarGistUri)) avatarGistUrl.Text = avatarGistUri;
            if (!string.IsNullOrEmpty(groupGistUri)) groupGistUrl.Text = groupGistUri;

            // Populate sound combo boxes
            try
            {
                UpdateAlertComboBoxValues();
            }
            catch { }


            #endregion

            // Initial load of Avatars, Groups and Users
            RefreshAvatarDb();
            RefreshGroupDb();
            RefreshUserDb();

            // Load Ollama models if credentials are configured
            Task.Run(async () => 
            {
                try 
                {
                    await LoadOllamaModelsAsync();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to load Ollama models during initialization");
                }
            });

            // Initialize button state for Test Profile Prompt
            UpdateCanTestProfilePrompt();

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

            // Load window layout from registry
            this.Loaded += Window_Loaded;
            this.SizeChanged += Window_SizeChanged;
            this.LocationChanged += Window_LocationChanged;
        }


        private void UpdateAlertComboBoxValues()
        {
            var sounds = SoundManager.GetAvailableSounds();
            AlertSoundOptions = [.. sounds.Select(s => new KeyValuePair<string, string>(s, s))];

            // Avatar Alerts
            AvatarWarnSound.SelectedValue = GetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key) ?? "*NONE";
            AvatarNuisanceSound.SelectedValue = GetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key) ?? "*NONE";
            AvatarCrasherSound.SelectedValue = GetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key) ?? "*NONE";
            AvatarWarnColor.SelectedValue = GetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key) ?? "Normal";
            AvatarNuisanceColor.SelectedValue = GetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key) ?? "Yellow";
            AvatarCrasherColor.SelectedValue = GetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key) ?? "Red";

            // Group Alerts
            GroupWarnSound.SelectedValue = GetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key) ?? "*NONE";
            GroupNuisanceSound.SelectedValue = GetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key) ?? "*NONE";
            GroupCrasherSound.SelectedValue = GetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key) ?? "*NONE";
            GroupWarnColor.SelectedValue = GetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key) ?? "Normal";
            GroupNuisanceColor.SelectedValue = GetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key) ?? "Yellow";
            GroupCrasherColor.SelectedValue = GetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key) ?? "Red";

            // Profile Alerts
            ProfileWarnSound.SelectedValue = GetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key) ?? "*NONE";
            ProfileNuisanceSound.SelectedValue = GetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key) ?? "*NONE";
            ProfileCrasherSound.SelectedValue = GetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key) ?? "*NONE";
            ProfileWarnColor.SelectedValue = GetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key) ?? "Normal";
            ProfileNuisanceColor.SelectedValue = GetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key) ?? "Yellow";
            ProfileCrasherColor.SelectedValue = GetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key) ?? "Red";
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save to registry protected store
                ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_UserName, VrUserBox.Text.Trim() ?? string.Empty);
                if (!string.IsNullOrEmpty(VrPassBox.Password)) ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_Password, VrPassBox.Password.Trim());
                if (!string.IsNullOrEmpty(Vr2FaBox.Password)) ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_2FactorKey, Vr2FaBox.Password.Trim());

                ConfigStore.PutStoredKeyString(Common.CommonConst.Registry_Avatar_Gist, avatarGistUrl.Text);
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Group_Gist, groupGistUrl.Text);

                ConfigStore.PutStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, DiscoveredAvatarCaching.IsChecked == true);
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, ModeratedAvatarCaching.IsChecked == true);
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_Discovered_Group_Caching, DiscoveredGroupCaching.IsChecked == true);

                System.Windows.MessageBox.Show("Configuration saved. Restart the Applicaton for all changes to take affect.", "Config", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void SaveAIConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save Ollama credentials to registry protected store
                if (!string.IsNullOrEmpty(VrOllamaBox.Password)) ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Key, VrOllamaBox.Password.Trim());
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint, VrOllamaEndpointBox.Text ?? CommonConst.Default_Ollama_API_Endpoint);
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Ollama_API_Prompt, VrOllamaPromptBox.Text ?? CommonConst.Default_Ollama_API_Prompt);
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Ollama_API_Image_Prompt, VrOllamaImagePromptBox.Text ?? CommonConst.Default_Ollama_API_Image_Prompt);
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Ollama_API_Model, (string)VrOllamaModelBox.SelectedValue ?? CommonConst.Default_Ollama_API_Model);

                // Load available models from Ollama after saving credentials
                await LoadOllamaModelsAsync();

                System.Windows.MessageBox.Show("AI Configuration saved. Restart the Application for all changes to take effect.", "AI Config", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save AI configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task LoadOllamaModelsAsync()
        {
            try
            {
                var models = await Clients.Ollama.OllamaClient.GetModels();

                // Update the ObservableCollection on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OllamaModelOptions.Clear();
                    OllamaModelOptions = [.. models.Select(s => new KeyValuePair<string, string>(s, s))];
                    
                    // If there's a currently saved model, try to select it
                    string? currentModel = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model);
                    if (!string.IsNullOrEmpty(currentModel) && OllamaModelOptions.Contains(new KeyValuePair<string, string>( currentModel, currentModel )))
                    {
                        VrOllamaModelBox.SelectedValue = currentModel;
                    }
                    else if (OllamaModelOptions.Count > 0)
                    {
                        VrOllamaModelBox.SelectedValue = OllamaModelOptions[0].Value;
                    }

                    // Update the test button state after loading models
                    UpdateCanTestProfilePrompt();
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load Ollama models");
                System.Windows.MessageBox.Show($"Failed to load Ollama models: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private void UpdateCanTestProfilePrompt()
        {
            CanTestProfilePrompt =
                !string.IsNullOrEmpty(ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint)) &&
                !string.IsNullOrEmpty(ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key)) &&
                !string.IsNullOrEmpty((string)VrOllamaModelBox.SelectedValue) &&
                (VrOllamaPromptBox.Text?.Length ?? 0) > 60 &&
                (UserAccountTestBox.Text?.StartsWith("usr_") ?? false);
        }

        private void TestProfilePromptInput_Changed(object sender, EventArgs e)
        {
            UpdateCanTestProfilePrompt();
        }

        private async void TestProfilePrompt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestProfilePromptButton.IsEnabled = false;
                var userId = UserAccountTestBox.Text?.Trim();
                var prompt = VrOllamaPromptBox.Text?.Trim();
                var model = ((string)VrOllamaModelBox.SelectedValue)?.Trim();

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(prompt) || string.IsNullOrEmpty(model))
                {
                    System.Windows.MessageBox.Show("Please ensure User ID, Prompt, and Model are specified.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Call Ollama test method
                ProfileEvaluation result = await Clients.Ollama.OllamaClient.TestProfilePrompt(_serviceRegistry, userId, prompt, model);
                if (result != null) {
                    logger.Info("Profile prompt test successful for user {UserId} with model {Model} as {Evaluation}", userId, model, result.Evaluation);

                    OverlayTestProfileEvalUserIdTextBox.Text = userId;
                    OverlayTestProfileEvalProfileTextBox.Text = System.Text.Encoding.UTF8.GetString( result.ProfileText );
                    OverlayTestProfileEvalEvaluationTextBox.Text = System.Text.Encoding.UTF8.GetString( result.Evaluation );
                    OverlayTestProfileEval.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to test profile prompt");
                System.Windows.MessageBox.Show($"Failed to test profile prompt: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestProfilePromptButton.IsEnabled = true;
            }
        }

        private async void TestProfilePrompt_Ok_Click(object sender, RoutedEventArgs e)
        {
            OverlayTestProfileEvalUserIdTextBox.Text = string.Empty;
            OverlayTestProfileEvalProfileTextBox.Text = string.Empty;
            OverlayTestProfileEvalEvaluationTextBox.Text = string.Empty;
            OverlayTestProfileEval.Visibility = Visibility.Collapsed;
        }

        private async Task ProcessAIImagePromptTest()
        {
            OllamaClient ollamaClient = _serviceRegistry.GetOllamaAPIClient();
            try
            {
                var prompt = VrOllamaImagePromptBox.Text?.Trim();
                var model = ((string)VrOllamaModelBox.SelectedValue)?.Trim();

                if (model != null && prompt != null)
                {
                    // Clear existing items
                    TestImageAIEvalItems.Clear();

                    // Get test images from the test-images folder
                    List<string> testImages = tailgrab.Common.TestImageManager.GetAvailableImages();

                    // TODO: Implement actual AI image prompt testing logic
                    // This would typically:
                    // 1. Load each test image from the test-images folder
                    // 2. Send to Ollama for evaluation with the configured prompt
                    // 3. Populate TestImageAIEvalItems with results

                    // Stub implementation - create placeholder items
                    foreach (string imageName in testImages)
                    {
                        string? imagePath = GetTestImagePath(imageName);
                        logger.Info("Processing test image {ImageName} at path {ImagePath}", imageName, imagePath);
                        if (imagePath != null)
                        {

                            string evaluation = await ollamaClient.TestImagePrompt(model, prompt, imagePath);

                            Models.TestImageAIEvalItem item = new()
                            {
                                ImagePath = imagePath,
                                AIEvaluation = evaluation
                            };

                            TestImageAIEvalItems.Add(item);
                        }
                    }

                    logger.Info($"Loaded {TestImageAIEvalItems.Count} test images for AI evaluation");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to process AI image prompt test");
                System.Windows.MessageBox.Show($"Failed to load test images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? GetTestImagePath(string imageName)
        {
            string[] extensions = [".png", ".jpg", ".gif", ".webp"];
            foreach (string extension in extensions)
            {
                string imagePath = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "test-images", imageName + extension);
                if (System.IO.File.Exists(imagePath))
                {
                    return imagePath;
                }

            }

            return null;
        }

        private void CloseTestImageEval_Click(object sender, RoutedEventArgs e)
        {
            OverlayTestImageEval.Visibility = Visibility.Collapsed;
            TestImageAIEvalItems.Clear();
        }

        private async void TestImagePrompt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestImagePromptButton.IsEnabled = false;

                // Process the test images
                await ProcessAIImagePromptTest();

                // Show the overlay with results
                OverlayTestImageEval.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to test image prompt");
                System.Windows.MessageBox.Show($"Failed to test image prompt: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestImagePromptButton.IsEnabled = true;
            }
        }


        private void SaveAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Avatar Alerts
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key, (string)AvatarWarnSound.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key, (string)AvatarNuisanceSound.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key, (string)AvatarCrasherSound.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key, (string)AvatarWarnColor.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key, (string)AvatarNuisanceColor.SelectedValue);
                SetAlertKeyString(CommonConst.Avatar_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key, (string)AvatarCrasherColor.SelectedValue);

                // Group Alerts
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key, (string)GroupWarnSound.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key, (string)GroupNuisanceSound.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key, (string)GroupCrasherSound.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key, (string)GroupWarnColor.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key, (string)GroupNuisanceColor.SelectedValue);
                SetAlertKeyString(CommonConst.Group_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key, (string)GroupCrasherColor.SelectedValue);

                // Group Alerts
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch, CommonConst.Sound_Alert_Key, (string)ProfileWarnSound.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Sound_Alert_Key, (string)ProfileNuisanceSound.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Sound_Alert_Key, (string)ProfileCrasherSound.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch, CommonConst.Color_Alert_Key, (string)ProfileWarnColor.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance, CommonConst.Color_Alert_Key, (string)ProfileNuisanceColor.SelectedValue);
                SetAlertKeyString(CommonConst.Profile_Alert_Key, AlertTypeEnum.Crasher, CommonConst.Color_Alert_Key, (string)ProfileCrasherColor.SelectedValue);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestSound_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string soundName)
            {
                SoundManager.PlaySound(soundName);
            }
        }

        private void GistUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable/disable the corresponding "Check Now" button based on whether there's text in the textbox
            if (sender == avatarGistUrl)
            {
                avatarGistCheckButton.IsEnabled = !string.IsNullOrWhiteSpace(avatarGistUrl.Text);
            }
            else if (sender == groupGistUrl)
            {
                groupGistCheckButton.IsEnabled = !string.IsNullOrWhiteSpace(groupGistUrl.Text);
            }
        }

        private async void CheckAvatarGist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                avatarGistCheckButton.IsEnabled = false;
                avatarGistCheckButton.Content = "Checking...";

                await Task.Run(() => _serviceRegistry.ProcessAvatarGist());

                System.Windows.MessageBox.Show("Avatar GIST list processing in the background.", "Check Avatar GIST", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to process Avatar GIST");
                System.Windows.MessageBox.Show($"Failed to process Avatar GIST: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                avatarGistCheckButton.Content = "Check Now";
                avatarGistCheckButton.IsEnabled = !string.IsNullOrWhiteSpace(avatarGistUrl.Text);
            }
        }

        private async void CheckGroupGist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                groupGistCheckButton.IsEnabled = false;
                groupGistCheckButton.Content = "Checking...";

                await Task.Run(() => _serviceRegistry.ProcessGroupGist());

                System.Windows.MessageBox.Show("Group GIST list processing in the background.", "Check Group GIST", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to process Group GIST");
                System.Windows.MessageBox.Show($"Failed to process Group GIST: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                groupGistCheckButton.Content = "Check Now";
                groupGistCheckButton.IsEnabled = !string.IsNullOrWhiteSpace(groupGistUrl.Text);
            }
        }

        private static void SetAlertKeyString(string alertKey, AlertTypeEnum alertType, string subType, object value)
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

        private static string? GetAlertKeyString(string alertKey, AlertTypeEnum alertType, string subType)
        {
            string key = CommonConst.ConfigRegistryPath + "\\" + alertKey + "\\" + alertType.ToString();

            return ConfigStore.GetStoredKeyString(key, subType);
        }

        private void LoadHighlightColors()
        {
            try
            {
                UpdateAlertColorOptions();

                // Load colors from registry or use defaults
                var normalBg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Normal_Background) ?? CommonConst.Default_HighlightClass_Normal_Background;
                var normalFg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Normal_Foreground) ?? CommonConst.Default_HighlightClass_Normal_Foreground;
                var friendBg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Friend_Background) ?? CommonConst.Default_HighlightClass_Friend_Background;
                var friendFg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Friend_Foreground) ?? CommonConst.Default_HighlightClass_Friend_Foreground;
                var class01Bg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class01_Background) ?? CommonConst.Default_HighlightClass_Class01_Background;
                var class01Fg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class01_Foreground) ?? CommonConst.Default_HighlightClass_Class01_Foreground;
                var class02Bg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class02_Background) ?? CommonConst.Default_HighlightClass_Class02_Background;
                var class02Fg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class02_Foreground) ?? CommonConst.Default_HighlightClass_Class02_Foreground;
                var class03Bg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class03_Background) ?? CommonConst.Default_HighlightClass_Class03_Background;
                var class03Fg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class03_Foreground) ?? CommonConst.Default_HighlightClass_Class03_Foreground;
                var class04Bg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class04_Background) ?? CommonConst.Default_HighlightClass_Class04_Background;
                var class04Fg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Class04_Foreground) ?? CommonConst.Default_HighlightClass_Class04_Foreground;
                var selectedBg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Selected_Background) ?? CommonConst.Default_HighlightClass_Selected_Background;
                var selectedFg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_Selected_Foreground) ?? CommonConst.Default_HighlightClass_Selected_Foreground;
                var mouseOverBg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_MouseOver_Background) ?? CommonConst.Default_HighlightClass_MouseOver_Background;
                var mouseOverFg = ConfigStore.GetStoredKeyString(CommonConst.Registry_HighlightClass_MouseOver_Foreground) ?? CommonConst.Default_HighlightClass_MouseOver_Foreground;

                // Set the brush properties
                var converter = new BrushConverter();
                NormalBackground = (System.Windows.Media.Brush)converter.ConvertFromString(normalBg)!;
                NormalForeground = (System.Windows.Media.Brush)converter.ConvertFromString(normalFg)!;
                FriendBackground = (System.Windows.Media.Brush)converter.ConvertFromString(friendBg)!;
                FriendForeground = (System.Windows.Media.Brush)converter.ConvertFromString(friendFg)!;
                Class01Background = (System.Windows.Media.Brush)converter.ConvertFromString(class01Bg)!;
                Class01Foreground = (System.Windows.Media.Brush)converter.ConvertFromString(class01Fg)!;
                Class02Background = (System.Windows.Media.Brush)converter.ConvertFromString(class02Bg)!;
                Class02Foreground = (System.Windows.Media.Brush)converter.ConvertFromString(class02Fg)!;
                Class03Background = (System.Windows.Media.Brush)converter.ConvertFromString(class03Bg)!;
                Class03Foreground = (System.Windows.Media.Brush)converter.ConvertFromString(class03Fg)!;
                Class04Background = (System.Windows.Media.Brush)converter.ConvertFromString(class04Bg)!;
                Class04Foreground = (System.Windows.Media.Brush)converter.ConvertFromString(class04Fg)!;
                SelectedBackground = (System.Windows.Media.Brush)converter.ConvertFromString(selectedBg)!;
                SelectedForeground = (System.Windows.Media.Brush)converter.ConvertFromString(selectedFg)!;
                MouseOverBackground = (System.Windows.Media.Brush)converter.ConvertFromString(mouseOverBg)!;
                MouseOverForeground = (System.Windows.Media.Brush)converter.ConvertFromString(mouseOverFg)!;

                // Set selected options in ComboBoxes
                SelectedNormalBackground = ColorOptions.FirstOrDefault(c => c.Value.Equals(normalBg, StringComparison.OrdinalIgnoreCase));
                SelectedNormalForeground = ColorOptions.FirstOrDefault(c => c.Value.Equals(normalFg, StringComparison.OrdinalIgnoreCase));
                SelectedFriendBackground = ColorOptions.FirstOrDefault(c => c.Value.Equals(friendBg, StringComparison.OrdinalIgnoreCase));
                SelectedFriendForeground = ColorOptions.FirstOrDefault(c => c.Value.Equals(friendFg, StringComparison.OrdinalIgnoreCase));
                SelectedClass01Background = ColorOptions.FirstOrDefault(c => c.Value.Equals(class01Bg, StringComparison.OrdinalIgnoreCase));
                SelectedClass01Foreground = ColorOptions.FirstOrDefault(c => c.Value.Equals(class01Fg, StringComparison.OrdinalIgnoreCase));
                SelectedClass02Background = ColorOptions.FirstOrDefault(c => c.Value.Equals(class02Bg, StringComparison.OrdinalIgnoreCase));
                SelectedClass02Foreground = ColorOptions.FirstOrDefault(c => c.Value.Equals(class02Fg, StringComparison.OrdinalIgnoreCase));
                SelectedClass03Background = ColorOptions.FirstOrDefault(c => c.Value.Equals(class03Bg, StringComparison.OrdinalIgnoreCase));
                SelectedClass03Foreground = ColorOptions.FirstOrDefault(c => c.Value.Equals(class03Fg, StringComparison.OrdinalIgnoreCase));
                SelectedClass04Background = ColorOptions.FirstOrDefault(c => c.Value.Equals(class04Bg, StringComparison.OrdinalIgnoreCase));
                SelectedClass04Foreground = ColorOptions.FirstOrDefault(c => c.Value.Equals(class04Fg, StringComparison.OrdinalIgnoreCase));
                SelectedSelectedBackground = ColorOptions.FirstOrDefault(c => c.Value.Equals(selectedBg, StringComparison.OrdinalIgnoreCase));
                SelectedSelectedForeground = ColorOptions.FirstOrDefault(c => c.Value.Equals(selectedFg, StringComparison.OrdinalIgnoreCase));

                // Need to update alert color options after loading new colors to ensure they reflect in the ComboBoxes
                UpdateAlertColorOptions();
                UpdateAlertComboBoxValues();

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load highlight colors");
            }
        }

        private void UpdateAlertColorOptions()
        {
            AlertColorOptions.Clear();
            AlertColorOptions.Add(new AlertColorOption("*NONE", "Normal", NormalBackground, NormalForeground));
            AlertColorOptions.Add(new AlertColorOption("Class 1", "Class01", Class01Background, Class01Foreground));
            AlertColorOptions.Add(new AlertColorOption("Class 2", "Class02", Class02Background, Class02Foreground));
            AlertColorOptions.Add(new AlertColorOption("Class 3", "Class03", Class03Background, Class03Foreground));
            AlertColorOptions.Add(new AlertColorOption("Class 4", "Class04", Class04Background, Class04Foreground));
        }

        private void SaveColors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save all color settings to registry
                if (SelectedNormalBackground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Normal_Background, SelectedNormalBackground.Value);
                if (SelectedNormalForeground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Normal_Foreground, SelectedNormalForeground.Value);
                if (SelectedFriendBackground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Friend_Background, SelectedFriendBackground.Value);
                if (SelectedFriendForeground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Friend_Foreground, SelectedFriendForeground.Value);
                if (SelectedClass01Background != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class01_Background, SelectedClass01Background.Value);
                if (SelectedClass01Foreground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class01_Foreground, SelectedClass01Foreground.Value);
                if (SelectedClass02Background != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class02_Background, SelectedClass02Background.Value);
                if (SelectedClass02Foreground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class02_Foreground, SelectedClass02Foreground.Value);
                if (SelectedClass03Background != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class03_Background, SelectedClass03Background.Value);
                if (SelectedClass03Foreground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class03_Foreground, SelectedClass03Foreground.Value);
                if (SelectedClass04Background != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class04_Background, SelectedClass04Background.Value);
                if (SelectedClass04Foreground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Class04_Foreground, SelectedClass04Foreground.Value);
                if (SelectedSelectedBackground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Selected_Background, SelectedSelectedBackground.Value);
                if (SelectedSelectedForeground != null)
                    ConfigStore.PutStoredKeyString(CommonConst.Registry_HighlightClass_Selected_Foreground, SelectedSelectedForeground.Value);

                // Reload colors with new settings
                LoadHighlightColors();

                System.Windows.MessageBox.Show("Color settings saved successfully. Changes are applied immediately.", "Colors Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save color settings");
                System.Windows.MessageBox.Show($"Failed to save color settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetColors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to reset all colors to their default values?",
                    "Reset Colors",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Remove all color settings from registry
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Normal_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Normal_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Friend_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Friend_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class01_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class01_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class02_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class02_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class03_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class03_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class04_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Class04_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Selected_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_Selected_Foreground);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_MouseOver_Background);
                    ConfigStore.RemoveStoredKeyString(CommonConst.Registry_HighlightClass_MouseOver_Foreground);

                    // Reload colors with defaults
                    LoadHighlightColors();

                    System.Windows.MessageBox.Show("Colors reset to defaults successfully.", "Colors Reset", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to reset color settings");
                System.Windows.MessageBox.Show($"Failed to reset color settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StatusBarTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var ollamaClient = _serviceRegistry.GetOllamaAPIClient();

                AvatarQueueLength = PlayerManager.GetQueueCount();
                OllamaQueueLength = ollamaClient?.GetQueueSize() ?? 0;

                // Update Open Logs collection
                RefreshOpenLogs();

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
                    var elapsed = DateTime.Now - currentSession.StartDateTime;
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
                var players = PlayerManager.GetAllPlayers().ToList();
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

            if (p.InstanceEndTime == null)
            {
                UpdatePlayerData(p);

                UpdatePlayerPrintData(p);

                UpdatePlayerEmojiData(p);
            }
            else
            {
                PlayerViewModel? vm = ActivePlayers.FirstOrDefault(x => x.UserId == p.UserId);
                if (vm != null)
                {
                    PastPlayers.Add(vm);
                }
            }
        }

        private void UpdatePlayerData(Player p)
        {
            PlayerViewModel? vm = ActivePlayers.FirstOrDefault(x => x.UserId == p.UserId);
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
        }

        private void UpdatePlayerEmojiData(Player p)
        {
            // If player has Emojis, add/update emoji list
            if (p.Inventory != null && p.Inventory.Count > 0)
            {
                PlayerViewModel? vmEmoji = EmojiPlayers.FirstOrDefault(x => x.UserId == p.UserId);
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

        private void UpdatePlayerPrintData(Player p)
        {
            // If player has prints, add/update print list
            if (p.PrintData != null && p.PrintData.Count > 0)
            {
                PlayerViewModel? vmPrint = PrintPlayers.FirstOrDefault(x => x.UserId == p.UserId);
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

            PurgePastOlderThan(-15);
        }

        private void PurgePastOlderThan(int minutes)
        {
            var olderThan = DateTime.Now.AddMinutes(minutes);
            List<PlayerViewModel> toRemove = [];
            foreach (PlayerViewModel oldPlayer in PastPlayers)
            {
                if (!string.IsNullOrEmpty(oldPlayer.InstanceEndTime))
                {
                    if (DateTime.TryParseExact(oldPlayer.InstanceEndTime, "u", null, System.Globalization.DateTimeStyles.None, out var endTime))
                    {
                        if (endTime < olderThan)
                        {
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
                sb.AppendLine($"Evaluation of Profile:\n");
                sb.AppendLine($"{pvm.AIEval}");

                var text = sb.ToString();

                System.Windows.Clipboard.SetText(text);
                return;
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

        private static void ToggleSort(ICollectionView view, string property)
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

        private static void UpdateHeaderSortIndicator(GridViewColumnHeader clickedHeader, ICollectionView view, string property)
        {
            // Clear indicators on sibling headers in same ListView
            var lv = FindAncestor<System.Windows.Controls.ListView>(clickedHeader);
            if (lv == null) return;

            if (lv.View is not GridView gridView) return;

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
            OverlayProfileReportReasonComboBox.ItemsSource = ProfileReportReasonsOptions;
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
                    Player? player = PlayerManager.GetPlayerByUserId(userId);

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

        // Reusable method to submit profile report - can be called from other places in the future if needed
        private async Task<bool> SubmitProfileReport(string userId, string category, string reportReason, string reportDescription)
        {
            ModerationReportPayload rpt = new()
            {
                Type = "user",
                Category = "profile",
                Reason = reportReason,
                ContentId = userId,
                Description = reportDescription
            };

            ModerationReportDetails rptDtls = new()
            {
                InstanceType = "Group Public",
                InstanceAgeGated = false
            };
            rpt.Details = [rptDtls];

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

        private void ReportPlayerPast_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                try
                {
                    ShowProfileReportOverlayPast(pvm);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to open Report Profile overlay");
                    System.Windows.MessageBox.Show($"Failed to open Report Profile overlay: {ex.Message}",
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }


        private void ShowProfileReportOverlayPast(PlayerViewModel pvm)
        {
            // Populate the overlay fields
            ProfilePastReportUserId.Text = pvm.UserId;
            // Setup report reasons for profile (includes Child Exploitation)

            ProfilePastReportReason.ItemsSource = ProfileReportReasonsOptions;
            ProfilePastReportReason.SelectedIndex = 0;

            if (string.IsNullOrEmpty(pvm.UserId))
            {
                ProfilePastReportDescription.Text = string.Empty;
            }
            else
            {
                try
                {
                    if (!string.IsNullOrEmpty(pvm.AIEval))
                    {
                        ProfilePastReportDescription.Text = pvm.AIEval;
                        ProfilePastReportReason.SelectedIndex = 0;
                        logger.Debug($"Loaded AI evaluation for user: {pvm.UserId}");
                    }
                    else
                    {
                        ProfilePastReportDescription.Text = "No AI evaluation available for this user.";
                        logger.Debug($"No AI evaluation found for user: {pvm.UserId}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error loading AI evaluation for user: {pvm.UserId}");
                    ProfilePastReportDescription.Text = $"Error loading AI evaluation: {ex.Message}";
                }
            }

            // Show the overlay
            ProfilePastReportOverlay.Visibility = Visibility.Visible;
        }

        private void OverlayProfileReportPastCancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            ProfilePastReportOverlay.Visibility = Visibility.Collapsed;

            // Clear the fields
            ProfilePastReportUserId.Text = string.Empty;
            ProfilePastReportDescription.Text = string.Empty;

            // Clear validation errors
            ClearProfileReportValidationErrors();
        }

        private async void OverlayProfileReportPastSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userId = ProfilePastReportUserId.Text.Trim();
                string category = ProfilePastReportCategory.Text;
                string reportReason = ProfilePastReportReason.SelectedValue?.ToString() ?? string.Empty;
                string reportDescription = ProfilePastReportDescription.Text;

                // Disable the submit button to prevent double-submission
                OverlayProfileReportPastSubmitButton.IsEnabled = false;

                // Call the method that will handle the future web service call
                bool success = await SubmitProfileReport(userId, category, reportReason, reportDescription);

                // Show success message
                if (!success)
                {
                    System.Windows.MessageBox.Show("Failed to submit report. Please try again later.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    OverlayProfileReportPastSubmitButton.IsEnabled = true;
                    return;
                }

                // Hide the overlay
                ProfilePastReportOverlay.Visibility = Visibility.Collapsed;

                // Clear the fields
                ProfilePastReportUserId.Text = string.Empty;
                ProfilePastReportDescription.Text = string.Empty;

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
                OverlayProfileReportPastSubmitButton.IsEnabled = true;
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
            ModerationReportPayload rpt = new()
            {
                Type = category,
                Category = category,
                Reason = reportReason,
                ContentId = printId,
                Description = reportDescription
            };

            ModerationReportDetails rptDtls = new()
            {
                InstanceType = "Group Public",
                InstanceAgeGated = false,
                HolderId = userId
            };
            rpt.Details = [rptDtls];

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

                    List<string> imageUrls = [];

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

                    ImageEvaluation? classification = await _serviceRegistry.GetOllamaAPIClient().ClassifyImageList(userId, inventoryId, imageUrlList);

                    if (classification != null)
                    {
                        PrintOverlayReportDescriptionTextBox.Text = System.Text.Encoding.UTF8.GetString(classification.Evaluation);
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
        public List<ReportReasonItem> ReportReasons { get; } =
        [
            new ReportReasonItem("Sexual Content", "sexual"),
            new ReportReasonItem("Hateful Content", "hateful"),
            new ReportReasonItem("Gore and Violence", "gore"),
            new ReportReasonItem("Other", "other")
        ];

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
            ModerationReportPayload rpt = new()
            {
                Type = category.ToLower(),
                Category = category.ToLower(),
                Reason = reportReason,
                ContentId = inventoryId,
                Description = reportDescription
            };

            ModerationReportDetails rptDtls = new()
            {
                InstanceType = "Group Public",
                InstanceAgeGated = false,
                HolderId = userId
            };
            rpt.Details = [rptDtls];

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

                    List<string> imageUrls = [];

                    if (!string.IsNullOrEmpty(inventoryItem.Metadata?.ImageUrl))
                    {
                        imageUrls.Add(inventoryItem.Metadata.ImageUrl);
                    }

                    if (!string.IsNullOrEmpty(inventoryItem.ImageUrl))
                    {
                        imageUrls.Add(inventoryItem.ImageUrl);
                    }

                    if (imageUrls.Count > 0)
                    {
                        // Load and display the image
                        LoadImage(imageUrls.First());

                        await LoadImageEvaluation(inventoryId, userId, imageUrls);
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

                    ImageEvaluation? classification = await _serviceRegistry.GetOllamaAPIClient().ClassifyImageList(userId, inventoryId, imageUrlList);

                    if (classification != null)
                    {
                        OverlayReportDescriptionTextBox.Text = System.Text.Encoding.UTF8.GetString(classification.Evaluation);
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

                        if (vm.AlertType >= AlertTypeEnum.Nuisance)
                        {
                            await _serviceRegistry.GetVRChatAPIClient().BlockAvatarGlobal(vm.AvatarId);
                        }
                        else
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
                            UserName = avatar.AuthorName ?? string.Empty,
                            AvatarName = avatar.Name ?? string.Empty,
                            ImageUrl = avatar.ImageUrl ?? string.Empty,
                            CreatedAt = avatar.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
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

        //
        // Open Logs UI handlers
        #region Open Logs handlers
        private void RefreshOpenLogs()
        {
            try
            {
                var activeTasks = FileTailer.GetActiveTailTasks();

                // Remove tasks that are no longer active
                var toRemove = OpenLogs.Where(vm => !activeTasks.ContainsKey(vm.FilePath)).ToList();
                foreach (var item in toRemove)
                {
                    OpenLogs.Remove(item);
                }

                // Add or update tasks
                foreach (var kvp in activeTasks)
                {
                    var existing = OpenLogs.FirstOrDefault(vm => vm.FilePath == kvp.Key);
                    if (existing == null)
                    {
                        OpenLogs.Add(new TailTaskViewModel(kvp.Value));
                    }
                    else
                    {
                        existing.UpdateFromStatus();
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to refresh open logs");
            }
        }

        private void CancelTailTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is TailTaskViewModel viewModel)
                {
                    viewModel.RequestCancellation();
                    logger.Info($"Cancellation requested for: {viewModel.FilePath}");
                }
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to cancel tail task");
            }
        }
        #endregion

        //
        // Migration UI handlers
        #region Migration handlers
        private void MigrationBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select avatars.sqlite file",
                Filter = "SQLite Database (*.sqlite)|*.sqlite|All Files (*.*)|*.*",
                FilterIndex = 1,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                MigrationFilePathTextBox.Text = openFileDialog.FileName;
                logger.Info($"Selected migration file: {openFileDialog.FileName}");
            }
        }

        private void MigrationCancel_Click(object sender, RoutedEventArgs e)
        {
            MigrationFilePathTextBox.Text = string.Empty;
            logger.Info("Migration file selection cleared");
        }

        private void MigrationSubmit_Click(object sender, RoutedEventArgs e)
        {
            var filePath = MigrationFilePathTextBox.Text;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                System.Windows.MessageBox.Show("Please select a file first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!System.IO.File.Exists(filePath))
            {
                System.Windows.MessageBox.Show($"The selected file does not exist:\n{filePath}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            logger.Info($"Migration file selected successfully: {filePath}");

            MigrationStatus status = _serviceRegistry.GetDBContext().MigrateOldVersion(filePath);

            StringBuilder sb = new();
            foreach (var msg in status.Messages)
            {
                sb.AppendLine(msg);
            }

            System.Windows.MessageBox.Show(sb.ToString(), "Migration Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        //
        // Window Layout Management
        #region Window Layout Management

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load window size and position
                WindowLayoutManager.LoadWindowSize(this);
                WindowLayoutManager.LoadWindowPosition(this);

                // Load column widths for Active Players tab
                LoadGridViewColumnWidths(ActiveListView, new Dictionary<string, double>
                {
                    { "DisplayName", WindowLayoutManager.DefaultActiveDisplayNameWidth },
                    { "Age", WindowLayoutManager.DefaultActiveAgeWidth },
                    { "AvatarName", WindowLayoutManager.DefaultActiveAvatarNameWidth },
                    { "InstanceStartTime", WindowLayoutManager.DefaultActiveInstanceStartWidth },
                    { "UserId", WindowLayoutManager.DefaultActiveAlertMessagesWidth },
                });

                // Load column widths for Past Players tab
                LoadGridViewColumnWidths(PastListView, new Dictionary<string, double>
                {
                    { "DisplayName", WindowLayoutManager.DefaultPastDisplayNameWidth },
                    { "Age", WindowLayoutManager.DefaultPastAgeWidth },
                    { "AvatarName", WindowLayoutManager.DefaultPastAvatarNameWidth },
                    { "InstanceEndTime", WindowLayoutManager.DefaultPastInstanceEndWidth },
                    { "UserId", WindowLayoutManager.DefaultPastAlertMessagesWidth },
                });

                // Load column widths for Known Avatars DataGrid
                LoadDataGridColumnWidths(AvatarDbGrid, new Dictionary<string, double>
                {
                    { "Alert", WindowLayoutManager.DefaultAvatarAlertWidth },
                    { "Avatar Name", WindowLayoutManager.DefaultAvatarNameWidth },
                    { "Avatar ID", WindowLayoutManager.DefaultAvatarIdWidth },
                    { "User Name", WindowLayoutManager.DefaultAvatarUserNameWidth },
                    { "Last Updated", WindowLayoutManager.DefaultAvatarUpdatedWidth },
                    { "Browser", WindowLayoutManager.DefaultAvatarBrowserWidth },
                });

                // Load column widths for Known Groups DataGrid
                LoadDataGridColumnWidths(GroupDbGrid, new Dictionary<string, double>
                {
                    { "Alert", WindowLayoutManager.DefaultGroupAlertWidth },
                    { "Group Name", WindowLayoutManager.DefaultGroupNameWidth },
                    { "Group ID", WindowLayoutManager.DefaultGroupIdWidth },
                    { "Last Updated", WindowLayoutManager.DefaultGroupUpdatedWidth },
                    { "Browser", WindowLayoutManager.DefaultGroupBrowserWidth },
                });

                // Load column widths for Known Users DataGrid
                LoadDataGridColumnWidths(UserDbGrid, new Dictionary<string, double>
                {
                    { "Display Name", WindowLayoutManager.DefaultUserDisplayNameWidth },
                    { "User ID", WindowLayoutManager.DefaultUserIdWidth },
                    { "Elapsed Time (hh:mm)", WindowLayoutManager.DefaultUserElapsedWidth },
                    { "Last Updated", WindowLayoutManager.DefaultUserUpdatedWidth },
                    { "Browser", WindowLayoutManager.DefaultUserBrowserWidth },
                });

                // Load column widths for Open Logs ListView
                LoadGridViewColumnWidths(OpenLogsListView, new Dictionary<string, double>
                {
                    { "FileName", WindowLayoutManager.DefaultLogFileNameWidth },
                    { "StartTime", WindowLayoutManager.DefaultLogOpenedWidth },
                    { "LastLineProcessedTime", WindowLayoutManager.DefaultLogLastLineWidth },
                    { "LinesProcessed", WindowLayoutManager.DefaultLogLinesProcessedWidth },
                });

                // Load splitter positions
                LoadRowSplitter("ActivePlayers", WindowLayoutManager.DefaultActiveRowSplitterHeight);
                LoadRowSplitter("PastPlayers", WindowLayoutManager.DefaultPastRowSplitterHeight);
                LoadColSplitter("ActivePlayers", WindowLayoutManager.DefaultActiveColSplitterWidth);
                LoadColSplitter("PastPlayers", WindowLayoutManager.DefaultPastColSplitterWidth);

                // Subscribe to column width change events
                SubscribeToColumnWidthChanges();

                logger.Info("Window layout loaded from registry");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load window layout");
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                WindowLayoutManager.SaveWindowSize(this);
            }
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                WindowLayoutManager.SaveWindowPosition(this);
            }
        }

        private void LoadGridViewColumnWidths(System.Windows.Controls.ListView listView, Dictionary<string, double> defaults)
        {
            if (listView.View is GridView gridView)
            {
                foreach (var column in gridView.Columns)
                {
                    if (column.Header is GridViewColumnHeader header)
                    {
                        string columnName = (header.Tag as string) ?? header.Content?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(columnName) && defaults.TryGetValue(columnName, out double value))
                        {
                            double width = WindowLayoutManager.LoadColumnWidth(
                                $"{listView.Name}_{columnName}", value);
                            column.Width = width;
                        }
                    }
                }
            }
        }

        private void LoadDataGridColumnWidths(System.Windows.Controls.DataGrid dataGrid, Dictionary<string, double> defaults)
        {
            foreach (var column in dataGrid.Columns)
            {
                string columnName = column.Header?.ToString() ?? "";
                if (!string.IsNullOrEmpty(columnName) && defaults.TryGetValue(columnName, out double value))
                {
                    double width = WindowLayoutManager.LoadColumnWidth(
                        $"{dataGrid.Name}_{columnName}", value);
                    column.Width = new DataGridLength(width);
                }
            }
        }

        private void LoadRowSplitter(string tabName, double defaultHeight)
        {
            RowDefinition? gridRow = null;
            double height = WindowLayoutManager.LoadSplitterHeight($"{tabName}_Horz", defaultHeight);

            // Find the grid for the specified tab and set the row width
            if (tabName == "ActivePlayers")
            {
                gridRow = ActivePlayerGridRow;
            }
            else if (tabName == "PastPlayers")
            {
                gridRow = PastPlayerGridRow;
            }

            if (gridRow != null)
            {
                logger.Debug($"Attempting to load splitter height for Row Splitter {tabName}, height to {height}");
                gridRow.Height = new GridLength(height, GridUnitType.Star);
            }
        }

        private void LoadColSplitter(string tabName, double defaultWidth)
        {
            ColumnDefinition? gridColumn = null;
            double width = WindowLayoutManager.LoadSplitterHeight($"{tabName}_Vert", defaultWidth);

            // Find the grid for the specified tab and set the row width
            if (tabName == "ActivePlayers")
            {
                gridColumn = ActivePlayerGridColumn;
            }
            else if (tabName == "PastPlayers")
            {
                gridColumn = PastPlayerGridColumn;
            }

            if (gridColumn != null)
            {
                logger.Debug($"Attempting to load splitter width for Col Splitter {tabName}, width to {width}");
                if (width == -1)
                {
                    gridColumn.Width = new GridLength(1, GridUnitType.Star);
                }
                else
                {
                    gridColumn.Width = new GridLength(width, GridUnitType.Pixel);
                }                
            }
        }



        private void SubscribeToColumnWidthChanges()
        {
            // Subscribe to GridView column width changes
            SubscribeToGridViewColumnChanges(ActiveListView);
            SubscribeToGridViewColumnChanges(PastListView);
            SubscribeToGridViewColumnChanges(OpenLogsListView);

            // Subscribe to DataGrid column width changes
            SubscribeToDataGridColumnChanges(AvatarDbGrid);
            SubscribeToDataGridColumnChanges(GroupDbGrid);
            SubscribeToDataGridColumnChanges(UserDbGrid);
        }

        private void SubscribeToGridViewColumnChanges(System.Windows.Controls.ListView listView)
        {
            if (listView.View is GridView gridView)
            {
                foreach (var column in gridView.Columns)
                {
                    var dpd = DependencyPropertyDescriptor.FromProperty(
                        GridViewColumn.WidthProperty,
                        typeof(GridViewColumn));

                    dpd?.AddValueChanged(column, (s, e) =>
                    {
                        if (s is GridViewColumn col && col.Header is GridViewColumnHeader header)
                        {
                            string columnName = (header.Tag as string) ?? header.Content?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(columnName))
                            {
                                WindowLayoutManager.SaveColumnWidth(
                                    $"{listView.Name}_{columnName}",
                                    col.ActualWidth);
                            }
                        }
                    });
                }
            }
        }

        private void SubscribeToDataGridColumnChanges(System.Windows.Controls.DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                var dpd = DependencyPropertyDescriptor.FromProperty(
                    DataGridColumn.ActualWidthProperty,
                    typeof(DataGridColumn));

                dpd?.AddValueChanged(column, (s, e) =>
                {
                    if (s is DataGridColumn col)
                    {
                        string columnName = col.Header?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(columnName))
                        {
                            WindowLayoutManager.SaveColumnWidth(
                                $"{dataGrid.Name}_{columnName}",
                                col.ActualWidth);
                        }
                    }
                });
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (sender is GridSplitter splitter && splitter.Parent is Grid grid)
            {
                string splitterName = splitter.Name;
                if (string.IsNullOrEmpty(splitterName))
                {
                    logger.Warn("GridSplitter has no name, cannot save position");
                    return;
                }

                string resizeDirection = splitter.ResizeDirection.ToString();
                logger.Debug($"Grid Splitter '{splitterName}' dragged, ResizeDirection: {resizeDirection}");

                if (resizeDirection == "Rows")
                {
                    // Horizontal splitter - save row width
                    int rowIndex = Grid.GetRow(splitter);
                    if (rowIndex + 1 < grid.RowDefinitions.Count)
                    {
                        var rowDef = grid.RowDefinitions[rowIndex + 1];
                        if (rowDef.Height.IsStar || rowDef.Height.IsAbsolute)
                        {
                            double height = rowDef.Height.IsStar ? rowDef.Height.Value : rowDef.ActualHeight;
                            WindowLayoutManager.SaveSplitterPosition(splitterName, height);
                            logger.Debug($"Saved row width for splitter '{splitterName}': {height}");
                        }
                    }
                }
                else if (resizeDirection == "Columns")
                {
                    // Vertical splitter - save column width
                    int colIndex = Grid.GetColumn(splitter);
                    if (colIndex == 1)
                    {
                        var colDef = grid.ColumnDefinitions[0];
                        if (colDef.Width.IsStar || colDef.Width.IsAbsolute)
                        {
                            double width = colDef.Width.IsStar ? colDef.Width.Value : colDef.ActualWidth;
                            WindowLayoutManager.SaveSplitterPosition(splitterName, width);
                            logger.Debug($"Saved column width for splitter '{splitterName}': {width}");
                        }
                    }
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void ResetLayout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "This will reset the window size, position, all column widths, and splitter positions to their default values. Do you want to continue?",
                    "Reset Layout",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    WindowLayoutManager.ResetLayoutSettings();

                    System.Windows.MessageBox.Show(
                        "Layout settings have been reset. Please restart the application for changes to take effect.",
                        "Reset Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    logger.Info("Layout settings reset to defaults");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to reset layout settings");
                System.Windows.MessageBox.Show(
                    $"Failed to reset layout: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

        #region Ban Management handlers
        private ObservableCollection<GroupBanItem> _banMgmtGroupList = [];
        private string _currentBanMgmtUserId = string.Empty;
        private User? _currentBanMgmtUser = null;

        private async void BanMgmtLoadUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userId = BanMgmtUserIdTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    BanMgmtUserStatusText.Text = "Please enter a User ID";
                    BanMgmtUserStatusText.Foreground = System.Windows.Media.Brushes.Yellow;
                    return;
                }

                if (!userId.StartsWith("usr_"))
                {
                    BanMgmtUserStatusText.Text = "Invalid User ID format (must start with usr_)";
                    BanMgmtUserStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                BanMgmtUserStatusText.Text = "Loading...";
                BanMgmtUserStatusText.Foreground = System.Windows.Media.Brushes.Yellow;

                // Call GetProfile
                var user = _serviceRegistry.GetVRChatAPIClient().GetProfile(userId);

                if (user == null || string.IsNullOrEmpty(user.Id))
                {
                    BanMgmtUserStatusText.Text = "User not found";
                    BanMgmtUserStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    BanMgmtUserInfoGroup.Visibility = Visibility.Collapsed;
                    return;
                }

                _currentBanMgmtUser = user;
                _currentBanMgmtUserId = userId;

                logger.Info($"Fetched user profile for ban management: {user.DisplayName} ({user})");

                // Populate user info
                BanMgmtUserName.Text = user.DisplayName ?? "Unknown";
                BanMgmtUserStatusDesc.Text = user.StatusDescription ?? user.Status.ToString();
                BanMgmtUserPronouns.Text = string.IsNullOrEmpty(user.Pronouns) ? "Not specified" : user.Pronouns;
                BanMgmtUserJoinDate.Text = user.DateJoined.ToString("yyyy-MM-dd");
                BanMgmtUserAgeVerified.Text = user.AgeVerified ? "Yes" : "No";
                BanMgmtUserState.Text = user.State.ToString() ;


                string? accountThumbnailUrl = !string.IsNullOrEmpty(user.ProfilePicOverrideThumbnail) ? user.ProfilePicOverrideThumbnail : user.CurrentAvatarThumbnailImageUrl;

                // Load profile image if available
                if (!string.IsNullOrEmpty(accountThumbnailUrl))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(accountThumbnailUrl);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        BanMgmtUserImage.Source = bitmap;
                    }
                    catch
                    {
                        BanMgmtUserImage.Source = null;
                    }
                }
                else
                {
                    BanMgmtUserImage.Source = null;
                }

                BanMgmtUserStatusText.Text = "User loaded successfully";
                BanMgmtUserStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                BanMgmtUserInfoGroup.Visibility = Visibility.Visible;
                BanMgmtGroupListGroup.Visibility = Visibility.Visible;

                // Load groups from database
                await LoadBanManagementGroupsAsync();

                logger.Info($"Loaded user profile for ban management: {user.DisplayName} ({userId})");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading user for ban management");
                BanMgmtUserStatusText.Text = $"Error: {ex.Message}";
                BanMgmtUserStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private async Task LoadBanManagementGroupsAsync()
        {
            try
            {
                _banMgmtGroupList.Clear();

                // Load all groups from the database
                var groups = _serviceRegistry.GetDBContext().GroupManagements.ToList();

                foreach (var group in groups)
                {
                    var item = new GroupBanItem
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        Status = "Checking...",
                        CanBan = false,
                        CanUnban = false
                    };

                    _banMgmtGroupList.Add(item);

                    // Check member status asynchronously
                    _ = Task.Run(async () =>
                    {
                        var status = await _serviceRegistry.GetVRChatAPIClient().GetGroupMemberStatus(group.GroupId, _currentBanMgmtUserId);

                        await Dispatcher.InvokeAsync(() =>
                        {
                            item.Status = status switch
                            {
                                VRChatClient.TGGroupMemberStatus.Member => "Member",
                                VRChatClient.TGGroupMemberStatus.Banned => "Banned",
                                VRChatClient.TGGroupMemberStatus.NotMember => "Not Member",
                                _ => "Unknown"
                            };

                            item.CanBan = status != VRChatClient.TGGroupMemberStatus.Banned && status != VRChatClient.TGGroupMemberStatus.Unknown;
                            item.CanUnban = status == VRChatClient.TGGroupMemberStatus.Banned;
                        });
                    });
                }

                BanMgmtGroupList.ItemsSource = _banMgmtGroupList;
                logger.Info($"Loaded {_banMgmtGroupList.Count} groups for ban management");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading ban management groups");
            }
        }

        private async void BanMgmtAddGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
                string groupId = BanMgmtAddGroupIdTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(groupId))
                {
                    System.Windows.MessageBox.Show("Please enter a Group ID", 
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!groupId.StartsWith("grp_"))
                {
                    System.Windows.MessageBox.Show("Invalid Group ID format (must start with grp_)", 
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if group already exists in database
                var existingGroup = dBContext.GroupManagements.FirstOrDefault(g => g.GroupId == groupId);

                if (existingGroup != null)
                {
                    System.Windows.MessageBox.Show("This group already exists in the database", 
                        "Duplicate Group", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Verify group exists in VRChat
                var group = _serviceRegistry.GetVRChatAPIClient().GetGroupById(groupId);
                if (group == null || string.IsNullOrEmpty(group.Id))
                {
                    System.Windows.MessageBox.Show("Group not found in VRChat. Please verify the Group ID.", 
                        "Group Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Add to database
                var newGroup = new tailgrab.src.Models.GroupManagement
                {
                    GroupId = groupId,
                    GroupName = group.Name ?? "Unknown",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dBContext.Add(newGroup);
                dBContext.SaveChanges();

                // Add to the UI list
                var item = new GroupBanItem
                {
                    GroupId = groupId,
                    GroupName = group.Name ?? "Unknown",
                    Status = "Checking...",
                    CanBan = false,
                    CanUnban = false
                };

                _banMgmtGroupList.Add(item);

                // Check member status
                var status = await _serviceRegistry.GetVRChatAPIClient().GetGroupMemberStatus(groupId, _currentBanMgmtUserId);

                item.Status = status switch
                {
                    VRChatClient.TGGroupMemberStatus.Member => "Member",
                    VRChatClient.TGGroupMemberStatus.Banned => "Banned",
                    VRChatClient.TGGroupMemberStatus.NotMember => "Not Member",
                    _ => "Unknown"
                };

                item.CanBan = status != VRChatClient.TGGroupMemberStatus.Banned && status != VRChatClient.TGGroupMemberStatus.Unknown;
                item.CanUnban = status == VRChatClient.TGGroupMemberStatus.Banned;

                // Clear the text box
                BanMgmtAddGroupIdTextBox.Text = string.Empty;

                logger.Info($"Added group {groupId} ({group.Name}) to ban management");
                System.Windows.MessageBox.Show($"Group '{group.Name}' added successfully", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding group to ban management");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BanMgmtRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
                if (sender is System.Windows.Controls.Button button && button.Tag is GroupBanItem item)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Are you sure you want to remove group '{item.GroupName}' from the list?\n\nThis will remove it from the database.",
                        "Confirm Remove",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    // Remove from database
                    var dbGroup = dBContext.GroupManagements.FirstOrDefault(g => g.GroupId == item.GroupId);

                    if (dbGroup != null)
                    {
                        dBContext.GroupManagements.Remove(dbGroup);
                        dBContext.SaveChanges();
                    }

                    // Remove from UI
                    _banMgmtGroupList.Remove(item);

                    logger.Info($"Removed group {item.GroupId} ({item.GroupName}) from ban management");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error removing group from ban management");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BanMgmtBanUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is GroupBanItem item)
                {
                    if (string.IsNullOrWhiteSpace(item.GroupId) || string.IsNullOrWhiteSpace(_currentBanMgmtUserId))
                    {
                        return;
                    }

                    var result = MessageBoxResult.Yes;
                    //var result = System.Windows.MessageBox.Show(
                    //    $"Are you sure you want to ban {_currentBanMgmtUser?.DisplayName} from group {item.GroupName}?",
                    //    "Confirm Ban",
                    //    MessageBoxButton.YesNo,
                    //    MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    button.IsEnabled = false;
                    item.Status = "Banning...";

                    bool success = await _serviceRegistry.GetVRChatAPIClient().BanUserFromGroup(item.GroupId, _currentBanMgmtUserId);

                    if (success)
                    {
                        item.Status = "Banned";
                        item.CanBan = false;
                        item.CanUnban = true;
                        logger.Info($"Banned user {_currentBanMgmtUserId} from group {item.GroupId}");
                        //System.Windows.MessageBox.Show("User banned successfully", "Success", 
                        //    MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        item.Status = "Ban Failed";
                        button.IsEnabled = true;
                        //System.Windows.MessageBox.Show("Failed to ban user", "Error", 
                        //    MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error banning user from group");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BanMgmtUnbanUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is GroupBanItem item)
                {
                    if (string.IsNullOrWhiteSpace(item.GroupId) || string.IsNullOrWhiteSpace(_currentBanMgmtUserId))
                    {
                        return;
                    }
                    var result = MessageBoxResult.Yes;
                    //var result = System.Windows.MessageBox.Show(
                    //    $"Are you sure you want to unban {_currentBanMgmtUser?.DisplayName} from group {item.GroupName}?",
                    //    "Confirm Unban",
                    //    MessageBoxButton.YesNo,
                    //    MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    button.IsEnabled = false;
                    item.Status = "Unbanning...";

                    bool success = await _serviceRegistry.GetVRChatAPIClient().UnbanUserFromGroup(item.GroupId, _currentBanMgmtUserId);

                    if (success)
                    {
                        item.Status = "Not Member";
                        item.CanBan = true;
                        item.CanUnban = false;
                        logger.Info($"Unbanned user {_currentBanMgmtUserId} from group {item.GroupId}");
                        //System.Windows.MessageBox.Show("User unbanned successfully", "Success", 
                        //    MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        item.Status = "Unban Failed";
                        button.IsEnabled = true;
                        //System.Windows.MessageBox.Show("Failed to unban user", "Error", 
                        //    MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error unbanning user from group");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

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

        private void TestProfilePromptInput_Changed(object sender, SelectionChangedEventArgs e)
        {

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
        public string ProfileElapsedTime { get; private set; } = "N/A";
        public bool IsWatched { get; set; } = false;
        public string History { get; set; } = string.Empty;
        public string AlertMessages { get; set; } = string.Empty;
        public ObservableCollection<PrintInfoViewModel> Prints { get; private set; } = [];
        public ObservableCollection<EmojiInfoViewModel> Emojis { get; private set; } = [];
        private bool IsFriend {  get; set; }
        public string ProfileUrl { get; set; }


        private string _AlertColor = "Normal";
        public string HighlightClass
        {
            get
            {
                return _AlertColor;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            ProfileElapsedTime = p.ProfileElapsedTime;
            IsWatched = p.IsWatched;
            AlertMessages = p.AlertMessage;
            _AlertColor = p.AlertColor;
            IsFriend = p.IsFriend;
            ProfileUrl = p.ProfileImage;

            PopulateCollectionsFromPlayer(p); ;
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
            if (ProfileElapsedTime != p.ProfileElapsedTime) { ProfileElapsedTime = p.ProfileElapsedTime; changed = true; }
            if (IsWatched != p.IsWatched) { IsWatched = p.IsWatched; changed = true; }
            if (AlertMessages != p.AlertMessage) { AlertMessages = p.AlertMessage ?? string.Empty; changed = true; }
            if (_AlertColor != p.AlertColor) { _AlertColor = p.AlertColor; changed = true; }
            if (IsFriend != p.IsFriend) { IsFriend = p.IsFriend; changed = true; }
            if (ProfileUrl != p.ProfileImage) { ProfileUrl = p.ProfileImage; changed = true; }

            if (changed) OnPropertyChanged(string.Empty);

            PopulateCollectionsFromPlayer(p); ;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PlayerViewModel:");
            sb.AppendLine($"DisplayName: {DisplayName}");
            sb.AppendLine($"UserId: {UserId}");
            sb.AppendLine($"AvatarName: {AvatarName}");
            sb.AppendLine($"PenActivity: {PenActivity}");
            sb.AppendLine($"InstanceStartTime: {InstanceStartTime}");
            sb.AppendLine($"InstanceEndTime: {InstanceEndTime}");
            sb.AppendLine($"Profile: {Profile}");
            sb.AppendLine($"AIEval: {AIEval}");
            sb.AppendLine($"IsWatched: {IsWatched}");
            sb.AppendLine($"Prints (Count): {Prints.Count}");
            sb.AppendLine($"Emojis (Count): {Emojis.Count}");
            sb.AppendLine($"History: {History}");
            sb.AppendLine($"AlertColor: {_AlertColor}");
            sb.AppendLine($"AlertMessages: {AlertMessages}");
            sb.AppendLine($"IsFriend: {IsFriend}");
            sb.AppendLine($"ProfileUrl: {ProfileUrl}");
            return sb.ToString();
        }
        private void PopulateCollectionsFromPlayer(Player p)
        {
            // Print Collection
            Prints.Clear();
            if (p.PrintData != null)
            {
                foreach (var pr in p.PrintData.Values)
                {
                    Prints.Add(new PrintInfoViewModel(pr));
                }
            }

            // Emoji and Sticker Inventory Collection
            Emojis.Clear();
            if (p.Inventory != null)
            {
                foreach (var inv in p.Inventory)
                {
                    Emojis.Add(new EmojiInfoViewModel(p.UserId, inv));
                }
            }

            // Event History
            string history = string.Empty;
            foreach (var hist in p.Events)
            {
                history += $"({hist.EventTime:u} - {hist.EventDescription})\n";
            }
            History = history.TrimEnd();
        }
    }

    public class PrintInfoViewModel(PlayerPrint p) : INotifyPropertyChanged
    {
        public string PrintId { get; set; } = p.PrintId;
        public string OwnerId { get; set; } = p.OwnerId;
        public DateTime CreatedAt { get; set; } = p.CreatedAt;
        public DateTime Timestamp { get; set; } = p.Timestamp;
        public string PrintUrl { get; set; } = p.PrintUrl;
        public string AIEvaluation { get; set; } = p.AIEvaluation;
        public string AIClass { get; set; } = p.AIClass;
        public string AuthorName { get; set; } = p.AuthorName;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class EmojiInfoViewModel(string userId, PlayerInventory i)
    {
        public string UserId { get; set; } = userId;
        public string InventoryId { get; set; } = i.InventoryId;
        public DateTime SpawnedAt { get; set; } = i.SpawnedAt;
        public string ImageUrl { get; set; } = i.ItemUrl;
        public string InventoryType { get; set; } = i.InventoryType;
        public string AIEvalutation { get; set; } = i.AIEvaluation;
    }

    public class TailTaskViewModel : INotifyPropertyChanged
    {
        private readonly FileTailStatus _status;

        public string FilePath => _status.FilePath;
        public string FileName => Path.GetFileName(_status.FilePath);
        public DateTime StartTime => _status.StartTime;
        public int LinesProcessed => _status.LinesProcessed;
        public DateTime? LastLineProcessedTime => _status.LastLineProcessedTime;

        public string LastLineProcessedTimeFormatted => 
            LastLineProcessedTime.HasValue ? LastLineProcessedTime.Value.ToString("u") : "N/A";

        public TailTaskViewModel(FileTailStatus status) => _status = status;

        public void UpdateFromStatus()
        {
            OnPropertyChanged(nameof(LinesProcessed));
            OnPropertyChanged(nameof(LastLineProcessedTime));
            OnPropertyChanged(nameof(LastLineProcessedTimeFormatted));
        }

        public void RequestCancellation()
        {
            _status.RequestCancellation();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ReportReasonItem(string displayName, string value)
    {
        public string DisplayName { get; set; } = displayName;
        public string Value { get; set; } = value;
    }
    #endregion
}