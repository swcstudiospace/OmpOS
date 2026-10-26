namespace OmpOS.Presentation.Settings;

/// <summary>
/// The General page: the motion setting and a read-only privacy statement. The statement is text only; OmpOS sends nothing itself,
/// so there is no telemetry switch to offer. Motion is a draft until <see cref="EditableSettingsPage{TValues}.SaveAsync"/> writes it through the source.
/// </summary>
public sealed class GeneralPage : EditableSettingsPage<GeneralSettingsValues>
{
    private static readonly MotionOption[] AllOptions =
    [
        new(MotionPreference.System, SettingsCopy.MotionSystemLabel, SettingsCopy.MotionSystemDescription),
        new(MotionPreference.Reduced, SettingsCopy.MotionReducedLabel, SettingsCopy.MotionReducedDescription),
        new(MotionPreference.Full, SettingsCopy.MotionFullLabel, SettingsCopy.MotionFullDescription),
    ];

    private readonly IGeneralSettingsSource _source;

    /// <summary>Initializes a new instance of the <see cref="GeneralPage"/> class.</summary>
    /// <param name="source">Where the values are read and written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public GeneralPage(IGeneralSettingsSource source)
        : base(SettingsSection.General, SettingsCopy.GeneralTitle, GeneralSettingsValues.Defaults)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the motion choices in display order.</summary>
    public static IReadOnlyList<MotionOption> MotionOptions => AllOptions;

    /// <summary>Gets the motion preference in the draft.</summary>
    public MotionPreference Motion => Draft.Motion;

    /// <summary>Gets the choice the draft selects.</summary>
    public MotionOption SelectedMotionOption => AllOptions.Single(option => option.Value == Draft.Motion);

    /// <summary>Sets the motion preference in the draft. Ignored while the page is not ready.</summary>
    /// <param name="motion">The preference.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="motion"/> is not a defined preference.</exception>
    public void SetMotion(MotionPreference motion)
    {
        if (!Enum.IsDefined(motion))
        {
            throw new ArgumentOutOfRangeException(nameof(motion), motion, "Unknown motion preference.");
        }

        SetDraft(Draft with { Motion = motion });
    }

    /// <inheritdoc/>
    protected override Task<GeneralSettingsValues> LoadValuesAsync(CancellationToken cancellationToken) => _source.LoadAsync(cancellationToken);

    /// <inheritdoc/>
    protected override Task SaveValuesAsync(GeneralSettingsValues values, CancellationToken cancellationToken) =>
        _source.SaveAsync(values, cancellationToken);

    /// <inheritdoc/>
    protected override void NotifyValueProperties()
    {
        NotifyPropertyChanged(nameof(Motion));
        NotifyPropertyChanged(nameof(SelectedMotionOption));
    }
}
