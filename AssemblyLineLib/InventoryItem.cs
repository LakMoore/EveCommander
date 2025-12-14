namespace AssemblyLineLib
{
  public record InventoryItem
  {
    public int TypeId { get; init; }
    public required string TypeName { get; init; }
    public int GroupId { get; init; }
    public required string GroupName { get; init; }
    public int OpeningStockQuantity { get; set; }
    public int QuantityNeeded { get; set; }
    public int QuantityProduced { get; set; }
    public int QuantityToBuy => Math.Max(0, QuantityNeeded - OpeningStockQuantity - QuantityProduced);
    public int ToBuyCostAtSell { get; init; }
    public double ToBuyVolume => AssemblyLine.GetVolume(TypeId, QuantityToBuy);
    public int ClosingStockQuantity => Math.Max(0, OpeningStockQuantity + QuantityProduced + QuantityToBuy - QuantityNeeded);
  }
}