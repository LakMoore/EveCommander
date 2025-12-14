namespace AssemblyLineLib
{
  public record JobToRun
  {
    public int BlueprintTypeId { get; init; }
    public required string BlueprintTypeName { get; init; }
    public int TotalRunsToInstall { get; init; }
    public int ActivityId { get; init; }
    public int MaxRuns { get; init; }
    public float BaseTimeInSeconds { get; init; }
    public long EstimatedItemValue { get; init; }
    public int OutputTypeId { get; init; }
    public int OutputQuantity { get; init; }
    public required string OutputGroupName { get; init; }
    public int OutputGroupId { get; init; }
  }
}
