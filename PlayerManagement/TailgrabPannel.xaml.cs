using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

using System.Windows.Media;

namespace Tailgrab.PlayerManagement
{
    public partial class TailgrabPannel : Window, IDisposable
    {
        private readonly DispatcherTimer fallbackTimer;

        public ObservableCollection<PlayerViewModel> ActivePlayers { get; } = new ObservableCollection<PlayerViewModel>();
        public ObservableCollection<PlayerViewModel> PastPlayers { get; } = new ObservableCollection<PlayerViewModel>();

        public ICollectionView ActiveView { get; }
        public ICollectionView PastView { get; }

        public PlayerViewModel? SelectedActive { get; set; }
        public PlayerViewModel? SelectedPast { get; set; }

        public TailgrabPannel()
        {
            InitializeComponent();
            DataContext = this;

            ActiveView = CollectionViewSource.GetDefaultView(ActivePlayers);
            PastView = CollectionViewSource.GetDefaultView(PastPlayers);

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
                    break;
            }
        }

        private void AddOrUpdatePlayer(Player p)
        {
            var vm = ActivePlayers.FirstOrDefault(x => x.UserId == p.UserId);
            var vmPast = PastPlayers.FirstOrDefault(x => x.UserId == p.UserId);

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

                // Ensure not in past
                if (vmPast != null)
                {
                    PastPlayers.Remove(vmPast);
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
                var player = PlayerManager.GetPlayerByUserId(pvm.UserId);
                if (player == null)
                {
                    // Build formatted string from the viewmodel alone
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"DisplayName: {pvm.DisplayName}");
                    sb.AppendLine($"UserId: {pvm.UserId}");
                    sb.AppendLine($"AvatarName: {pvm.AvatarName}");
                    sb.AppendLine($"InstanceStart: {pvm.InstanceStartTime}");
                    sb.AppendLine($"InstanceEnd: {pvm.InstanceEndTime}");
                    var text = sb.ToString();
                    System.Windows.Clipboard.SetText(text);
                    return;
                }

                var sb2 = new System.Text.StringBuilder();
                sb2.AppendLine($"DisplayName: {player.DisplayName}");
                sb2.AppendLine($"UserId: {player.UserId}");
                sb2.AppendLine($"AvatarName: { (string.IsNullOrEmpty(player.AvatarName) ? string.Empty : player.AvatarName) }");
                sb2.AppendLine($"InstanceStart: {player.InstanceStartTime:u}");
                sb2.AppendLine($"InstanceEnd: { (player.InstanceEndTime.HasValue ? player.InstanceEndTime.Value.ToString("u") : string.Empty) }");

                if (player.Events != null && player.Events.Count > 0)
                {
                    sb2.AppendLine("Events:");
                    foreach (var ev in player.Events)
                    {
                        sb2.AppendLine($"  - {ev.EventTime:u} {ev.Type} {ev.EventDescription}");
                    }
                }

                System.Windows.Clipboard.SetText(sb2.ToString());
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
        public string InstanceStartTime { get; private set; }
        public string InstanceEndTime { get; private set; }

        public PlayerViewModel(Player p)
        {
            UserId = p.UserId;
            DisplayName = p.DisplayName;
            AvatarName = p.AvatarName;
            InstanceStartTime = p.InstanceStartTime.ToString("u");
            InstanceEndTime = p.InstanceEndTime.HasValue ? p.InstanceEndTime.Value.ToString("u") : string.Empty;
        }

        public void UpdateFrom(Player p)
        {
            bool changed = false;

            if (UserId != p.UserId) { UserId = p.UserId; changed = true; }
            if (DisplayName != p.DisplayName) { DisplayName = p.DisplayName; changed = true; }
            if (AvatarName != p.AvatarName) { AvatarName = p.AvatarName; changed = true; }

            var start = p.InstanceStartTime.ToString("u");
            if (InstanceStartTime != start) { InstanceStartTime = start; changed = true; }

            var end = p.InstanceEndTime.HasValue ? p.InstanceEndTime.Value.ToString("u") : string.Empty;
            if (InstanceEndTime != end) { InstanceEndTime = end; changed = true; }

            if (changed) OnPropertyChanged(string.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
