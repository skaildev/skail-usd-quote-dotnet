using System.Text.Json;
using System.Text.Json.Serialization;
using SkailUsdQuote.Contracts;
using SkailUsdQuote.Web.Runs;
using SkailUsdQuote.Web.Skail;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<RunStore>();
builder.Services.AddSingleton(SkailOptions.FromEnvironment());
builder.Services.AddHttpClient<ISkailGateway, SkailApiGateway>(c => c.Timeout = TimeSpan.FromSeconds(10));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache" });

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapPost("/api/runs", async (StartRunRequest? request, RunStore store, ISkailGateway skail, CancellationToken ct) =>
{
    var currency = Currencies.Default;
    if (!string.IsNullOrWhiteSpace(request?.Currency) && !Currencies.TryParse(request.Currency, out currency))
        return Results.BadRequest(new { error = $"Unsupported currency '{request.Currency}'" });

    var runId = Guid.NewGuid().ToString();
    var run = store.Create(runId, currency);
    try
    {
        await skail.TriggerAsync(runId, currency, ct);
    }
    catch (SkailGatewayException e)
    {
        store.Remove(runId);
        return Results.Json(new { error = e.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
    return Results.Ok(RunView.From(run));
});

app.MapGet("/api/runs/{id}", (string id, RunStore store) =>
    store.Get(id) is { } run ? Results.Ok(RunView.From(run)) : Results.NotFound());

app.MapPost("/api/runs/{id}/stop", async (string id, RunStore store, ISkailGateway skail, CancellationToken ct) =>
{
    switch (store.RequestStop(id))
    {
        case StopRequest.NotFound:
            return Results.NotFound();
        case StopRequest.AlreadyFinished:
            return Results.Ok();
        case StopRequest.AlreadyRequested:
            return Results.Accepted();
    }

    try
    {
        await skail.FireStopAsync(id, ct);
        return Results.Accepted();
    }
    catch (SkailGatewayException e)
    {
        store.CancelStopRequest(id); // keep the Stop button available for another attempt
        return Results.Json(new { error = e.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
});

// called by the worker's Notify command
app.MapPost("/internal/runs/{id}/events", (string id, RunEvent evt, RunStore store) =>
{
    store.Apply(evt with { RunId = id });
    return Results.NoContent();
});

app.MapGet("/api/runs/{id}/stream", async (string id, HttpContext http, RunStore store, CancellationToken ct) =>
{
    using var subscription = store.Subscribe(id); // subscribe before reading the snapshot: no update is lost
    if (store.Get(id) is not { } current) return Results.NotFound();

    http.Response.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";

    async Task Send(RunState state)
    {
        var json = JsonSerializer.Serialize(RunView.From(state), Json.Options);
        await http.Response.WriteAsync($"data: {json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    try
    {
        await Send(current);
        if (current.IsTerminal) return Results.Empty;

        await foreach (var state in subscription.Reader.ReadAllAsync(ct))
        {
            await Send(state);
            if (state.IsTerminal) break;
        }
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
    return Results.Empty;
});

app.Run();

public sealed record StartRunRequest(string? Currency);

public sealed record RunView(
    string RunId,
    Currency Currency,
    RunStage Stage,
    int Iteration,
    int MaxIterations,
    IReadOnlyList<Quote> Quotes,
    string? Error,
    bool StopRequested,
    UiState Ui)
{
    public static RunView From(RunState s) =>
        new(s.RunId, s.Currency, s.Stage, s.Iteration, QuoteLimits.MaxIterations, s.Quotes, s.Error, s.StopRequested, ButtonRules.For(s));
}

public partial class Program;
