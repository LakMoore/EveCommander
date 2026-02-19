using Commander.Models;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Commander.Services;

/// <summary>
/// Handles Discord OAuth 2.0 authentication flow using PKCE (Proof Key for Code Exchange)
/// This is secure for distributed desktop applications as it doesn't require a client secret
/// </summary>
public class DiscordAuthService : IDisposable
{
  private const string DISCORD_API_BASE = "https://discord.com/api/v10";
  private const string DISCORD_OAUTH_AUTHORIZE = "https://discord.com/api/oauth2/authorize";
  private const string DISCORD_OAUTH_TOKEN = "https://discord.com/api/oauth2/token";
  private const string DISCORD_OAUTH_REVOKE = "https://discord.com/api/oauth2/token/revoke";

  private readonly string _clientId;
  private readonly string _redirectUri;
  private readonly HttpClient _httpClient;
  private HttpListener? _httpListener;
  private CancellationTokenSource? _listenerCts;

  private string? _codeVerifier;
  private string? _codeChallenge;

  public DiscordUserInfo? CurrentUser { get; private set; }

  public event EventHandler<DiscordUserInfo>? AuthenticationCompleted;
  public event EventHandler<string>? AuthenticationFailed;
  public event EventHandler? AuthenticationRevoked;

  public DiscordAuthService(int callbackPort = 5000)
  {
    // This Client ID is PUBLIC and SAFE to commit to Git (PKCE security model)
    _clientId = "1474062291296321712";
    _redirectUri = $"http://localhost:{callbackPort}/discord-callback";
    _httpClient = new HttpClient();
  }

  /// <summary>
  /// Initialize by loading existing credentials
  /// </summary>
  public async Task InitializeAsync()
  {
    CurrentUser = await SecureCredentialStorage.LoadCredentialsAsync();

    // Note: PKCE doesn't support traditional refresh tokens
    // If token is expired, user needs to re-authenticate
    if (CurrentUser != null && CurrentUser.IsTokenExpired)
    {
      // Clear expired credentials
      await RevokeAuthenticationAsync();
    }
  }

  /// <summary>
  /// Start the OAuth authentication flow with PKCE
  /// </summary>
  public async Task<bool> AuthenticateAsync()
  {
    try
    {
      GeneratePkceChallenge();

      var port = ExtractPortFromRedirectUri();

      if (!await StartHttpListenerAsync(port))
        return false;

      OpenBrowserForAuth();

      return true;
    }
    catch (Exception ex)
    {
      AuthenticationFailed?.Invoke(this, $"Failed to start authentication: {ex.Message}");
      return false;
    }
  }

