using System.Globalization;
using System.Text.Json;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker.Quotes;

/// <summary>AwesomeAPI: GET {base}/json/last/USD-{code}. No key required.</summary>
public sealed class HttpQuoteProvider(HttpClient http) : IQuoteProvider
{
    public async Task<Quote> GetAsync(Currency currency, CancellationToken ct = default)
    {
        var code = Currencies.Info(currency).Code;
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
