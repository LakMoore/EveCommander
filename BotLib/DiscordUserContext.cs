namespace BotLib;

/// <summary>
/// Static storage for Discord authentication - set by Commander, accessed by plugins
/// This uses a simple static field approach since dependency injection isn't available
/// </summary>
public static class DiscordUserContext
{
  private static string? _discordUserId;
  private static string? _accessToken;

  /// <summary>
  /// Set the current Discord user ID (called by Commander when user authenticates)
  /// </summary>
  public static void SetDiscordUserId(string? discordUserId)
  {
    _discordUserId = discordUserId;
  }

  /// <summary>
  /// Set Discord authentication info (user ID and access token)
  /// </summary>
  public static void SetDiscordAuth(string? discordUserId, string? accessToken)
  {
    _discordUserId = discordUserId;
    _accessToken = accessToken;
  }

  /// <summary>
  /// Get the current Discord user ID (called by plugins)
  /// </summary>
  public static string? GetDiscordUserId()
  {
    return _discordUserId;
  }

  /// <summary>
  /// Get the current Discord access token for server authentication
  /// </summary>
  public static string? GetAccessToken()
  {
    return _accessToken;
  }

  /// <summary>
  /// Clear the Discord user ID and token (called when user disconnects)
  /// </summary>
  public static void Clear()
  {
    _discordUserId = null;
    _accessToken = null;
  }
}
