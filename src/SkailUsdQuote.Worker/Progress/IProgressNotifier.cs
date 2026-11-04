using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Worker.Progress;

public interface IProgressNotifier
{
    Task NotifyAsync(RunEvent evt, CancellationToken ct = default);
}
