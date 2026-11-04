using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker;

public class QuoteFunctions(QuoteCommands commands)
{
    /// <summary>
    /// Tracks the USD quote: fetches it, waits 30 seconds with a durable delay and fetches again, up to 20 quotes.
    /// The STOP_TRACKING event, fired with the run id as instance id, ends the run during any wait.
    /// </summary>
    [SkailFunction]
    public async SkailTask<RunResult> TrackUsdQuote(string runId, string currency)
    {
        // one wait for the stop event, created without await and reused by every WhenAny below
        var stopEvent = SkailTask.WaitForEvent(Events.StopTracking, runId);

        for (var quoteNumber = 1; quoteNumber <= QuoteLimits.MaxQuotes; quoteNumber++)
        {
            Quote quote;
            try
            {
                quote = await commands.FetchQuote(currency);
            }
            catch (Exception ex)
            {
                await commands.ReportFailure(runId, quoteNumber, ex.Message);
                throw;
            }

            await commands.ReportQuote(runId, quoteNumber, quote);

            if (quoteNumber == QuoteLimits.MaxQuotes) break; // last quote: no wait after it

            var interval = SkailTask.DelaySeconds(QuoteLimits.IntervalSeconds);
            if (await SkailTask.WhenAny(stopEvent, interval) == stopEvent)
            {
                await commands.ReportStopped(runId, quoteNumber);
                return new RunResult(quoteNumber, RunEnd.Stopped);
            }
        }

        await commands.ReportCompleted(runId, QuoteLimits.MaxQuotes);
        return new RunResult(QuoteLimits.MaxQuotes, RunEnd.LimitReached);
    }
}
