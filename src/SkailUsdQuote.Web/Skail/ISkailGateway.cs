using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Web.Skail;

public sealed class SkailGatewayException(string message, Exception? inner = null) : Exception(message, inner);

public interface ISkailGateway
{
    /// <summary>Starts the GetUsdQuote function; <paramref name="runId"/> is the execution id.</summary>
    Task TriggerAsync(string runId, Currency currency, CancellationToken ct = default);

    /// <summary>Fires the external stop event whose instance id is the run id.</summary>
    Task FireStopAsync(string runId, CancellationToken ct = default);
}
