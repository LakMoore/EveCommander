using Commander.Models;
using BotLib;

namespace Commander.Services;

/// <summary>
/// Global access point for Discord authentication state
/// </summary>
public static class DiscordAuthManager
{
  private static DiscordAuthService? _instance;

  /// <summary>
  /// Initialize the Discord authentication service
  /// </summary>
  public static async Task InitializeAsync()
  {
    _instance = new DiscordAuthService();
    _instance.AuthenticationCompleted += OnAuthenticationCompleted;
    _instance.AuthenticationRevoked += OnAuthenticationRevoked;

    await _instance.InitializeAsync();

    // Update the global context with user ID and access token
    if (_instance.CurrentUser != null)
    {
      DiscordUserContext.SetDiscordAuth(
        _instance.CurrentUser.DiscordUserId,
        _instance.CurrentUser.AccessToken
      );
    }
  }

  private static void OnAuthenticationCompleted(object? sender, DiscordUserInfo user)
  {
    DiscordUserContext.SetDiscordAuth(user.DiscordUserId, user.AccessToken);
  }

  private static void OnAuthenticationRevoked(object? sender, EventArgs e)
  {
    DiscordUserContext.Clear();
  }

  /// <summary>
  /// Get the currently authenticated Discord user
  /// </summary>
  public static DiscordUserInfo? GetCurrentUser()
  {
    return _instance?.CurrentUser;
  }

  /// <summary>
  /// Get the Discord user ID of the currently authenticated user
  /// </summary>
  public static string? GetDiscordUserId()
  {
    return _instance?.CurrentUser?.DiscordUserId;
  }

  /// <summary>
  /// Check if a user is currently authenticated
  /// </summary>
  public static bool IsAuthenticated()
  {
    return _instance?.CurrentUser != null && !_instance.CurrentUser.IsTokenExpired;
  }

  /// <summary>
  /// Get the underlying service instance for advanced operations
  /// </summary>
  internal static DiscordAuthService? GetService()
  {
    return _instance;
  }
}

