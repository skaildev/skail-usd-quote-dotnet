using SkailUsdQuote.Contracts;
using SkailUsdQuote.Web.Runs;

namespace SkailUsdQuote.Tests;

public class RunStoreTests
{
    private static Quote Q(decimal rate) => new("BRL", rate, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Create_starts_run_with_chosen_currency()
    {
        var store = new RunStore();

        var run = store.Create("r1", Currency.EUR);

        Assert.Equal(RunStage.Starting, run.Stage);
        Assert.Equal(Currency.EUR, run.Currency);
        Assert.Empty(run.Quotes);
        Assert.Same(run, store.Get("r1"));
    }

    [Fact]
    public void Quoted_event_appends_quote_and_updates_quote_count()
    {
        var store = new RunStore();
        store.Create("r1", Currency.BRL);

        store.Apply(new RunEvent("r1", RunStage.Quoted, 1, Q(5.0m)));
        store.Apply(new RunEvent("r1", RunStage.Quoted, 2, Q(5.1m)));

        var run = store.Get("r1")!;
        Assert.Equal(RunStage.Quoted, run.Stage);
        Assert.Equal(2, run.QuoteCount);
        Assert.Equal(new[] { 5.0m, 5.1m }, run.Quotes.Select(q => q.Rate));
    }

    [Fact]
    public void Repeated_event_is_ignored_for_the_same_stage_and_quote_number()
    {
        var store = new RunStore();
        store.Create("r1", Currency.BRL);
        var evt = new RunEvent("r1", RunStage.Quoted, 1, Q(5.0m));

        Assert.True(store.Apply(evt));
        Assert.False(store.Apply(evt));

        Assert.Single(store.Get("r1")!.Quotes);
    }

    [Fact]
    public void Failure_keeps_the_count_of_quotes_received()
    {
        var store = new RunStore();
        store.Create("r1", Currency.BRL);
        store.Apply(new RunEvent("r1", RunStage.Quoted, 1, Q(5.0m)));

        store.Apply(new RunEvent("r1", RunStage.Failed, 2, null, "Quote API answered 429"));

        var run = store.Get("r1")!;
        Assert.Equal(RunStage.Failed, run.Stage);
        Assert.Equal(1, run.QuoteCount);
        Assert.Equal("Quote API answered 429", run.Error);
    }

    [Theory]
    [InlineData(RunStage.Stopped)]
    [InlineData(RunStage.Completed)]
    [InlineData(RunStage.Failed)]
    public void Terminal_stage_ends_the_run_and_ignores_later_events(RunStage terminal)
    {
        var store = new RunStore();
        store.Create("r1", Currency.BRL);
        store.Apply(new RunEvent("r1", RunStage.Quoted, 1, Q(5.0m)));

        store.Apply(new RunEvent("r1", terminal, 1, null, terminal == RunStage.Failed ? "boom" : null));
        var late = store.Apply(new RunEvent("r1", RunStage.Quoted, 2, Q(5.1m)));

        var run = store.Get("r1")!;
        Assert.False(late);
        Assert.Equal(terminal, run.Stage);
        Assert.True(run.IsTerminal);
        Assert.Single(run.Quotes);
        Assert.Equal(terminal == RunStage.Failed ? "boom" : null, run.Error);
    }

    [Fact]
    public void Event_for_unknown_run_is_ignored()
    {
        Assert.False(new RunStore().Apply(new RunEvent("nope", RunStage.Quoted, 1, Q(1m))));
    }

    [Fact]
    public void RequestStop_marks_stopping_once_and_keeps_quotes_coming_until_stopped()
    {
        var store = new RunStore();
        store.Create("r1", Currency.BRL);

        Assert.Equal(StopRequest.Requested, store.RequestStop("r1"));
        Assert.Equal(StopRequest.AlreadyRequested, store.RequestStop("r1"));
        Assert.True(store.Get("r1")!.StopRequested);

        store.Apply(new RunEvent("r1", RunStage.Stopped, 1, null));
        Assert.Equal(StopRequest.AlreadyFinished, store.RequestStop("r1"));
    }

    [Fact]
    public void RequestStop_of_unknown_run_is_not_found()
    {
        Assert.Equal(StopRequest.NotFound, new RunStore().RequestStop("nope"));
    }

    [Fact]
    public async Task Subscribers_receive_snapshots_in_order()
    {
        var store = new RunStore();
        store.Create("r1", Currency.BRL);
        using var sub = store.Subscribe("r1");

        store.Apply(new RunEvent("r1", RunStage.Quoted, 1, Q(5.0m)));
        store.Apply(new RunEvent("r1", RunStage.Stopped, 1, null));

        var first = await sub.Reader.ReadAsync();
        var second = await sub.Reader.ReadAsync();
        Assert.Equal(RunStage.Quoted, first.Stage);
        Assert.Equal(RunStage.Stopped, second.Stage);
    }
}
