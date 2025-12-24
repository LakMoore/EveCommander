using BotLib;
using eve_parse_ui;
using read_memory_64_bit;
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

    // list of ship types to watch for
    private readonly List<string> ShipTypesToWatch =
    [
      "Providence",
      "Charon",
      "Obelisk",
      "Fenrir",
      "Avalanche",
      "Mobile Observatory"
    ];

    private readonly List<OverviewEntry> previousGrid = [];
    private bool gridInitialized = false;
    private bool decloakedWarningSent = false;
    private bool disconnectWarningSent = false;

    private int lastPilotCount = -1;
    private long lastMessageTime = 0;
    private const long GRID_CHANGE_NOTIFICATION_DURATION = 1 * TimeSpan.TicksPerMinute; // 1 minutes in ticks

    [PluginSettingKey]
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

        // if we are not tethered then ensure we are cloaked
        if (!bot.IsCloaked() && !bot.IsTethered())
        {
          var cloak = bot.GetCloakModule();
        }

        if (bot.IsInWarp())
        {
          return true;
        }

        var overviews = bot.GetAllOverviewWindows();

        if (!overviews.Any())
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
            decloakedWarningSent = false;
          }

          var currentGrid = overviews
            .SelectMany(o => o.Entries)
            .Select(overviews => new OverviewEntry
            {
              Type = overviews.ObjectType ?? string.Empty,
              Name = overviews.ObjectName ?? string.Empty
            })
            .ToList();

          if (!gridInitialized)
          {
            currentGrid.ForEach(entry => previousGrid.Add(entry));
            gridInitialized = true;
          } 
          else
          {
            // compare current grid to previous grid
            var newShipsOfInterest = currentGrid
              .Where(cg => !previousGrid.Any(pg => pg.Type == cg.Type && pg.Name == cg.Name))
              .Where(cg => ShipTypesToWatch.Any(stw => cg.Type.Contains(stw)))
              .ToList();

            if (newShipsOfInterest.Count != 0)
            {
              await SendNewShips(newShipsOfInterest, bot.CurrentSystemName());
              lastMessageTime = DateTime.Now.Ticks;
              return new PluginResult
              {
                WorkDone = true,
                Message = $"{newShipsOfInterest.Count} new ship{ (newShipsOfInterest.Count == 1 ? "" : "s") } of interest on grid",
                Background = Color.Orange,
                Foreground = Color.White,
              };
            }
          }

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

          return true;
        }

        // Nothing to do right now
        return true;
      }
    }

    private async Task SendNewShips(List<OverviewEntry> newShipsOfInterest, string systemName)
    {
      var shipList = string.Join("\n, ", newShipsOfInterest.Select(s => $"{s.Type} [{s.Name}]"));
      var message = $"{this.CharacterName} has seen the following ships in {systemName}:\n{shipList}";

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
          Console.WriteLine($"Sent '{message}' for {this.CharacterName}");
          Console.WriteLine(await response.Content.ReadAsStringAsync());
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error sending Discord message: {ex.Message}");
      }
    }
  }
}
