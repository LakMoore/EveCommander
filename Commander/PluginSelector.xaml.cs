using BotLib;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Commander
{
  /// <summary>
  /// Interaction logic for PluginSelector.xaml
  /// </summary>
  public partial class PluginSelector : Window
  {
    private ObservableCollection<PluginSelectionModel> _items = new();
    // expose for XAML binding
    public ObservableCollection<PluginSelectionModel> Items => _items;

    public PluginSelector()
    {
      InitializeComponent();
      DataContext = this;
      // ensure the ListBox is explicitly bound to our collection (failsafe for different loading orders)
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

    // Centralized click handling on the ListBoxItem container to avoid event interference.
    // Ignore clicks that originate from the CheckBox itself so the checkbox's own behavior remains primary.
    private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      // If the click came from inside a CheckBox, don't handle here (let the CheckBox toggle normally)
      DependencyObject? src = e.OriginalSource as DependencyObject;
      while (src != null)
      {
        if (src is System.Windows.Controls.Primitives.ButtonBase || src is System.Windows.Controls.CheckBox)
        {
          return; // let the checkbox or button handle the click
        }
        src = VisualTreeHelper.GetParent(src);
      }

      if (sender is System.Windows.Controls.ListBoxItem item && item.DataContext is PluginSelectionModel model)
      {
        // toggle selection model when clicking anywhere on the item (except checkbox)
        model.IsSelected = !model.IsSelected;
        e.Handled = true; // prevent other handlers from also toggling
      }
    }

    // Failsafe central handler: handle clicks on the ListBox that may not go through the ListBoxItem event
    private void PluginList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      // Only handle if clicked on a ListBoxItem area
      var src = e.OriginalSource as DependencyObject;
      while (src != null && !(src is System.Windows.Controls.ListBoxItem))
      {
        src = VisualTreeHelper.GetParent(src);
      }
      if (src is System.Windows.Controls.ListBoxItem item && item.DataContext is PluginSelectionModel model)
      {
        // ignore clicks originating from the CheckBox itself
        var origin = e.OriginalSource as DependencyObject;
        while (origin != null)
        {
          if (origin is System.Windows.Controls.CheckBox) return;
          origin = VisualTreeHelper.GetParent(origin);
        }

        model.IsSelected = !model.IsSelected;
        e.Handled = true;
      }
    }
  }
}
