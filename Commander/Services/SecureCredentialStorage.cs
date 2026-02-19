using Commander.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Commander.Services;

/// <summary>
/// Handles secure storage and retrieval of Discord OAuth credentials using Windows DPAPI
/// </summary>
public static class SecureCredentialStorage
{
  private static readonly string _appDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Commander"
  );

  private static readonly string _credentialFilePath = Path.Combine(_appDataFolder, "discord_auth.dat");

  /// <summary>
  /// Save Discord user info with encrypted tokens
  /// </summary>
  public static async Task SaveCredentialsAsync(DiscordUserInfo userInfo)
  {
    try
    {
      Directory.CreateDirectory(_appDataFolder);

      var data = new DiscordCredentialData
      {
        DiscordUserId = userInfo.DiscordUserId,
        Username = userInfo.Username,
        Discriminator = userInfo.Discriminator,
        AvatarHash = userInfo.AvatarHash,
        EncryptedAccessToken = EncryptString(userInfo.AccessToken),
        EncryptedRefreshToken = EncryptString(userInfo.RefreshToken),
        TokenExpiry = userInfo.TokenExpiry,
        LastAuthenticated = userInfo.LastAuthenticated
      };

      var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
      await File.WriteAllTextAsync(_credentialFilePath, json);
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException("Failed to save Discord credentials", ex);
    }
  }

  /// <summary>
  /// Load Discord user info with decrypted tokens
  /// </summary>
  public static async Task<DiscordUserInfo?> LoadCredentialsAsync()
  {
    try
    {
      if (!File.Exists(_credentialFilePath))
        return null;

      var json = await File.ReadAllTextAsync(_credentialFilePath);
      var data = JsonSerializer.Deserialize<DiscordCredentialData>(json);

      if (data == null)
        return null;

      return new DiscordUserInfo
      {
        DiscordUserId = data.DiscordUserId,
        Username = data.Username,
        Discriminator = data.Discriminator,
        AvatarHash = data.AvatarHash,
        AccessToken = DecryptString(data.EncryptedAccessToken),
        RefreshToken = DecryptString(data.EncryptedRefreshToken),
        TokenExpiry = data.TokenExpiry,
        LastAuthenticated = data.LastAuthenticated
      };
    }
    catch (CryptographicException)
    {
      // Token was encrypted on different machine/user - delete invalid file
      DeleteCredentials();
      return null;
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException("Failed to load Discord credentials", ex);
    }
  }

  /// <summary>
  /// Delete stored credentials
  /// </summary>
  public static void DeleteCredentials()
  {
    try
    {
      if (File.Exists(_credentialFilePath))
        File.Delete(_credentialFilePath);
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException("Failed to delete Discord credentials", ex);
    }
  }

  /// <summary>
  /// Check if credentials exist
  /// </summary>
  public static bool CredentialsExist()
  {
    return File.Exists(_credentialFilePath);
  }

  private static string EncryptString(string plainText)
  {
    if (string.IsNullOrEmpty(plainText))
      return string.Empty;

    var plainBytes = Encoding.UTF8.GetBytes(plainText);
    var encryptedBytes = ProtectedData.Protect(
      plainBytes,
      null,
      DataProtectionScope.CurrentUser
    );
    return Convert.ToBase64String(encryptedBytes);
  }

  private static string DecryptString(string encryptedText)
  {
    if (string.IsNullOrEmpty(encryptedText))
      return string.Empty;

    var encryptedBytes = Convert.FromBase64String(encryptedText);
    var plainBytes = ProtectedData.Unprotect(
      encryptedBytes,
      null, DataProtectionScope.CurrentUser
    );
    return Encoding.UTF8.GetString(plainBytes);
  }

  private record DiscordCredentialData
  {
    public required string DiscordUserId { get; init; }
    public required string Username { get; init; }
    public string? Discriminator { get; init; }
    public string? AvatarHash { get; init; }
    public required string EncryptedAccessToken { get; init; }
    public required string EncryptedRefreshToken { get; init; }
    public required DateTime TokenExpiry { get; init; }
    public required DateTime LastAuthenticated { get; init; }
  }
}

