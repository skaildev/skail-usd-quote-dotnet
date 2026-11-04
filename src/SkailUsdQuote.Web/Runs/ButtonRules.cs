namespace SkailUsdQuote.Web.Runs;

public enum ButtonAction
{
    Start,
    Stop,
}

/// <param name="CurrencyEnabled">The currency can only be changed before a run starts.</param>
public sealed record UiState(ButtonAction Action, string Label, bool Enabled, bool CurrencyEnabled);

/// <summary>Single source of truth for the Start/Stop toggle: derived only from the run state.</summary>
public static class ButtonRules
{
    public static UiState For(RunState? run)
    {
        if (run is null || run.IsTerminal)
            return new UiState(ButtonAction.Start, "Start", Enabled: true, CurrencyEnabled: true);

        return run.StopRequested
            ? new UiState(ButtonAction.Stop, "Stopping…", Enabled: false, CurrencyEnabled: false)
            : new UiState(ButtonAction.Stop, "Stop", Enabled: true, CurrencyEnabled: false);
    }
}
