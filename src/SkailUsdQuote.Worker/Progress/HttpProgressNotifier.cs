using System.Net.Http.Json;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker.Progress;

public sealed class HttpProgressNotifier(HttpClient http) : IProgressNotifier
{
    public async Task NotifyAsync(RunEvent evt, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"internal/runs/{evt.RunId}/events", evt, Json.Options, ct);
        response.EnsureSuccessStatusCode();
    }
}
