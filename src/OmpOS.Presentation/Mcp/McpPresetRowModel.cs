using System.Windows.Input;

namespace OmpOS.Presentation.Mcp;

/// <summary>One preset of the add-from-preset list: what it is, how it signs in, where its vendor documents it, and the button that adds it.</summary>
public sealed class McpPresetRowModel
{
    /// <summary>Creates a row.</summary>
    /// <param name="choice">The preset.</param>
    /// <param name="isAdded">Whether a server of the preset's name is already configured.</param>
    /// <param name="canAdd">Whether a workspace is open, so the preset can be written.</param>
    /// <param name="add">Adds the preset.</param>
    /// <exception cref="ArgumentNullException"><paramref name="choice"/> or <paramref name="add"/> is null.</exception>
    public McpPresetRowModel(McpPresetChoice choice, bool isAdded, bool canAdd, ICommand add)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(add);
        Choice = choice;
        AddCommand = add;
        IsAdded = isAdded;
        CanAdd = canAdd && !isAdded;
        TransportLabel = choice.Transport == McpTemplateChoice.Http ? McpCopy.PresetRemote : McpCopy.PresetLocal;
        DocumentationText = McpCopy.PresetDocumentation(choice.DocumentationAddress);
        AddLabel = isAdded ? McpCopy.PresetAddedLabel : McpCopy.PresetAddLabel;
        AddName = (isAdded ? McpCopy.PresetAddedLabel + " " : McpCopy.PresetAddLabel + " ") + choice.DisplayName;
        HasNote = choice.Note is not null;
        NoteText = choice.Note ?? string.Empty;
    }

    /// <summary>Gets the preset.</summary>
    public McpPresetChoice Choice { get; }

    /// <summary>Gets the name to show.</summary>
    public string DisplayName => Choice.DisplayName;

    /// <summary>Gets the sentence that says what the server offers.</summary>
    public string Description => Choice.Description;

    /// <summary>Gets how the server is reached: <c>Remote (HTTP)</c> or <c>Local (stdio)</c>.</summary>
    public string TransportLabel { get; }

    /// <summary>Gets the sentence that says how the server is signed in to.</summary>
    public string AuthNote => Choice.AuthNote;

    /// <summary>Gets the vendor page, as a sentence, for example <c>Vendor docs: https://linear.app/docs/mcp</c>.</summary>
    public string DocumentationText { get; }

    /// <summary>Gets whether the preset carries a note to read before switching it on.</summary>
    public bool HasNote { get; }

    /// <summary>Gets the note, or an empty string.</summary>
    public string NoteText { get; }

    /// <summary>Gets whether a server of the preset's name is already configured.</summary>
    public bool IsAdded { get; }

    /// <summary>Gets whether the button can add the preset: a workspace is open and the preset is not added yet.</summary>
    public bool CanAdd { get; }

    /// <summary>Gets the text of the button.</summary>
    public string AddLabel { get; }

    /// <summary>Gets the accessible name of the button, which names the preset.</summary>
    public string AddName { get; }

    /// <summary>Gets the command of the button.</summary>
    public ICommand AddCommand { get; }
}
