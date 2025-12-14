using read_memory_64_bit;

namespace Commander
{
  public record CommanderClient
  {
    public required GameClient GameClient { get; init; }
    public required HashSet<CommanderCharacter> Characters { get; init; }
  }
}
