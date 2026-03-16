using BotLib;
using eve_parse_ui;
using GridScout2;
using read_memory_64_bit;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;

namespace BotLibPlugins
{
  internal class LocalIntel(string characterName, long windowID) : IBotLibPlugin(characterName, windowID)
  {
    public override string Name => "Local Intel";

    private List<string> previousPilots = [];
    private List<string> previousGridPilots = [];
    private string? previousStatus = null;
    private string? previousSystemName = null;
    private long lastChangeTime = 0;
    private long lastGridChangeTime = 0;
    private const long CHANGE_NOTIFICATION_DURATION = 1 * TimeSpan.TicksPerMinute;
    private const long GRID_CHANGE_NOTIFICATION_DURATION = 30 * TimeSpan.TicksPerSecond;

    [SupportedOSPlatform("windows5.0")]
    public override async Task<PluginResult> DoWork(ParsedUserInterface uiRoot, GameClient gameClient, IEnumerable<IBotLibPlugin> allPlugins)
    {
      if (IsCompleted)
      {
        _IsCompleted = false;
        return true;
      }

      var bot = new EveBot(uiRoot);

      // Get the system name early while UI is available
      var currentSystemName = bot.CurrentSystemName();

      // Get non-friendly pilots on grid from overview (cache to avoid multiple calls)
      var gridPilots = bot.GetNonFriendlyPilotsOnGrid().ToList();

      // Determine current status using cached grid pilots
      var currentStatus = DetermineStatus(bot, gridPilots);

      if (bot.IsDisconnected())
      {
        await SendReport(new List<ChatUserEntry>(), new List<OverviewWindowEntry>(), currentSystemName, "Disconnected");
        return new PluginResult
        {
          WorkDone = true,
          Message = "Disconnected",
          Background = Color.Red,
          Foreground = Color.White,
        };
      }

      if (bot.IsInSessionChange() || bot.IsDocking())
      {
        return true;
      }

      var local = bot.GetLocalChatWindow();

      if (local == null)
      {
        return new PluginResult
        {
          WorkDone = true,
          Message = "No Local Chat Found",
          Background = Color.Red,
          Foreground = Color.White,
        };
      }

      if (local.Userlist?.VisibleUsers == null || !local.Userlist.VisibleUsers.Any())
      {
        return new PluginResult
        {
          WorkDone = true,
          Message = "Local userlist hidden - expand to monitor",
          Background = Color.Yellow,
          Foreground = Color.Black
        };
      }

      var locals = bot.GetLocalCharacters().ToList();

      // Get grid pilot names for comparison
      var currentGridPilotNames = gridPilots
          .Select(p => p.ObjectName ?? "Unknown")
          .OrderBy(name => name)
          .ToList();

      // Detect grid changes
      bool gridChanged = !currentGridPilotNames.SequenceEqual(previousGridPilots);
      string? gridChangeMessage = null;

      if (gridChanged)
      {
        var pilotsEntered = currentGridPilotNames.Except(previousGridPilots).ToList();
        var pilotsLeft = previousGridPilots.Except(currentGridPilotNames).ToList();

        if (pilotsEntered.Any())
        {
          gridChangeMessage = $"+{pilotsEntered.Count} entered grid";
        }
        if (pilotsLeft.Any())
        {
          gridChangeMessage = gridChangeMessage != null 
            ? $"{gridChangeMessage}, -{pilotsLeft.Count} left grid"
            : $"-{pilotsLeft.Count} left grid";
        }

        previousGridPilots = currentGridPilotNames;
        lastGridChangeTime = DateTime.Now.Ticks;
      }

      // Get non-friendly pilot names for comparison
      var currentPilotNames = locals
          .Select(p => p.Name)
          .OrderBy(name => name)
          .ToList();

      // Check if we've changed systems
      bool systemChanged = currentSystemName != previousSystemName;

      // Check if the pilot list or status has changed
      bool pilotsChanged = !currentPilotNames.SequenceEqual(previousPilots);
      bool statusChanged = currentStatus != previousStatus;

      if (pilotsChanged || statusChanged || gridChanged || systemChanged)
      {
        // Play audio alert if under attack
        if (currentStatus == "UnderAttack")
        {
          PlayAttackAlert();
        }
        else if (currentStatus == "NeutralsOnGrid" && statusChanged)
        {
          PlayNeutralAlert();
        }
        // Play audio alert if new pilots enter local while undocked
        // BUT NOT if we just changed systems (new baseline)
        else if (pilotsChanged && !bot.IsDocked() && !bot.IsDisconnected() && !systemChanged)
        {
          var pilotsEntered = currentPilotNames.Except(previousPilots).ToList();
          if (pilotsEntered.Any())
          {
            PlayLocalChangeAlert();
          }
        }

        // Send the report
        string? sendResult = await SendReport(locals, gridPilots, currentSystemName, currentStatus);
        if (!string.IsNullOrEmpty(sendResult))
        {
          return new PluginResult
          {
            WorkDone = true,
            Message = sendResult,
            Background = Color.Red,
            Foreground = Color.White,
          };
        }

        previousPilots = currentPilotNames;
        previousStatus = currentStatus;
        previousSystemName = currentSystemName;
        lastChangeTime = DateTime.Now.Ticks;
      }

      // Build the result message
      var message = $"{currentStatus}: {locals.Count - 1} pilot{((locals.Count - 1) != 1 ? "s" : "")} in Local";

      // Add grid change info if recent
      long gridDeltaTime = DateTime.Now.Ticks - lastGridChangeTime;
      if (gridDeltaTime < GRID_CHANGE_NOTIFICATION_DURATION && gridChangeMessage != null)
      {
        message = $"{message} | {gridChangeMessage}";
      }

      var result = new PluginResult
      {
        WorkDone = true,
        Message = message,
        Background = currentStatus switch
        {
          "UnderAttack" => Color.Red,
          "NeutralsOnGrid" => Color.Orange,
          _ => ThemeColors.Surface
        },
        Foreground = currentStatus == "UnderAttack" || currentStatus == "NeutralsOnGrid" 
          ? Color.White 
          : ThemeColors.Foreground,
      };

      // Visual feedback for recent changes (local or grid)
      long deltaTime = DateTime.Now.Ticks - lastChangeTime;
      long minDelta = Math.Min(deltaTime, gridDeltaTime);

      if (minDelta < CHANGE_NOTIFICATION_DURATION)
      {
        byte alpha = (byte)(255f - (255f * (double)minDelta / CHANGE_NOTIFICATION_DURATION));

        // Grid changes show as purple flash, local changes as orange
        if (gridDeltaTime < GRID_CHANGE_NOTIFICATION_DURATION)
        {
          result.Background = Color.FromArgb(alpha, 128, 0, 255); // Purple for grid changes
        }
        else
        {
          result.Background = Color.FromArgb(alpha, 255, 128, 0); // Orange for local changes
        }
        result.Foreground = Color.White;
      }

      return result;
    }

