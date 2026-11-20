using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Hosting;
using SkailUsdQuote;
using SkailUsdQuote.Quotes;
using SkailUsdQuote.Runs;
using SkailUsdQuote.Skail;

[assembly: VisibleToSkailPlatform]

// usage: dotnet run [-- <currency> | <run id>]
//   no argument: starts a run in BRL; BRL, EUR, CNY or JPY: starts a run in that currency;
//   a run id: follows a run that already exists (to see it resume after the app restarts)
var argument = args.FirstOrDefault(a => !a.StartsWith('-'));
if (!RunTarget.TryParse(argument, out var target))
{
    Console.Error.WriteLine(
        $"Unknown argument '{argument}'. Use a currency ({string.Join(", ", Enum.GetNames<Currency>())}) or a run id.");
    return 1;
}

DotEnv.Load();
string[] required = ["SKAIL_WORKLOAD", "SKAIL_NAMESPACE", "SKAIL_KEY", "SKAIL_SIDECAR"];
var missing = required.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
if (missing.Length > 0)
{
    Console.Error.WriteLine($"Missing {string.Join(", ", missing)}. Copy .env.example to .env and fill it in.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

// keep the console for the quotes: only warnings and errors from the host and the libraries
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddHttpClient<AwesomeApiQuoteProvider>(http =>
{
    http.BaseAddress = AwesomeApiQuoteProvider.BaseAddress;
    http.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton(SkailApiOptions.FromEnvironment());
builder.Services.AddHttpClient<SkailApiClient>(http => http.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddSingleton(target);
builder.Services.AddSingleton(Console.In);
builder.Services.AddHostedService<QuoteRun>();

// hosts the worker that executes QuoteFunctions and QuoteCommands; reads the SKAIL_* variables
builder.UseSkail();

await builder.Build().RunAsync();
return 0;
