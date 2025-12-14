using BotLib;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Commander
{
  /// <summary>
  /// Interaction logic for PluginSelector.xaml
  /// </summary>
  public partial class PluginSelector : Window
  {
    private ObservableCollection<PluginSelectionModel> _items = new();

    public PluginSelector()
    {
      InitializeComponent();
      PluginList.ItemsSource = _items;
    }

    // Existing per-character usage: build selector from IBotLibPlugin instances
    public void SetPlugins(IEnumerable<IBotLibPlugin> plugins)
    {
      _items.Clear();
      foreach (var p in plugins)
      {
        _items.Add(new PluginSelectionModel(p.Name, p.IsEnabled));
      }
    }

    // New API for multi-character flow: provide plugin names and an optional initial selection
    public void SetPluginsForMultiple(IEnumerable<string> pluginNames, IEnumerable<string>? initiallySelected = null)
    {
      var selectedSet = (initiallySelected ?? Enumerable.Empty<string>()).ToHashSet();
      _items.Clear();
      foreach (var name in pluginNames.Distinct().OrderBy(n => n))
      {
        _items.Add(new PluginSelectionModel(name, selectedSet.Contains(name)));
      }
    }

    // Returns the names selected by the user
    public IEnumerable<string> GetSelectedPluginNames()
    {
      return _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = true;
      Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = false;
      Close();
    }
  }
}
