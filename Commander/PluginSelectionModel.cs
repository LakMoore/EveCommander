using System.ComponentModel;

namespace Commander
{
    // Simple view-model for plugin selection in the shared dialog
    public class PluginSelectionModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public PluginSelectionModel(string name, bool isSelected = false)
        {
            Name = name;
            _isSelected = isSelected;
        }

        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}