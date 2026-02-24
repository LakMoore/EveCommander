namespace GridScout2
{
  public record GridPilot
  {
    public required string PilotName { get; init; }
    public required string ShipType { get; init; }
    public int? ShipTypeId { get; init; }
    public required string StandingHint { get; init; }
    public int? StandingIconId { get; init; }
    public required string Action { get; init; }
    public string? Distance { get; init; }
    public int? DistanceMeters { get; init; }
    public string? Corporation { get; init; }
    public string? Alliance { get; init; }
  }
}
