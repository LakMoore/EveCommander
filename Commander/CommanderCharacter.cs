using BotLib;
using System.ComponentModel;
using System.Windows.Media;
using System.Xml.Serialization;

namespace Commander
{
  public record CommanderCharacter : BotCharacter, INotifyPropertyChanged
  {
    [XmlIgnore]
    public IEnumerable<IBotLibPlugin> Plugins { get; set; } = Enumerable.Empty<IBotLibPlugin>();

    private bool _isOnline;
    /// <summary>
    /// Indicates whether the character is currently online.
    /// Bound to the row checkbox in CharacterWindow.
    /// </summary>
    public bool IsOnline
    {
      get => _isOnline;
      set
      {
        if (_isOnline == value) return;
        _isOnline = value;
        OnPropertyChanged(nameof(IsOnline));
      }
    }

    // New property to back UI selection; keeps selection state in data so virtualized rows update correctly
    private bool _isSelected;
    [XmlIgnore]
    public bool IsSelected
    {
      get => _isSelected;
      set
      {
        if (_isSelected == value) return;
        _isSelected = value;
        OnPropertyChanged(nameof(IsSelected)); // raise notification so bindings (ListViewItem.IsSelected / CheckBox) update
        OnPropertyChanged(nameof(BackgroundColor));
      }
    }

    [XmlIgnore]
    public Brush BackgroundColor
    {
      get
      {
        if (IsSelected)
        {
          // Use dark-mode selection color if available via resources; fall back to a muted blue
          try
          {
            var brush = (System.Windows.Application.Current.Resources["SelectionBrush"] as SolidColorBrush);
            if (brush != null)
              return brush;
          }
          catch { }
          return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FF264F66"));
        }
        return Brushes.Transparent;
      }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    internal void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string EnabledPluginDescription => Plugins
        .Where(p => p.IsEnabled)
        .Select(p => p.Name)
        .Count() switch
    {
      0 => "No plugins enabled",
      1 => Plugins.First(p => p.IsEnabled).Name,
      _ => $"{Plugins.Count(p => p.IsEnabled)} plugins enabled"
    };
  }
}
