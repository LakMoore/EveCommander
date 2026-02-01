
namespace BotLib
{
  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
  public class BotLibSetting : Attribute
  {
    public enum Type
    {
      SingleLineText,
      MultiLineText,
      Integer,
      Decimal
    }

    public required Type SettingType { get; set; }
    public string? Description { get; set; }

  }
}
