using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using BotLib;

namespace Commander
{
  public partial class SettingsWindow : Window
  {
    private Dictionary<string, Dictionary<string, string>> _originalCache = [];
    private Dictionary<string, Dictionary<string, TextBox>> _textBoxes = [];

    public SettingsWindow()
    {
      InitializeComponent();

      // copy current cache so cancel can revert
      _originalCache = PluginSettingsCache.GetCache().ToDictionary(kvp => kvp.PluginName, kvp => kvp.Settings.ToDictionary(k => k.Key, v => v.Value));

      // find all the IBotLibPlugins and build the framework for the UI from the PluginInfo

      // use reflection to find all the classes that inherit from IBotLibPlugin
      var allPluginsWithSettings = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(s => s.GetTypes())
        .Where(p => typeof(IBotLibPlugin).IsAssignableFrom(p) && !p.IsAbstract)
        .Select(t => Activator.CreateInstance(t, "DummyCharacter", 0L) as IBotLibPlugin)
        .Where(p => p != null && p.GetSettingsInfo().Any())
        .Cast<IBotLibPlugin>()
        .ToList();

      LoadUi(allPluginsWithSettings);
    }

    private void LoadUi(List<IBotLibPlugin> allPluginsWithSettings)
    {
      var cache = PluginSettingsCache.GetCache();
      PluginsStack.Children.Clear();

      foreach (var plugin in allPluginsWithSettings)
      {
        var grp = new GroupBox() { Header = plugin.Name, Margin = new Thickness(5) };
        var panel = new StackPanel();
        var tbDict = new Dictionary<string, TextBox>();

        foreach (var settingInfo in plugin.GetSettingsInfo())
        {
          var sp = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
          sp.Children.Add(new TextBlock() { Text = settingInfo.Key, Width = 200, VerticalAlignment = VerticalAlignment.Center });

          var value = cache
            .FirstOrDefault(p => p.PluginName == plugin.Name)?
            .Settings
            .FirstOrDefault(s => s.Key == settingInfo.Key)?
            .Value ?? string.Empty;
          var tb = new TextBox() { Text = value, Width = 300 };

          // configure textbox based on setting type
          if (settingInfo.SettingType == BotLibSetting.Type.MultiLineText)
          {
            tb.Height = 100;
            tb.AcceptsReturn = true;
            tb.TextWrapping = TextWrapping.Wrap;
            tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
          }
          if (settingInfo.SettingType == BotLibSetting.Type.Integer)
          {
            tb.PreviewTextInput += (s, e) =>
            {
              e.Handled = !e.Text.All(c => char.IsDigit(c));
            };
          }
          if (settingInfo.SettingType == BotLibSetting.Type.Decimal)
          {
            tb.PreviewTextInput += (s, e) =>
            {
              e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.' || c == ',');
            };
          }
          // if the setting has a description, add a tooltip
          if (!string.IsNullOrWhiteSpace(settingInfo.Description))
          {
            tb.ToolTip = settingInfo.Description;
          }

          sp.Children.Add(tb);
          panel.Children.Add(sp);
          tbDict[settingInfo.Key] = tb;
        }

        grp.Content = panel;
        PluginsStack.Children.Add(grp);
        _textBoxes[plugin.Name] = tbDict;
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
