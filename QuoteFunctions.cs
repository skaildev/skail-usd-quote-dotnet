using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Demo;
using SkailUsdQuote.Quotes;

namespace SkailUsdQuote;

public class QuoteFunctions(CoinbaseQuoteProvider quotes, QuoteDesk desk)
{
    private const int IntervalSeconds = 60;

    /// <summary>
    /// Fetches the USD quote every minute, with a durable delay between fetches, until STOP_TRACKING is fired
    /// with the run id as instance id. Returns how many quotes it fetched.
    /// The name and the parameters are the trigger contract: ["{runId}", "{currency}"].
    /// </summary>
    /// <remarks>retryCount: 0 because skail already retries FetchQuote; a failure that outlasts those retries ends
    /// the run as Failed in the Monitor.</remarks>
    [SkailFunction(retryCount: 0)]
    public async SkailTask<int> TrackUsdQuote(string runId, string currency)
    {
        // one wait for the stop event, created without await and reused by every WhenAny below
        var stopEvent = SkailTask.WaitForEvent(Events.StopTracking, runId);

        for (var quoteNumber = 1; ; quoteNumber++)
        {
            var quote = await FetchQuote(currency);
            await ReportQuote(runId, quoteNumber, quote);

            var interval = SkailTask.DelaySeconds(IntervalSeconds);
            if (await SkailTask.WhenAny(stopEvent, interval) == stopEvent)
            {
                await ReportStopped(runId, quoteNumber);
                return quoteNumber;
            }
        }
    }

    // The commands are the I/O. Once a command completes, skail records it and never runs it again, so a replay of
    // the function does not fetch again nor show the same quote twice.

    /// <summary>
    /// A GET is safe to repeat, so skail retries it when the quote API fails, with a growing wait
    /// (15 s, 30 s, 1 min, 2 min, 4 min), up to 5 times.
    /// </summary>
    [SkailCommand(retryCount: 5)]
    public async SkailTask<Quote> FetchQuote(string currency)
    {
        if (!Currencies.TryParse(currency, out var parsed))
            throw new ArgumentException($"Invalid currency: {currency}", nameof(currency));

        // the clock is read inside the command: the time is recorded with the result and stays the same on replay
        return await quotes.GetAsync(parsed);
    }

    [SkailCommand]
    public async SkailTask ReportQuote(string runId, int quoteNumber, Quote quote)
    {
        desk.AddQuote(runId, quoteNumber, quote);
        await Task.CompletedTask;
    }

    [SkailCommand]
    public async SkailTask ReportStopped(string runId, int quoteCount)
    {
        desk.Stopped(runId, quoteCount);
        await Task.CompletedTask;
    }
}
