using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;

namespace Commander
{
  public partial class SettingsWindow : Window
  {
    private Dictionary<string, Dictionary<string, string>> _originalCache = [];
    private Dictionary<string, Dictionary<string, TextBox>> _textBoxes = new();

    public SettingsWindow()
    {
      InitializeComponent();

      // copy current cache so cancel can revert
      _originalCache = PluginSettingsCache.GetCache().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToDictionary(k => k.Key, v => v.Value));

      LoadUi();
    }

    private void LoadUi()
    {
      var cache = PluginSettingsCache.GetCache();
      PluginsStack.Children.Clear();

      foreach (var plugin in cache)
      {
        var grp = new GroupBox() { Header = plugin.Key, Margin = new Thickness(5) };
        var panel = new StackPanel();
        var tbDict = new Dictionary<string, TextBox>();

        foreach (var setting in plugin.Value)
        {
          var sp = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
          sp.Children.Add(new TextBlock() { Text = setting.Key, Width = 200, VerticalAlignment = VerticalAlignment.Center });
          var tb = new TextBox() { Text = setting.Value, Width = 300 };
          sp.Children.Add(tb);
          panel.Children.Add(sp);
          tbDict[setting.Key] = tb;
        }

        grp.Content = panel;
        PluginsStack.Children.Add(grp);
        _textBoxes[plugin.Key] = tbDict;
      }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
      // write back values into the cache
      var cache = PluginSettingsCache.GetCache();
      foreach (var plugin in _textBoxes)
      {
        if (!cache.ContainsKey(plugin.Key))
          cache[plugin.Key] = new Dictionary<string, string>();

        foreach (var kv in plugin.Value)
        {
          cache[plugin.Key][kv.Key] = kv.Value.Text;
        }
      }

      // persist
      Properties.Settings.Default.pluginSettingsCache = PluginSettingsCache.SaveCache();
      Properties.Settings.Default.Save();

      this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      // revert the cache
      var cache = PluginSettingsCache.GetCache();
      cache.Clear();
      foreach (var kv in _originalCache)
      {
        cache[kv.Key] = kv.Value.ToDictionary(k => k.Key, v => v.Value);
      }

      this.Close();
    }
  }
}
