using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker.Quotes;

/// <summary>
/// Quote that works without network (QUOTE_PROVIDER=fake). It follows the clock: up to 0.99% above the base rate,
/// rising every second and starting over every 100 seconds, so a worker restart does not reset it.
/// </summary>
public sealed class FakeQuoteProvider(TimeProvider clock) : IQuoteProvider
{
    public FakeQuoteProvider() : this(TimeProvider.System) { }

    public Task<Quote> GetAsync(Currency currency, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var step = now.ToUnixTimeSeconds() % 100;
        var baseRate = currency switch
        {
            Currency.BRL => 5.00m,
            Currency.EUR => 0.92m,
            Currency.CNY => 7.10m,
            _ => 150.00m,
        };
        var rate = Math.Round(baseRate * (1 + step / 10000m), 4);
        return Task.FromResult(new Quote(Currencies.Info(currency).Code, rate, now));
    }
}