    private string DetermineStatus(EveBot bot, List<OverviewWindowEntry> gridPilots)
    {
      // Check for disconnected
      if (bot.IsDisconnected())
      {
        return "Disconnected";
      }

      // Check for docked
      if (bot.IsDocked())
      {
        return "Docked";
      }

      // Check if being red-boxed (actively attacked)
      if (gridPilots.Any(p => p.CommonIndications.IsAttackingMe))
      {
        return "UnderAttack";
      }

      // Check for neutrals on grid
      if (gridPilots.Any())
      {
        return "NeutralsOnGrid";
      }

      // Default to undocked
      return "Undocked";
    }

    private async Task SendDisconnectedReport(string systemName)
    {
      await SendReport(new List<ChatUserEntry>(), new List<OverviewWindowEntry>(), systemName, "Disconnected");
    }

    private LocalReport? lastReportMessage;
    private long lastReportTime;
    private const long KEEP_ALIVE_INTERVAL = 5 * TimeSpan.TicksPerMinute;

    private async Task SendIfChangedOrOld(LocalReport message, string discordToken)
    {
      if (!message.MyEquals(lastReportMessage) 
          || lastReportTime < DateTime.Now.Ticks - KEEP_ALIVE_INTERVAL)
      {
        await Server.SendLocalReport(message, discordToken);
        lastReportMessage = message;
        lastReportTime = DateTime.Now.Ticks;
      }
    }

