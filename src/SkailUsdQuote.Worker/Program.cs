using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Hosting;
using SkailUsdQuote.Worker.Progress;
using SkailUsdQuote.Worker.Quotes;

[assembly: VisibleToSkailPlatform]

var builder = Host.CreateApplicationBuilder(args);

// Skail configuration (SKAIL_WORKLOAD, SKAIL_NAMESPACE, SKAIL_KEY, SKAIL_SIDECAR) comes from environment variables (.env)
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

builder.UseSkail();

await builder.Build().RunAsync();
