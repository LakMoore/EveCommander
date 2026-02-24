namespace GridScout2
{
  public record LocalReport
  {
    public required string ScoutName { get; init; }
    public required string System { get; init; }
    public required long Time { get; init; }
    public required List<LocalPilot> Locals { get; init; }
    public string? Status { get; init; }
    public List<GridPilot>? OnGrid { get; init; }

    public bool MyEquals(object? obj)
    {
      return obj is LocalReport report &&
      ScoutName == report.ScoutName &&
      System == report.System &&
      Status == report.Status &&
      Locals.SequenceEqual(report.Locals) &&
      (OnGrid == null && report.OnGrid == null || 
       OnGrid != null && report.OnGrid != null && OnGrid.SequenceEqual(report.OnGrid));
    }
  }
}
