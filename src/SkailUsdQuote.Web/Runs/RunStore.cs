using System.Threading.Channels;
using SkailUsdQuote.Contracts;

namespace SkailUsdQuote.Web.Runs;

public enum StopRequest
{
    Requested,
    AlreadyRequested,
    AlreadyFinished,
    NotFound,
}

/// <summary>In-memory state of the runs. The durable state lives in skail; this is only the UI view.</summary>
public sealed class RunStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RunState> _runs = [];
    private readonly Dictionary<string, HashSet<(RunStage, int)>> _seen = [];
    private readonly Dictionary<string, List<Channel<RunState>>> _subscribers = [];

    public RunState Create(string runId, Currency currency)
    {
        lock (_gate)
        {
            var run = new RunState(runId, currency, RunStage.Starting, 0, [], null, false);
            _runs[runId] = run;
            _seen[runId] = [];
            return run;
        }
    }

    public RunState? Get(string runId)
    {
        lock (_gate) return _runs.GetValueOrDefault(runId);
    }

    public void Remove(string runId)
    {
        lock (_gate)
        {
            _runs.Remove(runId);
            _seen.Remove(runId);
            Complete(runId);
        }
    }

    /// <returns>true when the event changed the run; false when ignored (unknown run, finished run or duplicate).</returns>
    public bool Apply(RunEvent evt)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(evt.RunId, out var run) || run.IsTerminal) return false;
            if (!_seen[evt.RunId].Add((evt.Stage, evt.Iteration))) return false;

            var quotes = evt.Quote is not null && evt.Stage == RunStage.Quoted ? [.. run.Quotes, evt.Quote] : run.Quotes;
            var next = run with
            {
                Stage = evt.Stage,
                Iteration = Math.Max(run.Iteration, evt.Iteration),
                Quotes = quotes,
                Error = evt.Error ?? run.Error,
            };
            Publish(next);
            return true;
        }
    }

    public StopRequest RequestStop(string runId)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var run)) return StopRequest.NotFound;
            if (run.IsTerminal) return StopRequest.AlreadyFinished;
            if (run.StopRequested) return StopRequest.AlreadyRequested;

            Publish(run with { StopRequested = true });
            return StopRequest.Requested;
        }
    }

    /// <summary>Undoes <see cref="RequestStop"/> when the stop event could not be fired.</summary>
    public void CancelStopRequest(string runId)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(runId, out var run) && !run.IsTerminal && run.StopRequested)
                Publish(run with { StopRequested = false });
        }
    }

    public Subscription Subscribe(string runId)
    {
        var channel = Channel.CreateUnbounded<RunState>();
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(runId, out var list)) _subscribers[runId] = list = [];
            list.Add(channel);
        }
        return new Subscription(channel.Reader, () =>
        {
            lock (_gate)
            {
                if (_subscribers.TryGetValue(runId, out var list)) list.Remove(channel);
            }
        });
    }

    // must be called with _gate held
    private void Publish(RunState state)
    {
        _runs[state.RunId] = state;
        if (_subscribers.TryGetValue(state.RunId, out var list))
            foreach (var channel in list) channel.Writer.TryWrite(state);
        if (state.IsTerminal) Complete(state.RunId);
    }

    private void Complete(string runId)
    {
        if (!_subscribers.TryGetValue(runId, out var list)) return;
        foreach (var channel in list) channel.Writer.TryComplete();
    }

    public sealed class Subscription(ChannelReader<RunState> reader, Action onDispose) : IDisposable
    {
        public ChannelReader<RunState> Reader { get; } = reader;
        public void Dispose() => onDispose();
    }
}
