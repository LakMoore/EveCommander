using read_memory_64_bit;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace Commander
{
  internal class GameClientCache
  {
    private static List<CommanderClient> _cache = [];

    internal static void LoadCache(string uiRootAddressCache)
    {
      string xmlString = uiRootAddressCache;
      if (string.IsNullOrEmpty(xmlString))
      {
        return;
      }
      XmlSerializer serializer = new(typeof(List<CommanderClient>), []);
      using var reader = new StringReader(xmlString);
      if (serializer.Deserialize(reader) is List<CommanderClient> serializableDictionary)
      {
        _cache = serializableDictionary;
      }
    }

    internal static string SaveCache()
    {
      XmlSerializer serializer = new(typeof(List<CommanderClient>), []);
      using var writer = new StringWriter();
      serializer.Serialize(writer, _cache);
      return writer.ToString();
    }

    // get a game client from the cache, or make a new one if not found
    internal static CommanderClient GetGameClient(int processId, long mainWindowId)
    {
      CommanderClient? gameClient = _cache.FirstOrDefault(x =>
          x.GameClient.processId == processId && x.GameClient.mainWindowId == mainWindowId
      );

      // if not found, make a new one
      if (gameClient == null)
      {
        gameClient = new CommanderClient()
        {
          GameClient = new GameClient() { processId = processId, mainWindowId = mainWindowId },
          Characters = [],
        };
        _cache.Add(gameClient);
      }

      return gameClient;
    }

    // get a game client from the cache by character name
    internal static CommanderClient? GetGameClientForCharacter(string characterName)
    {
      if (string.IsNullOrEmpty(characterName)) return null;

      Debug.Assert(characterName != "EVE");
      Debug.Assert(_cache.Where(x => x.Characters.Any(c => c.Name == characterName)).Count() < 2);

      return _cache.FirstOrDefault(x =>
          x.Characters.Any(c => c.Name == characterName)
      );
    }

    internal static IReadOnlySet<CommanderCharacter> GetAllCharacters()
    {
      return _cache.SelectMany(x => x.Characters).ToHashSet();
    }

    internal static void CleanCache()
    {
      _cache.
          Where(x =>
              x.Characters.Count == 0
          )
          .ToList()
          .ForEach(x => _cache.Remove(x));
    }
  }
}
