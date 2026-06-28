using System.Drawing;

namespace BotLib
{
  public class PluginResult
  {
    public static PluginResult True(string? message = null)
      => new() { WorkDone = true, Message = message, Background = null, Foreground = null };

    public static PluginResult False(string? message = null)
      => new() { WorkDone = false, Message = message, Background = null, Foreground = null };

    public static PluginResult Success(string message)
    {
      return new() { WorkDone = false, Message = message, Background = Color.Green, Foreground = Color.White };
    }

    public static PluginResult Failure(string message)
    {
      return new() { WorkDone = false, Message = message, Background = Color.Red, Foreground = Color.White };
    }

    public required bool WorkDone;
    public string? Message;
    public Color? Background;
    public Color? Foreground;

    public static implicit operator PluginResult(bool value)
      => value ? True() : False();

    // can cast to bool
    public static implicit operator bool(PluginResult result)
      => result.WorkDone;

  }
}
