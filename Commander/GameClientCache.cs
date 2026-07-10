using BotLib;
using read_memory_64_bit;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace Commander
{
  internal class GameClientCache
  {
    private static List<ClientGroup> _cache = [];

    internal static void LoadCache(string uiRootAddressCache)
    {
      string xmlString = uiRootAddressCache;
      if (string.IsNullOrEmpty(xmlString))
      {
        return;
      }
      XmlSerializer serializer = new(typeof(List<ClientGroup>), [typeof(CommanderCharacter)]);
      using var reader = new StringReader(xmlString);
      try
      {
        if (serializer.Deserialize(reader) is List<ClientGroup> serializableDictionary)
        {
          _cache = serializableDictionary;
        }
      }
      catch (InvalidOperationException)
      {
        _cache = [];
      }
    }

    internal static string SaveCache()
    {
      var serializer = new XmlSerializer(typeof(List<ClientGroup>), [typeof(CommanderCharacter)]);
      using var writer = new StringWriter();
      serializer.Serialize(writer, _cache);
      return writer.ToString();
    }

    // get a game client from the cache, or make a new one if not found
    internal static ClientGroup GetGameClient(int processId, long mainWindowId)
    {
      ClientGroup? gameClient = _cache.FirstOrDefault(x =>
          x.GameClient.processId == processId && x.GameClient.mainWindowId == mainWindowId
      );

      // if not found, make a new one
      if (gameClient == null)
      {
        gameClient = new ClientGroup()
        {
          GameClient = new GameClient() { processId = processId, mainWindowId = mainWindowId },
          Characters = [],
        };
        _cache.Add(gameClient);
      }

      return gameClient;
    }

    // get a game client from the cache by character name
    internal static ClientGroup? GetGameClientForCharacter(string characterName)
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
      return _cache.SelectMany(x => x.Characters).OfType<CommanderCharacter>().ToHashSet();
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
