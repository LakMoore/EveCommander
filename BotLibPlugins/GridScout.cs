using BotLib;
using eve_parse_ui;
using GridScout2;
using read_memory_64_bit;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;

namespace BotLibPlugins
{

  internal class GridScout(string characterName, long windowID) : IBotLibPlugin(characterName, windowID)
  {
    public override string Name => "Grid Scout";

    private int lastPilotCount = -1;
    private long lastPilotCountChangeTime = 0;
    private const long GRID_CHANGE_NOTIFICATION_DURATION = 1 * TimeSpan.TicksPerMinute; // 1 minutes in ticks

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

      var gridscoutOverview = bot.GetAllOverviewWindows()
          .FirstOrDefault(ow => ow.OverviewTabName.Equals("gridscout", StringComparison.CurrentCultureIgnoreCase));

      if (gridscoutOverview == null)
      {
        return new PluginResult
        {
          WorkDone = true,
          Message = "No GridScout Overview Found",
          Background = Color.Red,
          Foreground = Color.White,
        };
      }
      else
      {
        if (bot.IsDisconnected())
        {
          await SendDisconnectedReport(bot.CurrentSystemName());
        }
        else
        {
          PluginResult result = new()
          {
            WorkDone = true,
            Message = string.Empty,
            Background = ThemeColors.Surface,
            Foreground = ThemeColors.Foreground,
          };

          var wormholeCode = gridscoutOverview.Entries
              .Where(e =>
                  e.ObjectType?.StartsWith("Wormhole ", StringComparison.CurrentCultureIgnoreCase) == true
              )
              .Select(wormhole => wormhole?.ObjectName?.Substring(9))
              .SingleOrDefault("")!;

          wormholeCode = "TEST";

          if (string.IsNullOrEmpty(wormholeCode))
          {
            result.Message += "No Wormhole";
          }
          else
          {
            result.Message += wormholeCode;
          }

          if (!bot.IsCloaked())
          {
            result.Message += "NOT Cloaked!";
          }

          var pilotCount = gridscoutOverview.Entries
              .Where(e =>
                  e.ObjectType?.StartsWith("Wormhole ", StringComparison.CurrentCultureIgnoreCase) != true
              )
              .Count();

          if (pilotCount == 0)
          {
            result.Message += "No pilots on grid";
          }
          else
          {
            result.Message += $"{pilotCount} pilot{(pilotCount > 1 ? "s" : "")} on grid";
          }

          if (pilotCount != lastPilotCount)
          {
            lastPilotCount = pilotCount;
            lastPilotCountChangeTime = DateTime.Now.Ticks;
          }

          // send the report
          await SendReport(gridscoutOverview, bot.CurrentSystemName(), wormholeCode);

          long deltaTime = DateTime.Now.Ticks - lastPilotCountChangeTime;
          if (deltaTime < GRID_CHANGE_NOTIFICATION_DURATION)
          {
            // Lerp the colour from orange to transparent over time
            byte alpha = (byte)(255f - (255f * (double)deltaTime / GRID_CHANGE_NOTIFICATION_DURATION));
            result.Background = Color.FromArgb(alpha, ThemeColors.Surface.R, ThemeColors.Surface.G, ThemeColors.Surface.B);
          }


          return result;
        }

        // Nothing to do right now
        return true;
      }
    }

    private async Task SendDisconnectedReport(string systemName)
    {
      var discordToken = DiscordUserContext.GetAccessToken();

      // Validate DiscordToken is available
      if (string.IsNullOrEmpty(discordToken))
      {
        Debug.WriteLine("[GridScout] Cannot send report: Discord token not available. User must authenticate with Discord.");
        return;
      }

      GridScout2.ScoutMessage message = new()
      {
        Message = "",
        Scout = this.CharacterName ?? "No Name",
        System = systemName,
        Wormhole = "Lost Connection",
        Entries = [],
        Disconnected = true,
        Version = "CommanderGridScout" // MainWindow.Version
      };

      await SendIfChangedOrOld(message, discordToken);
    }

    private GridScout2.ScoutMessage? lastReportMessage;
    private long lastReportTime;
    private const long KEEP_ALIVE_INTERVAL = 5 * TimeSpan.TicksPerMinute; // 5 minutes in ticks
    private async Task SendIfChangedOrOld(GridScout2.ScoutMessage message, string discordToken)
    {
      // if the message has changed or it's been a while, send it
      if (
          !message.MyEquals(lastReportMessage)
          || lastReportTime < DateTime.Now.Ticks - KEEP_ALIVE_INTERVAL
      )
      {
        await GridScout2.Server.SendReport(message, discordToken);

        lastReportMessage = message;
        lastReportTime = DateTime.Now.Ticks;
      }
    }

    private async Task SendReport(OverviewWindow gridscoutOverview, string systemName, string wormhole)
    {
      var discordToken = DiscordUserContext.GetAccessToken();

      // Validate DiscordToken is available
      if (string.IsNullOrEmpty(discordToken))
      {
        Debug.WriteLine("[GridScout] Cannot send report: Discord token not available. User must authenticate with Discord.");
        return;
      }

      var text = gridscoutOverview.Entries
          //.Where(e => e.ObjectType?.StartsWith("Wormhole ", StringComparison.CurrentCultureIgnoreCase) != true)
          .Select(e => e.ObjectType + " " + e.ObjectCorporation + " " + e.ObjectAlliance + " " + e.ObjectName)
          .DefaultIfEmpty(string.Empty)
          .Aggregate((a, b) => a + "\n" + b);

      var entries = gridscoutOverview.Entries
          .Select(e => new ScoutEntry()
          {
            Type = e.ObjectType,
            Corporation = e.ObjectCorporation,
            Alliance = e.ObjectAlliance,
            Name = e.ObjectName,
            Distance = (e.ObjectDistanceInMeters ?? 0).ToString(),
            Velocity = (e.ObjectVelocity ?? 0).ToString()
          })
          .ToList();

      ScoutMessage message = new()
      {
        Message = text,
        Scout = this.CharacterName,
        System = systemName,
        Wormhole = wormhole,
        Entries = entries,
        Disconnected = false,
        Version = "CommanderGridScout" // MainWindow.Version
      };

      await SendIfChangedOrOld(message, discordToken);
    }

  }
}
