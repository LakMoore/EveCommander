using System;

namespace BotLib
{
  public class PluginSetting
  {
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public override string ToString()
    {
      return $"Key: {Key}, Value: {Value}";
    }

    // override Equals and GetHashCode for proper comparison in HashSet
    public override bool Equals(object? obj)
    {
      if (obj is PluginSetting other)
      {
        return this.Key == other.Key;
      }
      return false;
    }
    public override int GetHashCode()
    {
      return Key.GetHashCode();
    }
  }
}
