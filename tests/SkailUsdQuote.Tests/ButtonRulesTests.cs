using SkailUsdQuote.Contracts;
using SkailUsdQuote.Web.Runs;

namespace SkailUsdQuote.Tests;

public class ButtonRulesTests
{
    private static RunState Run(RunStage stage, bool stopRequested = false) =>
        new("r1", Currency.BRL, stage, 0, [], null, stopRequested);

    [Fact]
    public void No_run_shows_enabled_start_and_enabled_currency()
    {
        var ui = ButtonRules.For(null);

        Assert.Equal(ButtonAction.Start, ui.Action);
        Assert.Equal("Start", ui.Label);
        Assert.True(ui.Enabled);
        Assert.True(ui.CurrencyEnabled);
    }

    [Theory]
    [InlineData(RunStage.Stopped)]
    [InlineData(RunStage.Completed)]
    [InlineData(RunStage.Failed)]
    public void Finished_run_goes_back_to_start(RunStage stage)
    {
        var ui = ButtonRules.For(Run(stage));

        Assert.Equal(ButtonAction.Start, ui.Action);
        Assert.True(ui.Enabled);
        Assert.True(ui.CurrencyEnabled);
    }

    [Theory]
    [InlineData(RunStage.Starting)]
    [InlineData(RunStage.Quoted)]
    public void Active_run_shows_enabled_stop_and_locks_currency(RunStage stage)
    {
        var ui = ButtonRules.For(Run(stage));

        Assert.Equal(ButtonAction.Stop, ui.Action);
        Assert.Equal("Stop", ui.Label);
        Assert.True(ui.Enabled);
        Assert.False(ui.CurrencyEnabled);
    }

    [Fact]
    public void Stop_requested_disables_the_button_until_the_worker_confirms()
    {
        var ui = ButtonRules.For(Run(RunStage.Quoted, stopRequested: true));

        Assert.Equal(ButtonAction.Stop, ui.Action);
        Assert.Equal("Stopping…", ui.Label);
        Assert.False(ui.Enabled);
        Assert.False(ui.CurrencyEnabled);
    }
}
