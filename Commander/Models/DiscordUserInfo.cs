namespace Commander.Models;

/// <summary>
/// Represents Discord user information and OAuth credentials
/// </summary>
public record DiscordUserInfo
{
  public required string DiscordUserId { get; init; }
  public required string Username { get; init; }
  public string? Discriminator { get; init; }
  public string? AvatarHash { get; init; }
  public required string AccessToken { get; init; }
  public required string RefreshToken { get; init; }
  public required DateTime TokenExpiry { get; init; }
  public required DateTime LastAuthenticated { get; init; }

  public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
  
  public bool IsTokenExpired => DateTime.UtcNow >= TokenExpiry;
  
  public bool IsTokenExpiringSoon => DateTime.UtcNow.AddMinutes(5) >= TokenExpiry;

  public string GetAvatarUrl()
  {
    if (string.IsNullOrEmpty(AvatarHash))
      return $"https://cdn.discordapp.com/embed/avatars/{int.Parse(Discriminator ?? "0") % 5}.png";
    
    var extension = AvatarHash.StartsWith("a_") ? "gif" : "png";
    return $"https://cdn.discordapp.com/avatars/{DiscordUserId}/{AvatarHash}.{extension}";
  }

  public string GetDisplayName()
  {
    if (string.IsNullOrEmpty(Discriminator) || Discriminator == "0")
      return Username;
    
    return $"{Username}#{Discriminator}";
  }
}
