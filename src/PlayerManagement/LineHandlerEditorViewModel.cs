using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NLog;
using Tailgrab.Common;
using Tailgrab.Configuration;

namespace Tailgrab.PlayerManagement
{
    public class LineHandlerEditorViewModel : INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ConfigurationManager _configurationManager;

        private LineHandlerConfig? _selectedHandler;
        private ActionBase? _selectedAction;
        private string? _patternValidationError;
        private bool _isPatternValid = true;
        private bool _isSaving = false;

        public ObservableCollection<LineHandlerConfig> Handlers { get; }
        public ObservableCollection<ActionBase> Actions { get; }

        public LineHandlerEditorViewModel(ConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));

            Handlers = new ObservableCollection<LineHandlerConfig>();
            Actions = new ObservableCollection<ActionBase>();

            LoadHandlers();
        }

        public LineHandlerConfig? SelectedHandler
        {
            get => _selectedHandler;
            set
            {
                if (_selectedHandler != value)
                {
                    _selectedHandler = value;
                    OnPropertyChanged();
                    UpdateActionsForSelectedHandler();
                }
            }
        }

        public ActionBase? SelectedAction
        {
            get => _selectedAction;
            set
            {
                if (_selectedAction != value)
                {
                    _selectedAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? PatternValidationError
        {
            get => _patternValidationError;
            set
            {
                if (_patternValidationError != value)
                {
                    _patternValidationError = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsPatternValid
        {
            get => _isPatternValid;
            set
            {
                if (_isPatternValid != value)
                {
                    _isPatternValid = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (_isSaving != value)
                {
                    _isSaving = value;
                    OnPropertyChanged();
                }
            }
        }

        public void LoadHandlers()
        {
            Handlers.Clear();
            var configs = _configurationManager.LoadConfig();
            foreach (var config in configs)
            {
                Handlers.Add(config);
            }
            logger.Debug($"Loaded {Handlers.Count} handlers");
        }

        public void ValidatePattern(string? pattern)
        {
            var (isValid, errorMessage) = _configurationManager.ValidateRegexPattern(pattern);
            IsPatternValid = isValid;
            PatternValidationError = errorMessage;
        }

        public async Task<bool> SaveHandlers()
        {
            IsSaving = true;
            try
            {
                var handlersList = Handlers.ToList();
                var (success, errorMessage) = _configurationManager.SaveConfig(handlersList);

                if (!success)
                {
                    logger.Error($"Save failed: {errorMessage}");
                    PatternValidationError = errorMessage;
                }
                else
                {
                    PatternValidationError = null;
                    logger.Info("Configuration saved successfully");
                }

                return success;
            }
            finally
            {
                IsSaving = false;
            }
        }

        public void AddHandler(LineHandlerType handlerType)
        {
            // Check if handler type already exists
            if (Handlers.Any(h => h.HandlerTypeValue == handlerType))
            {
                logger.Warn($"Handler type {handlerType} already exists");
                return;
            }

            var newHandler = new LineHandlerConfig
            {
                HandlerTypeValue = handlerType,
                Enabled = true,
                PatternTypeValue = PatternType.Default,
                Pattern = null,
                LogOutput = true,
                LogOutputColor = "Default",
                Actions = new List<ActionBase>()
            };

            Handlers.Add(newHandler);
            SelectedHandler = newHandler;
            logger.Debug($"Added new handler: {handlerType}");
        }

        public void RemoveHandler(LineHandlerConfig? handler)
        {
            if (handler == null) return;

            Handlers.Remove(handler);
            SelectedHandler = Handlers.FirstOrDefault();
            logger.Debug($"Removed handler: {handler.HandlerTypeValue}");
        }

        public void AddAction(ActionType actionType)
        {
            if (_selectedHandler == null)
            {
                logger.Warn("No handler selected; cannot add action");
                return;
            }

            ActionBase newAction = actionType switch
            {
                ActionType.OSCAction => new OSCActionConfig(),
                ActionType.DelayAction => new DelayActionConfig(),
                ActionType.PlaySoundAction => new PlaySoundActionConfig(),
                ActionType.KeyPressAction => new KeyStrokeConfig(),
                ActionType.TTSAction => new TTSActionConfig(),
                _ => throw new InvalidOperationException($"Unknown action type: {actionType}")
            };

            _selectedHandler.Actions.Add(newAction);
            Actions.Add(newAction);
            SelectedAction = newAction;
            logger.Debug($"Added new action: {actionType} to handler {_selectedHandler.HandlerTypeValue}");
        }

        public void RemoveAction(ActionBase? action)
        {
            if (action == null || _selectedHandler == null) return;

            _selectedHandler.Actions.Remove(action);
            Actions.Remove(action);
            SelectedAction = Actions.FirstOrDefault();
            logger.Debug($"Removed action from handler {_selectedHandler.HandlerTypeValue}");
        }

        public void MoveActionUp(ActionBase? action)
        {
            if (action == null || _selectedHandler == null) return;

            int currentIndex = _selectedHandler.Actions.IndexOf(action);
            if (currentIndex <= 0) return;

            _selectedHandler.Actions.RemoveAt(currentIndex);
            _selectedHandler.Actions.Insert(currentIndex - 1, action);

            // Update the observable collection to reflect the change
            int observableIndex = Actions.IndexOf(action);
            if (observableIndex > 0)
            {
                Actions.RemoveAt(observableIndex);
                Actions.Insert(observableIndex - 1, action);
            }

            logger.Debug($"Moved action up in handler {_selectedHandler.HandlerTypeValue}");
        }

        public void MoveActionDown(ActionBase? action)
        {
            if (action == null || _selectedHandler == null) return;

            int currentIndex = _selectedHandler.Actions.IndexOf(action);
            if (currentIndex < 0 || currentIndex >= _selectedHandler.Actions.Count - 1) return;

            _selectedHandler.Actions.RemoveAt(currentIndex);
            _selectedHandler.Actions.Insert(currentIndex + 1, action);

            // Update the observable collection to reflect the change
            int observableIndex = Actions.IndexOf(action);
            if (observableIndex >= 0 && observableIndex < Actions.Count - 1)
            {
                Actions.RemoveAt(observableIndex);
                Actions.Insert(observableIndex + 1, action);
            }

            logger.Debug($"Moved action down in handler {_selectedHandler.HandlerTypeValue}");
        }

        private void UpdateActionsForSelectedHandler()
        {
            Actions.Clear();
            if (_selectedHandler != null && _selectedHandler.Actions != null)
            {
                foreach (var action in _selectedHandler.Actions)
                {
                    Actions.Add(action);
                }
            }
            SelectedAction = Actions.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
