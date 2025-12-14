using SDEdotNet;

namespace AssemblyLineLib
{
  public class AssemblyLine
  {
    private readonly SqliteLatestContext dbContext;

    private AssemblyLine()
    {
      dbContext = new();
    }

    // singleton
    public static AssemblyLine Instance { get; } = new();

    public int GetTypeId(string typeName)
    {
      var query = from t in dbContext.InvTypes
                  where t.TypeName == typeName
                  select t.TypeId;

      return query.ToList().FirstOrDefault(0);
    }

    public async Task<BuildPlan> GetBuildPlan(IEnumerable<Item> toBuild, IEnumerable<Item> stock, IEnumerable<Item> closingStockTarget)
    {
      Queue<InventoryItem> requiredInputs = [];

      foreach (var item in toBuild)
      {
        var inputs = GetBlueprintInputs(GetTypeId(item.TypeName), 0, item.Quantity);
        foreach (var input in inputs)
        {
          requiredInputs.Enqueue(input);
        }
      }

      BuildPlan buildPlan = new();

      buildPlan.AddStock(stock);

      while (requiredInputs.Count > 0)
      {
        var inputItem = requiredInputs.Dequeue();

        //var input = GetTypeName(inputItem.MaterialTypeId);
        //var output = GetTypeName(inputItem.TypeId);

        if (inputItem?.TypeId is int typeId && typeId > 0)
        {
          // add it to the build plan
          buildPlan.AddNeededItem(inputItem);

          var subInputs = GetBlueprintInputs(typeId, 0, inputItem.QuantityNeeded);
          // add its inputs to the queue
          foreach (var subInput in subInputs)
          {
            requiredInputs.Enqueue(subInput);
          }
        }
      }

      // reset the list to now include all the parts we know we are going to make
      requiredInputs = new(buildPlan.GetPartsToBuild());

      while (requiredInputs.Count > 0)
      {
        var inputItem = requiredInputs.Dequeue();

        var job = await GetJobToRun(inputItem.TypeId, 0, inputItem.QuantityNeeded);

        if (job != null)
        {
          buildPlan.AddJobToRun(job);
        }
      }

      // What do we need to build or buy to get to the closing stock targets
      var additionalStock = closingStockTarget.
          Select(i =>
          {
            var buildPlanItem = buildPlan.GetClosingStock().FirstOrDefault(cs => cs.TypeId == i.TypeId);
            if (buildPlanItem == null)
            {
              return i;
            }
            return new Item()
            {
              TypeId = GetTypeId(i.TypeName),
              TypeName = i.TypeName,
              Quantity = Math.Max(0, i.Quantity - buildPlanItem.ClosingStockQuantity)
            };
          })
          .Where(i => i.Quantity > 0)
          .ToList();

      if (additionalStock.Count > 0)
      {
        // if we need some additional parts, make a plan for them
        var additionalPlan = await GetBuildPlan(additionalStock, [], []);
        buildPlan.Merge(additionalPlan);
      }

      return buildPlan;
    }

    private async Task<JobToRun?> GetJobToRun(int resultItemTypeId, int ME, int requiredOutputQuantity)
    {
      var inputQuery =
      from b in dbContext.IndustryActivityProducts
      join m in dbContext.IndustryActivityMaterials on b.TypeId equals m.TypeId
      where b.ProductTypeId == resultItemTypeId && (b.ActivityId == 1 || b.ActivityId == 11)
      select new { PriceTask = MarketPrices.GetAdjustedPrice(m.MaterialTypeId), m.Quantity };

      var inputCosts = await Task.WhenAll(inputQuery
          .ToList()
          .Select(async m =>
          {
            var price = await m.PriceTask;
            return price * (m.Quantity ?? 1);
          })
          .ToList());

      var EIV = (long)Math.Ceiling(inputCosts.Sum());

      var bpQuery = from b in dbContext.IndustryActivityProducts
                    join bp in dbContext.IndustryBlueprints on b.TypeId equals bp.TypeId
                    join i in dbContext.IndustryActivities on new { ActivityId = b.ActivityId ?? 0, TypeId = b.TypeId ?? 0 } equals new { i.ActivityId, i.TypeId }
                    join t in dbContext.InvTypes on b.TypeId equals t.TypeId
                    join tp in dbContext.InvTypes on b.ProductTypeId equals tp.TypeId
                    join g in dbContext.InvGroups on t.GroupId equals g.GroupId
                    join gp in dbContext.InvGroups on tp.GroupId equals gp.GroupId
                    where b.ProductTypeId == resultItemTypeId && (b.ActivityId == 1 || b.ActivityId == 11)
                    select new JobToRun()
                    {
                      BlueprintTypeId = b.TypeId ?? 0,
                      BlueprintTypeName = t.TypeName ?? "Unknown Item",
                      TotalRunsToInstall = (int)Math.Ceiling((float)requiredOutputQuantity / b.Quantity ?? 1f),
                      ActivityId = b.ActivityId ?? 0,
                      MaxRuns = bp.MaxProductionLimit ?? 1,
                      BaseTimeInSeconds = i.Time ?? 1,
                      EstimatedItemValue = EIV,
                      OutputTypeId = b.ProductTypeId ?? 0,
                      OutputGroupId = g.GroupId,
                      OutputGroupName = gp.GroupName ?? "Unknown Group",
                      OutputQuantity = (int)Math.Ceiling((float)requiredOutputQuantity / b.Quantity ?? 1f) * (b.Quantity ?? 1),
                    };


      return bpQuery.FirstOrDefault();
    }

