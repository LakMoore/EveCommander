
namespace BotLib
{
  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
  public class BotLibSetting : Attribute
  {
    public string? Label { get; set; }
    public enum Type
    {
      SingleLineText,
      MultiLineText,
      Integer,
      Decimal,
      Boolean
    }
    public required Type SettingType { get; set; }
    public string? Description { get; set; }
  }
}
