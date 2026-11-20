namespace SkailUsdQuote.Runs;

/// <summary>
/// Which run this process follows: a new one (a fresh GUID and the currency to quote) or an existing one, by id.
/// </summary>
public sealed record RunTarget(string RunId, Currency? NewRunCurrency)
{
    public bool IsNew => NewRunCurrency is not null;

    public static RunTarget New(Currency currency) => new(Guid.NewGuid().ToString(), currency);

    public static RunTarget Existing(Guid runId) => new(runId.ToString(), null);

    /// <summary>
    /// Parses the command line: no argument starts a run in the default currency, a currency code starts a run in
    /// that currency and a run id (GUID) follows a run that already exists.
    /// </summary>
    public static bool TryParse(string? argument, out RunTarget target)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            target = New(Currencies.Default);
            return true;
        }
        if (Guid.TryParse(argument, out var runId))
        {
            target = Existing(runId);
            return true;
        }
        if (Currencies.TryParse(argument, out var currency))
        {
            target = New(currency);
            return true;
        }

        target = New(Currencies.Default);
        return false;
    }
}
