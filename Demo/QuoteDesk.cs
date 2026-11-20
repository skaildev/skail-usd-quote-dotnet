using System.Globalization;
using SkailUsdQuote.Skail;

namespace SkailUsdQuote.Demo;

/// <summary>
/// What the page shows: the runs this app started or that reported a quote here, each with its quotes.
/// A run the app does not know (it was started before the app restarted) shows up with its next quote.
/// </summary>
public sealed class QuoteDesk(SkailApiClient skail)
{
    private sealed class Run(string id, string currency)
    {
        public string Id { get; } = id;
        public string Currency { get; } = currency;
        public string Status { get; set; } = "running";
        public List<(int Number, Quote Quote)> Quotes { get; } = [];
    }

    private readonly object gate = new();
    private readonly List<Run> runs = [];

    public async Task<string> StartAsync(Currency currency, CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString();
        await skail.TriggerAsync(runId, currency, ct);
        lock (gate) runs.Add(new Run(runId, currency.ToString()));
        Console.WriteLine($"[{runId[..8]}] started: 1 USD in {currency} every minute, until you stop it");
        return runId;
    }

    public async Task<bool> StopAsync(string runId, CancellationToken ct)
    {
        Run? run;
        lock (gate) run = runs.Find(r => r.Id == runId);
        if (run is null) return false;
        await skail.FireStopAsync(runId, ct);
        lock (gate) if (run.Status == "running") run.Status = "stopping";
        Console.WriteLine($"[{runId[..8]}] STOP_TRACKING sent: the run ends during the current wait");
        return true;
    }

    public void AddQuote(string runId, int quoteNumber, Quote quote)
    {
        lock (gate) Find(runId, quote.Currency).Quotes.Add((quoteNumber, quote));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[{runId[..8]}] #{quoteNumber,-3} 1 USD = {quote.Rate:0.0000} {quote.Currency}  " +
            $"(fetched {quote.FetchedAt.ToLocalTime():HH:mm:ss})"));
    }

    public void Stopped(string runId, int quoteCount)
    {
        lock (gate) Find(runId, "").Status = "stopped";
        Console.WriteLine($"[{runId[..8]}] stopped after {quoteCount} {(quoteCount == 1 ? "quote" : "quotes")}");
    }

    public object View()
    {
        lock (gate)
        {
            return runs.AsEnumerable().Reverse().Select(r => new
            {
                r.Id, r.Currency, r.Status,
                quotes = r.Quotes.Select((q, i) => new
                {
                    number = q.Number,
                    rate = q.Quote.Rate,
                    at = q.Quote.FetchedAt,
                    // up, down or the same as the previous quote this page saw, at the 4 decimals the page shows
                    trend = i == 0 ? "" : Trend(q.Quote.Rate, r.Quotes[i - 1].Quote.Rate),
                }).Reverse().ToList(),
            }).ToList();
        }
    }

    private static string Trend(decimal rate, decimal previous) =>
        Math.Sign(Math.Round(rate, 4) - Math.Round(previous, 4)) switch { 1 => "up", -1 => "down", _ => "same" };

    // a run started before the app restarted: the page follows it from its next quote
    private Run Find(string runId, string currency)
    {
        var run = runs.Find(r => r.Id == runId);
        if (run is not null) return run;
        run = new Run(runId, currency);
        runs.Add(run);
        return run;
    }
}
