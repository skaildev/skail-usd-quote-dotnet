namespace SkailUsdQuote;

public static class Events
{
    /// <summary>Fired with the run id as instance id to end a run during any wait.</summary>
    public const string StopTracking = "STOP_TRACKING";
}

/// <summary>Currencies quoted against the US dollar (USD).</summary>
public enum Currency
{
    BRL,
    EUR,
    CNY,
    JPY,
}

public static class Currencies
{
    public const Currency Default = Currency.BRL;

    /// <summary>Parses an ISO code (case-insensitive). Numeric strings are rejected, only the names are valid.</summary>
    public static bool TryParse(string? code, out Currency currency) =>
        Enum.TryParse(code, ignoreCase: true, out currency)
        && Enum.IsDefined(currency)
        && !int.TryParse(code, out _);
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
