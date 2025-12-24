using eve_parse_ui;
using read_memory_64_bit;

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

    private Dictionary<string, string>? _settings;

    private List<string> SettingsKeys { get; init; } = [];

    protected void RegisterSettingKey(string key)
    {
      SettingsKeys.Add(key);
      CheckForNewSettings();
    }

    public IReadOnlyList<string> GetSettingKeys()
    {
      return SettingsKeys.AsReadOnly();
    }

    protected void CheckForNewSettings()
    {
      if (_settings != null)
      {
        foreach (var key in GetSettingKeys())
        {
          if (_settings.TryGetValue(key, out string? value))
          {
            var property = this.GetType().GetProperty(key);
            if (property != null)
            {
              if (property.PropertyType == typeof(string))
              {
                property.SetValue(this, value);
              }
              else if (property.PropertyType == typeof(int) && int.TryParse(value, out int intValue))
              {
                property.SetValue(this, intValue);
              }
              else if (property.PropertyType == typeof(bool) && bool.TryParse(value, out bool boolValue))
              {
                property.SetValue(this, boolValue);
              }
              // Add more type conversions as needed
            }
          }
        }
      }
    }

    public void SetSettings(Dictionary<string, string> settings)
    {
      this._settings = settings;
      CheckForNewSettings();
    }
  }
}
