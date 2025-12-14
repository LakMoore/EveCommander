namespace AssemblyLineLib
{
  public record Item
  {
    public int? TypeId { get; set; }
    public required string TypeName { get; init; }
    public required int Quantity { get; init; }
  }
}
