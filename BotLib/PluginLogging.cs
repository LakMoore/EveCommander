using System.Diagnostics;

namespace BotLib
{
  public enum PluginLogLevel
  {
    Debug,
    Information,
    Warning,
    Error
  }

  public sealed record PluginLogEntry
  {
    public required DateTimeOffset TimestampUtc { get; init; }
    public required PluginLogLevel Level { get; init; }
    public required string PluginName { get; init; }
    public required string CharacterName { get; init; }
    public required long WindowId { get; init; }
    public required string MemberName { get; init; }
    public required string Message { get; init; }
    public string? Exception { get; init; }
  }

  public interface IPluginLogger
  {
    void Log(PluginLogEntry entry);
  }

  public static class PluginLogManager
  {
    private static IPluginLogger _logger = DebugPluginLogger.Instance;

    public static IPluginLogger Logger
    {
      get => Volatile.Read(ref _logger);
    }

    public static void SetLogger(IPluginLogger? logger)
    {
      Volatile.Write(ref _logger, logger ?? DebugPluginLogger.Instance);
    }

    public static void Log(PluginLogEntry entry)
    {
      Logger.Log(entry);
    }

    public static string FormatEntry(PluginLogEntry entry)
    {
      return $"[{entry.TimestampUtc:O}] [{entry.Level}] [{entry.PluginName}] [{entry.CharacterName}] [Window {entry.WindowId}] [{entry.MemberName}] {entry.Message}";
    }

    private sealed class DebugPluginLogger : IPluginLogger
    {
      public static DebugPluginLogger Instance { get; } = new();

      public void Log(PluginLogEntry entry)
      {
        Debug.WriteLine(FormatEntry(entry));
        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
          Debug.WriteLine(entry.Exception);
        }
      }
    }
  }
}
