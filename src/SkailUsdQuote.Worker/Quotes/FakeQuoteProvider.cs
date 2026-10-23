using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker.Quotes;

/// <summary>Deterministic quote to run without network (QUOTE_PROVIDER=fake): rises 0.01 per call.</summary>
public sealed class FakeQuoteProvider : IQuoteProvider
{
    private int _calls;

    public Task<Quote> GetAsync(Currency currency, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref _calls);
        var baseRate = currency switch
        {
            Currency.BRL => 5.00m,
            Currency.EUR => 0.92m,
            Currency.CNY => 7.10m,
            _ => 150.00m,
        };
        return Task.FromResult(new Quote(Currencies.Info(currency).Code, baseRate + 0.01m * n, DateTimeOffset.UtcNow));
    }
}