  /// <summary>
  /// Generate PKCE code verifier and challenge
  /// </summary>
  private void GeneratePkceChallenge()
  {
    // Generate a cryptographically random code verifier (43-128 characters)
    var bytes = new byte[32];
    RandomNumberGenerator.Fill(bytes);
    _codeVerifier = Base64UrlEncode(bytes);

    // Create code challenge (SHA256 hash of verifier)
    using var sha256 = SHA256.Create();
    var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier));
    _codeChallenge = Base64UrlEncode(challengeBytes);
  }

  /// <summary>
  /// Base64 URL encode without padding (required for PKCE)
  /// </summary>
  private static string Base64UrlEncode(byte[] data)
  {
    return Convert.ToBase64String(data)
      .Replace('+', '-')
      .Replace('/', '_')
      .Replace("=", "");
  }

  /// <summary>
  /// Revoke authentication and delete credentials
  /// </summary>
  public async Task<bool> RevokeAuthenticationAsync()
  {
    if (CurrentUser == null)
      return false;

    try
    {
      await RevokeTokenAsync(CurrentUser.AccessToken);

      SecureCredentialStorage.DeleteCredentials();
      CurrentUser = null;

      AuthenticationRevoked?.Invoke(this, EventArgs.Empty);

      return true;
    }
    catch (Exception ex)
    {
      AuthenticationFailed?.Invoke(this, $"Failed to revoke authentication: {ex.Message}");
      return false;
    }
  }

  private async Task<bool> StartHttpListenerAsync(int port)
  {
    try
    {
      _listenerCts = new CancellationTokenSource();
      _httpListener = new HttpListener();
      _httpListener.Prefixes.Add($"http://localhost:{port}/");
      _httpListener.Start();

      _ = Task.Run(() => HandleCallbackAsync(_listenerCts.Token));

      return true;
    }
    catch (HttpListenerException)
    {
      AuthenticationFailed?.Invoke(this, $"Port {port} is already in use. Please close other applications and try again.");
      return false;
    }
  }

  private void OpenBrowserForAuth()
  {
    var authUrl = QueryHelpers.AddQueryString(DISCORD_OAUTH_AUTHORIZE, new Dictionary<string, string?>
    {
      ["client_id"] = _clientId,
      ["redirect_uri"] = _redirectUri,
      ["response_type"] = "code",
      ["scope"] = "identify",
      ["code_challenge"] = _codeChallenge,
      ["code_challenge_method"] = "S256"
    });

    Process.Start(new ProcessStartInfo
    {
      FileName = authUrl,
      UseShellExecute = true
    });
  }

  private async Task HandleCallbackAsync(CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested && _httpListener != null)
      {
        var context = await _httpListener.GetContextAsync();
        
        _ = Task.Run(async () =>
        {
          try
          {
            await ProcessCallbackAsync(context);
          }
          catch (Exception ex)
          {
            await SendErrorResponseAsync(context, ex.Message);
          }
        }, cancellationToken);
      }
    }
    catch (HttpListenerException)
    {
      // Listener was stopped
    }
    catch (Exception ex)
    {
      AuthenticationFailed?.Invoke(this, $"Callback handler error: {ex.Message}");
    }
  }

  private async Task ProcessCallbackAsync(HttpListenerContext context)
  {
    var query = context.Request.Url?.Query;
    
    if (string.IsNullOrEmpty(query))
    {
      await SendErrorResponseAsync(context, "No query parameters received");
      return;
    }

    var queryParams = QueryHelpers.ParseQuery(query);

    if (queryParams.TryGetValue("error", out var error))
    {
      await SendErrorResponseAsync(context, $"Authentication cancelled: {error}");
      StopHttpListener();
      return;
    }

    if (!queryParams.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
    {
      await SendErrorResponseAsync(context, "No authorization code received");
      return;
    }

    var tokenResponse = await ExchangeCodeForTokenAsync(code!);
    
    if (tokenResponse == null)
    {
      await SendErrorResponseAsync(context, "Failed to exchange code for token");
      return;
    }

    var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken);
    
    if (userInfo == null)
    {
      await SendErrorResponseAsync(context, "Failed to retrieve user information");
      return;
    }

    CurrentUser = new DiscordUserInfo
    {
      DiscordUserId = userInfo.Id,
      Username = userInfo.Username,
      Discriminator = userInfo.Discriminator,
      AvatarHash = userInfo.Avatar,
      AccessToken = tokenResponse.AccessToken,
      RefreshToken = tokenResponse.RefreshToken,
      TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
      LastAuthenticated = DateTime.UtcNow
    };

    await SecureCredentialStorage.SaveCredentialsAsync(CurrentUser);
    
    await SendSuccessResponseAsync(context);
    
    AuthenticationCompleted?.Invoke(this, CurrentUser);
    
    StopHttpListener();
  }

  private async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code)
  {
    try
    {
      var content = new FormUrlEncodedContent(new Dictionary<string, string>
      {
        ["client_id"] = _clientId,
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = _redirectUri,
        ["code_verifier"] = _codeVerifier!
      });

      var response = await _httpClient.PostAsync(DISCORD_OAUTH_TOKEN, content);
      response.EnsureSuccessStatusCode();

      return await response.Content.ReadFromJsonAsync<TokenResponse>();
    }
    catch (Exception ex)
    {
      AuthenticationFailed?.Invoke(this, $"Token exchange failed: {ex.Message}");
      return null;
    }
  }

  private async Task<DiscordUser?> GetUserInfoAsync(string accessToken)
  {
    try
    {
      var request = new HttpRequestMessage(HttpMethod.Get, $"{DISCORD_API_BASE}/users/@me");
      request.Headers.Add("Authorization", $"Bearer {accessToken}");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      return await response.Content.ReadFromJsonAsync<DiscordUser>();
    }
    catch (Exception ex)
    {
      AuthenticationFailed?.Invoke(this, $"Failed to get user info: {ex.Message}");
      return null;
    }
  }

  private async Task RevokeTokenAsync(string token)
  {
    try
    {
      var content = new FormUrlEncodedContent(new Dictionary<string, string>
      {
        ["client_id"] = _clientId,
        ["token"] = token
      });

      await _httpClient.PostAsync(DISCORD_OAUTH_REVOKE, content);
    }
    catch
    {
      // Ignore errors during revoke
    }
  }

  private async Task SendSuccessResponseAsync(HttpListenerContext context)
  {
    var html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Discord Authentication Success</title>
    <style>
        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #23272a; color: #dcddde; }
        .success { color: #43b581; font-size: 24px; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class='success'>✓ Authentication Successful!</div>
    <p>You can now close this window and return to Commander.</p>
</body>
</html>";

    var buffer = Encoding.UTF8.GetBytes(html);
    context.Response.ContentLength64 = buffer.Length;
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.OutputStream.WriteAsync(buffer);
    context.Response.OutputStream.Close();
  }

  private async Task SendErrorResponseAsync(HttpListenerContext context, string errorMessage)
  {
    var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Discord Authentication Error</title>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #23272a; color: #dcddde; }}
        .error {{ color: #f04747; font-size: 24px; margin-bottom: 20px; }}
    </style>
</head>
<body>
    <div class='error'>✗ Authentication Failed</div>
    <p>{errorMessage}</p>
    <p>Please close this window and try again.</p>
</body>
</html>";

    var buffer = Encoding.UTF8.GetBytes(html);
    context.Response.ContentLength64 = buffer.Length;
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.OutputStream.WriteAsync(buffer);
    context.Response.OutputStream.Close();

    AuthenticationFailed?.Invoke(this, errorMessage);
  }

  private void StopHttpListener()
  {
    try
    {
      _listenerCts?.Cancel();
      _httpListener?.Stop();
      _httpListener?.Close();
    }
    catch
    {
      // Ignore errors during cleanup
    }
  }

  private int ExtractPortFromRedirectUri()
  {
    var uri = new Uri(_redirectUri);
    return uri.Port;
  }

  public void Dispose()
  {
    StopHttpListener();
    _listenerCts?.Dispose();
    _httpClient?.Dispose();
  }

  private record TokenResponse
  {
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }
  }

  private record DiscordUser
  {
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("discriminator")]
    public string? Discriminator { get; init; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

  }
}
