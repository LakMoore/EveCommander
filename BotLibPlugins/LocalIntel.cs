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
    private long lastChangeTime = 0;
    private const long CHANGE_NOTIFICATION_DURATION = 1 * TimeSpan.TicksPerMinute;

    [BotLibSetting(SettingType = BotLibSetting.Type.MultiLineText, Description = "Friendly pilot names (one per line). These will be ignored when reporting changes.")]
    public string? FriendlyPilots;

    [SupportedOSPlatform("windows5.0")]
    public override async Task<PluginResult> DoWork(ParsedUserInterface uiRoot, GameClient gameClient, IEnumerable<IBotLibPlugin> allPlugins)
    {
      if (IsCompleted)
      {
        _IsCompleted = false;
        return true;
      }

      var bot = new EveBot(uiRoot);

      if (bot.IsDisconnected())
      {
        await SendDisconnectedReport(bot.CurrentSystemName());
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

      var locals = bot.GetLocalCharacters().ToList();

      if (!locals.Any())
      {
        return new PluginResult
        {
          WorkDone = true,
          Message = "No Local Chat Found",
          Background = Color.Orange,
          Foreground = Color.White,
        };
      }

      // Get the list of friendly pilots
      var friendlies = GetFriendlyPilotNames();

      // Filter out friendlies (case-insensitive)
      var nonFriendlyPilots = locals
          .Where(pilot => !friendlies.Contains(pilot.Name, StringComparer.OrdinalIgnoreCase))
          .ToList();

      // Get non-friendly pilot names for comparison
      var currentPilotNames = nonFriendlyPilots
          .Select(p => p.Name)
          .OrderBy(name => name)
          .ToList();

      // Check if the pilot list has changed
      bool pilotsChanged = !currentPilotNames.SequenceEqual(previousPilots);

      if (pilotsChanged)
      {
        previousPilots = currentPilotNames;
        lastChangeTime = DateTime.Now.Ticks;

        // Send the report
        await SendLocalReport(nonFriendlyPilots, bot.CurrentSystemName());
      }

      // Build the result message
      var result = new PluginResult
      {
        WorkDone = true,
        Message = $"{nonFriendlyPilots.Count} non-friendly pilot{(nonFriendlyPilots.Count != 1 ? "s" : "")} in Local",
        Background = ThemeColors.Surface,
        Foreground = ThemeColors.Foreground,
      };

      // Visual feedback for recent changes
      long deltaTime = DateTime.Now.Ticks - lastChangeTime;
      if (deltaTime < CHANGE_NOTIFICATION_DURATION)
      {
        byte alpha = (byte)(255f - (255f * (double)deltaTime / CHANGE_NOTIFICATION_DURATION));
        result.Background = Color.FromArgb(alpha, 255, 128, 0);
        result.Foreground = Color.White;
      }

      return result;
    }

    private HashSet<string> GetFriendlyPilotNames()
    {
      if (string.IsNullOrWhiteSpace(FriendlyPilots))
      {
        return [];
      }

      return FriendlyPilots
          .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Where(name => !string.IsNullOrWhiteSpace(name))
          .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SendDisconnectedReport(string systemName)
    {
      var discordToken = DiscordUserContext.GetAccessToken();

      if (string.IsNullOrEmpty(discordToken))
      {
        Debug.WriteLine("[LocalIntel] Cannot send report: Discord token not available. User must authenticate with Discord.");
        return;
      }

      LocalReport message = new()
      {
        ScoutName = this.CharacterName ?? "Unknown Scout",
        System = systemName,
        Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Locals = []
      };

      await SendIfChangedOrOld(message, discordToken);
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

    private async Task SendLocalReport(List<ChatUserEntry> pilots, string systemName)
    {
      var discordToken = DiscordUserContext.GetAccessToken();

      if (string.IsNullOrEmpty(discordToken))
      {
        Debug.WriteLine("[LocalIntel] Cannot send report: Discord token not available. User must authenticate with Discord.");
        return;
      }

      var localPilots = pilots
          .Select(p => new LocalPilot()
          {
            Name = p.Name,
            CharacterID = long.TryParse(p.CharacterID, out var id) ? id : 0,
            StandingHint = p.StandingIconHint ?? string.Empty
          })
          .ToList();

      LocalReport message = new()
      {
        ScoutName = this.CharacterName,
        System = systemName,
        Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Locals = localPilots
      };

      await SendIfChangedOrOld(message, discordToken);
    }
  }
}
