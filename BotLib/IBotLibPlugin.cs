using eve_parse_ui;
using read_memory_64_bit;
using System.Reflection;
using System.Runtime.CompilerServices;

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

    public virtual bool IsCompleted
    {
      get => _IsCompleted;
    }

    public virtual bool IsPaused
    {
      get => false;
    }

    private HashSet<PluginSetting>? _settings;

    public record PluginSettingInfo
    {
      public required string Key;
      public required string Label;
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
          keys.Add(new() { Key = field.Name, Label = attr.Label ?? field.Name, Description = attr.Description ?? "", SettingType = attr.SettingType });
        }
      }

      return keys.AsReadOnly();
    }

    protected void LogDebug(string message, [CallerMemberName] string memberName = "")
      => Log(PluginLogLevel.Debug, message, null, memberName);

    protected void LogInformation(string message, [CallerMemberName] string memberName = "")
      => Log(PluginLogLevel.Information, message, null, memberName);

    protected void LogWarning(string message, [CallerMemberName] string memberName = "")
      => Log(PluginLogLevel.Warning, message, null, memberName);

    protected void LogError(string message, Exception? exception = null, [CallerMemberName] string memberName = "")
      => Log(PluginLogLevel.Error, message, exception, memberName);

    protected void Log(PluginLogLevel level, string message, Exception? exception = null, [CallerMemberName] string memberName = "")
    {
      PluginLogManager.Log(new PluginLogEntry
      {
        TimestampUtc = DateTimeOffset.UtcNow,
        Level = level,
        PluginName = Name,
        CharacterName = string.IsNullOrWhiteSpace(CharacterName) ? "<unknown>" : CharacterName,
        WindowId = WindowID,
        MemberName = memberName,
        Message = message,
        Exception = exception?.ToString()
      });
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
              var fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

              if (fieldType == typeof(string))
              {
                field.SetValue(this, value.ToString());
              }
              else if (fieldType == typeof(int) && int.TryParse(value.ToString(), out int intValue))
              {
                field.SetValue(this, intValue);
              }
              else if (fieldType == typeof(decimal) && decimal.TryParse(value.ToString(), out decimal decimalValue))
              {
                field.SetValue(this, decimalValue);
              }
              else if (fieldType == typeof(bool))
              {
                if (value is bool boolValue)
                {
                  field.SetValue(this, boolValue);
                }
                else if (bool.TryParse(value.ToString(), out bool parsedBoolValue))
                {
                  field.SetValue(this, parsedBoolValue);
                }
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
