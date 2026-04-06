using BotLib;
using eve_parse_ui;
using read_memory_64_bit;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private const int DISCORD_MAX_CONTENT_LENGTH = 2000;
    private const int DISCORD_MAX_SEND_ATTEMPTS = 4;
    private static readonly JsonSerializerOptions DISCORD_JSON_OPTIONS = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

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

    [BotLibSetting(
      SettingType = BotLibSetting.Type.MultiLineText,
      Description = """
        List of structure types to monitor.
        Use one entry per line or comma separators.
        A notification will be sent to Discord when a structure from this list becomes unanchored.
        """
    )]
    public readonly string TargetStructureTypes = "";

    private readonly static char[] DELIMITERS = ['\r', '\n', ','];

    private readonly HashSet<OverviewEntry> previousGrid = [];
    private readonly Dictionary<string, long> structureLastNotificationTimes = [];
    private bool decloakedWarningSent = false;
    private bool disconnectWarningSent = false;

    private long lastMessageTime = 0;
    private const long GRID_CHANGE_NOTIFICATION_DURATION = 1 * TimeSpan.TicksPerMinute; // 1 minutes in ticks
    private const long STRUCTURE_NOTIFICATION_INTERVAL = 2 * TimeSpan.TicksPerMinute; // 2 minutes in ticks

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

      HashSet<string> StructuresToWatchSet = [.. TargetStructureTypes.Trim().Split(DELIMITERS, StringSplitOptions.RemoveEmptyEntries)
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

      if (string.IsNullOrWhiteSpace(DiscordWebhookUrl))
      {
        return new PluginResult
          {
            WorkDone = true,
            Message = "No Discord webhook URL configured. Add one in settings.",
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
        disconnectWarningSent = false; // Reset disconnect warning if we are connected

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

          // Check for structures where name matches type exactly (unanchored/vulnerable)
          var matchedStructures = new List<OverviewEntry>();
          if (StructuresToWatchSet.Count > 0)
          {
            matchedStructures = currentGrid
              .Where(entry => StructuresToWatchSet.Any(structType => entry.Type.Contains(structType)))
              .Where(entry => entry.Name == entry.Type)
              .ToList();

            // Send notifications for matched structures (new or repeat after interval)
            var now = DateTime.Now.Ticks;
            var structuresNeedingNotification = matchedStructures
              .Where(structure => 
              {
                var key = $"{structure.Type}|{structure.Name}";
                if (!structureLastNotificationTimes.TryGetValue(key, out long lastTime))
                {
                  return true; // New structure
                }
                return (now - lastTime) >= STRUCTURE_NOTIFICATION_INTERVAL; // Time for repeat
              })
              .ToList();

            if (structuresNeedingNotification.Count > 0)
            {
              await SendStructureAlert(structuresNeedingNotification, bot.CurrentSystemName());

              // Update last notification times
              foreach (var structure in structuresNeedingNotification)
              {
                var key = $"{structure.Type}|{structure.Name}";
                structureLastNotificationTimes[key] = now;
              }
            }

            // Clean up notification times for structures no longer on grid
            var currentStructureKeys = matchedStructures
              .Select(s => $"{s.Type}|{s.Name}")
              .ToHashSet();
            var keysToRemove = structureLastNotificationTimes.Keys
              .Where(key => !currentStructureKeys.Contains(key))
              .ToList();

            // Send "scooped" notification for structures that left the grid
            if (keysToRemove.Count > 0)
            {
              var scoopedStructures = keysToRemove
                .Select(key => key.Split('|')[0]) // Extract structure type from key
                .ToList();
              await SendStructureScoopedAlert(scoopedStructures, bot.CurrentSystemName());
            }

            foreach (var key in keysToRemove)
            {
              structureLastNotificationTimes.Remove(key);
            }
          }

          // Check for ships of interest (both new ships and initial grid scan)
          var newShipsOfInterest = currentGrid
            .Except(previousGrid)
            .Where(cg => ShipsToWatchSet.Any(stw => cg.Type.Contains(stw)))
            .ToHashSet();

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

          // If we have matched structures, show that in the status
          if (matchedStructures.Count > 0)
          {
            // Update previous grid
            previousGrid.Clear();
            previousGrid.UnionWith(currentGrid);

            return new PluginResult
            {
              WorkDone = true,
              Message = $"{matchedStructures.Count} unanchored structure{ (matchedStructures.Count == 1 ? "" : "s") } on grid",
              Background = Color.Red,
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

    private async Task SendNewShips(HashSet<OverviewEntry> newShipsOfInterest, string systemName)
    {
      var shipList = string.Join("\n", newShipsOfInterest.Select(s => $"{s.Type} [{s.Name}]"));
      var message = $"{this.CharacterName} has seen the following ships in {systemName}:\n{shipList}";

      await SendDiscordMessage(message);
    }

    private async Task SendStructureAlert(List<OverviewEntry> structures, string systemName)
    {
      var structureList = string.Join("\n", structures.Select(s => s.Type));
      var message = $"⚠️ {this.CharacterName} reports vulnerable structure(s) in {systemName}:\n{structureList}";

      await SendDiscordMessage(message);
    }

    private async Task SendStructureScoopedAlert(List<string> structureTypes, string systemName)
    {
      var structureList = string.Join("\n", structureTypes);
      var message = $"✅ {this.CharacterName} reports structure(s) scooped in {systemName}:\n{structureList}";

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
      if (string.IsNullOrWhiteSpace(DiscordWebhookUrl))
      {
        LogWarning("Discord webhook URL is missing.");
        return;
      }

      if (!Uri.TryCreate(DiscordWebhookUrl.Trim(), UriKind.Absolute, out var webhookUri))
      {
        LogWarning($"Discord webhook URL is invalid: '{DiscordWebhookUrl}'.");
        return;
      }

      foreach (var messageChunk in SplitDiscordMessage(message))
      {
        await SendDiscordMessageChunk(webhookUri, messageChunk);
      }
    }

    private async Task SendDiscordMessageChunk(Uri webhookUri, string message)
    {
      var payload = JsonSerializer.Serialize(
        new DiscordWebhookPayload(message, new DiscordAllowedMentions([])),
        DISCORD_JSON_OPTIONS);

      for (var attempt = 1; attempt <= DISCORD_MAX_SEND_ATTEMPTS; attempt++)
      {
        try
        {
          using var request = new HttpRequestMessage(HttpMethod.Post, webhookUri)
          {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
          };

          using var response = await SharedHttpClient.SendAsync(request);
          var responseBody = await response.Content.ReadAsStringAsync();

          if (response.IsSuccessStatusCode)
          {
            LogDebug($"Successfully sent Discord webhook chunk '{message}' ({message.Length} chars).");
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
              LogDebug($"Discord webhook response: {responseBody}");
            }
            return;
          }

          if (!ShouldRetry(response.StatusCode, attempt))
          {
            LogError($"Discord webhook failed with status {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}");
            return;
          }

          var delay = GetRetryDelay(response.Headers, responseBody, attempt);
          LogWarning($"Discord webhook retry {attempt}/{DISCORD_MAX_SEND_ATTEMPTS} after status {(int)response.StatusCode} ({response.StatusCode}). Waiting {delay.TotalSeconds:F1}s. Response: {responseBody}");
          await Task.Delay(delay);
        }
        catch (HttpRequestException ex) when (attempt < DISCORD_MAX_SEND_ATTEMPTS)
        {
          var delay = GetExponentialBackoff(attempt);
          LogWarning($"Discord webhook network error: {ex.Message}. Retrying in {delay.TotalSeconds:F1}s.");
          await Task.Delay(delay);
        }
        catch (TaskCanceledException ex) when (attempt < DISCORD_MAX_SEND_ATTEMPTS)
        {
          var delay = GetExponentialBackoff(attempt);
          LogWarning($"Discord webhook timeout: {ex.Message}. Retrying in {delay.TotalSeconds:F1}s.");
          await Task.Delay(delay);
        }
        catch (HttpRequestException ex)
        {
          LogError("Discord webhook network error.", ex);
          return;
        }
        catch (TaskCanceledException ex)
        {
          LogError("Discord webhook timeout.", ex);
          return;
        }
      }
    }

    private static HttpClient CreateHttpClient()
    {
      var client = new HttpClient
      {
        Timeout = TimeSpan.FromSeconds(15)
      };

      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      client.DefaultRequestHeaders.UserAgent.ParseAdd("EveCommander-KeepstarWatcher/1.0");

      return client;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode, int attempt)
    {
      return attempt < DISCORD_MAX_SEND_ATTEMPTS
        && (statusCode == HttpStatusCode.TooManyRequests
          || statusCode == HttpStatusCode.RequestTimeout
          || (int)statusCode >= 500);
    }

    private static TimeSpan GetRetryDelay(HttpResponseHeaders headers, string responseBody, int attempt)
    {
      var retryAfter = TryGetRetryDelay(headers, responseBody);
      return retryAfter ?? GetExponentialBackoff(attempt);
    }

    private static TimeSpan? TryGetRetryDelay(HttpResponseHeaders headers, string responseBody)
    {
      if (headers.RetryAfter?.Delta is TimeSpan retryAfterDelta && retryAfterDelta > TimeSpan.Zero)
      {
        return retryAfterDelta + GetJitter();
      }

      if (headers.TryGetValues("X-RateLimit-Reset-After", out var rateLimitValues))
      {
        var rateLimitValue = rateLimitValues.FirstOrDefault();
        if (double.TryParse(rateLimitValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var resetAfterSeconds)
          && resetAfterSeconds > 0)
        {
          return TimeSpan.FromSeconds(resetAfterSeconds) + GetJitter();
        }
      }

      if (!string.IsNullOrWhiteSpace(responseBody))
      {
        try
        {
          var rateLimitResponse = JsonSerializer.Deserialize<DiscordRateLimitResponse>(responseBody, DISCORD_JSON_OPTIONS);
          if (rateLimitResponse?.RetryAfter > 0)
          {
            return TimeSpan.FromSeconds(rateLimitResponse.RetryAfter) + GetJitter();
          }
        }
        catch (JsonException)
        {
        }
      }

      return null;
    }

    private static TimeSpan GetExponentialBackoff(int attempt)
    {
      var seconds = Math.Min(Math.Pow(2, attempt - 1), 10);
      return TimeSpan.FromSeconds(seconds) + GetJitter();
    }

    private static TimeSpan GetJitter()
    {
      return TimeSpan.FromMilliseconds(Random.Shared.Next(150, 500));
    }

    private static IEnumerable<string> SplitDiscordMessage(string message)
    {
      var normalizedMessage = message.Replace("\r\n", "\n");
      if (normalizedMessage.Length <= DISCORD_MAX_CONTENT_LENGTH)
      {
        yield return normalizedMessage;
        yield break;
      }

      var currentChunk = new StringBuilder();
      foreach (var line in normalizedMessage.Split('\n'))
      {
        if (currentChunk.Length == 0)
        {
          foreach (var chunk in SplitLine(line))
          {
            if (chunk.Length == DISCORD_MAX_CONTENT_LENGTH)
            {
              yield return chunk;
            }
            else
            {
              currentChunk.Append(chunk);
            }
          }
          continue;
        }

        if (currentChunk.Length + 1 + line.Length <= DISCORD_MAX_CONTENT_LENGTH)
        {
          currentChunk.Append('\n').Append(line);
          continue;
        }

        yield return currentChunk.ToString();
        currentChunk.Clear();

        foreach (var chunk in SplitLine(line))
        {
          if (chunk.Length == DISCORD_MAX_CONTENT_LENGTH)
          {
            yield return chunk;
          }
          else
          {
            currentChunk.Append(chunk);
          }
        }
      }

      if (currentChunk.Length > 0)
      {
        yield return currentChunk.ToString();
      }
    }

    private static IEnumerable<string> SplitLine(string line)
    {
      if (line.Length <= DISCORD_MAX_CONTENT_LENGTH)
      {
        yield return line;
        yield break;
      }

      for (var index = 0; index < line.Length; index += DISCORD_MAX_CONTENT_LENGTH)
      {
        var length = Math.Min(DISCORD_MAX_CONTENT_LENGTH, line.Length - index);
        yield return line.Substring(index, length);
      }
    }

    private sealed record DiscordWebhookPayload(
      [property: JsonPropertyName("content")] string Content,
      [property: JsonPropertyName("allowed_mentions")] DiscordAllowedMentions AllowedMentions);

    private sealed record DiscordAllowedMentions(
      [property: JsonPropertyName("parse")] string[] Parse);

    private sealed record DiscordRateLimitResponse(
      [property: JsonPropertyName("retry_after")] double RetryAfter);
  }
}
