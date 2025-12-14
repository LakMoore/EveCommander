using eve_parse_ui;
using read_memory_64_bit;

namespace BotLib
{
  /// <summary>
  /// Use with caution. Plugin inheriting this interface will start doing work as soon as the client is found.
  /// This may be useful for plugins that need to run immediately, but cannot be stopped with the hotkey.
  /// Avoid recursion and long running tasks in AutoRun plugins.
  /// </summary>
  public abstract class IAutoRunPlugin()
  {
    public abstract Task<PluginResult> DoWork(ParsedUserInterface uiRoot, GameClient GameClient, bool isRunning, IEnumerable<IBotLibPlugin> allPlugins);

    public abstract string Name { get; }

    public string? CharacterName { get; set; }

  }
}
