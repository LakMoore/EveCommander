using read_memory_64_bit;

namespace BotLib
{
  public record ClientGroup
  {
    public required GameClient GameClient { get; init; }
    public required HashSet<BotCharacter> Characters { get; init; }
  }
}
