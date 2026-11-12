using System.Text.Json.Serialization;

namespace SkailUsdQuote.Contracts;

public static class QuoteLimits
{
    public const int MaxQuotes = 20;
    public const int IntervalSeconds = 30;
}

public static class Events
{
    public const string StopTracking = "STOP_TRACKING";
}

/// <summary>
/// Price of 1 USD in the given currency (ISO code). SourceTime is when the source published the rate;
/// FetchedAt is when the function fetched it. The source publishes a new rate only every few minutes,
/// so consecutive fetches can carry the same rate and SourceTime.
/// </summary>
public sealed record Quote(string Currency, decimal Rate, DateTimeOffset SourceTime)
{
    public DateTimeOffset FetchedAt { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<RunStage>))]
public enum RunStage
{
    Starting,
    Quoted,
    Stopped,
    Completed,
    Failed,
}

/// <summary>How a run ended. Serialized as text, so the skail Monitor shows the name instead of a number.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunEnd>))]
public enum RunEnd
{
    Stopped,
    LimitReached,
}

public sealed record RunResult(int QuoteCount, RunEnd End);

/// <summary>Progress the worker sends to the web app (Report* commands).</summary>
public sealed record RunEvent(string RunId, RunStage Stage, int QuoteNumber, Quote? Quote, string? Error = null);
