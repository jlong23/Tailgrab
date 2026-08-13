using BuildSoft.VRChat.Osc;
using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.Models;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{

    public static class TailgrabPanelConst
    {
        public const int ZINDEX_LEVEL_NORMAL = 0;
        public const int ZINDEX_LEVEL_OVERLAY = 1000;
        public const int ZINDEX_LEVEL_REPORT_OVERLAY = 1010;
        public const int ZINDEX_LEVEL_MESSAGE_OVERLAY = 1050;
    }

    public partial class TailgrabPanel : Window, IDisposable, INotifyPropertyChanged
    {
        public const int CONST_CONFIG_TAB_INDEX = 5;
        public const int CONST_BAN_MGMT_TAB_INDEX = 9;

        public static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Initialize synthesizer
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();

        protected ServiceRegistry _serviceRegistry;

        private readonly DispatcherTimer fallbackTimer;
        private readonly DispatcherTimer statusBarTimer;

        private string SelectedAvatarId = string.Empty;

        public ObservableCollection<PlayerViewModel> ActivePlayers { get; } = [];
        public ObservableCollection<PlayerViewModel> PastPlayers { get; } = [];
        public ObservableCollection<PlayerViewModel> PrintPlayers { get; } = [];
        public ObservableCollection<PlayerViewModel> EmojiPlayers { get; } = [];
        public ObservableCollection<TailTaskViewModel> OpenLogs { get; } = [];
        public AvatarVirtualizingCollection AvatarDbItems { get; private set; }
        public GroupVirtualizingCollection GroupDbItems { get; private set; }
        public UserVirtualizingCollection UserDbItems { get; private set; }
        public ModerationVirtualizingCollection ModerationDbItems { get; private set; }

        public ICollectionView AvatarDbView { get; }
        public ICollectionView ActiveView { get; }
        public ICollectionView GroupDbView { get; }
        public ICollectionView UserDbView { get; }
        public ICollectionView ModerationDbView { get; }
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

        private string _groupGistStatus = string.Empty;
        public string GroupGistStatus
        {
            get => _groupGistStatus;
            set
            {
                if (_groupGistStatus != value)
                {
                    _groupGistStatus = value;
                    OnPropertyChanged(nameof(GroupGistStatus));
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

        private string _OverlayMessageTitle = string.Empty;
        public string OverlayMessageTitle {
            get => _OverlayMessageTitle;
            set
            {
                if (_OverlayMessageTitle != value)
                {
                    _OverlayMessageTitle = value;
                    OnPropertyChanged(nameof(OverlayMessageTitle));
                }
            }
        }

        private string _OverlayMessageBody = string.Empty;
        public string OverlayMessageBody {
            get => _OverlayMessageBody;
            set
            {
                if (_OverlayMessageBody != value)
                {
                    _OverlayMessageBody = value;
                    OnPropertyChanged(nameof(OverlayMessageBody));
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

        public List<ReportReasonItem> AvatarReportReasonsOptions =
        [
            new ReportReasonItem("Sexual Content", "sexual"),
            new ReportReasonItem("Harassment or Bullying", "harassing"),
            new ReportReasonItem("Hateful and Discriminatory Content", "hateful"),
            new ReportReasonItem("Violence or Gore", "gore"),
            new ReportReasonItem("Suicide and Self-Harm", "suicide"),
            new ReportReasonItem("Integrity and Authenticity", "integrity"),
            new ReportReasonItem("Child Exploitation and abuse", "child"),
            new ReportReasonItem("Copyright", "copyright"),
            new ReportReasonItem("Something Else", "other")
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

            // Hook paste event for AvatarDbFilterBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(AvatarDbFilterBox, AvatarSelectionTextBox_Pasting);

            // Hook paste event for GroupDbFilterBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(GroupDbFilterBox, GroupSelectionTextBox_Pasting);
            // Hook paste event for BanMgmtAddGroupIdTextBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(BanMgmtAddGroupIdTextBox, GroupSelectionTextBox_Pasting);

            // Hook paste event for ActiveFilterBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(ActiveFilterBox, UserSelectionTextBox_Pasting);
            // Hook paste event for PastFilterBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(PastFilterBox, UserSelectionTextBox_Pasting);
            // Hook paste event for BanMgmtUserIdTextBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(UserDbFilterBox, UserSelectionTextBox_Pasting);
            // Hook paste event for BanMgmtUserIdTextBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(BanMgmtUserIdTextBox, UserSelectionTextBox_Pasting);
            // Hook paste event for OverlayUserIdTextBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(OverlayUserIdTextBox, UserSelectionTextBox_Pasting);
            // User Account Test Box for Ollama testing
            System.Windows.DataObject.AddPastingHandler(UserAccountTestBox, UserSelectionTextBox_Pasting);

            // Hook paste event for EmojiFilterBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(EmojiFilterBox, InventorySelectionTextBox_Pasting);
            // Hook paste event for OverlayInventoryIdTextBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(OverlayInventoryIdTextBox, InventorySelectionTextBox_Pasting);

            // Hook paste event for PrintFilterBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(PrintFilterBox, PrintSelectionTextBox_Pasting);
            // Hook paste event for PrintOverlayInventoryIdTextBox to clear on paste
            System.Windows.DataObject.AddPastingHandler(PrintOverlayInventoryIdTextBox, PrintSelectionTextBox_Pasting);

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

            ModerationDbItems = new ModerationVirtualizingCollection(_serviceRegistry);
            ModerationDbView = CollectionViewSource.GetDefaultView(ModerationDbItems);
            ModerationDbView.SortDescriptions.Add(new SortDescription("EventDateTime", ListSortDirection.Descending));

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
            var xsOverlayLevel = ConfigStore.GetStoredKeyString(CommonConst.Registry_XSOverlay_Level) ?? CommonConst.XSOverlay_Level_None;

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
            if (!string.IsNullOrEmpty(xsOverlayLevel) && AlertTypeOptions.Any(o => o.Key == xsOverlayLevel))
                XSOverlayNotifications.SelectedValue = xsOverlayLevel;

            ModeratedAvatarCaching.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, true);
            DiscoveredAvatarCaching.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, true);
            DiscoveredGroupCaching.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Group_Caching, true);


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
            RefreshModerationDb();

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

            InitializeLineMatchersTab();

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

            synthesizer.SelectVoice("Microsoft Zira Desktop");
            // Speak text asynchronously to avoid freezing the UI
            synthesizer.SpeakAsync("Tail Grab is up and running");
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

                ConfigStore.PutStoredKeyString(CommonConst.Registry_XSOverlay_Level, XSOverlayNotifications.SelectedValue.ToString() ?? CommonConst.XSOverlay_Level_None);

                System.Windows.MessageBox.Show("Configuration saved. Restart the Applicaton for all changes to take affect.", "Config", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Message Overlay Handling
        public void ShowOverlayMessage(string title, string body)
        {
            OverlayMessageTitle = title;
            OverlayMessageBody = body;
            OverlayMessageBodyTextBox.Text = body;
            OverlayMessage.Visibility = Visibility.Visible;
        }

        public void HideOverlayMessage(object sender, RoutedEventArgs e)
        {
            OverlayMessage.Visibility = Visibility.Collapsed;
            OverlayMessageTitle = string.Empty;
            OverlayMessageBody = string.Empty;
            OverlayMessageBodyTextBox.Text = string.Empty;
        }
        #endregion

        #region Config / Secret Tab Handling
        private void Reset2FA_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "This will erase your Two Factor Authentication Seed Key. Are you sure you want to continue?",
                    "Confirm 2FA Key Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_2FactorKey);
                Vr2FaBox.Password = string.Empty;
                Vr2FaBox.ToolTip = null;
                System.Windows.MessageBox.Show("2FA key reset. Please re-enter your 2FA key or leave blank for Prompting of the One Time Codes.", "Reset 2FA", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to reset 2FA key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (groupGistUrl.Text != null)
                {
                    groupGistCheckButton.IsEnabled = false;
                    groupGistCheckButton.Content = "Checking...";
                    //System.Windows.MessageBox.Show("Group GIST list processing in the background.", "Check Group GIST", MessageBoxButton.OK, MessageBoxImage.Information);
                    await _serviceRegistry.ProcessGroupGist(groupGistUrl.Text, true);
                }
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

        private void ExportAvatarGist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var context = _serviceRegistry.GetDBContext();
                if (context == null)
                {
                    System.Windows.MessageBox.Show("Database context is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var avatarInfos = context.AvatarInfos
                    .Where(a => a.AlertType > AlertTypeEnum.None)
                    .OrderBy(a => a.AvatarName)
                    .ToList();

                if (avatarInfos.Count == 0)
                {
                    System.Windows.MessageBox.Show("No avatars with alerts found to export.", "Export to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                foreach (var avatar in avatarInfos)
                {
                    string alertTypeString = avatar.AlertType switch
                    {
                        AlertTypeEnum.Watch => "Watch",
                        AlertTypeEnum.Nuisance => "Nuisance",
                        AlertTypeEnum.Crasher => "Crasher",
                        _ => "NONE"
                    };

                    sb.AppendLine($"\"{avatar.AvatarId}\",\"{avatar.AvatarName}\",\"{alertTypeString}\"");
                }

                string result = sb.ToString();
                System.Windows.Clipboard.SetText(result);

                System.Windows.MessageBox.Show($"Exported {avatarInfos.Count} Avatar(s) to clipboard.", "Export to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to export Avatar GIST data");
                System.Windows.MessageBox.Show($"Failed to export Avatar data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportGroupGist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var context = _serviceRegistry.GetDBContext();
                if (context == null)
                {
                    System.Windows.MessageBox.Show("Database context is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var groupInfos = context.GroupInfos
                    .Where(g => g.AlertType > AlertTypeEnum.None)
                    .OrderBy(g => g.GroupName)
                    .ToList();

                if (groupInfos.Count == 0)
                {
                    System.Windows.MessageBox.Show("No Groups with alerts found to export.", "Export to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                foreach (var group in groupInfos)
                {
                    string alertTypeString = group.AlertType switch
                    {
                        AlertTypeEnum.Watch => "Watch",
                        AlertTypeEnum.Nuisance => "Nuisance",
                        AlertTypeEnum.Crasher => "Crasher",
                        _ => "NONE"
                    };

                    sb.AppendLine($"\"{group.GroupId}\",\"{group.GroupName}\",\"{alertTypeString}\"");
                }

                string result = sb.ToString();
                System.Windows.Clipboard.SetText(result);

                System.Windows.MessageBox.Show($"Exported {groupInfos.Count} group(s) to clipboard.", "Export to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to export group GIST data");
                System.Windows.MessageBox.Show($"Failed to export group data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Config / AI Config Tab Handling
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

        private void ResetOllama_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "This will erase your Ollama Configuration. Are you sure you want to continue?",
                    "Confirm Ollama Configuration Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;


                ConfigStore.DeleteSecret(CommonConst.Registry_Ollama_API_Key);
                ConfigStore.DeleteSecret(CommonConst.Registry_Ollama_API_Endpoint);
                ConfigStore.DeleteSecret(CommonConst.Registry_Ollama_API_Model);
                VrOllamaBox.ToolTip = null;
                VrOllamaBox.Password = string.Empty;
                VrOllamaEndpointBox.Text = string.Empty;
                VrOllamaModelBox.SelectedValue = null;
                System.Windows.MessageBox.Show("Ollama Configuration reset. Please re-enter your API key or leave blank for no Profile & Image AI Evalutation.", "Reset Ollama Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to reset Ollama Configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                ProfileEvaluation? result = await Clients.Ollama.OllamaClient.TestProfilePrompt(_serviceRegistry, userId, prompt, model);
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

                    // Stub implementation - create placeholder items
                    foreach (string imageName in testImages)
                    {
                        string? imagePath = GetTestImagePath(imageName);
                        logger.Info("Processing test image {ImageName} at path {ImagePath}", imageName, imagePath);
                        if (imagePath != null)
                        {

                            string evaluation = await OllamaClient.TestImagePrompt(model, prompt, imagePath);

                            Models.TestImageAIEvalItem item = new()
                            {
                                ImagePath = imagePath,
                                AIEvaluation = evaluation,
                                AlertInfo = AIEvalutionEnumMapper.MapEnumToAlertDisplayItem(evaluation)
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
        #endregion

        #region Test Image Evaluation Overlay Handling
        private void CloseTestImageEval_Click(object sender, RoutedEventArgs e)
        {
            OverlayTestImageEval.Visibility = Visibility.Collapsed;
            TestImageAIEvalItems.Clear();
        }
        #endregion

        #region Test Image Evaluation Handling
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
        #endregion

        #region Config / Alert Tab Handling
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
        #endregion

        private void StatusBarTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var ollamaClient = _serviceRegistry.GetOllamaAPIClient();

                AvatarQueueLength = AvatarManager.GetQueueCount();
                OllamaQueueLength = ollamaClient?.GetQueueSize() ?? 0;
                GroupGistStatus = _serviceRegistry.GetGroupManager().GetQueueSize();

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

                if (EmojiView.Filter != null)
                {
                    EmojiView.Refresh();
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

                if (PrintView.Filter != null)
                {
                    PrintView.Refresh();
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

        private void ActiveViewUserGroups_Click(object sender, RoutedEventArgs e)
        {

            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                ShowUserGroupsOverlay(pvm.UserId, pvm.DisplayName);
            }
        }

        private void PastViewUserGroups_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                ShowUserGroupsOverlay(SelectedPast.UserId, SelectedPast.DisplayName);
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a player first.", "No Player Selected", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void BanPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                string userId = pvm.UserId;
                SwitchToBanManagementTab(sender, e, userId);
            }
        }

        private void SwitchToBanManagementTab(object sender, RoutedEventArgs e, string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                // Set the user ID in Ban Management tab
                BanMgmtUserIdTextBox.Text = userId;

                // Activate the Config tab (index 4) in main TabControl
                MainTabControl.SelectedIndex = CONST_CONFIG_TAB_INDEX;

                // Activate the Ban Management tab (index 10) in Config TabControl
                ConfigTabControl.SelectedIndex = CONST_BAN_MGMT_TAB_INDEX;

                // Call the load user function
                BanMgmtLoadUser_Click(sender, e);
            }
        }


        private void BanAvatarOwner_Click(object sender, RoutedEventArgs e)
        {
            // Get the owner ID from the BanMgmtAvatarOwnerId TextBlock
            string ownerId = BanMgmtAvatarOwnerId.Text;

            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                SwitchToBanManagementTab(sender, e, ownerId);
            }
        }

        private void BanGroupOwner_Click(object sender, RoutedEventArgs e)
        {
            // Get the owner ID from the BanMgmtGroupOwnerId TextBlock
            string ownerId = BanMgmtGroupOwnerId.Text;

            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                SwitchToBanManagementTab(sender, e, ownerId);
            }
        }

        private void GroupCheckAvatars_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userId = BanMgmtGroupOwnerId.Text.Trim();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    System.Windows.MessageBox.Show("Please enter a User ID first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!userId.StartsWith("usr_"))
                {
                    System.Windows.MessageBox.Show("Invalid User ID format (must start with usr_).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get display name if user is already loaded, otherwise use userId
                string displayName = BanMgmtGroupOwner.Text ?? userId;

                // Call ShowUserAvatarsOverlay with the userId and display name
                ShowUserAvatarsOverlay(userId, displayName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking groups for user");
                System.Windows.MessageBox.Show($"Failed to check groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReportAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is UserAvatarViewModel uavm)
            {
                string avatarId = uavm.AvatarId;
                try
                {
                    ShowAvatarReportOverlay(avatarId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to open Report Avatar overlay");
                    System.Windows.MessageBox.Show($"Failed to open Report Avatar overlay: {ex.Message}",
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
                bool success = await _serviceRegistry.GetModerationManager().SubmitProfileReport(userId, category, reportReason, reportDescription);

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

        private void ShowAvatarReportOverlay(string avatarId)
        {
            // Populate the overlay fields
            OverlayAvatarReportAvatarIdTextBox.Text = avatarId.Trim();

            // Setup report reasons for avatar (includes Child Exploitation)
            OverlayAvatarReportReasonComboBox.ItemsSource = AvatarReportReasonsOptions;
            OverlayAvatarReportReasonComboBox.SelectedIndex = 0;

            if (string.IsNullOrEmpty(avatarId))
            {
                OverlayAvatarReportDescriptionTextBox.Text = string.Empty;
            }
            else
            {
                try
                {
                    // Get the avatar from ServiceRegistry
                    Result<Avatar?> result = _serviceRegistry.GetVRChatAPIClient().GetAvatarById(avatarId);

                    if (result.Value != null)
                    {
                        OverlayAvatarReportDescriptionTextBox.Text = $"{result.Value.Name} by \"{result.Value.AuthorName}\"\nID: {result.Value.Id}\n{result.Value.Description}";
                        logger.Debug($"Loaded Base Description for avatar: {avatarId}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error loading Base Description  for avatar: {avatarId}");
                    OverlayAvatarReportDescriptionTextBox.Text = $"Error loading Base Description: {ex.Message}";
                }
            }

            // Clear any validation errors
            ClearAvatarReportValidationErrors();

            // Show the overlay
            OverlayAvatarReport.Visibility = Visibility.Visible;
        }

        private void ClearAvatarReportValidationErrors()
        {
            // Reset AvatarID field
            OverlayAvatarReportAvatarIdTextBox.BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
            OverlayAvatarReportAvatarIdTextBox.BorderThickness = new Thickness(1);
            OverlayAvatarReportAvatarIdError.Visibility = Visibility.Collapsed;
        }

        private bool ValidateAvatarReportFields()
        {
            bool isValid = true;

            // Clear any previous validation errors first
            ClearAvatarReportValidationErrors();

            // Validate Avatar ID
            if (string.IsNullOrWhiteSpace(OverlayAvatarReportAvatarIdTextBox.Text))
            {
                OverlayAvatarReportAvatarIdTextBox.BorderBrush = new SolidColorBrush(Colors.Yellow);
                OverlayAvatarReportAvatarIdTextBox.BorderThickness = new Thickness(3);
                OverlayAvatarReportAvatarIdError.Visibility = Visibility.Visible;
                isValid = false;
            }

            return isValid;
        }

        private void OverlayAvatarReportCancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            OverlayAvatarReport.Visibility = Visibility.Collapsed;

            // Clear the fields
            OverlayAvatarReportAvatarIdTextBox.Text = string.Empty;
            OverlayAvatarReportDescriptionTextBox.Text = string.Empty;

            // Clear validation errors
            ClearAvatarReportValidationErrors();
        }

        private async void OverlayAvatarReportSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (!ValidateAvatarReportFields())
                {
                    return;
                }

                string avatarId = OverlayAvatarReportAvatarIdTextBox.Text.Trim();
                string category = OverlayAvatarReportCategoryTextBox.Text;
                string reportReason = OverlayAvatarReportReasonComboBox.SelectedValue?.ToString() ?? string.Empty;
                string reportDescription = OverlayAvatarReportDescriptionTextBox.Text;

                // Disable the submit button to prevent double-submission
                OverlayAvatarReportSubmitButton.IsEnabled = false;

                // Call the method that will handle the future web service call
                bool success = await _serviceRegistry.GetModerationManager().SubmitAvatarReport(avatarId, category, reportReason, reportDescription);

                // Show success message
                if (!success)
                {
                    System.Windows.MessageBox.Show("Failed to submit report. Please try again later.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    OverlayAvatarReportSubmitButton.IsEnabled = true;
                    return;
                }

                // Hide the overlay
                OverlayAvatarReport.Visibility = Visibility.Collapsed;

                // Clear the fields
                OverlayAvatarReportAvatarIdTextBox.Text = string.Empty;
                OverlayAvatarReportDescriptionTextBox.Text = string.Empty;

                // Clear validation errors
                ClearAvatarReportValidationErrors();

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to submit avatar report");
                System.Windows.MessageBox.Show($"Failed to submit report: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                OverlayAvatarReportSubmitButton.IsEnabled = true;
            }
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

        private void BanPlayerPast_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;

            // Find the DataContext for the row (should be PlayerViewModel)
            if (btn.DataContext is PlayerViewModel pvm)
            {
                string userId = pvm.UserId;

                // Set the user ID in Ban Management tab
                BanMgmtUserIdTextBox.Text = userId;

                // Activate the Config tab (index 4) in main TabControl
                MainTabControl.SelectedIndex = CONST_CONFIG_TAB_INDEX;

                // Activate the Ban Management tab (index 10) in Config TabControl
                ConfigTabControl.SelectedIndex = CONST_BAN_MGMT_TAB_INDEX;

                // Call the load user function
                BanMgmtLoadUser_Click(sender, e);
            }
        }
        #endregion

        //
        // Print Handlers
        #region Print handlers

        private void PrintApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyPrintFilter();
        }

        private void PrintClearFilter_Click(object sender, RoutedEventArgs e)
        {
            PrintFilterBox.Text = string.Empty;
            PrintAgeMinutesFilterBox.Text = string.Empty;
            ApplyPrintFilter();
        }

        private void PrintFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                PrintFilterBox.Text = SelectedPast.DisplayName;
                ApplyPrintFilter();
            }
        }

        private void PrintAgeMinutesFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyPrintFilter();
        }

        private void ApplyPrintFilter()
        {
            string textFilter = PrintFilterBox.Text?.Trim() ?? string.Empty;
            bool hasTextFilter = !string.IsNullOrWhiteSpace(textFilter);

            int? ageMinutesFilter = null;
            if (int.TryParse(PrintAgeMinutesFilterBox.Text?.Trim(), out int parsedMinutes) && parsedMinutes is >= 1 and <= 60)
            {
                ageMinutesFilter = parsedMinutes;
            }

            if (!hasTextFilter && !ageMinutesFilter.HasValue)
            {
                PrintView.Filter = null;
                PrintView.Refresh();
                return;
            }

            PrintView.Filter = obj =>
            {
                if (obj is not PlayerViewModel pvm)
                {
                    return false;
                }

                if (hasTextFilter)
                {
                    if (textFilter.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pvm.UserId?.IndexOf(textFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            return false;
                        }
                    }
                    else if (pvm.DisplayName?.IndexOf(textFilter, StringComparison.CurrentCultureIgnoreCase) < 0)
                    {
                        return false;
                    }
                }

                if (ageMinutesFilter.HasValue)
                {
                    DateTime cutoff = DateTime.Now.AddMinutes(-ageMinutesFilter.Value);
                    logger.Info(string.Format(cutoff.ToString("yyyy-MM-dd HH:mm:ss") + " cutoff for print filter"));
                    return pvm.Prints.Any(print => print.Timestamp >= cutoff);
                }

                return true;
            };

            PrintView.Refresh();
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
                System.Windows.MessageBox.Show($"Failed to submit report: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                PrintOverlaySubmitButton.IsEnabled = true;
            }
        }

        private async Task<bool> SubmitPrintReport(string userId, string printId, string category, string reportReason, string reportDescription)
        {
            bool success = true;
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

            ModerationReportResponse? report = await _serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (report != null)
            {
                logger.Info($"Print Report submitted - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                await _serviceRegistry.GetModerationManager().SaveModerationReport(rpt, report, userId, false);
            }
            else
            {
                logger.Warn($"Failed to submit Print Report - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                success = false;
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

        private void PrintSelectionTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Cancel the default paste operation
                e.CancelCommand();

                // Get the pasted text from clipboard
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string pastedText = (string)e.DataObject.GetData(typeof(string));

                    // Regex pattern to match VRChat print IDs (prnt_followed by UUID)
                    var match = Regex.Match(pastedText, @"prnt_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

                    if (match.Success)
                    {
                        // Extract and set the matched print ID
                        textBox.Text = match.Value;
                    }
                    else
                    {
                        // If no match, paste the original text
                        textBox.Text = pastedText;
                    }
                }
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
            ApplyEmojiFilter();
        }

        private void EmojiClearFilter_Click(object sender, RoutedEventArgs e)
        {
            EmojiFilterBox.Text = string.Empty;
            EmojiAgeMinutesFilterBox.Text = string.Empty;
            ApplyEmojiFilter();
        }

        private void EmojiFilterBySelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPast != null)
            {
                EmojiFilterBox.Text = SelectedPast.DisplayName;
                ApplyEmojiFilter();
            }
        }

        private void EmojiAgeMinutesFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyEmojiFilter();
        }

        private void ApplyEmojiFilter()
        {
            string textFilter = EmojiFilterBox.Text?.Trim() ?? string.Empty;
            bool hasTextFilter = !string.IsNullOrWhiteSpace(textFilter);

            int? ageMinutesFilter = null;
            if (int.TryParse(EmojiAgeMinutesFilterBox.Text?.Trim(), out int parsedMinutes) && parsedMinutes is >= 1 and <= 60)
            {
                ageMinutesFilter = parsedMinutes;
            }

            if (!hasTextFilter && !ageMinutesFilter.HasValue)
            {
                EmojiView.Filter = null;
                EmojiView.Refresh();
                return;
            }

            EmojiView.Filter = obj =>
            {
                if (obj is not PlayerViewModel pvm)
                {
                    return false;
                }

                if (hasTextFilter)
                {
                    if (textFilter.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pvm.UserId?.IndexOf(textFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            return false;
                        }
                    }
                    else if (pvm.DisplayName?.IndexOf(textFilter, StringComparison.CurrentCultureIgnoreCase) < 0)
                    {
                        return false;
                    }
                }

                if (ageMinutesFilter.HasValue)
                {
                    DateTime cutoff = DateTime.Now.AddMinutes(-ageMinutesFilter.Value);
                    logger.Info(string.Format(cutoff.ToString("yyyy-MM-dd HH:mm:ss") + " cutoff for emoji filter"));
                    return pvm.Emojis.Any(emoji => emoji.SpawnedAt >= cutoff);
                }

                return true;
            };

            EmojiView.Refresh();
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
                System.Windows.MessageBox.Show($"Failed to submit report: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

            ModerationReportResponse? report = await _serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            bool success = report != null;
            if (report != null)
            {
                logger.Info($"Inventory Report submitted - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                await _serviceRegistry.GetModerationManager().SaveModerationReport(rpt, report, userId, false);
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
                logger.Error(ex, "Failed to open group URL");
            }
            e.Handled = true;
        }

        private void InventorySelectionTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Cancel the default paste operation
                e.CancelCommand();

                // Get the pasted text from clipboard
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string pastedText = (string)e.DataObject.GetData(typeof(string));

                    // Regex pattern to match VRChat inventory IDs (inv_followed by UUID)
                    var match = Regex.Match(pastedText, @"inv_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

                    if (match.Success)
                    {
                        // Extract and set the matched inventory ID
                        textBox.Text = match.Value;
                    }
                    else
                    {
                        // If no match, paste the original text
                        textBox.Text = pastedText;
                    }
                }
            }
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
            ApplyAvatarDbFilter(AvatarDbFilterBox.Text);
        }

        private void AvatarDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            AvatarDbFilterBox.Text = string.Empty;
            ApplyAvatarDbFilter(string.Empty);
        }

        private void AvatarSelectionTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Cancel the default paste operation
                e.CancelCommand();

                // Get the pasted text from clipboard
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string pastedText = (string)e.DataObject.GetData(typeof(string));

                    // Regex pattern to match VRChat user IDs (usr_followed by UUID)
                    var match = Regex.Match(pastedText, @"avtr_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

                    if (match.Success)
                    {
                        // Extract and set the matched user ID
                        textBox.Text = match.Value;
                    }
                    else
                    {
                        // If no match, paste the original text
                        textBox.Text = pastedText;
                    }
                }
            }
        }

        private void ApplyAvatarDbFilter(string filterText)
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
            string? id = AvatarDbFilterBox.Text?.Trim();
            if (string.IsNullOrEmpty(id)) return;

            try
            {
                VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                Result<Avatar?> result = vrcClient.GetAvatarById(id);
                if (result.Value != null)
                {
                    TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                    AvatarInfo? existing = dbContext.AvatarInfos.Find(result.Value.Id);
                    if (existing == null)
                    {
                        var newEntity = new Tailgrab.Models.AvatarInfo
                        {
                            AvatarId = result.Value.Id,
                            UserId = result.Value.AuthorId ?? string.Empty,
                            UserName = result.Value.AuthorName ?? string.Empty,
                            AvatarName = result.Value.Name ?? string.Empty,
                            ImageUrl = result.Value.ImageUrl ?? string.Empty,
                            CreatedAt = result.Value.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            AlertType = AlertTypeEnum.None
                        };
                        dbContext.AvatarInfos.Add(newEntity);
                        dbContext.SaveChanges();
                    }
                    else
                    {
                        existing.UserId = result.Value.AuthorId ?? string.Empty;
                        existing.AvatarName = result.Value.Name ?? string.Empty;
                        existing.ImageUrl = result.Value.ImageUrl ?? string.Empty;
                        existing.CreatedAt = result.Value.CreatedAt;
                        existing.UpdatedAt = DateTime.UtcNow;
                        dbContext.AvatarInfos.Update(existing);
                        dbContext.SaveChanges();
                    }

                    // Filter the view to the fetched group
                    ApplyAvatarDbFilter(result.Value.Name ?? string.Empty);
                    AvatarDbFilterBox.Text = string.Empty;
                }
                else
                {
                    System.Windows.MessageBox.Show($"Avatar {id} not found via VRChat API.", "Fetch Avatar", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to fetch group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AvatarPurgeNoneRecords_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            var confirmResult = System.Windows.MessageBox.Show(
                "This will delete all Avatar Info Records that are marked 'NONE'. Do you want to continue?",
                "Purge NONE Records",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirmResult == MessageBoxResult.OK)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();

                    // Select all AvatarInfo records where AlertType is NONE
                    var noneRecords = db.AvatarInfos
                        .Where(a => a.AlertType == AlertTypeEnum.None)
                        .ToList();

                    int recordCount = noneRecords.Count;

                    // Delete the records
                    if (recordCount > 0)
                    {
                        db.AvatarInfos.RemoveRange(noneRecords);
                        db.SaveChanges();

                        // Refresh the grid
                        RefreshAvatarDb();

                        // Show confirmation message
                        System.Windows.MessageBox.Show(
                            $"{recordCount} record(s) were successfully removed.",
                            "Delete Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "No records with AlertType 'NONE' were found.",
                            "Delete Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to delete NONE records");
                    System.Windows.MessageBox.Show(
                        $"Failed to delete records: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
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
            ApplyGroupDbFilter(GroupDbFilterBox.Text);
        }

        private void GroupDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            GroupDbFilterBox.Text = string.Empty;
            ApplyGroupDbFilter(string.Empty);
        }

        private void GroupSelectionTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Cancel the default paste operation
                e.CancelCommand();

                // Get the pasted text from clipboard
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string pastedText = (string)e.DataObject.GetData(typeof(string));

                    // Regex pattern to match VRChat user IDs (usr_followed by UUID)
                    var match = Regex.Match(pastedText, @"grp_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

                    if (match.Success)
                    {
                        // Extract and set the matched user ID
                        textBox.Text = match.Value;
                    }
                    else
                    {
                        // If no match, paste the original text
                        textBox.Text = pastedText;
                    }
                }
            }
        }

        private void ApplyGroupDbFilter(string filterText)
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

        private async void GroupFetch_Click(object sender, RoutedEventArgs e)
        {
            string? id = GroupDbFilterBox.Text?.Trim();
            if (string.IsNullOrEmpty(id)) return;

            GroupInfo? existing = await _serviceRegistry.GetGroupManager().AddUpdateGroupFromVRC(id);

            if (existing == null)
            {
                System.Windows.MessageBox.Show($"Group {id} not found via VRChat API.", "Fetch Group", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Filter the view to the fetched Group
                ApplyGroupDbFilter(existing.GroupName ?? string.Empty);

                // Populate the group information box
                PopulateGroupInformation(existing.GroupId);
            }
        }

        private void GroupPurgeNoneRecords_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            var confirmResult = System.Windows.MessageBox.Show(
                "This will delete all Group Info Records that are marked 'NONE'. Do you want to continue?",
                "Purge NONE Records",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirmResult == MessageBoxResult.OK)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();

                    // Select all GroupInfo records where AlertType is NONE
                    var noneRecords = db.GroupInfos
                        .Where(g => g.AlertType == AlertTypeEnum.None)
                        .ToList();

                    int recordCount = noneRecords.Count;

                    // Delete the records
                    if (recordCount > 0)
                    {
                        db.GroupInfos.RemoveRange(noneRecords);
                        db.SaveChanges();

                        // Refresh the grid
                        RefreshGroupDb();

                        // Show confirmation message
                        System.Windows.MessageBox.Show(
                            $"{recordCount} record(s) were successfully removed.",
                            "Delete Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "No records with AlertType 'NONE' were found.",
                            "Delete Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to delete NONE records");
                    System.Windows.MessageBox.Show(
                        $"Failed to delete records: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void AvatarDbGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvatarDbGrid.SelectedItem is AvatarInfoViewModel selectedAvatar)
            {
                PopulateAvatarInformation(selectedAvatar.AvatarId);
            }
        }

        private async void PopulateAvatarInformation(string avatarId)
        {
            try
            {
                // Get the full avatar information from VRChat API
                VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                Result<Avatar?> result = await Task.Run(() => vrcClient.GetAvatarById(avatarId));

                if (result.Value != null)
                {
                    UseAvatarButton.IsEnabled = true;
                    SelectedAvatarId = result.Value.Id;

                    // Populate the UI fields
                    BanMgmtAvatarName.Text = result.Value.Name ?? "Unknown";
                    BanMgmtPublishDate.Text = result.Value.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                    BanMgmtUpdateDate.Text = result.Value.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                    BanMgmtAvatarState.Text = result.Value.ReleaseStatus.ToString();


                    VRChat.API.Model.User? user = await Task.Run(() => vrcClient.GetProfile(result.Value.AuthorId));

                    BanMgmtAvatarOwner.Text = user.DisplayName ?? "Unknown";
                    BanMgmtAvatarOwnerId.Text = result.Value.AuthorId ?? string.Empty;
                    BanMgmtAvatarDesc.Text = result.Value.Description ?? string.Empty;

                    // Enable the Ban Owner button if we have an owner ID
                    BanAvatarOwnerButton.IsEnabled = !string.IsNullOrWhiteSpace(result.Value.AuthorId);
                    ShowOwnerAvatarsButton.IsEnabled = !string.IsNullOrWhiteSpace(result.Value.AuthorId);

                    // Load avatar image
                    if (!string.IsNullOrEmpty(result.Value.ImageUrl))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(result.Value.ImageUrl);
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            BanMgmtAvatarImage.Source = bitmap;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Failed to load Avatar image for avatar {avatarId}");
                            BanMgmtAvatarImage.Source = null;
                        }
                    }
                    else
                    {
                        BanMgmtAvatarImage.Source = null;
                    }
                }
                else
                {
                    // Clear the fields if avatar not found
                    ClearAvatarInformation();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to populate avatar information for avatar {avatarId}");
                ClearAvatarInformation();
            }
        }

        private void ClearAvatarInformation()
        {
            BanMgmtAvatarName.Text = string.Empty;
            BanMgmtPublishDate.Text = string.Empty;
            BanMgmtUpdateDate.Text = string.Empty;
            BanMgmtAvatarOwner.Text = string.Empty;
            BanMgmtAvatarOwnerId.Text = string.Empty;
            BanMgmtAvatarDesc.Text = string.Empty;
            BanMgmtAvatarImage.Source = null;
            BanAvatarOwnerButton.IsEnabled = false;
            ShowOwnerAvatarsButton.IsEnabled = false;
            UseAvatarButton.IsEnabled = false;
            SelectedAvatarId = string.Empty;
        }

        private async void UseAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SelectedAvatarId))
            {
                // Call the load user function
                await _serviceRegistry.GetAvatarManager().SwitchAvatar(SelectedAvatarId);
            }
        }


        private void GroupDbGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupDbGrid.SelectedItem is GroupInfoViewModel selectedGroup)
            {
                PopulateGroupInformation(selectedGroup.GroupId);
            }
        }

        private async void PopulateGroupInformation(string groupId)
        {
            try
            {
                // Get the full group information from VRChat API
                VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                Result<VRChat.API.Model.Group?> groupResult = await Task.Run(() => vrcClient.GetGroupById(groupId));
                VRChat.API.Model.Group? group = groupResult.Value;

                if (group != null)
                {
                    // Populate the UI fields
                    BanMgmtGroupName.Text = group.Name ?? string.Empty;
                    BanMgmtGroupJoinState.Text = group.JoinState?.ToString() ?? string.Empty;
                    BanMgmtGroupmemberCount.Text = group.MemberCount.ToString();
                    BanMgmtGroupCreateDate.Text = group.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

                    // Combine ShortCode and Discriminator
                    string shortCode = group.ShortCode ?? string.Empty;
                    string discriminator = group.Discriminator ?? string.Empty;
                    BanMgmtGroupShortCode.Text = string.IsNullOrEmpty(shortCode) ? string.Empty : $"{shortCode}.{discriminator}";

                    VRChat.API.Model.User? user = await Task.Run(() => vrcClient.GetProfile(group.OwnerId));

                    BanMgmtGroupOwner.Text = user.DisplayName ?? string.Empty;
                    BanMgmtGroupOwnerId.Text = group.OwnerId ?? string.Empty;
                    BanMgmtGroupDesc.Text = group.Description ?? string.Empty;
                    BanMgmtGroupRules.Text = group.Rules ?? string.Empty;

                    // Enable the Ban Owner button if we have an owner ID
                    BanGroupOwnerButton.IsEnabled = !string.IsNullOrWhiteSpace(group.OwnerId);
                    ShowGroupOwnerAvatarsButton.IsEnabled = !string.IsNullOrWhiteSpace(group.OwnerId);

                    // Load group banner image
                    if (!string.IsNullOrEmpty(group.BannerUrl))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(group.BannerUrl);
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            BanMgmtGroupImage.Source = bitmap;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Failed to load group banner image for group {groupId}");
                            BanMgmtGroupImage.Source = null;
                        }
                    }
                    else
                    {
                        BanMgmtGroupImage.Source = null;
                    }

                    // Load group icon image
                    if (!string.IsNullOrEmpty(group.IconUrl))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(group.IconUrl);
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            BanMgmtGroupIcon.Source = bitmap;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Failed to load group icon image for group {groupId}");
                            BanMgmtGroupIcon.Source = null;
                        }
                    }
                    else
                    {
                        BanMgmtGroupIcon.Source = null;
                    }
                }
                else
                {
                    // Clear the fields if group not found
                    ClearGroupInformation();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to populate group information for group {groupId}");
                ClearGroupInformation();
            }
        }

        private void ClearGroupInformation()
        {
            BanMgmtGroupName.Text = string.Empty;
            BanMgmtGroupJoinState.Text = string.Empty;
            BanMgmtGroupmemberCount.Text = string.Empty;
            BanMgmtGroupCreateDate.Text = string.Empty;
            BanMgmtGroupShortCode.Text = string.Empty;
            BanMgmtGroupOwner.Text = string.Empty;
            BanMgmtGroupOwnerId.Text = string.Empty;
            BanMgmtGroupDesc.Text = string.Empty;
            BanMgmtGroupRules.Text = string.Empty;
            BanMgmtGroupImage.Source = null;
            BanMgmtGroupIcon.Source = null;
            BanGroupOwnerButton.IsEnabled = false;
            ShowGroupOwnerAvatarsButton.IsEnabled = false;
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

        private void GroupUserHyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
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
            ApplyUserDbFilter(UserDbFilterBox.Text);
        }

        private void UserDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            UserDbFilterBox.Text = string.Empty;
            ApplyUserDbFilter(string.Empty);
        }

        private void ApplyUserDbFilter(string filterText)
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

        private void PurgeInactiveUsers_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            MessageBoxResult confirmResult = System.Windows.MessageBox.Show(
                "This will purge all user records that have not been seen for two months and that have less than 15 minutes of elapsed time.",
                "Confirm Purge",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirmResult == MessageBoxResult.OK)
            {
                try
                {
                    var db = _serviceRegistry.GetDBContext();

                    // Calculate the cutoff date (2 months ago)
                    DateTime cutoffDate = DateTime.UtcNow.AddMonths(-2);

                    // Select all UserInfo records that meet the criteria
                    var inactiveUsers = db.UserInfos
                        .Where(u => u.UpdatedAt < cutoffDate && u.ElapsedMinutes < 15)
                        .ToList();

                    int userCount = inactiveUsers.Count;

                    if (userCount > 0)
                    {
                        // Collect LastProfileChecksum ids for ProfileEvaluation deletion
                        var checksumIds = inactiveUsers
                            .Where(u => !string.IsNullOrEmpty(u.LastProfileChecksum))
                            .Select(u => u.LastProfileChecksum)
                            .Distinct()
                            .ToList();

                        int profileEvalCount = 0;

                        // Delete matching ProfileEvaluation records
                        if (checksumIds.Any())
                        {
                            var profileEvaluations = db.ProfileEvaluations
                                .Where(p => checksumIds.Contains(p.Md5checksum))
                                .ToList();

                            profileEvalCount = profileEvaluations.Count;
                            if (profileEvalCount > 0)
                            {
                                db.ProfileEvaluations.RemoveRange(profileEvaluations);
                            }
                        }

                        // Delete the UserInfo records
                        db.UserInfos.RemoveRange(inactiveUsers);
                        db.SaveChanges();

                        // Refresh the grid
                        RefreshUserDb();

                        // Show success message
                        System.Windows.MessageBox.Show(
                            $"Successfully removed {userCount} user record(s) and {profileEvalCount} profile evaluation record(s).",
                            "Purge Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "No inactive user records were found matching the criteria.",
                            "Purge Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to purge inactive users");
                    System.Windows.MessageBox.Show(
                        $"Failed to purge records: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        //
        // Moderation DB UI handlers
        #region Moderation DB handlers
        private void ModerationDbRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshModerationDb();
        }

        private void ModerationDbApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyModerationDbFilter(ModerationUserIdFilterBox.Text, ModerationContentIdFilterBox.Text);
        }

        private void ModerationDbClearFilter_Click(object sender, RoutedEventArgs e)
        {
            ModerationUserIdFilterBox.Text = string.Empty;
            ModerationContentIdFilterBox.Text = string.Empty;
            ApplyModerationDbFilter(string.Empty, string.Empty);
        }

        private void ApplyModerationDbFilter(string userIdFilter, string contentIdFilter)
        {
            if (string.IsNullOrWhiteSpace(userIdFilter) && string.IsNullOrWhiteSpace(contentIdFilter))
            {
                ModerationDbItems.SetFilter(null, null);
            }
            else
            {
                ModerationDbItems.SetFilter(
                    string.IsNullOrWhiteSpace(userIdFilter) ? null : userIdFilter.Trim(),
                    string.IsNullOrWhiteSpace(contentIdFilter) ? null : contentIdFilter.Trim()
                );
            }
        }

        public void RefreshModerationDb()
        {
            try
            {
                ModerationDbItems.Refresh();
            }
            catch { }
        }

        private void ModerationDbGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModerationDbGrid.SelectedItem is ModerationInfoViewModel selectedReport)
            {
                PopulateModerationInformation(selectedReport);
            }
        }

        private void PopulateModerationInformation(ModerationInfoViewModel report)
        {
            try
            {
                ModerationReportId.Text = report.Id;
                ModerationReportContentType.Text = report.ContentType;
                ModerationReportUserId.Text = report.UserId;
                ModerationReportContentId.Text = report.ContentId;
                ModerationReportContentName.Text = report.ContentName;
                ModerationReportEventDateTime.Text = report.EventDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                ModerationReportCloseDate.Text = report.CloseDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not Closed";
                ModerationReportText.Text = report.Report;
                ModerationReportStatus.Text = report.IsDeleted ? "Deleted" : report.IsClosed ? "Closed" : "Open";

                if (!string.IsNullOrWhiteSpace(report.Thumbnail))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(report.Thumbnail);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ModerationReportThumbnail.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Failed to load moderation thumbnail for report {report.Id}");
                        ModerationReportThumbnail.Source = null;
                    }
                }
                else
                {
                    ModerationReportThumbnail.Source = null;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to populate moderation report information");
                ClearModerationInformation();
            }
        }

        private void ClearModerationInformation()
        {
            ModerationReportId.Text = string.Empty;
            ModerationReportContentType.Text = string.Empty;
            ModerationReportUserId.Text = string.Empty;
            ModerationReportContentId.Text = string.Empty;
            ModerationReportContentName.Text = string.Empty;
            ModerationReportEventDateTime.Text = string.Empty;
            ModerationReportCloseDate.Text = string.Empty;
            ModerationReportText.Text = string.Empty;
            ModerationReportStatus.Text = string.Empty;
            ModerationReportThumbnail.Source = null;
        }

        #endregion

        private void UserSelectionTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Cancel the default paste operation
                e.CancelCommand();

                // Get the pasted text from clipboard
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string pastedText = (string)e.DataObject.GetData(typeof(string));

                    // Regex pattern to match VRChat user IDs (usr_followed by UUID)
                    var match = Regex.Match(pastedText, @"usr_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

                    if (match.Success)
                    {
                        // Extract and set the matched user ID
                        textBox.Text = match.Value;
                    }
                    else
                    {
                        // If no match, paste the original text
                        textBox.Text = pastedText;
                    }
                }
            }
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
                    { "Trust", WindowLayoutManager.DefaultActiveTrustWidth },
                    { "AvatarName", WindowLayoutManager.DefaultActiveAvatarNameWidth },
                    { "InstanceStartTime", WindowLayoutManager.DefaultActiveInstanceStartWidth },
                    { "UserId", WindowLayoutManager.DefaultActiveAlertMessagesWidth },
                });

                // Load column widths for Past Players tab
                LoadGridViewColumnWidths(PastListView, new Dictionary<string, double>
                {
                    { "DisplayName", WindowLayoutManager.DefaultPastDisplayNameWidth },
                    { "Age", WindowLayoutManager.DefaultPastAgeWidth },
                    { "Trust", WindowLayoutManager.DefaultPastTrustWidth },
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
                    if( filterText.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                    {
                        return pvm.UserId?.IndexOf(ft, StringComparison.OrdinalIgnoreCase) >= 0;
                    }

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

                logger.Info($"Fetched user profile for ban management: {user.DisplayName})");

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
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Failed to load Avatar image for User {userId}");
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

        private void BanMgmtReportUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userId = BanMgmtUserIdTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    System.Windows.MessageBox.Show("Please enter a User ID first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!userId.StartsWith("usr_"))
                {
                    System.Windows.MessageBox.Show("Invalid User ID format (must start with usr_).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                    ShowProfileReportOverlay(userId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open Report Profile overlay");
                System.Windows.MessageBox.Show($"Failed to open Report Profile overlay: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }


        private void BanMgmtCheckGroups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userId = BanMgmtUserIdTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    System.Windows.MessageBox.Show("Please enter a User ID first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!userId.StartsWith("usr_"))
                {
                    System.Windows.MessageBox.Show("Invalid User ID format (must start with usr_).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get display name if user is already loaded, otherwise use userId
                string displayName = _currentBanMgmtUser?.DisplayName ?? userId;

                // Call ShowUserGroupsOverlay with the userId and display name
                ShowUserGroupsOverlay(userId, displayName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking groups for user");
                System.Windows.MessageBox.Show($"Failed to check groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadBanManagementGroupsAsync()
        {
            try
            {
                // Load all groups from the database on background thread
                var groups = await Task.Run(() => _serviceRegistry.GetDBContext().GroupManagements.ToList());

                // All UI operations must happen on the UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    _banMgmtGroupList.Clear();

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
                });
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
                Result<VRChat.API.Model.Group?> groupResult = _serviceRegistry.GetVRChatAPIClient().GetGroupById(groupId);
                VRChat.API.Model.Group? group = groupResult.Value;
                if (!groupResult.HasException && string.IsNullOrEmpty(groupResult.Value?.Id))
                {
                    System.Windows.MessageBox.Show("Group not found in VRChat. Please verify the Group ID.",
                        "Group Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Add to database
                var newGroup = new tailgrab.src.Models.GroupManagement
                {
                    GroupId = groupId,
                    GroupName = group?.Name ?? "Unknown",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dBContext.Add(newGroup);
                dBContext.SaveChanges();

                // Add to the UI list
                var item = new GroupBanItem
                {
                    GroupId = groupId,
                    GroupName = group?.Name ?? "Unknown",
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

                logger.Info($"Added group {groupId} ({group?.Name}) to ban management");
                System.Windows.MessageBox.Show($"Group '{group?.Name}' added successfully",
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

        private async void BanMgmtBanAllGroups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentBanMgmtUserId))
                {
                    return;
                }

                if (sender is System.Windows.Controls.Button button)
                {
                    button.IsEnabled = false;
                }

                foreach (var item in _banMgmtGroupList)
                {
                    if (string.IsNullOrWhiteSpace(item.GroupId))
                    {
                        continue;
                    }

                    item.Status = "Banning...";

                    bool success = await _serviceRegistry.GetVRChatAPIClient().BanUserFromGroup(item.GroupId, _currentBanMgmtUserId);

                    if (success)
                    {
                        item.Status = "Banned";
                        item.CanBan = false;
                        item.CanUnban = true;
                        logger.Info($"Banned user {_currentBanMgmtUserId} from group {item.GroupId}");
                    }
                    else
                    {
                        item.Status = "Ban Failed";
                    }

                    // Small delay to avoid overwhelming the API
                    await Task.Delay(100);
                }

                if (sender is System.Windows.Controls.Button btn)
                {
                    btn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error banning user from all groups");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BanMgmtUnbanAllGroups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentBanMgmtUserId))
                {
                    return;
                }

                if (sender is System.Windows.Controls.Button button)
                {
                    button.IsEnabled = false;
                }

                foreach (var item in _banMgmtGroupList)
                {
                    if (string.IsNullOrWhiteSpace(item.GroupId))
                    {
                        continue;
                    }

                    item.Status = "Unbanning...";

                    bool success = await _serviceRegistry.GetVRChatAPIClient().UnbanUserFromGroup(item.GroupId, _currentBanMgmtUserId);

                    if (success)
                    {
                        item.Status = "Not Member";
                        item.CanBan = true;
                        item.CanUnban = false;
                        logger.Info($"Unbanned user {_currentBanMgmtUserId} from group {item.GroupId}");
                    }
                    else
                    {
                        item.Status = "Unban Failed";
                    }

                    // Small delay to avoid overwhelming the API
                    await Task.Delay(100);
                }

                if (sender is System.Windows.Controls.Button btn)
                {
                    btn.IsEnabled = true;
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error unbanning user from all groups");
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

            synthesizer.Speak("Tail Grab shut down");
            synthesizer.Dispose();
        }

        private void TestProfilePromptInput_Changed(object sender, SelectionChangedEventArgs e)
        {

        }

        #region User Groups Overlay Management
        public async void ShowUserGroupsOverlay(string userId, string displayName)
        {
            try
            {
                // Set user information
                UserGroupsOverlayUserId.Text = userId;
                UserGroupsOverlayDisplayName.Text = displayName;

                // Clear existing data
                UserGroupsDataGrid.ItemsSource = null;

                // Show the overlay
                UserGroupsOverlay.Visibility = Visibility.Visible;

                // Fetch groups asynchronously (already async, no need for Task.Run)
                List<UserGroupViewModel> groups = await _serviceRegistry.GetGroupManager().LoadUserGroupsAsync(userId);

                // Update UI (already on UI thread)
                UserGroupsDataGrid.ItemsSource = groups;

                // Reset ScrollViewer to top
                UserGroupsScrollViewer.ScrollToTop();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error showing user groups overlay for user {userId}");
                System.Windows.MessageBox.Show($"Failed to load user groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UserGroupsOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void UserGroupsOverlayClose_Click(object sender, RoutedEventArgs e)
        {
            UserGroupsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void UserGroupsAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is UserGroupViewModel vm)
                {
                    if (vm.AlertType == AlertTypeEnum.None)
                    {
                        System.Windows.MessageBox.Show("Please select an Alert Type before adding.", "Alert Type Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string activityMessage = string.Empty;
                    UpdateGroupInfoResult result = _serviceRegistry.GetGroupManager().InsertUpdateGroupInfo(vm);
                    if (result.Success)
                    {
                        // Update view model to reflect database state
                        vm.ExistsInDatabase = true;
                        vm.DatabaseAlertType = vm.AlertType;
                        vm.UpdateAlertColors();
                        activityMessage += $"{result.Message}\n";
                    }

                    // Refresh the groups database view if it's visible
                    RefreshGroupDb();
                    if (!string.IsNullOrEmpty(activityMessage))
                    {
                        ShowOverlayMessage("Success", activityMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding/updating group in database");
                ShowOverlayMessage("Error", $"Failed to save group: {ex.Message}");
            }
        }

        public void UserGroupOverlaySaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is string source)
                {
                    if (source == "UserGroupsDataGrid")
                    {
                        string activityMessage = string.Empty;
                        List<UserGroupViewModel> groups = UserGroupsDataGrid.ItemsSource as List<UserGroupViewModel> ?? new List<UserGroupViewModel>();
                        foreach (var vm in groups)
                        {
                            if (vm.CanAdd)
                            {
                                UpdateGroupInfoResult result = _serviceRegistry.GetGroupManager().InsertUpdateGroupInfo(vm);
                                if (result.Success)
                                {
                                    // Update view model to reflect database state
                                    vm.ExistsInDatabase = true;
                                    vm.DatabaseAlertType = vm.AlertType;
                                    vm.UpdateAlertColors();
                                    activityMessage += $"{result.Message}\n";
                                }
                            }
                        }
                        // Refresh the groups database view if it's visible
                        RefreshGroupDb();
                        if (!string.IsNullOrEmpty(activityMessage))
                        {
                            ShowOverlayMessage("Success", activityMessage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding/updating group in database");
                ShowOverlayMessage("Error", $"Failed to save group: {ex.Message}");
            }
        }

        private void UserGroupsDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Check if we're over a ComboBox that's open - if so, let it handle the scroll
            if (e.OriginalSource is FrameworkElement element)
            {
                // Walk up the visual tree to see if we're inside a ComboBox
                DependencyObject parent = element;
                while (parent != null)
                {
                    if (parent is System.Windows.Controls.ComboBox comboBox && comboBox.IsDropDownOpen)
                    {
                        // Let the ComboBox handle its own scrolling
                        return;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            // Forward the mouse wheel event to the ScrollViewer
            if (UserGroupsScrollViewer != null)
            {
                e.Handled = true;
                var scrollEvent = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                UserGroupsScrollViewer.RaiseEvent(scrollEvent);
            }
        }
        #endregion

        #region User Avatars Overlay
        private void AvatarCheckAvatars_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userId = BanMgmtAvatarOwnerId.Text.Trim();

                if (string.IsNullOrWhiteSpace(userId))
                {
                    System.Windows.MessageBox.Show("Please enter a User ID first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!userId.StartsWith("usr_"))
                {
                    System.Windows.MessageBox.Show("Invalid User ID format (must start with usr_).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get display name if user is already loaded, otherwise use userId
                string displayName = BanMgmtAvatarOwner.Text ?? userId;

                // Call ShowUserAvatarsOverlay with the userId and display name
                ShowUserAvatarsOverlay(userId, displayName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking groups for user");
                System.Windows.MessageBox.Show($"Failed to check groups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async void ShowUserAvatarsOverlay(string userId, string displayName)
        {
            try
            {
                // Set user information
                UserAvatarOverlayUserId.Text = userId;
                UserAvatarOverlayDisplayName.Text = displayName;

                // Clear existing data
                UserAvatarDataGrid.ItemsSource = null;

                // Show the overlay
                UserAvatarOverlay.Visibility = Visibility.Visible;

                // Fetch avatars asynchronously (already async, no need for Task.Run)
                List<UserAvatarViewModel> avatars = await _serviceRegistry.GetAvatarManager().LoadUserAvatarAsync(userId);

                // Update UI (already on UI thread)
                UserAvatarDataGrid.ItemsSource = avatars;

                // Reset ScrollViewer to top
                UserAvatarScrollViewer.ScrollToTop();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error showing user avatars overlay for user {userId}");
                System.Windows.MessageBox.Show($"Failed to load user avatars: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UserAvatarOverlay.Visibility = Visibility.Collapsed;
            }
        }


        private async void UserAvatarAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is UserAvatarViewModel vm)
                {
                    if (vm.AlertType == AlertTypeEnum.None)
                    {
                        System.Windows.MessageBox.Show("Please select an Alert Type before adding.", "Alert Type Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }


                    string activityMessage = string.Empty;
                    UpdateAvatarInfoResult result = _serviceRegistry.GetAvatarManager().InsertUpdateAvatarInfo(vm);
                    if (result.Success)
                    {
                        // Update view model to reflect database state
                        vm.ExistsInDatabase = true;
                        vm.DatabaseAlertType = vm.AlertType;
                        vm.UpdateAlertColors();
                        activityMessage += $"{result.Message}\n";
                    }

                    // Refresh the avatars database view if it's visible
                    RefreshAvatarDb();
                    if (!string.IsNullOrEmpty(activityMessage))
                    {
                        ShowOverlayMessage("Success", activityMessage);
                        //System.Windows.MessageBox.Show(activityMessage, "Activity", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding/updating avatar in database");
                ShowOverlayMessage("Error", $"Failed to save avatar: {ex.Message}");
                //System.Windows.MessageBox.Show($"Failed to save avatar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void OverlayUseAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is UserAvatarViewModel vm)
            {
                if (!string.IsNullOrWhiteSpace(vm.AvatarId))
                {
                    // Call the load user function
                    await _serviceRegistry.GetAvatarManager().SwitchAvatar(vm.AvatarId);
                }
            }
        }

        private async void OverlayReportAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is UserAvatarViewModel vm)
            {
                if (!string.IsNullOrWhiteSpace(vm.AvatarId))
                {
                    // Call the load user function
                }
            }
        }

        private void UserAvatarOverlaySaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is string source)
                {
                    if (source == "UserAvatarDataGrid")
                    {
                        string activityMessage = string.Empty;
                        List<UserAvatarViewModel> avatars = UserAvatarDataGrid.ItemsSource as List<UserAvatarViewModel> ?? new List<UserAvatarViewModel>();
                        foreach (var vm in avatars)
                        {
                            if (vm.CanAdd)
                            {
                                UpdateAvatarInfoResult result = _serviceRegistry.GetAvatarManager().InsertUpdateAvatarInfo(vm);
                                if (result.Success)
                                {
                                    // Update view model to reflect database state
                                    vm.ExistsInDatabase = true;
                                    vm.DatabaseAlertType = vm.AlertType;
                                    vm.UpdateAlertColors();
                                    activityMessage += $"{result.Message}\n";
                                }
                            }
                        }
                        // Refresh the avatars database view if it's visible
                        RefreshAvatarDb();
                        if (!string.IsNullOrEmpty(activityMessage))
                        {
                            ShowOverlayMessage("Success", activityMessage);
                            //System.Windows.MessageBox.Show(activityMessage, "Activity", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding/updating avatar in database");
                ShowOverlayMessage("Error", $"Failed to save avatar: {ex.Message}");
                //System.Windows.MessageBox.Show($"Failed to save avatar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void UserAvatarOverlayClose_Click(object sender, RoutedEventArgs e)
        {
            UserAvatarOverlay.Visibility = Visibility.Collapsed;
        }

        private void UserAvatarDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Check if we're over a ComboBox that's open - if so, let it handle the scroll
            if (e.OriginalSource is FrameworkElement element)
            {
                // Walk up the visual tree to see if we're inside a ComboBox
                DependencyObject parent = element;
                while (parent != null)
                {
                    if (parent is System.Windows.Controls.ComboBox comboBox && comboBox.IsDropDownOpen)
                    {
                        // Let the ComboBox handle its own scrolling
                        return;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            // Forward the mouse wheel event to the ScrollViewer
            if (UserAvatarScrollViewer != null)
            {
                e.Handled = true;
                var scrollEvent = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                UserAvatarScrollViewer.RaiseEvent(scrollEvent);
            }
        }
        #endregion

        #region LineMatchers Event Handlers
        private LineHandlerEditorViewModel? _lineHandlerEditorViewModel;

        private void InitializeLineMatchersTab()
        {
            _lineHandlerEditorViewModel = new LineHandlerEditorViewModel(_serviceRegistry.GetConfigurationManager());

            if (HandlersDataGrid != null)
            {
                HandlersDataGrid.ItemsSource = _lineHandlerEditorViewModel.Handlers;
                HandlersDataGrid.SelectionChanged += HandlersDataGrid_SelectionChanged;
                HandlersDataGrid.CellEditEnding += HandlersDataGrid_CellEditEnding;
            }

            if (ActionsDataGrid != null)
            {
                ActionsDataGrid.ItemsSource = _lineHandlerEditorViewModel.Actions;
                ActionsDataGrid.SelectionChanged += ActionsDataGrid_SelectionChanged;
                ActionsDataGrid.DragEnter += ActionsDataGrid_DragEnter;
                ActionsDataGrid.DragOver += ActionsDataGrid_DragOver;
                ActionsDataGrid.Drop += ActionsDataGrid_Drop;
            }

            // Wire up event handlers
            if (SaveConfigButton != null) SaveConfigButton.Click += SaveConfigButton_Click;
            if (ReloadConfigButton != null) ReloadConfigButton.Click += ReloadConfigButton_Click;
            if (AddActionButton != null) AddActionButton.Click += AddActionButton_Click;
            if (RemoveActionButton != null) RemoveActionButton.Click += RemoveActionButton_Click;
            if (MoveActionUpButton != null) MoveActionUpButton.Click += MoveActionUpButton_Click;
            if (MoveActionDownButton != null) MoveActionDownButton.Click += MoveActionDownButton_Click;
            if (PatternTextBox != null) PatternTextBox.LostFocus += PatternTextBox_LostFocus;

            // Populate dropdowns
            InitializeComboBoxes();

            // Bind handler details
            BindHandlerDetails();
        }

        private void InitializeComboBoxes()
        {
            // Log Output Color options
            if (LogOutputColorCombo != null)
            {
                LogOutputColorCombo.ItemsSource = Enum.GetValues(typeof(AnsiColor)).Cast<AnsiColor>().ToList();
            }

            // Pattern Type options
            if (PatternTypeCombo != null)
            {
                PatternTypeCombo.ItemsSource = Enum.GetValues(typeof(PatternType)).Cast<PatternType>().ToList();
            }

            // Action Type options
            if (ActionTypeCombo != null)
            {
                ActionTypeCombo.ItemsSource = Enum.GetValues(typeof(ActionType)).Cast<ActionType>().ToList();
            }

            // Handler Type options (for Add Handler)
            // This will be used in a dialog/popup
        }

        private void BindHandlerDetails()
        {
            if (_lineHandlerEditorViewModel?.SelectedHandler != null)
            {
                var handler = _lineHandlerEditorViewModel.SelectedHandler;
                if (HandlerTypeTextBlock != null) HandlerTypeTextBlock.Text = handler.HandlerTypeValue.ToString();
                if (EnabledCheckBox != null) EnabledCheckBox.IsChecked = handler.Enabled;
                if (LogOutputCheckBox != null) LogOutputCheckBox.IsChecked = handler.LogOutput;
                if (LogOutputColorCombo != null) LogOutputColorCombo.SelectedItem =
                    Enum.TryParse<AnsiColor>(handler.LogOutputColor, out var color) ? color : AnsiColor.White;
                if (PatternTypeCombo != null) PatternTypeCombo.SelectedItem = handler.PatternTypeValue;
                if (PatternTextBox != null) PatternTextBox.Text = handler.Pattern;
                if (PatternErrorText != null) PatternErrorText.Text = "";

                // Wire up change handlers
                if (EnabledCheckBox != null) EnabledCheckBox.Checked -= EnabledCheckBox_Changed;
                if (EnabledCheckBox != null) EnabledCheckBox.Unchecked -= EnabledCheckBox_Changed;
                if (EnabledCheckBox != null)
                {
                    EnabledCheckBox.Checked += EnabledCheckBox_Changed;
                    EnabledCheckBox.Unchecked += EnabledCheckBox_Changed;
                }

                if (LogOutputCheckBox != null) LogOutputCheckBox.Checked -= LogOutputCheckBox_Changed;
                if (LogOutputCheckBox != null) LogOutputCheckBox.Unchecked -= LogOutputCheckBox_Changed;
                if (LogOutputCheckBox != null)
                {
                    LogOutputCheckBox.Checked += LogOutputCheckBox_Changed;
                    LogOutputCheckBox.Unchecked += LogOutputCheckBox_Changed;
                }

                if (LogOutputColorCombo != null) LogOutputColorCombo.SelectionChanged -= LogOutputColorCombo_SelectionChanged;
                if (LogOutputColorCombo != null) LogOutputColorCombo.SelectionChanged += LogOutputColorCombo_SelectionChanged;

                if (PatternTypeCombo != null) PatternTypeCombo.SelectionChanged -= PatternTypeCombo_SelectionChanged;
                if (PatternTypeCombo != null) PatternTypeCombo.SelectionChanged += PatternTypeCombo_SelectionChanged;
            }
            else
            {
                ClearHandlerDetails();
            }
        }

        private void ClearHandlerDetails()
        {
            if (HandlerTypeTextBlock != null) HandlerTypeTextBlock.Text = "";
            if (EnabledCheckBox != null) EnabledCheckBox.IsChecked = false;
            if (LogOutputCheckBox != null) LogOutputCheckBox.IsChecked = false;
            if (LogOutputColorCombo != null) LogOutputColorCombo.SelectedIndex = -1;
            if (PatternTypeCombo != null) PatternTypeCombo.SelectedIndex = -1;
            if (PatternTextBox != null) PatternTextBox.Text = "";
            if (PatternErrorText != null) PatternErrorText.Text = "";
        }

        private void HandlersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HandlersDataGrid?.SelectedItem is LineHandlerConfig handler)
            {
                _lineHandlerEditorViewModel!.SelectedHandler = handler;
                BindHandlerDetails();
            }
        }

        private void HandlersDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is System.Windows.Controls.CheckBox checkBox && e.Row.Item is LineHandlerConfig handler)
            {
                handler.Enabled = checkBox.IsChecked ?? false;
                // Update the detail panel checkbox to reflect the change
                if (EnabledCheckBox != null)
                {
                    EnabledCheckBox.IsChecked = handler.Enabled;
                }
                SaveConfigAsync();
            }
        }

        private void ActionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionsDataGrid?.SelectedItem is ActionBase action)
            {
                _lineHandlerEditorViewModel!.SelectedAction = action;
                BindActionDetails(action);
            }
        }

        private void BindActionDetails(ActionBase action)
        {
            if (ActionDetailsGrid == null) return;

            ActionDetailsGrid.Children.Clear();

            if (action is OSCActionConfig oscAction)
            {
                BuildOSCActionUI(oscAction);
            }
            else if (action is DelayActionConfig delayAction)
            {
                BuildDelayActionUI(delayAction);
            }
            else if (action is PlaySoundActionConfig soundAction)
            {
                BuildPlaySoundActionUI(soundAction);
            }
            else if (action is KeyStrokeConfig keyAction)
            {
                BuildKeyStrokeActionUI(keyAction);
            }
            else if (action is TTSActionConfig ttsAction)
            {
                BuildTTSActionUI(ttsAction);
            }
        }

        private void BuildOSCActionUI(OSCActionConfig action)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Parameter Name
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var paramLabel = new TextBlock { Text = "Parameter Name:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(paramLabel, 0);
            Grid.SetRow(paramLabel, 0);
            var paramBox = new System.Windows.Controls.TextBox { Text = action.ParameterName ?? "", Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(paramBox, 1);
            Grid.SetRow(paramBox, 0);
            paramBox.TextChanged += (s, e) => action.ParameterName = paramBox.Text;
            grid.Children.Add(paramLabel);
            grid.Children.Add(paramBox);

            // OSC Type
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var typeLabel = new TextBlock { Text = "Type:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(typeLabel, 0);
            Grid.SetRow(typeLabel, 1);
            var typeCombo = new System.Windows.Controls.ComboBox { Height = 24, Margin = new Thickness(6, 4, 0, 4) };
            typeCombo.ItemsSource = Enum.GetValues(typeof(OscType)).Cast<OscType>().ToList();
            typeCombo.SelectedItem = action.OscValueType;
            Grid.SetColumn(typeCombo, 1);
            Grid.SetRow(typeCombo, 1);
            typeCombo.SelectionChanged += (s, e) => { if (e.AddedItems.Count > 0 && e.AddedItems[0] is OscType t) action.OscValueType = t; };
            grid.Children.Add(typeLabel);
            grid.Children.Add(typeCombo);

            // Value
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var valueLabel = new TextBlock { Text = "Value:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(valueLabel, 0);
            Grid.SetRow(valueLabel, 2);
            var valueBox = new System.Windows.Controls.TextBox { Text = action.Value ?? "", Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(valueBox, 1);
            Grid.SetRow(valueBox, 2);
            valueBox.TextChanged += (s, e) => action.Value = valueBox.Text;
            grid.Children.Add(valueLabel);
            grid.Children.Add(valueBox);

            ActionDetailsGrid!.Children.Add(grid);
        }

        private void BuildDelayActionUI(DelayActionConfig action)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = "Milliseconds:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);
            var box = new System.Windows.Controls.TextBox { Text = action.Milliseconds.ToString(), Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(box, 1);
            box.TextChanged += (s, e) =>
            {
                if (int.TryParse(box.Text, out var ms)) action.Milliseconds = ms;
            };
            grid.Children.Add(label);
            grid.Children.Add(box);

            ActionDetailsGrid!.Children.Add(grid);
        }

        private void BuildPlaySoundActionUI(PlaySoundActionConfig action)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = "Sound File:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);
            var box = new System.Windows.Controls.TextBox { Text = action.SoundFile ?? "", Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(box, 1);
            box.TextChanged += (s, e) => action.SoundFile = box.Text;
            grid.Children.Add(label);
            grid.Children.Add(box);

            ActionDetailsGrid!.Children.Add(grid);
        }

        private void BuildKeyStrokeActionUI(KeyStrokeConfig action)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Window Title
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var wLabel = new TextBlock { Text = "Window Title:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(wLabel, 0);
            Grid.SetRow(wLabel, 0);
            var wBox = new System.Windows.Controls.TextBox { Text = action.WindowTitle ?? "", Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(wBox, 1);
            Grid.SetRow(wBox, 0);
            wBox.TextChanged += (s, e) => action.WindowTitle = wBox.Text;
            grid.Children.Add(wLabel);
            grid.Children.Add(wBox);

            // Keys
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var kLabel = new TextBlock { Text = "Keys:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(kLabel, 0);
            Grid.SetRow(kLabel, 1);
            var kBox = new System.Windows.Controls.TextBox { Text = action.Keys ?? "", Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(kBox, 1);
            Grid.SetRow(kBox, 1);
            kBox.TextChanged += (s, e) => action.Keys = kBox.Text;
            grid.Children.Add(kLabel);
            grid.Children.Add(kBox);

            ActionDetailsGrid!.Children.Add(grid);
        }

        private void BuildTTSActionUI(TTSActionConfig action)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Text
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tLabel = new TextBlock { Text = "Text:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tLabel, 0);
            Grid.SetRow(tLabel, 0);
            var tBox = new System.Windows.Controls.TextBox { Text = action.Text ?? "", Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(tBox, 1);
            Grid.SetRow(tBox, 0);
            tBox.TextChanged += (s, e) => action.Text = tBox.Text;
            grid.Children.Add(tLabel);
            grid.Children.Add(tBox);

            // Volume
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var vLabel = new TextBlock { Text = "Volume:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(vLabel, 0);
            Grid.SetRow(vLabel, 1);
            var vBox = new System.Windows.Controls.TextBox { Text = action.Volume.ToString(), Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(vBox, 1);
            Grid.SetRow(vBox, 1);
            vBox.TextChanged += (s, e) => { if (int.TryParse(vBox.Text, out var v)) action.Volume = v; };
            grid.Children.Add(vLabel);
            grid.Children.Add(vBox);

            // Rate
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rLabel = new TextBlock { Text = "Rate:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rLabel, 0);
            Grid.SetRow(rLabel, 2);
            var rBox = new System.Windows.Controls.TextBox { Text = action.Rate.ToString(), Margin = new Thickness(6, 4, 0, 4) };
            Grid.SetColumn(rBox, 1);
            Grid.SetRow(rBox, 2);
            rBox.TextChanged += (s, e) => { if (int.TryParse(rBox.Text, out var r)) action.Rate = r; };
            grid.Children.Add(rLabel);
            grid.Children.Add(rBox);

            ActionDetailsGrid!.Children.Add(grid);
        }

        private async void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel != null)
            {
                if (SavingProgress != null) SavingProgress.Visibility = Visibility.Visible;
                if (StatusText != null) StatusText.Text = "Saving...";

                var success = await _lineHandlerEditorViewModel.SaveHandlers();

                if (SavingProgress != null) SavingProgress.Visibility = Visibility.Collapsed;
                if (StatusText != null) StatusText.Text = success ? "Configuration saved successfully" : "Failed to save configuration";
            }
        }

        private void ReloadConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel != null)
            {
                _lineHandlerEditorViewModel.LoadHandlers();
                _lineHandlerEditorViewModel.SelectedHandler = _lineHandlerEditorViewModel.Handlers.FirstOrDefault();
                if (StatusText != null) StatusText.Text = "Configuration reloaded";
            }
        }

        private void AddHandlerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel != null)
            {
                var dialog = new SelectHandlerTypeDialog(_lineHandlerEditorViewModel.Handlers.Select(h => h.HandlerTypeValue).ToList());
                if (dialog.ShowDialog() == true && dialog.SelectedHandlerType.HasValue)
                {
                    _lineHandlerEditorViewModel.AddHandler(dialog.SelectedHandlerType.Value);
                }
            }
        }

        private void RemoveHandlerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedHandler != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Remove handler '{_lineHandlerEditorViewModel.SelectedHandler.HandlerTypeValue}'?",
                    "Confirm Removal", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _lineHandlerEditorViewModel.RemoveHandler(_lineHandlerEditorViewModel.SelectedHandler);
                }
            }
        }

        private void AddActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActionTypeCombo?.SelectedItem is ActionType actionType && _lineHandlerEditorViewModel != null)
            {
                _lineHandlerEditorViewModel.AddAction(actionType);
            }
        }

        private void RemoveActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedAction != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Remove this action?",
                    "Confirm Removal", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _lineHandlerEditorViewModel.RemoveAction(_lineHandlerEditorViewModel.SelectedAction);
                }
            }
        }

        private void MoveActionUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedAction != null)
            {
                var action = _lineHandlerEditorViewModel.SelectedAction;
                _lineHandlerEditorViewModel.MoveActionUp(action);
                _lineHandlerEditorViewModel.SelectedAction = action;
                if (ActionsDataGrid != null)
                {
                    ActionsDataGrid.SelectedItem = action;
                    ActionsDataGrid.ScrollIntoView(action);
                }
                SaveConfigAsync();
            }
        }

        private void MoveActionDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedAction != null)
            {
                var action = _lineHandlerEditorViewModel.SelectedAction;
                _lineHandlerEditorViewModel.MoveActionDown(action);
                _lineHandlerEditorViewModel.SelectedAction = action;
                if (ActionsDataGrid != null)
                {
                    ActionsDataGrid.SelectedItem = action;
                    ActionsDataGrid.ScrollIntoView(action);
                }
                SaveConfigAsync();
            }
        }

        private ActionBase? _draggedAction;

        private void ActionsDataGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ActionsDataGrid?.SelectedItem is ActionBase action)
            {
                _draggedAction = action;
                try
                {
                    System.Windows.DragDrop.DoDragDrop(ActionsDataGrid, action, System.Windows.DragDropEffects.Move);
                }
                catch
                {
                    // Drag-drop operation failed or was cancelled
                }
                _draggedAction = null;
            }
        }

        private void ActionsDataGrid_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ActionBase)))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void ActionsDataGrid_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ActionBase)))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ActionsDataGrid_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetData(typeof(ActionBase)) is ActionBase draggedAction)
            {
                if (ActionsDataGrid?.SelectedItem is ActionBase targetAction && draggedAction != targetAction)
                {
                    // Reorder actions
                    if (_lineHandlerEditorViewModel?.SelectedHandler != null)
                    {
                        int draggedIndex = _lineHandlerEditorViewModel.SelectedHandler.Actions.IndexOf(draggedAction);
                        int targetIndex = _lineHandlerEditorViewModel.SelectedHandler.Actions.IndexOf(targetAction);

                        if (draggedIndex >= 0 && targetIndex >= 0 && draggedIndex != targetIndex)
                        {
                            _lineHandlerEditorViewModel.SelectedHandler.Actions.RemoveAt(draggedIndex);
                            int insertIndex = draggedIndex < targetIndex ? targetIndex - 1 : targetIndex;
                            _lineHandlerEditorViewModel.SelectedHandler.Actions.Insert(insertIndex, draggedAction);

                            // Update observable collection
                            int draggedObservableIndex = _lineHandlerEditorViewModel.Actions.IndexOf(draggedAction);
                            int targetObservableIndex = _lineHandlerEditorViewModel.Actions.IndexOf(targetAction);

                            if (draggedObservableIndex >= 0 && targetObservableIndex >= 0)
                            {
                                _lineHandlerEditorViewModel.Actions.RemoveAt(draggedObservableIndex);
                                int insertObservableIndex = draggedObservableIndex < targetObservableIndex ? targetObservableIndex - 1 : targetObservableIndex;
                                _lineHandlerEditorViewModel.Actions.Insert(insertObservableIndex, draggedAction);
                            }

                            // Maintain selection on the dragged item
                            _lineHandlerEditorViewModel.SelectedAction = draggedAction;
                            if (ActionsDataGrid != null)
                            {
                                ActionsDataGrid.SelectedItem = draggedAction;
                                ActionsDataGrid.ScrollIntoView(draggedAction);
                            }
                            SaveConfigAsync();
                        }
                    }
                }
            }
            e.Handled = true;
        }

        private async void SaveConfigAsync()
        {
            if (_lineHandlerEditorViewModel != null && StatusText != null)
            {
                StatusText.Text = "Saving configuration...";
                bool success = await _lineHandlerEditorViewModel.SaveHandlers();
                StatusText.Text = success ? "Configuration saved" : "Failed to save configuration";
            }
        }

        private void PatternTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel != null && PatternTextBox != null)
            {
                _lineHandlerEditorViewModel.ValidatePattern(PatternTextBox.Text);
                if (_lineHandlerEditorViewModel.SelectedHandler != null)
                {
                    _lineHandlerEditorViewModel.SelectedHandler.Pattern = PatternTextBox.Text;
                }

                if (PatternErrorText != null)
                {
                    PatternErrorText.Text = _lineHandlerEditorViewModel.PatternValidationError ?? "";
                }
            }
        }

        private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedHandler != null && EnabledCheckBox != null)
            {
                _lineHandlerEditorViewModel.SelectedHandler.Enabled = EnabledCheckBox.IsChecked ?? false;
                // Refresh the DataGrid to show the updated value
                if (HandlersDataGrid != null)
                {
                    HandlersDataGrid.Items.Refresh();
                }
                SaveConfigAsync();
            }
        }

        private void LogOutputCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedHandler != null && LogOutputCheckBox != null)
            {
                _lineHandlerEditorViewModel.SelectedHandler.LogOutput = LogOutputCheckBox.IsChecked ?? false;
            }
        }

        private void LogOutputColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedHandler != null && LogOutputColorCombo?.SelectedItem is AnsiColor color)
            {
                _lineHandlerEditorViewModel.SelectedHandler.LogOutputColor = color.ToString();
            }
        }

        private void PatternTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_lineHandlerEditorViewModel?.SelectedHandler != null && PatternTypeCombo?.SelectedItem is PatternType patternType)
            {
                _lineHandlerEditorViewModel.SelectedHandler.PatternTypeValue = patternType;
            }
        }
        #endregion

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
        public List<AlertDisplayItem> AlertMessages { get; set; } = [];
        public ObservableCollection<PrintInfoViewModel> Prints { get; private set; } = [];
        public ObservableCollection<EmojiInfoViewModel> Emojis { get; private set; } = [];
        private bool IsFriend {  get; set; }
        public string ProfileUrl { get; set; }
        public TrustClassEnum UserTrustClass { get; set; }

        public AgeVerificationEnum AgeVerified { get; set; }

        public System.Windows.Media.Geometry UserTrustIconGeometry
        {
            get
            {
                return TrustClassEnumMapper.MapEnumToIcon(UserTrustClass);
            }
        }

        public System.Windows.Media.Brush UserTrustIconBrush
        {
            get
            {
                return TrustClassEnumMapper.MapEnumToBrush(UserTrustClass);
            }
        }

        public System.Windows.Media.Geometry AgeVerifiedIconGeometry
        {
            get
            {
                return AgeVerificationEnumMapper.MapEnumToIcon(AgeVerified);
            }
        }

        public System.Windows.Media.Brush AgeVerifiedIconBrush
        {
            get
            {
                return AgeVerificationEnumMapper.MapEnumToBrush(AgeVerified);
            }
        }

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
            AlertMessages = p.AlertMessages;
            _AlertColor = p.AlertColor;
            IsFriend = p.IsFriend;
            ProfileUrl = p.ProfileImage;
            UserTrustClass = p.UserTrustClass;
            AgeVerified = p.AgeVerified;


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
            if (AlertMessages != p.AlertMessages) { AlertMessages = p.AlertMessages; changed = true; }
            if (_AlertColor != p.AlertColor) { _AlertColor = p.AlertColor; changed = true; }
            if (IsFriend != p.IsFriend) { IsFriend = p.IsFriend; changed = true; }
            if (ProfileUrl != p.ProfileImage) { ProfileUrl = p.ProfileImage; changed = true; }
            if (UserTrustClass != p.UserTrustClass) { UserTrustClass = p.UserTrustClass; changed = true; }
            if (AgeVerified != p.AgeVerified) { AgeVerified = p.AgeVerified; changed = true; }

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
            sb.AppendLine($"AlertMessages (Count): {AlertMessages.Count}");
            sb.AppendLine($"IsFriend: {IsFriend}");
            sb.AppendLine($"ProfileUrl: {ProfileUrl}");
            sb.AppendLine($"UserTrustClass: {UserTrustClass}");
            sb.AppendLine($"AgeVerified: {AgeVerified}");
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
        public AlertDisplayItem AlertInfo { get; set; } = p.AlertInfo;
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
        public string EvaluatedText { get; set; } = i.EvaluatedText;
        public AlertDisplayItem AlertInfo { get; set; } = i.AlertInfo;
    }

    public class TailTaskViewModel(FileTailStatus? status) : INotifyPropertyChanged
    {
        private readonly FileTailStatus? _status = status;

        public string FilePath => _status?.FilePath ?? string.Empty;
        public string FileName => _status != null ? Path.GetFileName(_status.FilePath) : string.Empty;
        public DateTime StartTime => _status?.StartTime ?? DateTime.MinValue;
        public int LinesProcessed => _status?.LinesProcessed ?? 0;
        public DateTime? LastLineProcessedTime => _status?.LastLineProcessedTime;

        public string LastLineProcessedTimeFormatted => 
            LastLineProcessedTime.HasValue ? LastLineProcessedTime.Value.ToString("u") : "N/A";

        public void UpdateFromStatus()
        {
            OnPropertyChanged(nameof(LinesProcessed));
            OnPropertyChanged(nameof(LastLineProcessedTime));
            OnPropertyChanged(nameof(LastLineProcessedTimeFormatted));
        }

        public void RequestCancellation()
        {
            _status?.RequestCancellation();
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


    #region Support DTO Classes
    public class AvatarInfoDTO(string avatarId, AlertTypeEnum alertType, bool exists)
    {
        public string AvatarId { get; set; } = avatarId;
        public AlertTypeEnum AlertType { get; set; } = alertType;
        public bool Exists { get; set; } = exists;
    }
    #endregion
}
