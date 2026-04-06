using BotLib;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace Commander.Services;

internal static class PluginLoggingService
{
  private static SessionPluginLogger? _sessionLogger;

  internal static string GetLogDirectory()
  {
    return Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "Commander",
      "Logs");
  }

  internal static void Initialize()
  {
    if (_sessionLogger != null)
    {
      return;
    }

    try
    {
      _sessionLogger = new SessionPluginLogger();
      PluginLogManager.SetLogger(_sessionLogger);
    }
    catch (IOException ex)
    {
      Debug.WriteLine($"Failed to initialize session plugin logger: {ex}");
    }
    catch (UnauthorizedAccessException ex)
    {
      Debug.WriteLine($"Failed to initialize session plugin logger: {ex}");
    }
  }

  internal static void Shutdown()
  {
    var sessionLogger = Interlocked.Exchange(ref _sessionLogger, null);
    PluginLogManager.SetLogger(null);
    sessionLogger?.Dispose();
  }
}

internal sealed class SessionPluginLogger : IPluginLogger, IDisposable
{
  private const int MAX_SESSION_LOG_FILES = 10;
  private readonly Channel<PluginLogEntry> _channel;
  private readonly FileStream _logStream;
  private readonly StreamWriter _writer;
  private readonly Task _writerTask;
  private int _disposed;

  internal SessionPluginLogger()
  {
    var logDirectory = PluginLoggingService.GetLogDirectory();

    Directory.CreateDirectory(logDirectory);
    DeleteOldLogFiles(logDirectory);

    var logFilePath = Path.Combine(logDirectory, $"plugins-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    _logStream = new FileStream(logFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
    _writer = new StreamWriter(_logStream, new UTF8Encoding(false)) { AutoFlush = true };
    _channel = Channel.CreateUnbounded<PluginLogEntry>(new UnboundedChannelOptions
    {
      SingleReader = true,
      SingleWriter = false
    });
    _writerTask = Task.Run(ProcessQueueAsync);

    Log(new PluginLogEntry
    {
      TimestampUtc = DateTimeOffset.UtcNow,
      Level = PluginLogLevel.Information,
      PluginName = "Commander",
      CharacterName = "-",
      WindowId = 0,
      MemberName = nameof(SessionPluginLogger),
      Message = $"Session log started: {logFilePath}"
    });
  }

  public void Log(PluginLogEntry entry)
  {
    if (Volatile.Read(ref _disposed) == 1)
    {
      return;
    }

    Debug.WriteLine(PluginLogManager.FormatEntry(entry));
    if (!string.IsNullOrWhiteSpace(entry.Exception))
    {
      Debug.WriteLine(entry.Exception);
    }

    _channel.Writer.TryWrite(entry);
  }

  public void Dispose()
  {
    if (Interlocked.Exchange(ref _disposed, 1) == 1)
    {
      return;
    }

    _channel.Writer.TryComplete();

    try
    {
      _writerTask.GetAwaiter().GetResult();
    }
    finally
    {
      _writer.Dispose();
      _logStream.Dispose();
    }
  }

  private async Task ProcessQueueAsync()
  {
    try
    {
      await foreach (var entry in _channel.Reader.ReadAllAsync())
      {
        await _writer.WriteLineAsync(PluginLogManager.FormatEntry(entry));
        if (!string.IsNullOrWhiteSpace(entry.Exception))
        {
          await _writer.WriteLineAsync(entry.Exception);
        }
      }
    }
    catch (IOException ex)
    {
      Debug.WriteLine($"Plugin log writer failed: {ex}");
    }
    catch (UnauthorizedAccessException ex)
    {
      Debug.WriteLine($"Plugin log writer failed: {ex}");
    }
    catch (ObjectDisposedException ex)
    {
      Debug.WriteLine($"Plugin log writer stopped: {ex}");
    }
  }

  private static void DeleteOldLogFiles(string logDirectory)
  {
    var existingLogFiles = new DirectoryInfo(logDirectory)
      .GetFiles("plugins-*.log")
      .OrderByDescending(file => file.CreationTimeUtc)
      .ToList();

    foreach (var oldLogFile in existingLogFiles.Skip(MAX_SESSION_LOG_FILES - 1))
    {
      try
      {
        oldLogFile.Delete();
      }
      catch (IOException ex)
      {
        Debug.WriteLine($"Failed to delete old plugin log '{oldLogFile.FullName}': {ex.Message}");
      }
      catch (UnauthorizedAccessException ex)
      {
        Debug.WriteLine($"Failed to delete old plugin log '{oldLogFile.FullName}': {ex.Message}");
      }
    }
  }
}
