using System.ComponentModel;

namespace Tailgrab.Models;

public class TestImageAIEvalItem : INotifyPropertyChanged
{
    private string? _imagePath;
    private string? _aiEvaluation;

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            if (_imagePath != value)
            {
                _imagePath = value;
                OnPropertyChanged(nameof(ImagePath));
            }
        }
    }

    public string? AIEvaluation
    {
        get => _aiEvaluation;
        set
        {
            if (_aiEvaluation != value)
            {
                _aiEvaluation = value;
                OnPropertyChanged(nameof(AIEvaluation));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
