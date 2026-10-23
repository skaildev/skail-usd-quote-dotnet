using System.Text.Json;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Tests;

public class ContractsTests
{
    [Fact]
    public void Currencies_offers_real_euro_yuan_yen_with_real_as_default()
    {
        Assert.Equal(
            new[] { Currency.BRL, Currency.EUR, Currency.CNY, Currency.JPY },
            Currencies.All.Select(c => c.Currency));
        Assert.Equal(Currency.BRL, Currencies.Default);
        Assert.Equal(Currencies.Default, Currencies.All[0].Currency);
    }

    [Theory]
    [InlineData(Currency.BRL, "BRL", "Real", "R$")]
    [InlineData(Currency.EUR, "EUR", "Euro", "€")]
    [InlineData(Currency.CNY, "CNY", "Yuan", "¥")]
    [InlineData(Currency.JPY, "JPY", "Yen", "¥")]
    public void Currencies_info_has_code_name_and_symbol(Currency c, string code, string name, string symbol)
    {
        var info = Currencies.Info(c);
        Assert.Equal(code, info.Code);
        Assert.Equal(name, info.Name);
        Assert.Equal(symbol, info.Symbol);
    }

    [Theory]
    [InlineData("BRL", Currency.BRL)]
    [InlineData("eur", Currency.EUR)]
    [InlineData("Cny", Currency.CNY)]
    [InlineData("JPY", Currency.JPY)]
    public void TryParse_accepts_codes_ignoring_case(string code, Currency expected)
    {
        Assert.True(Currencies.TryParse(code, out var c));
        Assert.Equal(expected, c);
    }

    [Theory]
    [InlineData("")]
    [InlineData("USD")]
    [InlineData("1")]
    [InlineData(null)]
    public void TryParse_rejects_unknown_codes(string? code)
    {
        Assert.False(Currencies.TryParse(code, out _));
    }

    [Fact]
    public void Limits_and_event_name_are_the_agreed_constants()
    {
        Assert.Equal(100, QuoteLimits.MaxIterations);
        Assert.Equal(30, QuoteLimits.IntervalSeconds);
        Assert.Equal("STOP_QUOTE", Events.StopQuote);
    }

    [Fact]
    public void RunEvent_roundtrips_with_enums_as_strings()
    {
        var evt = new RunEvent("run-1", RunStage.Quoted, 3,
            new Quote("BRL", 5.25m, DateTimeOffset.Parse("2026-10-22T10:00:00Z")));

        var json = JsonSerializer.Serialize(evt, Json.Options);
        Assert.Contains("\"stage\":\"Quoted\"", json);

        var back = JsonSerializer.Deserialize<RunEvent>(json, Json.Options);
        Assert.Equal(evt, back);
    }
}
