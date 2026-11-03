using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker;

public class QuoteFunctions(QuoteCommands commands)
{
    /// <summary>
    /// Fetches the USD quote, waits 30 seconds with a durable delay and fetches again, up to 100 quotes.
    /// The STOP_QUOTE event, fired with the run id as instance id, ends the run during any wait.
    /// </summary>
    [SkailFunction]
    public async SkailTask<RunResult> GetUsdQuote(string runId, string currency)
    {
        // one wait for the stop event, created without await and reused by every WhenAny below
        var stopEvent = SkailTask.WaitForEvent(Events.StopQuote, runId);

        for (var i = 1; i <= QuoteLimits.MaxIterations; i++)
        {
            Quote quote;
            try
            {
                quote = await commands.FetchQuote(currency);
            }
            catch (Exception ex)
            {
                await commands.Notify(runId, RunStage.Failed, i, null, ex.Message);
                throw;
            }

            await commands.Notify(runId, RunStage.Quoted, i, quote, null);

            if (i == QuoteLimits.MaxIterations) break; // last quote: no wait after it

            var interval = SkailTask.DelaySeconds(QuoteLimits.IntervalSeconds);
            if (await SkailTask.WhenAny(stopEvent, interval) == stopEvent)
            {
                await commands.Notify(runId, RunStage.Stopped, i, quote, null);
                return new RunResult(i, RunEnd.Stopped);
            }
        }

        await commands.Notify(runId, RunStage.Completed, QuoteLimits.MaxIterations, null, null);
        return new RunResult(QuoteLimits.MaxIterations, RunEnd.LimitReached);
    }
}
