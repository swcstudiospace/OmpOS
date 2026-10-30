using System.Globalization;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Composer;

/// <summary>
/// The context window usage shown beside the composer chips: tokens used, the window size, the percentage and a state with two thresholds, 70 percent
/// (<see cref="ContextRingState.Warning"/>) and 90 percent (<see cref="ContextRingState.Critical"/>). omp reports <c>usage_update{size,used}</c> at the end of every turn; the host
/// feeds those two numbers to <see cref="ReportUsage"/>. When the window size is not known the ring says so and shows no percentage. Every state has words
/// (<see cref="Text"/>, <see cref="Tooltip"/>), so colour is never the only carrier. The attached mentions' estimate (<see cref="SetPending"/>) is shown as an addition.
/// </summary>
public sealed class ContextRingModel : ObservableModel
{
    /// <summary>The percentage from which the state is <see cref="ContextRingState.Warning"/>.</summary>
    public const int WarningPercent = 70;

    /// <summary>The percentage from which the state is <see cref="ContextRingState.Critical"/>.</summary>
    public const int CriticalPercent = 90;

    private long? _usedTokens;
    private long? _windowTokens;
    private long _pendingTokens;

    /// <summary>Gets the tokens the conversation uses, or <see langword="null"/> before the first usage report.</summary>
    public long? UsedTokens => _usedTokens;

    /// <summary>Gets the size of the context window in tokens, or <see langword="null"/> when not known.</summary>
    public long? WindowTokens => _windowTokens;

    /// <summary>Gets the estimated tokens of the attached mentions that the next message adds.</summary>
    public long PendingTokens => _pendingTokens;

    /// <summary>Gets whether a window size is known.</summary>
    public bool HasWindow => _windowTokens is > 0;

    /// <summary>Gets whether usage has been reported.</summary>
    public bool HasUsage => _usedTokens is not null;

    /// <summary>Gets the percentage of the window used, rounded down and capped at 100, or <see langword="null"/> when the window size or the usage is not known.</summary>
    public int? Percent => Percentage(_usedTokens);

    /// <summary>Gets the percentage the window will hold once the attached mentions are sent, or <see langword="null"/> when it cannot be told.</summary>
    public int? ProjectedPercent => _pendingTokens > 0 ? Percentage((_usedTokens ?? 0) + _pendingTokens) : Percent;

    /// <summary>Gets the state from <see cref="Percent"/>.</summary>
    public ContextRingState State => StateOf(Percent);

    /// <summary>Gets the state the window will be in once the attached mentions are sent.</summary>
    public ContextRingState ProjectedState => StateOf(ProjectedPercent);

    /// <summary>Gets the short text beside the ring, for example <c>42%</c>, <c>?</c> when the window is unknown, or empty before any usage.</summary>
    public string Text
    {
        get
        {
            if (_usedTokens is null && _pendingTokens == 0)
            {
                return string.Empty;
            }

            return ProjectedPercent is { } percent ? string.Create(CultureInfo.InvariantCulture, $"{percent}%") : "?";
        }
    }

    /// <summary>Gets the sweep of the ring in degrees, 0 to 360, for the head that draws the arc.</summary>
    public double SweepDegrees => (ProjectedPercent ?? 0) * 3.6;

    /// <summary>Gets the tooltip: the usage in tokens, the window, the percentage and the state in words.</summary>
    public string Tooltip
    {
        get
        {
            if (_usedTokens is null && _pendingTokens == 0)
            {
                return ContextRingCopy.NoUsageYet;
            }

            var used = ContextRingCopy.FormatTokens(_usedTokens ?? 0);
            var pending = _pendingTokens > 0 ? " plus about " + ContextRingCopy.FormatTokens(_pendingTokens) + " from attached items" : string.Empty;
            if (!HasWindow)
            {
                return "Context: " + used + " tokens used" + pending + ". " + ContextRingCopy.StateUnknown + ".";
            }

            return "Context: " + used + " of " + ContextRingCopy.FormatTokens(_windowTokens!.Value) + " tokens used" + pending + ", "
                + ProjectedPercent!.Value.ToString(CultureInfo.InvariantCulture) + "%. " + ContextRingCopy.StateWord(ProjectedState) + ".";
        }
    }

    /// <summary>Gets the accessible name: the ring's name followed by the tooltip text.</summary>
    public string AutomationName => ContextRingCopy.Name + ". " + Tooltip;

    /// <summary>Sets the usage the engine reported.</summary>
    /// <param name="usedTokens">The tokens in use; negative is treated as zero.</param>
    /// <param name="windowTokens">The window size, or <see langword="null"/>/non-positive to keep the size already known.</param>
    public void ReportUsage(long usedTokens, long? windowTokens = null)
    {
        _usedTokens = Math.Max(usedTokens, 0);
        if (windowTokens is > 0)
        {
            _windowTokens = windowTokens;
        }

        NotifyAll();
    }

    /// <summary>Sets the window size from the model's catalogue entry. A <see langword="null"/> or non-positive size marks the window unknown.</summary>
    /// <param name="windowTokens">The window size in tokens.</param>
    public void SetWindow(long? windowTokens)
    {
        _windowTokens = windowTokens is > 0 ? windowTokens : null;
        NotifyAll();
    }

    /// <summary>Sets the estimated tokens of the attached mentions.</summary>
    /// <param name="estimatedTokens">The estimate; negative is treated as zero.</param>
    public void SetPending(long estimatedTokens)
    {
        var value = Math.Max(estimatedTokens, 0);
        if (value == _pendingTokens)
        {
            return;
        }

        _pendingTokens = value;
        NotifyAll();
    }

    /// <summary>Forgets the usage of the conversation (a new conversation starts), and keeps the window size.</summary>
    public void Reset()
    {
        _usedTokens = null;
        _pendingTokens = 0;
        NotifyAll();
    }

    private static ContextRingState StateOf(int? percent) => percent switch
    {
        null => ContextRingState.Unknown,
        >= CriticalPercent => ContextRingState.Critical,
        >= WarningPercent => ContextRingState.Warning,
        _ => ContextRingState.Normal,
    };

    private int? Percentage(long? used)
    {
        if (used is null || _windowTokens is not > 0)
        {
            return null;
        }

        var percent = used.Value * 100 / _windowTokens.Value;
        return (int)Math.Min(percent, 100);
    }

    private void NotifyAll()
    {
        NotifyPropertyChanged(nameof(UsedTokens));
        NotifyPropertyChanged(nameof(WindowTokens));
        NotifyPropertyChanged(nameof(PendingTokens));
        NotifyPropertyChanged(nameof(HasWindow));
        NotifyPropertyChanged(nameof(HasUsage));
        NotifyPropertyChanged(nameof(Percent));
        NotifyPropertyChanged(nameof(ProjectedPercent));
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(ProjectedState));
        NotifyPropertyChanged(nameof(Text));
        NotifyPropertyChanged(nameof(SweepDegrees));
        NotifyPropertyChanged(nameof(Tooltip));
        NotifyPropertyChanged(nameof(AutomationName));
    }
}
