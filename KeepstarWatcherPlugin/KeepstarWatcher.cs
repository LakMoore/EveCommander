using BotLib;
using eve_parse_ui;
using read_memory_64_bit;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;

namespace BotLibPlugins
{

  record OverviewEntry
  {
    public required string Type { get; init; }
    public required string Name { get; init; }
  }

  internal class KeepstarWatcher(string characterName, long windowID) : IBotLibPlugin(characterName, windowID)
  {
    public override string Name => "Keepstar Watcher";

    private static readonly HttpClient SharedHttpClient = new();

    [BotLibSetting(
      SettingType = BotLibSetting.Type.MultiLineText, 
      Description = """
        List of ship types to watch for.
        Use one entry per line or comma separators.
        A message will be sent to Discord when a new ship of any of these types is spotted on grid.
        Partial matches are fine, spelling mistakes are not!
        """
    )]
    public readonly string ShipTypesToWatch = "";
    private readonly static char[] DELIMITERS = ['\r', '\n', ','];

    private readonly HashSet<OverviewEntry> previousGrid = [];
    private bool decloakedWarningSent = false;
    private bool disconnectWarningSent = false;

    private long lastMessageTime = 0;
    private const long GRID_CHANGE_NOTIFICATION_DURATION = 1 * TimeSpan.TicksPerMinute; // 1 minutes in ticks

    [BotLibSetting(
      SettingType = BotLibSetting.Type.SingleLineText, 
      Description = "Get a webhook URL from Discord for the channel where you want alerts to appear."
    )]
    public string? DiscordWebhookUrl;

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

      HashSet<string> ShipsToWatchSet = [.. ShipTypesToWatch.Trim().Split(DELIMITERS, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .Where(s => !string.IsNullOrEmpty(s))];

      if (ShipsToWatchSet.Count == 0)
      {
        return new PluginResult
        {
          WorkDone = true,
          Message = "No ship types configured to watch for. Add some in settings.",
          Background = Color.Red,
          Foreground = Color.White,
        };
      }

      var bot = new EveBot(uiRoot);

      if (bot.IsDisconnected())
      {
        if (!disconnectWarningSent)
        {
          await SendDisconnectedWarning(bot.CurrentSystemName());
          disconnectWarningSent = true;
        }
        return new PluginResult
        {
          WorkDone = true,
          Message = "Disconnected",
          Background = Color.Red,
          Foreground = Color.Black,
        };
      }
      else
      {
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

        if (bot.IsInWarp())
        {
          return true;
        }

        if (!bot.GetAllOverviewWindows().Any())
        {
          return new PluginResult
          {
            WorkDone = true,
            Message = "No Overview Found",
            Background = Color.Red,
            Foreground = Color.Black,
          };
        }
        else
        {
          if (!bot.IsCloaked())
          {
            if (!decloakedWarningSent)
            {
              await SendDecloakedWarning(bot.CurrentSystemName());
              decloakedWarningSent = true;
              return new PluginResult
              {
                WorkDone = true,
                Message = "Decloaked!!!",
                Background = Color.Red,
                Foreground = Color.Black,
              };
            }
          }
          else
          {
            // Show "Cloak Reengaged" message if we previously sent a warning
            if (decloakedWarningSent)
            {
              decloakedWarningSent = false;
              return new PluginResult
              {
                WorkDone = true,
                Message = "Cloak Reengaged",
                Background = Color.Transparent,
                Foreground = Color.Black,
              };
            }
          }

          var currentGrid = bot.GetUniqueOverviewEntriesByNameAndType()
            .Select(overviews => new OverviewEntry
            {
              Type = overviews.ObjectType ?? string.Empty,
              Name = overviews.ObjectName ?? string.Empty
            })
            .ToHashSet();

          // Check for ships of interest (both new ships and initial grid scan)
          var newShipsOfInterest = currentGrid
            .Except(previousGrid)
            .Where(cg => ShipsToWatchSet.Any(stw => cg.Type.Contains(stw)))
            .ToList();

          if (newShipsOfInterest.Count != 0)
          {
            await SendNewShips(newShipsOfInterest, bot.CurrentSystemName());
            lastMessageTime = DateTime.Now.Ticks;

            // Update previous grid
            previousGrid.Clear();
            previousGrid.UnionWith(currentGrid);

            return new PluginResult
            {
              WorkDone = true,
              Message = $"{newShipsOfInterest.Count} new ship{ (newShipsOfInterest.Count == 1 ? "" : "s") } of interest on grid",
              Background = Color.Orange,
              Foreground = Color.White,
            };
          }

          // Update previous grid
          previousGrid.Clear();
          previousGrid.UnionWith(currentGrid);

          long deltaTime = DateTime.Now.Ticks - lastMessageTime;
          if (deltaTime < GRID_CHANGE_NOTIFICATION_DURATION)
          {
            // Lerp the colour from orange to transparent over time
            byte alpha = (byte)(255f - (255f * (double)deltaTime / GRID_CHANGE_NOTIFICATION_DURATION));
            return new PluginResult
            {
              WorkDone = true,
              Message = string.Empty,
              Background = Color.FromArgb(alpha, 255, 128, 0),
              Foreground = Color.White,
            };
          }

          return new PluginResult
          {
            WorkDone = true,
            Message = "Grid clear",
            Background = Color.Transparent,
            Foreground = Color.White
          };
        }

        // Nothing to do right now
      }
    }

    private async Task SendNewShips(List<OverviewEntry> newShipsOfInterest, string systemName)
    {
      var shipList = string.Join("\n, ", newShipsOfInterest.Select(s => $"{s.Type} [{s.Name}]"));
      var message = $"{this.CharacterName} has seen the following ships in {systemName}:\\n{shipList}";

      await SendDiscordMessage(message);
    }

    private async Task SendDecloakedWarning(string systemName)
    {
      var message = $"{this.CharacterName} is not cloaked in system {systemName}!";

      await SendDiscordMessage(message);
    }

    private async Task SendDisconnectedWarning(string systemName)
    {
      var message = $"{this.CharacterName} has lost connection in system {systemName}!";

      await SendDiscordMessage(message);
    }

    private async Task SendDiscordMessage(string message)
    {
      try
      {
        // post a message to discord
        var content = new StringContent($"{{\"content\":\"{message}\"}}", System.Text.Encoding.UTF8, "application/json");
        var response = await SharedHttpClient.PostAsync(DiscordWebhookUrl, content);
        if (response != null)
        {
          Debug.WriteLine($"Sent '{message}' for {this.CharacterName}");
          Debug.WriteLine(await response.Content.ReadAsStringAsync());
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Error sending Discord message: {ex.Message}");
      }
    }
  }
}
