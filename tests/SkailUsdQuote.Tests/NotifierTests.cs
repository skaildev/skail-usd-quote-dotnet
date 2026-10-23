using System.Net;
using System.Text.Json;
using SkailUsdQuote.Contracts;
using SkailUsdQuote.Worker.Progress;

namespace SkailUsdQuote.Tests;

public class NotifierTests
{
    private static HttpProgressNotifier Notifier(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://web.test:8080/") });

    [Fact]
    public async Task Posts_the_event_to_the_internal_endpoint_of_the_run()
    {
        var handler = StubHandler.Status(HttpStatusCode.NoContent);
        var evt = new RunEvent("run-7", RunStage.Quoted, 2, new Quote("EUR", 0.9m, DateTimeOffset.UnixEpoch));

        await Notifier(handler).NotifyAsync(evt);

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://web.test:8080/internal/runs/run-7/events", request.RequestUri!.ToString());
        Assert.Equal(evt, JsonSerializer.Deserialize<RunEvent>(body, Json.Options));
    }

    [Fact]
    public async Task Throws_on_failure_so_the_command_is_retried()
    {
        var notifier = Notifier(StubHandler.Status(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            notifier.NotifyAsync(new RunEvent("r", RunStage.Stopped, 1, null)));
    }
}
