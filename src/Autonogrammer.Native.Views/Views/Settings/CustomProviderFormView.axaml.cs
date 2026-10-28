using Avalonia;
using Avalonia.Controls;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The form of a custom provider: the fields of a <see cref="CustomProviderDraft"/> with the draft's inline message under each field, the
/// protocol and authentication choices, the capability checkboxes and the plain http switch with its warning. The form draws only the
/// fields; the page that hosts it supplies Save and Cancel and the error of a refused save.
/// </summary>
internal partial class CustomProviderFormView : UserControl
{
    private readonly ModelWatcher<CustomProviderDraft> _watch;
    private bool _syncing;
    private bool _attached;

    /// <summary>Creates a form with no draft.</summary>
    public CustomProviderFormView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<CustomProviderDraft>(SyncChoices);
        FillChoices();
        ProtocolBox.SelectionChanged += OnProtocolChanged;
        AuthBox.SelectionChanged += OnAuthChanged;
    }

    /// <summary>Gets the draft the form edits, or null.</summary>
    internal CustomProviderDraft? Draft => DataContext as CustomProviderDraft;

    /// <summary>Gets the display name entry.</summary>
    internal TextBox DisplayName => DisplayNameBox;

    /// <summary>Gets the id entry.</summary>
    internal TextBox Id => IdBox;

    /// <summary>Gets the protocol choice.</summary>
    internal ComboBox Protocol => ProtocolBox;

    /// <summary>Gets the endpoint entry.</summary>
    internal TextBox Endpoint => EndpointBox;

    /// <summary>Gets the authentication choice.</summary>
    internal ComboBox Auth => AuthBox;

    /// <summary>Gets the model list entry.</summary>
    internal TextBox Models => ModelsBox;

    /// <summary>Gets the chat capability checkbox.</summary>
    internal CheckBox Chat => ChatBox;

    /// <summary>Gets the plain http checkbox.</summary>
    internal CheckBox PlainHttp => PlainHttpBox;

    /// <summary>Gets the message controls of the entries, in field order: name, id, endpoint, models.</summary>
    internal IReadOnlyList<TextBlock> ErrorLines => [DisplayNameError, IdError, EndpointError, ModelsError];

    /// <summary>Gets the line that says where the draft came from, when it came from a template.</summary>
    internal TextBlock TemplateNoteText => TemplateNoteLine;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(Draft);
        SyncChoices();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            _watch.Set(Draft);
        }

        SyncChoices();
    }

    private void FillChoices()
    {
        foreach (var protocol in Enum.GetValues<CustomProviderProtocol>())
        {
            ProtocolBox.Items.Add(new ComboBoxItem { Content = ModelsCopy.ProtocolLabel(protocol), Tag = protocol });
        }

        foreach (var auth in Enum.GetValues<CustomProviderAuth>())
        {
            AuthBox.Items.Add(new ComboBoxItem { Content = ModelsCopy.AuthChoiceLabel(auth), Tag = auth });
        }
    }

    private void SyncChoices()
    {
        _syncing = true;
        try
        {
            var draft = Draft;
            ProtocolBox.SelectedItem = draft is null
                ? null
                : ProtocolBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag is CustomProviderProtocol protocol && protocol == draft.Protocol);
            AuthBox.SelectedItem = draft is null
                ? null
                : AuthBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag is CustomProviderAuth auth && auth == draft.Auth);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnProtocolChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Draft is { } draft && ProtocolBox.SelectedItem is ComboBoxItem { Tag: CustomProviderProtocol protocol })
        {
            draft.Protocol = protocol;
        }
    }

    private void OnAuthChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Draft is { } draft && AuthBox.SelectedItem is ComboBoxItem { Tag: CustomProviderAuth auth })
        {
            draft.Auth = auth;
        }
    }
}
