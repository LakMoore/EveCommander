using eve_parse_ui;
using read_memory_64_bit;

namespace BotLib
{
  public abstract class IBotLibPlugin(string characterName, long windowID)
  {
    private bool _IsEnabled = false;

    protected bool _IsCompleted = false;

    public abstract Task<bool> DoWork(ParsedUserInterface uiRoot, GameClient gameClient, IEnumerable<IBotLibPlugin> otherCharsWithPlugins);

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
  }
}