    private async Task<string?> SendReport(
      List<ChatUserEntry> pilots, 
      List<OverviewWindowEntry> gridPilots,
      string systemName, 
      string status)
    {
      var discordToken = DiscordUserContext.GetAccessToken();

      if (string.IsNullOrEmpty(discordToken))
      {
        return "[LocalIntel] Cannot send report. Must authenticate with Discord.";
      }

      var localPilots = pilots
          .Select(p => new LocalPilot()
          {
            Name = p.Name,
            CharacterID = p.CharacterID ?? 0,
            StandingHint = p.StandingIconHint ?? string.Empty,
            StandingIconId = p.StandingIconId
          })
          .ToList();

      var onGridPilots = gridPilots
          .Select(entry => new GridPilot()
          {
            PilotName = entry.ObjectName ?? "Unknown",
            ShipType = entry.ObjectType ?? "Unknown",
            ShipTypeId = entry.TypeID,
            StandingHint = entry.FlagStateHint ?? string.Empty,
            StandingIconId = null, // OverviewWindowEntry doesn't have StandingIconId
            Action = DetermineAction(entry),
            Distance = entry.ObjectDistance,
            DistanceMeters = entry.ObjectDistanceInMeters,
            Corporation = entry.ObjectCorporation,
            Alliance = entry.ObjectAlliance
          })
          .ToList();

      LocalReport message = new()
      {
        ScoutName = this.CharacterName,
        System = systemName,
        Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Locals = localPilots,
        Status = status,
        OnGrid = onGridPilots
      };

      await SendIfChangedOrOld(message, discordToken);
      return null;
    }

    private string DetermineAction(OverviewWindowEntry entry)
    {
      var actions = new List<string>();

      // Priority: Red-box (attacking) is most critical
      if (entry.CommonIndications.IsAttackingMe)
      {
        actions.Add("Red-Boxing");
      }
      else if (entry.CommonIndications.IsTargetingMe)
      {
        actions.Add("Yellow-Boxing");
      }

      if (entry.CommonIndications.TargetedByMe)
      {
        actions.Add("Targeted");
      }

      if (entry.CommonIndications.Targeting)
      {
        actions.Add("Targeting");
      }

      if (entry.CommonIndications.IsJammingMe)
      {
        actions.Add("Jamming");
      }

      if (entry.CommonIndications.IsWarpDisruptingMe)
      {
        actions.Add("Pointing");
      }

      if (entry.NamesUnderSpaceObjectIcon?.Contains("myActiveTarget") == true)
      {
        actions.Add("ActiveTarget");
      }

      return actions.Any() ? string.Join(", ", actions) : "None";
    }

    private void PlayAttackAlert()
    {
      // Critical alert: Three rapid beeps
      Task.Run(() =>
      {
        Console.Beep(1000, 200);
        Thread.Sleep(100);
        Console.Beep(1000, 200);
        Thread.Sleep(100);
        Console.Beep(1000, 200);
      });
    }

    private void PlayNeutralAlert()
    {
      // Warning alert: Single beep
      Task.Run(() =>
      {
        Console.Beep(800, 300);
      });
    }

    private void PlayLocalChangeAlert()
    {
      // Local change alert: Two quick beeps
      Task.Run(() =>
      {
        Console.Beep(600, 150);
        Thread.Sleep(100);
        Console.Beep(600, 150);
      });
    }
  }
}
