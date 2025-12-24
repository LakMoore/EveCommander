using BotLib;
using read_memory_64_bit;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace Commander
{
  internal class PluginSettingsCache
  {
    private static Dictionary<string, Dictionary<string, string>> _cache = [];

    internal static void LoadCache(string xmlString)
    {
      if (string.IsNullOrEmpty(xmlString))
      {
        return;
      }
      XmlSerializer serializer = new(typeof(Dictionary<string, Dictionary<string, string>>), []);
      using var reader = new StringReader(xmlString);
      if (serializer.Deserialize(reader) is Dictionary<string, Dictionary<string, string>> serializableDictionary)
      {
        _cache = serializableDictionary;
      }
    }

    internal static string SaveCache()
    {
      XmlSerializer serializer = new(typeof(Dictionary<string, Dictionary<string, string>>), []);
      using var writer = new StringWriter();
      serializer.Serialize(writer, _cache);
      return writer.ToString();
    }

    // get a game client from the cache, or make a new one if not found
    internal static Dictionary<string, string> GetSettings(IBotLibPlugin plugin)
    {
      var settings = _cache.GetValueOrDefault(plugin.Name);

      // if not found, make a new one
      if (settings == null)
      {
        settings = [];
        _cache.Add(plugin.Name, settings);
      }

      // add any missing keys
      var settingsKeys = plugin.GetSettingKeys();
      foreach (var key in settingsKeys)
      {
        if (!settings.ContainsKey(key))
        {
          settings[key] = string.Empty;
        }
      }

      return settings;
    }

    // Provide access to the underlying cache so UI can enumerate and modify settings
    internal static Dictionary<string, Dictionary<string, string>> GetCache()
    {
      return _cache;
    }

  }
}