    public int GetBlueprintTypeId(int typeId)
    {
      var query = from b in dbContext.IndustryActivityProducts
                  where b.ProductTypeId == typeId
                  select b.TypeId;

      return query.ToList().FirstOrDefault(0) ?? 0;
    }

    public IReadOnlyList<InventoryItem> GetBlueprintInputs(int resultItemTypeId, int ME, int requiredOutputQuantity)
    {
      var bpQuery = from b in dbContext.IndustryActivityProducts
                    where b.ProductTypeId == resultItemTypeId && (b.ActivityId == 1 || b.ActivityId == 11)
                    select b;

      var bp = bpQuery.ToList().FirstOrDefault();

      if (bp == null)
        return [];

      var runsRequired = (int)Math.Ceiling((float)requiredOutputQuantity / bp.Quantity ?? 1f);

      var query = from m in dbContext.IndustryActivityMaterials
                  join t in dbContext.InvTypes on m.MaterialTypeId equals t.TypeId
                  join g in dbContext.InvGroups on t.GroupId equals g.GroupId
                  where m.TypeId == bp.TypeId
                  select new InventoryItem()
                  {
                    TypeId = t.TypeId,
                    TypeName = t.TypeName ?? "Unknown Item",
                    GroupId = t.GroupId ?? -1,
                    GroupName = g.GroupName ?? "Unknown Group",
                    QuantityNeeded = (int)Math.Ceiling((runsRequired * m.Quantity ?? 0) * (100.0 - ME) / 100.0),
                  };


      var result = query.ToList();

      return result;
    }

    public string GetTypeName(int? typeId)
    {
      if (typeId == null)
        return "Unknown Item";

      var query = from t in dbContext.InvTypes
                  where t.TypeId == typeId
                  select t.TypeName;

      return query.ToList().FirstOrDefault("Unknown Item");
    }

    public string GetGroupName(int? typeId)
    {
      if (typeId == null)
        return "Unknown Group";

      var query = from t in dbContext.InvTypes
                  join g in dbContext.InvGroups on t.GroupId equals g.GroupId
                  where t.TypeId == typeId
                  select g.GroupName;

      return query.ToList().FirstOrDefault("Unknown Group");
    }

    public InventoryItem? GetInventoryItem(int typeId)
    {
      if (typeId < 1) return null;

      var query = from t in dbContext.InvTypes
                  join g in dbContext.InvGroups on t.GroupId equals g.GroupId
                  where t.TypeId == typeId
                  select new InventoryItem()
                  {
                    TypeId = t.TypeId,
                    TypeName = t.TypeName ?? "Unknown Item",
                    GroupId = t.GroupId ?? -1,
                    GroupName = g.GroupName ?? "Unknown Group",
                  };

      return query.FirstOrDefault();
    }

    public static double GetVolume(int typeId, int quantityToBuy)
    {
      var query = from t in Instance.dbContext.InvTypes
                  where t.TypeId == typeId
                  select t.Volume * quantityToBuy;

      return query.ToList().FirstOrDefault() ?? 0;
    }

    //var stockText = "Navy Cap Booster 150\t46\r\nCaldari Navy Mjolnir Heavy Missile\t7686\r\nCaldari Navy Nova Heavy Missile\t24000\r\nNanite Repair Paste\t7000\r\nSisters Core Scanner Probe\t100\r\nMegacyte\t75000\r\nMorphite\t10000\r\nNocxium\t220000\r\nZydrine\t160000\r\nCadmium\t17000\r\nCaesium\t10000\r\nChromium\t10000\r\nCobalt\t50000\r\nDysprosium\t7000\r\nHafnium\t22000\r\nMercury\t20000\r\nNeodymium\t10424\r\nPlatinum\t13000\r\nScandium\t45000\r\nTechnetium\t6000\r\nThulium\t8000\r\nTitanium\t40000\r\nVanadium\t57000\r\nLarge Ancillary Remote Shield Booster\t1\r\nIFFA Compact Damage Control\t20\r\nRepublic Fleet Large Shield Extender\t3";

    public static IEnumerable<Item> ParseStockFromClipboard(string stockText)
    {
      var lines = stockText.Split('\n');

      return lines
          .Where(l => !string.IsNullOrWhiteSpace(l))
          .Select(l =>
          {
            var parts = l.Split('\t');

            if (parts.Length == 1)
            {
              return new Item()
              {
                TypeName = parts[0].Trim(),
                Quantity = 1,
              };
            }

            if (parts.Length == 2)
            {
              return new Item()
              {
                TypeName = parts[0].Trim(),
                Quantity = int.Parse(parts[1].Trim()),
              };
            }

            return null;
          })
          .Where(i => i != null)
          .Cast<Item>();
    }
  }
}
