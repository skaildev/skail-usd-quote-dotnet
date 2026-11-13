# skail USD quote (.NET 8)

[Português (Brasil)](README.pt-BR.md)

A console app with a skail function that fetches the US dollar quote, waits 30 seconds with a durable delay and fetches again, until you press **Enter**.

It shows three skail features:

- a **durable delay**: close the app in the middle of a wait, start it again and the run goes on from where it was;
- an **external event** (`STOP_TRACKING`) that stops the run in the middle of a wait;
- **automatic retries**: when the quote API fails, skail tries again by itself.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A skail environment with a workload registered for the `SKAIL_WORKLOAD` in `.env` (`usdquote:v1.0.0` by default). The [Quickstart](https://docs.skail.dev/comece-aqui/quickstart) shows how to create the account, the environment and the workload.

The project uses `Skail.Platform.Runtime` **2.3.0-rc.4**.

## Run

```bash
cp .env.example .env    # fill in SKAIL_SIDECAR, SKAIL_NAMESPACE and SKAIL_KEY (portal > Environments & keys > Connect)
dotnet run              # 1 USD in BRL
dotnet run -- EUR       # or EUR, CNY, JPY
```

```text
Run 130142cd-353b-4637-a53c-fc82435e3f6d started: 1 USD in BRL every 30 s, until you stop it.
Press Enter to stop the run, or Ctrl+C to exit and leave it running in skail.
[130142cd] #1  1 USD = 5.3578 BRL  (fetched 15:09:27, source 15:09:00)
[130142cd] #2  1 USD = 5.3578 BRL  (fetched 15:09:58, source 15:09:00)

STOP_TRACKING sent: the run ends during the current wait. Press Ctrl+C to exit.
[130142cd] Stopped after 2 quotes.
```

- **Enter** stops the run: the app fires `STOP_TRACKING`.
- **Ctrl+C** closes the app; the run stays in skail, waiting.
- **fetched** is when the function fetched the quote; **source** is when AwesomeAPI published the rate, which changes only every few minutes.

## How it works

The app does two jobs in one process: it hosts the worker that runs the function (`builder.UseSkail()`), and it starts and stops the run through the skail HTTP API:

```http
POST {SKAIL_SIDECAR}/api/v1/trigger/usdquote/v1.0.0/TrackUsdQuote/{runId}
["{runId}", "BRL"]

POST {SKAIL_SIDECAR}/api/v1/fire/STOP_TRACKING/{runId}
{}
```

The function, in [`QuoteFunctions.cs`](QuoteFunctions.cs):

```csharp
[SkailFunction(retryCount: 0)]
public async SkailTask<int> TrackUsdQuote(string runId, string currency)
{
    var stopEvent = SkailTask.WaitForEvent(Events.StopTracking, runId);

    for (var quoteNumber = 1; ; quoteNumber++)
    {
        var quote = await FetchQuote(currency);
        await ReportQuote(runId, quoteNumber, quote);

        var interval = SkailTask.DelaySeconds(IntervalSeconds);
        if (await SkailTask.WhenAny(stopEvent, interval) == stopEvent)
        {
            await ReportStopped(runId, quoteNumber);
            return quoteNumber;
        }
    }
}
```

- The function only orchestrates. Each time the run wakes up (after a delay, an event or an app restart), skail runs it again from the start (replay).
- The I/O lives in commands: `FetchQuote` calls the quote API, and `ReportQuote` and `ReportStopped` print to the console. A command that completed is never run again, so a replay does not fetch again nor print the same line twice.
- The wait for `STOP_TRACKING` is created once and reused in every `WhenAny` against the 30-second delay: whichever comes first wins.

| File | What it has |
|---|---|
| `QuoteFunctions.cs` | The function and its commands. |
| `Quotes/AwesomeApiQuoteProvider.cs` | The call to AwesomeAPI. |
| `Runs/QuoteRun.cs` | Starts the run and fires `STOP_TRACKING` on Enter. |
| `Skail/SkailApiClient.cs` | Trigger and fire through the skail HTTP API. |
| `Program.cs` | Reads `.env` and the argument, and wires everything up. |

## See the durability

1. Run the app and wait for the first quote.
2. During the 30-second wait, press **Ctrl+C**. The app closes; the run stays in skail.
3. Start the app again with the run id it printed: `dotnet run -- <run id>`. With an id, the app starts no new run: it only hosts the worker again, and skail hands it the run that was waiting.
4. The next quote arrives on schedule. Press **Enter** to stop.

In the portal, open the [Monitor](https://docs.skail.dev/referencia/portal/monitor) of your environment and click the run: each quote shows up as `FetchQuote` and `ReportQuote`, each wait as a `WhenAny` with its `Delay`, and the stop as `ReportStopped`.

## When the quote API answers 429

AwesomeAPI answers `429` when its quota runs out. `FetchQuote` throws, and skail retries it with a growing wait (15 s, 30 s, 1 min, 2 min, 4 min), up to 5 times (`[SkailCommand(retryCount: 5)]`). The console stays quiet meanwhile; in the Monitor, `FetchQuote` shows each attempt. When the quota comes back, the next attempt succeeds and the run goes on. If it does not come back in time, the run ends as Failed in the Monitor (`retryCount: 0` on the function).

## Notes

- The run has no limit: it goes on until you stop it. Stop the runs you start, so they do not keep running in your environment.
- Apps running the same `SKAIL_WORKLOAD` on the same environment share the same runs: use one workload (or environment) per person.

## Disclaimer

**This is a demo, not production code.** It exists only to show how [skail](https://docs.skail.dev) works and is provided "as is", without warranty of any kind. It uses a free third-party quote API ([AwesomeAPI](https://docs.awesomeapi.com.br/api-de-moedas)) that may limit or refuse requests at any time. Do not rely on its quotes for any decision.

## License

[Apache-2.0](LICENSE)
