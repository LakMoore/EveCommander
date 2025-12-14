using System.Diagnostics;

namespace AssemblyLineLib
{
  public record BuildPlan
  {
    private List<InventoryItem> Inventory { get; init; } = [];
    private List<JobToRun> JobsToInstall { get; init; } = [];

    public IEnumerable<Item> AddStock(IEnumerable<Item> inventoryItems)
    {
      List<Item> failedToAdd = [];

      foreach (var item in inventoryItems)
      {
        if ((item.TypeId == null || item.TypeId < 1) && !string.IsNullOrWhiteSpace(item.TypeName))
        {
          item.TypeId = AssemblyLine.Instance.GetTypeId(item.TypeName);
        }

        if (item.TypeId != null && item.TypeId > 0)
        {
          InventoryItem? existing = Inventory.FirstOrDefault(i => i.TypeId == item.TypeId);
          if (existing != null)
          {
            existing.OpeningStockQuantity += item.Quantity;
          }
          else
          {
            InventoryItem? i = AssemblyLine.Instance.GetInventoryItem((int)item.TypeId);
            if (i != null)
            {
              i.OpeningStockQuantity = item.Quantity;
              Inventory.Add(i);
            }
          }
        }
        else
        {
          failedToAdd.Add(item);
        }
      }

      return failedToAdd;
    }

    public void AddNeededItem(InventoryItem item)
    {
      InventoryItem? existing = Inventory.FirstOrDefault(i => i.TypeId == item.TypeId);
      if (existing != null)
      {
        existing.QuantityNeeded += item.QuantityNeeded;
      }
      else
      {
        Inventory.Add(item);
      }
    }

    public void AddJobToRun(JobToRun job)
    {
      InventoryItem? existing = Inventory.FirstOrDefault(i => i.TypeId == job.OutputTypeId);

      Debug.Assert(existing != null);

      existing.QuantityProduced += job.OutputQuantity;

      JobsToInstall.Add(job);

    }

    internal IEnumerable<InventoryItem> GetOpeningStock()
    {
      return Inventory.Where(i => i.OpeningStockQuantity > 0);
    }

    public IEnumerable<InventoryItem> GetPartsToBuy()
    {
      return Inventory.Where(i => i.QuantityToBuy > 0);
    }

    internal IEnumerable<InventoryItem> GetPartsToBuild()
    {
      return Inventory.Where(i => i.QuantityNeeded > 0);
    }

    public IEnumerable<JobToRun> GetJobsToRun()
    {
      return JobsToInstall;
    }

    public IEnumerable<InventoryItem> GetClosingStock()
    {
      return Inventory.Where(i => i.ClosingStockQuantity > 0);
    }

    public void Merge(BuildPlan additionalPlan)
    {
      foreach (var item in additionalPlan.GetPartsToBuy())
      {
        AddNeededItem(item);
      }

      foreach (var item in additionalPlan.GetPartsToBuild())
      {
        AddNeededItem(item);
      }

      foreach (var job in additionalPlan.GetJobsToRun())
      {
        AddJobToRun(job);
      }
    }
  }
}