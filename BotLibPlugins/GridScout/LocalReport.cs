namespace GridScout2
{
  public record LocalReport
  {
    public required string ScoutName { get; init; }
    public required string System { get; init; }
    public required long Time { get; init; }
    public required List<LocalPilot> Locals { get; init; }

    public bool MyEquals(object? obj)
    {
      return obj is LocalReport report &&
      ScoutName == report.ScoutName &&
      System == report.System &&
      Time == report.Time &&
      Locals.SequenceEqual(report.Locals);
    }
  }
}
