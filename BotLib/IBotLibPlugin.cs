using eve_parse_ui;
using read_memory_64_bit;
using System.Reflection;

namespace BotLib
{
  public abstract class IBotLibPlugin(string characterName, long windowID)
  {
    private bool _IsEnabled = false;

    protected bool _IsCompleted = false;

    public abstract Task<PluginResult> DoWork(ParsedUserInterface uiRoot, GameClient gameClient, IEnumerable<IBotLibPlugin> otherCharsWithPlugins);

    public abstract string Name { get; }

    public long WindowID { get; init; } = windowID;

    public string CharacterName { get; init; } = characterName;

    public bool IsEnabled
    {
      get => _IsEnabled;
      set => _IsEnabled = value;
    }

    public bool IsCompleted
    {
      get => _IsCompleted;
    }

    private HashSet<PluginSetting>? _settings;

    public record PluginSettingInfo
    {
      public required string Key;
      public required string Description;
      public required BotLibSetting.Type SettingType;
    }

    public IReadOnlyList<PluginSettingInfo> GetSettingsInfo()
    {
      Type type = this.GetType();
      FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
      List<PluginSettingInfo> keys = [];

      foreach (FieldInfo field in fields)
      {
        var attr = field.GetCustomAttribute<BotLibSetting>();
        if (attr != null)
        {
          keys.Add(new() { Key = field.Name, Description = attr.Description ?? "", SettingType = attr.SettingType });
        }
      }

      return keys.AsReadOnly();
    }

    protected void CheckForNewSettings()
    {
      if (_settings != null)
      {
        foreach (var info in GetSettingsInfo())
        {
          var value = _settings.FirstOrDefault(s => s.Key == info.Key)?.Value;
          if (value != null)
          {
            var field = this.GetType().GetField(info.Key);
            if (field != null)
            {
              if (field.FieldType == typeof(string))
              {
                field.SetValue(this, value);
              }
              else if (field.FieldType == typeof(int) && int.TryParse(value, out int intValue))
              {
                field.SetValue(this, intValue);
              }
              else if (field.FieldType == typeof(bool) && bool.TryParse(value, out bool boolValue))
              {
                field.SetValue(this, boolValue);
              }
              // Add more type conversions as needed
            }
          }
        }
      }
    }

    public void SetSettings(HashSet<PluginSetting> settings)
    {
      this._settings = settings;
      CheckForNewSettings();
    }
  }
}
