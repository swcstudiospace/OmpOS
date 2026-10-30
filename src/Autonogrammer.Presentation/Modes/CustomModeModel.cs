using System.Collections.ObjectModel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Modes;

/// <summary>
/// Custom modes of one session (Cursor's Use as Mode): any user-invocable skill can be pinned so its body goes in front of every prompt of the session until the user exits the mode. The model lists the
/// skills, pins one, shows the mode chip and builds the prompt block; the head calls <see cref="IPromptPreamble.Apply"/> wherever it sends a prompt to the engine.
/// </summary>
/// <remarks>
/// <para>
/// Alt+Enter: on a skill highlighted in the slash palette, Alt+Enter (Option+Enter on macOS) means Use as Mode, and a plain accept inserts the skill's slash token as before.
/// <see cref="AcceptFromPaletteAsync"/> implements that choice so every head gets the same rule.
/// </para>
/// <para>The block put in front of a prompt is <c>&lt;mode name="..."&gt;</c>, the body, <c>&lt;/mode&gt;</c>, a blank line, then the prompt. A body longer than <see cref="CustomModeCopy.MaxBodyChars"/> is refused, not cut.</para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class CustomModeModel : ObservableModel, IPromptPreamble
{
    private static readonly string[] ChipColors = ["default", "green", "cyan", "blue", "purple", "magenta", "orange", "yellow", "red", "brand"];

    private readonly IModeSkillSource _source;
    private readonly ObservableCollection<ModeSkill> _skills = [];
    private ActiveMode? _active;
    private string _statusText = string.Empty;
    private bool _hasError;
    private bool _isBusy;

    /// <summary>Creates the modes of one session.</summary>
    /// <param name="source">Lists the skills and reads their bodies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public CustomModeModel(IModeSkillSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Skills = new ReadOnlyObservableCollection<ModeSkill>(_skills);
    }

    /// <summary>Gets the skills that can be pinned.</summary>
    public ReadOnlyObservableCollection<ModeSkill> Skills { get; }

    /// <summary>Gets the sentence shown while there is no skill, or an empty string.</summary>
    public string EmptyText => _skills.Count == 0 ? CustomModeCopy.Empty : string.Empty;

    /// <summary>Gets the pinned mode, or <see langword="null"/>.</summary>
    public ActiveMode? Active => _active;

    /// <inheritdoc/>
    public bool IsActive => _active is not null;

    /// <summary>Gets the text of the mode chip, or an empty string while no mode is pinned.</summary>
    public string ChipText => _active is null ? string.Empty : CustomModeCopy.ChipText(_active.Skill.Name);

    /// <summary>Gets the accessible name of the mode chip, or an empty string while no mode is pinned.</summary>
    public string ChipName => _active is null ? string.Empty : CustomModeCopy.ChipName(_active.Skill.Name);

    /// <summary>Gets the colour key of the chip: one of the frontmatter colours <c>default</c>, <c>green</c>, <c>cyan</c>, <c>blue</c>, <c>purple</c>, <c>magenta</c>, <c>orange</c>, <c>yellow</c>, <c>red</c> or <c>brand</c>. The head maps the key to its theme tokens.</summary>
    public string ChipColorKey
    {
        get
        {
            var color = _active?.Skill.Color?.Trim();
            return ChipColors.FirstOrDefault(known => string.Equals(known, color, StringComparison.OrdinalIgnoreCase)) ?? "default";
        }
    }

    /// <summary>Gets the frontmatter icon of the pinned skill, or <see langword="null"/>.</summary>
    public string? ChipIcon => _active?.Skill.Icon;

    /// <summary>Gets the last sentence for the status line and assistive technology.</summary>
    public string StatusText => _statusText;

    /// <summary>Gets whether <see cref="StatusText"/> reports a failure.</summary>
    public bool HasError => _hasError;

    /// <summary>Gets whether skills are loading or a skill is being read.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Reads the skills that can be pinned.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when <see cref="Skills"/> is current.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        SetProperty(ref _isBusy, true, nameof(IsBusy));
        try
        {
            var skills = await _source.ListAsync(cancellationToken).ConfigureAwait(true);
            _skills.Clear();
            foreach (var skill in skills)
            {
                _skills.Add(skill);
            }

            NotifyPropertyChanged(nameof(EmptyText));
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            SetStatus(CustomModeCopy.Failed(exception.Message), isError: true);
        }
        finally
        {
            SetProperty(ref _isBusy, false, nameof(IsBusy));
        }
    }

    /// <summary>Pins a skill as the session's mode, replacing the mode that was pinned.</summary>
    /// <param name="name">The skill name, as the palette lists it.</param>
    /// <param name="cancellationToken">Cancels the read of the skill.</param>
    /// <returns><see langword="true"/> when the mode is on. On <see langword="false"/> the mode that was pinned before stays, and the status says why.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public async Task<bool> UseAsModeAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var skill = _skills.FirstOrDefault(candidate => string.Equals(candidate.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (skill is null)
        {
            SetStatus(CustomModeCopy.Unknown(name.Trim()), isError: true);
            return false;
        }

        SetProperty(ref _isBusy, true, nameof(IsBusy));
        try
        {
            var body = await _source.ReadBodyAsync(skill, cancellationToken).ConfigureAwait(true);
            if (body is null)
            {
                SetStatus(CustomModeCopy.Unreadable(skill.Name), isError: true);
                return false;
            }

            var trimmed = body.Trim();
            if (trimmed.Length == 0)
            {
                SetStatus(CustomModeCopy.NoBody(skill.Name), isError: true);
                return false;
            }

            if (trimmed.Length > CustomModeCopy.MaxBodyChars)
            {
                SetStatus(CustomModeCopy.TooLarge(skill.Name), isError: true);
                return false;
            }

            _active = new ActiveMode(skill, trimmed);
            NotifyActive();
            SetStatus(CustomModeCopy.Started(skill.Name), isError: false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            SetStatus(CustomModeCopy.Failed(exception.Message), isError: true);
            return false;
        }
        finally
        {
            SetProperty(ref _isBusy, false, nameof(IsBusy));
        }
    }

    /// <summary>Applies the palette's accept key to a highlighted skill: Alt+Enter pins it, a plain accept leaves the palette to insert the skill's token.</summary>
    /// <param name="name">The highlighted skill.</param>
    /// <param name="altEnter"><see langword="true"/> for Alt+Enter (Option+Enter on macOS).</param>
    /// <param name="cancellationToken">Cancels the read of the skill.</param>
    /// <returns>What the head does next.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public async Task<ModePaletteResult> AcceptFromPaletteAsync(string name, bool altEnter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!altEnter)
        {
            return ModePaletteResult.InsertSkillToken;
        }

        return await UseAsModeAsync(name, cancellationToken).ConfigureAwait(true) ? ModePaletteResult.ModeActivated : ModePaletteResult.Failed;
    }

    /// <summary>Exits the mode: prompts go as the user wrote them again.</summary>
    /// <returns><see langword="false"/> when no mode was pinned.</returns>
    public bool Clear()
    {
        if (_active is null)
        {
            return false;
        }

        _active = null;
        NotifyActive();
        SetStatus(CustomModeCopy.Exited, isError: false);
        return true;
    }

    /// <inheritdoc/>
    public string Apply(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return _active is null
            ? prompt
            : "<mode name=\"" + _active.Skill.Name.Replace("\"", "'", StringComparison.Ordinal) + "\">\n" + _active.Body + "\n</mode>\n\n" + prompt;
    }

    private void NotifyActive()
    {
        NotifyPropertyChanged(nameof(Active));
        NotifyPropertyChanged(nameof(IsActive));
        NotifyPropertyChanged(nameof(ChipText));
        NotifyPropertyChanged(nameof(ChipName));
        NotifyPropertyChanged(nameof(ChipColorKey));
        NotifyPropertyChanged(nameof(ChipIcon));
    }

    private void SetStatus(string text, bool isError)
    {
        SetProperty(ref _statusText, text, nameof(StatusText));
        SetProperty(ref _hasError, isError, nameof(HasError));
    }
}
