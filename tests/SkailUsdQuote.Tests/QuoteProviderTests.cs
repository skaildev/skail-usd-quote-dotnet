using System.Net;
using SkailUsdQuote.Contracts;
using SkailUsdQuote.Worker.Quotes;

namespace SkailUsdQuote.Tests;

public class QuoteProviderTests
{
    private static string AwesomeResponse(string code = "BRL") =>
        $$$"""{"USD{{{code}}}":{"code":"USD","codein":"{{{code}}}","name":"US Dollar/{{{code}}}","high":"5.30","low":"5.20","varBid":"0.01","pctChange":"0.2","bid":"5.2512","ask":"5.2530","timestamp":"1761130800","create_date":"2026-10-22 10:00:00"}}""";

    private static HttpQuoteProvider Provider(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

    [Theory]
    [InlineData(Currency.BRL, "https://api.example.test/json/last/USD-BRL")]
    [InlineData(Currency.EUR, "https://api.example.test/json/last/USD-EUR")]
    [InlineData(Currency.CNY, "https://api.example.test/json/last/USD-CNY")]
    [InlineData(Currency.JPY, "https://api.example.test/json/last/USD-JPY")]
    public async Task Http_provider_requests_the_usd_pair_of_the_currency(Currency c, string url)
    {
        var handler = StubHandler.Json(AwesomeResponse(c.ToString()));

        await Provider(handler).GetAsync(c);

        Assert.Equal(url, Assert.Single(handler.Calls).Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Http_provider_parses_bid_and_time()
    {
        var quote = await Provider(StubHandler.Json(AwesomeResponse())).GetAsync(Currency.BRL);

        Assert.Equal("BRL", quote.Currency);
        Assert.Equal(5.2512m, quote.Rate);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761130800), quote.At);
    }

    [Fact]
    public async Task Http_provider_fails_on_error_status_so_the_command_retries()
    {
        var provider = Provider(StubHandler.Status(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetAsync(Currency.BRL));
    }

    [Fact]
    public async Task Http_provider_error_message_has_status_and_response_body()
    {
        var provider = Provider(StubHandler.Json("slow down please", HttpStatusCode.TooManyRequests));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetAsync(Currency.BRL));

        Assert.Contains("429", error.Message);
        Assert.Contains("slow down please", error.Message);
    }

    [Fact]
    public async Task Http_provider_fails_when_pair_is_missing_in_response()
    {
        var provider = Provider(StubHandler.Json("{}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Currency.EUR));
    }

    [Theory]
    [InlineData(0, Currency.BRL, "5")]
    [InlineData(10, Currency.BRL, "5.005")]
    [InlineData(10, Currency.EUR, "0.9209")]
    [InlineData(10, Currency.JPY, "150.15")]
    [InlineData(100, Currency.BRL, "5")]
    public async Task Fake_provider_follows_the_clock(long unixSeconds, Currency currency, string expected)
    {
        var fake = new FakeQuoteProvider(new FixedClock(DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));

        var quote = await fake.GetAsync(currency);

        Assert.Equal(Currencies.Info(currency).Code, quote.Currency);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), quote.Rate);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
