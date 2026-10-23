using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Web.Runs;

public sealed record RunState(
    string RunId,
    Currency Currency,
    RunStage Stage,
    int Iteration,
    IReadOnlyList<Quote> Quotes,
    string? Error,
    bool StopRequested)
{
    public bool IsTerminal => Stage is RunStage.Stopped or RunStage.Completed or RunStage.Failed;
}
