using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Hosting;
using SkailUsdQuote.Worker.Progress;
using SkailUsdQuote.Worker.Quotes;

[assembly: VisibleToSkailPlatform]

var builder = Host.CreateApplicationBuilder(args);

// HttpClient request logs would show up as extra lines under each command in the skail Monitor
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

var webBaseUrl = Environment.GetEnvironmentVariable("WEB_BASE_URL") ?? "http://localhost:8080";
var provider = Environment.GetEnvironmentVariable("QUOTE_PROVIDER") ?? "http";
var quoteApiUrl = Environment.GetEnvironmentVariable("QUOTE_API_URL") ?? "https://economia.awesomeapi.com.br/";

if (provider.Equals("fake", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IQuoteProvider, FakeQuoteProvider>();
else
    builder.Services.AddHttpClient<IQuoteProvider, HttpQuoteProvider>(c =>
    {
        c.BaseAddress = new Uri(quoteApiUrl.EndsWith('/') ? quoteApiUrl : quoteApiUrl + "/");
        c.Timeout = TimeSpan.FromSeconds(15);
    });

builder.Services.AddHttpClient<IProgressNotifier, HttpProgressNotifier>(c =>
{
    c.BaseAddress = new Uri(webBaseUrl.EndsWith('/') ? webBaseUrl : webBaseUrl + "/");
    c.Timeout = TimeSpan.FromSeconds(10);
});

// reads SKAIL_WORKLOAD, SKAIL_NAMESPACE, SKAIL_KEY and SKAIL_SIDECAR from the environment (.env)
builder.UseSkail();

await builder.Build().RunAsync();
