using System.Windows;
using Tailgrab.Configuration;

namespace Tailgrab.PlayerManagement
{
    public partial class SelectHandlerTypeDialog : Window
    {
        public LineHandlerType? SelectedHandlerType { get; set; }

        private readonly List<LineHandlerType> _existingTypes;

        public SelectHandlerTypeDialog(List<LineHandlerType> existingTypes)
        {
            _existingTypes = existingTypes;
            InitializeComponent();
            LoadAvailableHandlerTypes();
        }

        private void LoadAvailableHandlerTypes()
        {
            var allTypes = Enum.GetValues(typeof(LineHandlerType)).Cast<LineHandlerType>().ToList();
            var availableTypes = allTypes.Where(t => !_existingTypes.Contains(t)).ToList();

            HandlerTypeCombo.ItemsSource = availableTypes;
            if (availableTypes.Count > 0)
            {
                HandlerTypeCombo.SelectedItem = availableTypes[0];
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (HandlerTypeCombo.SelectedItem is LineHandlerType selectedType)
            {
                SelectedHandlerType = selectedType;
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
