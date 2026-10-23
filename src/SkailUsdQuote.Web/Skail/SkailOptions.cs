namespace SkailUsdQuote.Web.Skail;

public sealed class SkailOptions
{
    /// <summary>Base URL of the skail HTTP API for the environment (SKAIL_BASE_URL, shown in the skail portal).</summary>
    public string BaseUrl { get; init; } = "";

    /// <summary>SKAIL_NAMESPACE (tenant/environment), sent in the Skail-namespace header.</summary>
    public string Namespace { get; init; } = "";

    /// <summary>SKAIL_KEY, sent in the skail-key header. Sensitive.</summary>
    public string Key { get; init; } = "";

    /// <summary>SKAIL_WORKLOAD as name:version, exactly as registered in the portal.</summary>
    public string Workload { get; init; } = "";

    public static SkailOptions FromEnvironment() => new()
    {
        BaseUrl = Environment.GetEnvironmentVariable("SKAIL_BASE_URL") ?? "",
        Namespace = Environment.GetEnvironmentVariable("SKAIL_NAMESPACE") ?? "",
        Key = Environment.GetEnvironmentVariable("SKAIL_KEY") ?? "",
        Workload = Environment.GetEnvironmentVariable("SKAIL_WORKLOAD") ?? "",
    };
}
