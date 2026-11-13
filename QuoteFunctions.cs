using System.Globalization;
using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Quotes;

namespace SkailUsdQuote;

public class QuoteFunctions(AwesomeApiQuoteProvider quotes)
{
    private const int IntervalSeconds = 30;

    /// <summary>
    /// Fetches the USD quote every 30 seconds, with a durable delay between fetches, until STOP_TRACKING is fired
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
    // the function does not fetch again nor print the same line twice.

    /// <summary>
    /// The quote API answers 429 when its quota runs out. A GET is safe to repeat, so skail retries it, with a
    /// growing wait (15 s, 30 s, 1 min, 2 min, 4 min), up to 5 times.
    /// </summary>
    [SkailCommand(retryCount: 5)]
    public async SkailTask<Quote> FetchQuote(string currency)
    {
        if (!Currencies.TryParse(currency, out var parsed))
            throw new ArgumentException($"Invalid currency: {currency}", nameof(currency));

        var quote = await quotes.GetAsync(parsed);

        // the clock is read inside the command: the time is recorded with the result and stays the same on replay
        return quote with { FetchedAt = DateTimeOffset.UtcNow };
    }

    [SkailCommand]
    public async SkailTask ReportQuote(string runId, int quoteNumber, Quote quote) =>
        await Print(runId, string.Create(CultureInfo.InvariantCulture,
            $"#{quoteNumber,-2} 1 USD = {quote.Rate:0.0000} {quote.Currency}  " +
            $"(fetched {quote.FetchedAt.ToLocalTime():HH:mm:ss}, source {quote.SourceTime.ToLocalTime():HH:mm:ss})"));

    [SkailCommand]
    public async SkailTask ReportStopped(string runId, int quoteCount) =>
        await Print(runId, $"Stopped after {quoteCount} {(quoteCount == 1 ? "quote" : "quotes")}.");

    // each line starts with the first 8 characters of the run id
    private static Task Print(string runId, string message) => Console.Out.WriteLineAsync($"[{runId[..8]}] {message}");
}
