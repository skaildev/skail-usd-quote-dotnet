using System.Globalization;
using System.Text.Json;

namespace SkailUsdQuote.Quotes;

/// <summary>AwesomeAPI: GET https://economia.awesomeapi.com.br/json/last/USD-{code}. No key required.</summary>
public sealed class AwesomeApiQuoteProvider(HttpClient http)
{
    public static readonly Uri BaseAddress = new("https://economia.awesomeapi.com.br/");

    public async Task<Quote> GetAsync(Currency currency, CancellationToken ct = default)
    {
        var code = currency.ToString();
        using var response = await http.GetAsync($"json/last/USD-{code}", ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Quote API answered {(int)response.StatusCode}: {content[..Math.Min(content.Length, 300)]}",
                inner: null,
                response.StatusCode);

        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty($"USD{code}", out var pair))
            throw new InvalidOperationException($"Response has no USD{code} pair");

        var rate = decimal.Parse(pair.GetProperty("bid").GetString()!, CultureInfo.InvariantCulture);
        var seconds = long.Parse(pair.GetProperty("timestamp").GetString()!, CultureInfo.InvariantCulture);
        return new Quote(code, rate, DateTimeOffset.FromUnixTimeSeconds(seconds));
    }
}
