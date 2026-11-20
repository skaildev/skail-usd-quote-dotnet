using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkailUsdQuote.Skail;

namespace SkailUsdQuote.Runs;

/// <summary>
/// Starts the run (unless it already exists) and sends STOP_TRACKING when Enter is pressed.
/// The worker that executes the run is hosted in the same process (UseSkail), so its output shows up here.
/// </summary>
public sealed class QuoteRun(SkailApiClient skail, RunTarget target, TextReader input, ILogger<QuoteRun> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (target.NewRunCurrency is { } currency)
        {
            await skail.TriggerAsync(target.RunId, currency, stoppingToken);
            Console.WriteLine(
                $"Run {target.RunId} started: 1 USD in {currency} every 30 s, until you stop it.");
        }
        else
        {
            Console.WriteLine($"Following run {target.RunId}: it continues from where it is.");
        }
        Console.WriteLine("Press Enter to stop the run, or Ctrl+C to exit and leave it running in skail.");

        // ReadLine cannot be cancelled: WaitAsync lets Ctrl+C shut down without waiting for it
        var line = await Task.Run(input.ReadLine, stoppingToken).WaitAsync(stoppingToken);
        if (line is null) return; // no console input (redirected or closed): only Ctrl+C ends the app

        try
        {
            await skail.FireStopAsync(target.RunId, stoppingToken);
            Console.WriteLine("STOP_TRACKING sent: the run ends during the current wait. Press Ctrl+C to exit.");
        }
        catch (SkailApiException ex)
        {
            logger.LogError("Could not send STOP_TRACKING: {Error}", ex.Message);
        }
    }
}
