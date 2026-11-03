using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkailUsdQuote.Contracts;
using SkailUsdQuote.Web.Skail;

namespace SkailUsdQuote.Tests;

public sealed class FakeGateway : ISkailGateway
{
    public List<(string RunId, Currency Currency)> Triggers { get; } = [];
    public List<string> Stops { get; } = [];
    public Exception? FailWith { get; set; }

    public Task TriggerAsync(string runId, Currency currency, CancellationToken ct = default)
    {
        if (FailWith is not null) throw FailWith;
        Triggers.Add((runId, currency));
        return Task.CompletedTask;
    }

    public Task FireStopAsync(string runId, CancellationToken ct = default)
    {
        if (FailWith is not null) throw FailWith;
        Stops.Add(runId);
        return Task.CompletedTask;
    }
}

public sealed class WebFactory : WebApplicationFactory<Program>
{
    public FakeGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<ISkailGateway>();
            s.AddSingleton<ISkailGateway>(Gateway);
        });
    }
}

public class WebApiTests : IClassFixture<WebFactory>
{
    private readonly WebFactory _factory;
    private readonly HttpClient _http;

    public WebApiTests(WebFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient();
    }

    private async Task<JsonElement> StartAsync(string? currency = null)
    {
        var res = await _http.PostAsJsonAsync("/api/runs", currency is null ? (object)new { } : new { currency });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Health_endpoint_is_ok()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/healthz")).StatusCode);
    }

    [Fact]
    public async Task Index_page_is_served_with_the_currency_dropdown()
    {
        var html = await _http.GetStringAsync("/");

        Assert.Contains("<select", html);
        foreach (var code in new[] { "BRL", "EUR", "CNY", "JPY" })
            Assert.Contains($"value=\"{code}\"", html);
    }

    [Fact]
    public async Task Start_without_currency_defaults_to_real_and_triggers_the_function()
    {
        var run = await StartAsync();

        var runId = run.GetProperty("runId").GetString()!;
        Assert.Equal("BRL", run.GetProperty("currency").GetString());
        Assert.Equal("Starting", run.GetProperty("stage").GetString());
        Assert.Contains((runId, Currency.BRL), _factory.Gateway.Triggers);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("cny")]
    [InlineData("JPY")]
    public async Task Start_uses_the_chosen_currency(string code)
    {
        var run = await StartAsync(code);

        Assert.Equal(code.ToUpperInvariant(), run.GetProperty("currency").GetString());
        Assert.Contains(_factory.Gateway.Triggers, t =>
            t.RunId == run.GetProperty("runId").GetString() && t.Currency.ToString() == code.ToUpperInvariant());
    }

    [Fact]
    public async Task Start_with_invalid_currency_is_a_bad_request_and_does_not_trigger()
    {
        var before = _factory.Gateway.Triggers.Count;

        var res = await _http.PostAsJsonAsync("/api/runs", new { currency = "USD" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(before, _factory.Gateway.Triggers.Count);
    }

    [Fact]
    public async Task Start_reports_bad_gateway_when_skail_is_unreachable()
    {
        var factory = new WebFactory { };
        factory.Gateway.FailWith = new SkailGatewayException("down");
        var res = await factory.CreateClient().PostAsJsonAsync("/api/runs", new { });

        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
    }

    [Fact]
    public async Task Run_state_exposes_ui_rules_for_the_button()
    {
        var run = await StartAsync();

        var ui = run.GetProperty("ui");
        Assert.Equal("Stop", ui.GetProperty("action").GetString());
        Assert.Equal("Stop", ui.GetProperty("label").GetString());
        Assert.False(ui.GetProperty("currencyEnabled").GetBoolean());
    }

    [Fact]
    public async Task Internal_events_update_the_run_visible_via_get()
    {
        var runId = (await StartAsync("EUR")).GetProperty("runId").GetString()!;

        var evt = new RunEvent(runId, RunStage.Quoted, 1, new Quote("EUR", 0.92m, DateTimeOffset.UnixEpoch));
        var post = await _http.PostAsJsonAsync($"/internal/runs/{runId}/events", evt, Json.Options);
        Assert.Equal(HttpStatusCode.NoContent, post.StatusCode);

        var run = await _http.GetFromJsonAsync<JsonElement>($"/api/runs/{runId}");
        Assert.Equal("Quoted", run.GetProperty("stage").GetString());
        Assert.Equal(1, run.GetProperty("quoteCount").GetInt32());
        Assert.Equal(0.92m, run.GetProperty("quotes")[0].GetProperty("rate").GetDecimal());
    }

    [Fact]
    public async Task Stop_fires_the_event_once_and_is_idempotent()
    {
        var runId = (await StartAsync()).GetProperty("runId").GetString()!;

        var first = await _http.PostAsync($"/api/runs/{runId}/stop", null);
        var second = await _http.PostAsync($"/api/runs/{runId}/stop", null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(1, _factory.Gateway.Stops.Count(id => id == runId));

        var run = await _http.GetFromJsonAsync<JsonElement>($"/api/runs/{runId}");
        Assert.Equal("Stopping…", run.GetProperty("ui").GetProperty("label").GetString());
    }

    [Fact]
    public async Task Stop_after_the_run_finished_is_a_noop()
    {
        var runId = (await StartAsync()).GetProperty("runId").GetString()!;
        await _http.PostAsJsonAsync($"/internal/runs/{runId}/events", new RunEvent(runId, RunStage.Stopped, 1, null), Json.Options);

        var res = await _http.PostAsync($"/api/runs/{runId}/stop", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.DoesNotContain(runId, _factory.Gateway.Stops);
    }

    [Fact]
    public async Task Stop_of_unknown_run_is_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _http.PostAsync("/api/runs/nope/stop", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/runs/nope")).StatusCode);
    }

    [Fact]
    public async Task Failed_stop_fire_keeps_the_button_available_for_a_new_attempt()
    {
        var factory = new WebFactory();
        var http = factory.CreateClient();
        var runId = (await (await http.PostAsJsonAsync("/api/runs", new { })).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("runId").GetString()!;
        factory.Gateway.FailWith = new SkailGatewayException("down");

        var res = await http.PostAsync($"/api/runs/{runId}/stop", null);

        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
        var run = await http.GetFromJsonAsync<JsonElement>($"/api/runs/{runId}");
        Assert.True(run.GetProperty("ui").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Stream_emits_run_snapshots_as_server_sent_events_until_the_run_ends()
    {
        var runId = (await StartAsync()).GetProperty("runId").GetString()!;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var res = await _http.GetAsync($"/api/runs/{runId}/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType!.MediaType);
        using var reader = new StreamReader(await res.Content.ReadAsStreamAsync(cts.Token));

        await _http.PostAsJsonAsync($"/internal/runs/{runId}/events",
            new RunEvent(runId, RunStage.Quoted, 1, new Quote("BRL", 5m, DateTimeOffset.UnixEpoch)), Json.Options);
        await _http.PostAsJsonAsync($"/internal/runs/{runId}/events", new RunEvent(runId, RunStage.Stopped, 1, null), Json.Options);

        var stages = new List<string>();
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            if (!line.StartsWith("data: ")) continue;
            stages.Add(JsonDocument.Parse(line[6..]).RootElement.GetProperty("stage").GetString()!);
        }

        Assert.Equal(new[] { "Starting", "Quoted", "Stopped" }, stages);
    }
}
