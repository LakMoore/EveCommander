namespace GridScout2
{
  public record LocalPilot
  {
    public required string Name { get; init; }
    public required long CharacterID { get; init; }
    public required string StandingHint { get; init; }
  }
}
