using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker;

public class QuoteFunctions(QuoteCommands commands)
{
    /// <summary>
    /// Fetches the USD quote every 30s (durable delay) up to 100 times, or until the external event
    /// STOP_QUOTE (instance id = runId) arrives. The currency is an argument: fixed per execution.
    /// </summary>
    [SkailFunction]
    public async SkailTask<RunResult> GetUsdQuote(string runId, string currency)
    {
        // created ONCE, outside the loop, and reused by every WhenAny
        var parar = SkailTask.WaitForEvent(Events.StopQuote, runId);

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

            if (i == QuoteLimits.MaxIterations) break; // 100th quote: no further wait

            var prazo = SkailTask.DelaySeconds(QuoteLimits.IntervalSeconds);
            if (await SkailTask.WhenAny(parar, prazo) == parar)
            {
                await commands.Notify(runId, RunStage.Stopped, i, quote, null);
                return new RunResult(i, RunEnd.Stopped);
            }
        }

        await commands.Notify(runId, RunStage.Completed, QuoteLimits.MaxIterations, null, null);
        return new RunResult(QuoteLimits.MaxIterations, RunEnd.LimitReached);
    }
}
