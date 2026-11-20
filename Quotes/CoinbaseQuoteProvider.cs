using System.Globalization;
using System.Text.Json;

namespace SkailUsdQuote.Quotes;

/// <summary>
/// Coinbase exchange rates: GET https://api.coinbase.com/v2/exchange-rates?currency=USD. No key required.
/// The rates move during the day, so consecutive fetches show the quote going up and down.
/// </summary>
public sealed class CoinbaseQuoteProvider(HttpClient http)
{
    public static readonly Uri BaseAddress = new("https://api.coinbase.com/");

    public async Task<Quote> GetAsync(Currency currency, CancellationToken ct = default)
    {
        using var response = await http.GetAsync("v2/exchange-rates?currency=USD", ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Quote API answered {(int)response.StatusCode}: {content[..Math.Min(content.Length, 300)]}",
                inner: null,
                response.StatusCode);

        using var doc = JsonDocument.Parse(content);
        var code = currency.ToString();
        if (!doc.RootElement.GetProperty("data").GetProperty("rates").TryGetProperty(code, out var rate))
            throw new InvalidOperationException($"Response has no {code} rate");

        return new Quote(code, decimal.Parse(rate.GetString()!, CultureInfo.InvariantCulture), DateTimeOffset.UtcNow);
    }
}
