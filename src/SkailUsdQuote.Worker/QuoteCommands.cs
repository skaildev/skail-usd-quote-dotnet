using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Contracts;
using SkailUsdQuote.Worker.Progress;
using SkailUsdQuote.Worker.Quotes;

namespace SkailUsdQuote.Worker;

/// <summary>
/// The demo's I/O. Each command does a single thing, because a retry runs the whole command again.
/// The Report* commands are idempotent: the web app ignores a repeated event (same run, stage and quote number).
/// </summary>
public class QuoteCommands(IQuoteProvider quotes, IProgressNotifier notifier)
{
    [SkailCommand(retryCount: 3)]
    public async SkailTask<Quote> FetchQuote(string currency)
    {
        if (!Currencies.TryParse(currency, out var parsed))
            throw new ArgumentException($"Invalid currency: {currency}", nameof(currency));

        var quote = await quotes.GetAsync(parsed);

        // the clock is read inside the command: the time is recorded with the result and stays the same on replay
        return quote with { FetchedAt = DateTimeOffset.UtcNow };
    }

    [SkailCommand(retryCount: 3)]
    public async SkailTask ReportQuote(string runId, int quoteNumber, Quote quote)
    {
        await notifier.NotifyAsync(new RunEvent(runId, RunStage.Quoted, quoteNumber, quote));
    }

    [SkailCommand(retryCount: 3)]
    public async SkailTask ReportStopped(string runId, int quoteCount)
    {
        await notifier.NotifyAsync(new RunEvent(runId, RunStage.Stopped, quoteCount, null));
    }

    [SkailCommand(retryCount: 3)]
    public async SkailTask ReportCompleted(string runId, int quoteCount)
    {
        await notifier.NotifyAsync(new RunEvent(runId, RunStage.Completed, quoteCount, null));
    }

    [SkailCommand(retryCount: 3)]
    public async SkailTask ReportFailure(string runId, int quoteNumber, string error)
    {
        await notifier.NotifyAsync(new RunEvent(runId, RunStage.Failed, quoteNumber, null, error));
    }
}
