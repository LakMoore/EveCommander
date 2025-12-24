using BotLib;
using System.IO;
using System.Xml.Serialization;
using System.Linq;

namespace Commander
{
  public class PluginSettings
  {
    public string PluginName { get; set; } = string.Empty;
    public HashSet<PluginSetting> Settings { get; set; } = [];
  }

  internal class PluginSettingsCache
  {
    private static HashSet<PluginSettings> _cache = [];

    internal static void LoadCache(string xmlString)
    {
      if (string.IsNullOrEmpty(xmlString))
      {
        return;
      }
      XmlSerializer serializer = new(typeof(HashSet<PluginSettings>), []);
      using var reader = new StringReader(xmlString);
      if (serializer.Deserialize(reader) is HashSet<PluginSettings> pluginSettings)
      {
        _cache = pluginSettings;
      }
    }

    internal static string SaveCache()
    {
      XmlSerializer serializer = new(typeof(HashSet<PluginSettings>), []);
      using var writer = new StringWriter();
      serializer.Serialize(writer, _cache);
      return writer.ToString();
    }

    // get a game client from the cache, or make a new one if not found
    internal static HashSet<PluginSetting> GetSettings(IBotLibPlugin plugin)
    {
      var settings = _cache.FirstOrDefault(ps => ps.PluginName == plugin.Name)?.Settings;

      // if not found, make a new one
      if (settings == null)
      {
        settings = [];
        _cache.Add(new() {
          PluginName = plugin.Name,
          Settings = settings
        });
      }

      // add any missing keys
      var settingsKeys = plugin.GetSettingKeys();
      foreach (var key in settingsKeys)
      {
        if (settings.FirstOrDefault(s => s.Key == key) == null)
        {
          settings.Add(new() { Key = key, Value = string.Empty } );
        }
      }

      return settings;
    }

    // Provide access to the underlying cache so UI can enumerate and modify settings
    internal static HashSet<PluginSettings> GetCache()
    {
      return _cache;
    }

  }
}
