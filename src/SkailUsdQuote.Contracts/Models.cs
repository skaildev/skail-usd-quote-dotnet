namespace SkailUsdQuote.Contracts;

public static class QuoteLimits
{
    public const int MaxIterations = 100;
    public const int IntervalSeconds = 30;
}

public static class Events
{
    public const string StopQuote = "STOP_QUOTE";
}

/// <summary>Price of 1 USD in the given currency (ISO code).</summary>
public sealed record Quote(string Currency, decimal Rate, DateTimeOffset At);

public enum RunStage
{
    Starting,
    Quoted,
    Stopped,
    Completed,
    Failed,
}

public enum RunEnd
{
    Stopped,
    LimitReached,
}

public sealed record RunResult(int Iterations, RunEnd End);

/// <summary>Progresso enviado pelo worker ao web (command Notify).</summary>
public sealed record RunEvent(string RunId, RunStage Stage, int Iteration, Quote? Quote, string? Error = null);
