using SDEdotNet;

namespace AssemblyLineLib
{
  public class PlanetaryIndustry
  {

    public static List<int> GetPITypeInputs(int typeId)
    {
      SqliteLatestContext dbContext = new();

      var inputs = from output in dbContext.PlanetSchematicsTypeMaps
                   join input in dbContext.PlanetSchematicsTypeMaps on output.SchematicId equals input.SchematicId
                   where output.TypeId == typeId && output.IsInput == false
                   select input.TypeId;

      return [.. inputs];
    }
  }
}
