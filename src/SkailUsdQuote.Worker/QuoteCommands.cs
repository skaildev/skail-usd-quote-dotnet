using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Contracts;
using SkailUsdQuote.Worker.Progress;
using SkailUsdQuote.Worker.Quotes;

namespace SkailUsdQuote.Worker;

/// <summary>Commands are the demo's I/O: each does one thing only (a retry re-runs the whole command).</summary>
public class QuoteCommands(IQuoteProvider quotes, IProgressNotifier notifier)
{
    [SkailCommand(retryCount: 3)]
    public async SkailTask<Quote> FetchQuote(string currency)
    {
        if (!Currencies.TryParse(currency, out var parsed))
            throw new ArgumentException($"Invalid currency: {currency}", nameof(currency));

        return await quotes.GetAsync(parsed);
    }

    /// <summary>Idempotent: the web app ignores a repeated event (same run, stage and iteration).</summary>
    [SkailCommand(retryCount: 3)]
    public async SkailTask Notify(string runId, RunStage stage, int iteration, Quote? quote, string? error)
    {
        await notifier.NotifyAsync(new RunEvent(runId, stage, iteration, quote, error));
    }
}
