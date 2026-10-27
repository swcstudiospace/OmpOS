namespace OmpOS.Presentation.Commands;

/// <summary>What the composer does after a send was dispatched.</summary>
public enum SlashDispatchKind
{
    /// <summary>Send <see cref="SlashDispatch.Text"/> to the engine as the prompt.</summary>
    SendPrompt = 0,

    /// <summary>The command was handled here; show <see cref="SlashDispatch.Text"/> on the status line and send nothing.</summary>
    Handled = 1,
}
