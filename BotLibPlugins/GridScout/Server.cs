using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GridScout2
{
  internal class Server
  {
    private static string SERVER_URL =>
        Debugger.IsAttached
            ? "http://localhost:3000/"
            : "https://ffew.space/gridscout/";

    // Helper to build absolute URIs robustly from SERVER_URL + relative path
    private static Uri BuildUri(string relativePath)
    {
      return new Uri(new Uri(SERVER_URL), relativePath);
    }

    // Send a report with Discord token authentication
    public static async Task SendReport(ScoutMessage message, string discordToken)
    {
      // Validate DiscordToken is present
      if (string.IsNullOrEmpty(discordToken))
      {
        var errorMsg = "Cannot send report: Discord token not available. User must authenticate with Discord.";
        Console.WriteLine($"[GridScout] {errorMsg}");
        Debug.WriteLine($"[GridScout] {errorMsg}");
        return;
      }

      // Serialize message to JSON (token NOT included in body)
      var json = JsonConvert.SerializeObject(message);

      using var client = new HttpClient();
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      // Add Discord token as Authorization header (ONLY place token is sent)
      client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordToken);

      try
      {
        // Use correct endpoint: /api/report/scout
        var response = await client.PostAsync(BuildUri("api/report/scout"), content);
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
          Debug.WriteLine($"[GridScout] Report sent successfully: {response.StatusCode}");
        }
        else
        {
          // Parse error response
          var errorInfo = TryParseErrorResponse(body);
          var statusCode = (int)response.StatusCode;

          if (statusCode == 400)
          {
            Console.WriteLine($"[GridScout] Report rejected (400): {errorInfo}");
            Debug.WriteLine($"[GridScout] Report rejected (400): {errorInfo}");
            Debug.WriteLine($"[GridScout] Response body: {body}");
          }
          else if (statusCode == 401)
          {
            Console.WriteLine($"[GridScout] Authentication failed (401): {errorInfo}");
            Debug.WriteLine($"[GridScout] Authentication failed (401): {errorInfo}");
            Debug.WriteLine($"[GridScout] Discord token may be expired or invalid. Response: {body}");
          }
          else
          {
            Console.WriteLine($"[GridScout] Server error ({statusCode}): {body}");
            Debug.WriteLine($"[GridScout] Server error ({statusCode}): {body}");
          }
        }
      }
      catch (HttpRequestException ex)
      {
        Console.WriteLine($"[GridScout] Network error: {ex.Message}");
        Debug.WriteLine($"[GridScout] Network error: {ex.Message}");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[GridScout] Unexpected error: {ex.Message}");
        Debug.WriteLine($"[GridScout] Unexpected error: {ex}");
      }
    }

    // Try to parse error response from server
    private static string TryParseErrorResponse(string responseBody)
    {
      try
      {
        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp) &&
            root.TryGetProperty("reason", out var reasonProp))
        {
          return $"{errorProp.GetString()}: {reasonProp.GetString()}";
        }

        return responseBody;
      }
      catch
      {
        return responseBody;
      }
    }

    // Send a local report with Discord token authentication
    public static async Task SendLocalReport(LocalReport message, string discordToken)
    {
      // Validate DiscordToken is present
      if (string.IsNullOrEmpty(discordToken))
      {
        var errorMsg = "Cannot send local report: Discord token not available. User must authenticate with Discord.";
        Console.WriteLine($"[LocalIntel] {errorMsg}");
        Debug.WriteLine($"[LocalIntel] {errorMsg}");
        return;
      }

      // Serialize message to JSON (token NOT included in body)
      var json = JsonConvert.SerializeObject(message);

      using var client = new HttpClient();
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      // Add Discord token as Authorization header (ONLY place token is sent)
      client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", discordToken);

      try
      {
        // Use correct endpoint: /api/report/local
        var response = await client.PostAsync(BuildUri("api/report/local"), content);
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
          Debug.WriteLine($"[LocalIntel] Report sent successfully: {response.StatusCode}");
        }
        else
        {
          // Parse error response
          var errorInfo = TryParseErrorResponse(body);
          var statusCode = (int)response.StatusCode;

          if (statusCode == 400)
          {
            Console.WriteLine($"[LocalIntel] Report rejected (400): {errorInfo}");
            Debug.WriteLine($"[LocalIntel] Report rejected (400): {errorInfo}");
            Debug.WriteLine($"[LocalIntel] Response body: {body}");
          }
          else if (statusCode == 401)
          {
            Console.WriteLine($"[LocalIntel] Authentication failed (401): {errorInfo}");
            Debug.WriteLine($"[LocalIntel] Authentication failed (401): {errorInfo}");
            Debug.WriteLine($"[LocalIntel] Discord token may be expired or invalid. Response: {body}");
          }
          else
          {
            Console.WriteLine($"[LocalIntel] Server error ({statusCode}): {body}");
            Debug.WriteLine($"[LocalIntel] Server error ({statusCode}): {body}");
          }
        }
      }
      catch (HttpRequestException ex)
      {
        Console.WriteLine($"[LocalIntel] Network error: {ex.Message}");
        Debug.WriteLine($"[LocalIntel] Network error: {ex.Message}");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[LocalIntel] Unexpected error: {ex.Message}");
        Debug.WriteLine($"[LocalIntel] Unexpected error: {ex}");
      }
    }

    // send an error
    public static async Task SendError(Exception error)
    {
      var json = JsonConvert.SerializeObject(error);

      using var client = new HttpClient();
      var content = new StringContent(json, Encoding.UTF8, "application/json");
      var response = await client.PostAsync(BuildUri("api/error"), content);
      var body = await response.Content.ReadAsStringAsync();
      Console.WriteLine(body);
    }
  }
}
