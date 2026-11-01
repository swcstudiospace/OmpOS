using System.Globalization;
using Autonogrammer.Presentation.Usage;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Usage;

/// <summary>
/// Shows per-session and per-day usage. Occupancy is last and peak, never a sum across turns. A missing cost shows
/// <see cref="UsageMeterModel.UsageUnavailable"/> instead of zero. The view has no plan limits and no billing.
/// </summary>
internal sealed partial class UsageMeterView : UserControl
{
    private bool _applying;

    /// <summary>Creates an empty meter.</summary>
    public UsageMeterView()
        : this(UsageMeterModel.Empty)
    {
    }

    /// <summary>Creates the meter over <paramref name="model"/>.</summary>
    /// <param name="model">The summed usage. Not null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public UsageMeterView(UsageMeterModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        DataContextChanged += (_, _) => Apply(DataContext as UsageMeterModel);
        Apply(model);
    }

    /// <summary>Gets the unavailable text, so a headless test can read the state without walking the tree.</summary>
    public TextBlock Unavailable => UnavailableText;

    private void Apply(UsageMeterModel? model)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        try
        {
            model ??= UsageMeterModel.Empty;
            DataContext = model;
            UnavailableText.IsVisible = model.CostUnavailable;
            UnavailableText.Text = model.UnavailableText;
            SessionRows.Children.Clear();
            foreach (var session in model.Sessions)
            {
                SessionRows.Children.Add(Block(
                    "Session " + session.SessionId,
                    Line(session.SessionId, session.LastUsed, session.PeakUsed, session.Size, session.Amount, session.Currency, session.Unavailable)));
            }

            DayRows.Children.Clear();
            foreach (var day in model.Days)
            {
                DayRows.Children.Add(Block(
                    "Day " + day.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Line(day.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), day.LastUsed, day.PeakUsed, 0, day.Amount, day.Currency, day.Unavailable)));
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private static TextBlock Block(string accessibleName, string text)
    {
        var block = new TextBlock { Text = text };
        AutomationProperties.SetName(block, accessibleName);
        return block;
    }

    private static string Line(string label, long lastUsed, long peakUsed, long window, decimal? amount, string? currency, bool unavailable)
    {
        var figure = unavailable || amount is null
            ? UsageMeterModel.UsageUnavailable
            : amount.Value.ToString(CultureInfo.InvariantCulture) + " " + currency;
        var windowText = window == 0 ? string.Empty : " window " + window.ToString(CultureInfo.InvariantCulture);
        return label + " last " + lastUsed.ToString(CultureInfo.InvariantCulture) + " peak " + peakUsed.ToString(CultureInfo.InvariantCulture) + windowText + " " + figure;
    }
}
