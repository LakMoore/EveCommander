using Newtonsoft.Json;

namespace AssemblyLineLib
{
  public record MarketPrice
  {
    public int type_id { get; init; }
    public double adjusted_price { get; init; }
    public double average_price { get; init; }
  }

  internal class MarketPrices
  {
    private List<MarketPrice> Prices { get; set; } = [];
    private DateTime LastUpdated { get; set; } = DateTime.Now;
    private bool Fetching { get; set; }

    private MarketPrices() { }

    private static MarketPrices Instance { get; } = new();

    public static async Task<double> GetAdjustedPrice(int? typeId)
    {
      if (typeId == null)
        return 0;

      await Instance.GetMarketPrices();
      MarketPrice? price = Instance.Prices.FirstOrDefault(p => p.type_id == typeId);
      return price?.adjusted_price ?? 0;
    }

    private async Task GetMarketPrices()
    {
      while (Fetching)
      {
        await Task.Delay(500);
      }

      // if lastupdated over an hour ago, update the prices
      if (Prices.Count > 0 && DateTime.Now - LastUpdated < TimeSpan.FromHours(1))
      {
        return;
      }

      Fetching = true;

      // make a GET HTTP request
      using var client = new HttpClient();
      var response = await client.GetAsync("https://esi.evetech.net/markets/prices");
      if (response.IsSuccessStatusCode)
      {
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseBody);

        var result = JsonConvert.DeserializeObject<List<MarketPrice>>(responseBody);
        if (result != null)
        {
          Prices = result;
          LastUpdated = DateTime.Now;
        }
      }
      else
      {
        Console.WriteLine("Failed to retrieve price data");
      }

      LastUpdated = DateTime.Now;
      Fetching = false;
    }
  }
}
