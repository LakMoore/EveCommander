using BotLib;
using eve_parse_ui;
using read_memory_64_bit;
using System.Runtime.Versioning;

namespace BotLibPlugins
{

  internal class WormholeScout(string characterName, long windowID) : IBotLibPlugin(characterName, windowID)
  {
    public override string Name => "Wormhole Scout";

    public string? ScoutingBookmarkName { get; set; }

    [SupportedOSPlatform("windows5.0")]
    public override async Task<PluginResult> DoWork(ParsedUserInterface uiRoot, GameClient gameClient, IEnumerable<IBotLibPlugin> allPlugins)
    {
      // Are we done?
      if (IsCompleted)
      {
        // never stop scouting!
        _IsCompleted = false;
        return true;
      }

      var bot = new EveBot(uiRoot);

      // are we warping or changing session?
      if (bot.IsInSessionChange() || bot.IsDocking())
      {
        // Do nothing
        return true;
      }

      if (bot.IsDocked())
      {
        // do nothing
        return true;
      }

      // if we are not tethered then ensure we are cloaked
      if (!bot.IsCloaked() && !bot.IsTethered())
      {
        var cloak = bot.GetCloakModule();
      }

      if (bot.IsInWarp())
      {
        return true;
      }

      var wormholeBookmarks = bot.GetStandaloneBookmarks()
          .SelectMany(UIParser.GetAllContainedDisplayTextsWithRegion)
          .Where(b => b.Text.StartsWith('#'))
          .ToList();

      if (!string.IsNullOrWhiteSpace(ScoutingBookmarkName) && !wormholeBookmarks.Any(b => b.Text.Contains(ScoutingBookmarkName)))
      {
        // Our Bookmark is missing!
        ScoutingBookmarkName = string.Empty;
      }

      if (string.IsNullOrWhiteSpace(ScoutingBookmarkName))
      {

        // if we're scouting a wormhole
        if (!string.IsNullOrWhiteSpace(ScoutingBookmarkName))
        {
          var ourBookmark = wormholeBookmarks
              .FirstOrDefault(b => b.Text.Contains(ScoutingBookmarkName));

          if (ourBookmark == null)
          {
            // we are scouting a dead wormhole!
            ScoutingBookmarkName = null;
            // start again
            return true;
          }

          var wormhole = bot.GetOverviewEntries()
              .FirstOrDefault(entry =>
                  entry.ObjectType?
                  .Contains("Wormhole", StringComparison.CurrentCultureIgnoreCase) == true
              );
        }
        else
        {
          // we are not currently scouting a wormhole
          var bookmarksBeingScouted = allPlugins
              .OfType<WormholeScout>()
              .Select(p => p.ScoutingBookmarkName)
              .Where(b => !string.IsNullOrWhiteSpace(b));

          // find a bookmark for a hole that isn't being scouted by another character
          var availableBookmark = wormholeBookmarks
              .FirstOrDefault(b => !bookmarksBeingScouted
                  .Contains(b.Text.Split("<t>").FirstOrDefault())
              );

          if (availableBookmark != null && availableBookmark.Text != ScoutingBookmarkName)
          {
            ScoutingBookmarkName = availableBookmark.Text
                .Split("<t>").FirstOrDefault();
            return true;
          }

          // if we get here there are no more holes that need scouts
          ScoutingBookmarkName = null;
        }
      }

      // Nothing to do right now
      return true;
    }
  }
}
