using System.Drawing;

namespace BotLib
{
  public class PluginResult
  {
    public static PluginResult True 
      => new() { WorkDone = true, Message = null, Background = null, Foreground = null };

    public static PluginResult False
      => new() { WorkDone = false, Message = null, Background = null, Foreground = null };

    public required bool WorkDone;
    public string? Message;
    public Color? Background;
    public Color? Foreground;

    public static implicit operator PluginResult(bool value)
      => value ? True : False;
  }
}
