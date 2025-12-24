using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BotLib;

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
      _originalCache = PluginSettingsCache.GetCache().ToDictionary(kvp => kvp.PluginName, kvp => kvp.Settings.ToDictionary(k => k.Key, v => v.Value));

      LoadUi();
    }

    private void LoadUi()
    {
      var cache = PluginSettingsCache.GetCache();
      PluginsStack.Children.Clear();

      foreach (var plugin in cache)
      {
        var grp = new GroupBox() { Header = plugin.PluginName, Margin = new Thickness(5) };
        var panel = new StackPanel();
        var tbDict = new Dictionary<string, TextBox>();

        foreach (var setting in plugin.Settings)
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
        _textBoxes[plugin.PluginName] = tbDict;
      }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
      // write back values into the cache
      var cache = PluginSettingsCache.GetCache();
      foreach (var plugin in _textBoxes)
      {
        var pluginEntry = cache.FirstOrDefault(p => p.PluginName == plugin.Key) ?? new PluginSettings() { PluginName = plugin.Key, Settings = [] };

        foreach (var kv in plugin.Value)
        {
          var setting = pluginEntry.Settings.FirstOrDefault(
            s => s.Key == kv.Key,
            new PluginSetting() { Key = kv.Key, Value = string.Empty }
          );
          setting.Value = kv.Value.Text;
          pluginEntry.Settings.Add(setting);
        }
      }

      // persist
      Properties.Settings.Default.pluginSettingsCache = PluginSettingsCache.SaveCache();
      Properties.Settings.Default.Save();

      this.DialogResult = true;
      this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      // revert the cache
      var cache = PluginSettingsCache.GetCache();
      cache.Clear();
      foreach (var kv in _originalCache)
      {
        cache.Add(new PluginSettings()
        {
          PluginName = kv.Key,
          Settings = [.. kv.Value.Select(pair => new PluginSetting { Key = pair.Key, Value = pair.Value })]
        });
      }

      this.DialogResult = false;
      this.Close();
    }
  }
}
