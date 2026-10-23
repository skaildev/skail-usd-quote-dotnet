using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker.Quotes;

public interface IQuoteProvider
{
    Task<Quote> GetAsync(Currency currency, CancellationToken ct = default);
}
