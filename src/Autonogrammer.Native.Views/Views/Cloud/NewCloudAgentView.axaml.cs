using System.ComponentModel;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Cloud;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cloud;

/// <summary>
/// The new cloud agent form: name, repositories, branches, environment, model, secret names (names only: no control takes a value), the
/// idle time, a first task, the cost confirmation, Create and Cancel. Create is enabled by the model only when the entries are valid and the
/// cost is confirmed. The view makes no host call itself; every action goes through <see cref="NewCloudAgentModel"/>.
/// </summary>
internal partial class NewCloudAgentView : UserControl
{
    private static readonly CloudChoice DefaultModel = new(string.Empty, CloudCopy.DefaultModelLabel);

    private NewCloudAgentModel? _model;
    private bool _syncing;

    /// <summary>Creates the form with no model.</summary>
    public NewCloudAgentView()
    {
        InitializeComponent();

        // Enter adds the entry while it tunnels down, before the text box can use it for itself.
        RepositoryBox.AddHandler(KeyDownEvent, OnRepositoryKeyDown, RoutingStrategies.Tunnel);
        SecretBox.AddHandler(KeyDownEvent, OnSecretKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Gets the name field.</summary>
    internal TextBox NameField => NameBox;

    /// <summary>Gets the repository address field.</summary>
    internal TextBox RepositoryField => RepositoryBox;

    /// <summary>Gets the Add repository button.</summary>
    internal Button AddRepository => AddRepositoryButton;

    /// <summary>Gets the list of added repositories.</summary>
    internal ItemsControl Repositories => RepositoryList;

    /// <summary>Gets the agent branch field.</summary>
    internal TextBox BranchField => BranchBox;

    /// <summary>Gets the secret name field.</summary>
    internal TextBox SecretField => SecretBox;

    /// <summary>Gets the Add secret name button.</summary>
    internal Button AddSecret => AddSecretButton;

    /// <summary>Gets the list of added secret names.</summary>
    internal ItemsControl Secrets => SecretList;

    /// <summary>Gets the idle time field.</summary>
    internal TextBox IdleField => IdleBox;

    /// <summary>Gets the cost confirmation checkbox.</summary>
    internal CheckBox CostConfirm => CostConfirmBox;

    /// <summary>Gets the cost sentence.</summary>
    internal TextBlock CostNotice => CostNoticeText;

    /// <summary>Gets the Create button.</summary>
    internal Button Create => CreateButton;

    /// <summary>Gets the Cancel button.</summary>
    internal Button Cancel => CancelButton;

    /// <summary>Gets the text that says why Create is unavailable.</summary>
    internal TextBlock WhyDisabled => WhyDisabledText;

    /// <summary>Gets the result of the latest create attempt.</summary>
    internal TextBlock Result => ResultText;

    /// <summary>Gets the environment pick list.</summary>
    internal ComboBox Environments => EnvironmentBox;

    /// <summary>Gets the model pick list.</summary>
    internal ComboBox Models => ModelBox;

    /// <summary>Gets the pick list of stored secret names.</summary>
    internal ComboBox KnownSecrets => KnownSecretBox;

    /// <summary>Moves focus to the first field, the name.</summary>
    internal void FocusFirstField() => NameBox.FocusWhenReady();

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext as NewCloudAgentModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
        }

        Bind();
    }

    private void Bind()
    {
        _syncing = true;
        try
        {
            var model = _model;
            EnvironmentBox.ItemsSource = model?.Environments;
            EnvironmentBox.SelectedItem = model?.Environments.FirstOrDefault(choice => string.Equals(choice.Id, model.EnvironmentId, StringComparison.Ordinal));
            ModelBox.ItemsSource = model is null ? null : (IReadOnlyList<CloudChoice>)[DefaultModel, .. model.Models];
            SelectModel();
        }
        finally
        {
            _syncing = false;
        }

        RenderRepositories();
        RenderSecrets();
        RenderSuggestions();
        RenderCreate();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NewCloudAgentModel.Repositories):
                RenderRepositories();
                break;
            case nameof(NewCloudAgentModel.SecretNames):
                RenderSecrets();
                break;
            case nameof(NewCloudAgentModel.SuggestedSecretNames):
                RenderSuggestions();
                break;
            case nameof(NewCloudAgentModel.IsCreating):
            case nameof(NewCloudAgentModel.Result):
                RenderCreate();
                break;
            default:
                break;
        }
    }

    private void RenderRepositories()
    {
        var items = _model?.Repositories.ToArray() ?? [];
        RepositoryList.ItemsSource = items;
        NoRepositoriesText.IsVisible = items.Length == 0;
    }

    private void RenderSecrets()
    {
        var items = _model?.SecretNames.ToArray() ?? [];
        SecretList.ItemsSource = items;
        NoSecretsText.IsVisible = items.Length == 0;
    }

    private void RenderSuggestions()
    {
        var items = _model?.SuggestedSecretNames.ToArray() ?? [];
        KnownSecretBox.ItemsSource = items;
        KnownSecretBox.SelectedIndex = items.Length > 0 ? 0 : -1;
        KnownSecretBlock.IsVisible = items.Length > 0;
    }

    private void RenderCreate()
    {
        var creating = _model?.IsCreating == true;
        CreateButton.Content = creating ? CloudCopy.CreatingLabel : CloudCopy.CreateLabel;
        var result = _model?.Result;
        ResultText.Text = result?.Message ?? string.Empty;
        ResultText.IsVisible = result is not null;
        ResultText.Classes.Set("tone-error", result is { IsCreated: false });
        ResultText.Classes.Set("tone-success", result is { IsCreated: true });
    }

    private void SelectModel()
    {
        var id = _model?.ModelId;
        ModelBox.SelectedItem = id is null
            ? DefaultModel
            : _model?.Models.FirstOrDefault(choice => string.Equals(choice.Id, id, StringComparison.Ordinal)) ?? DefaultModel;
    }

    private void OnEnvironmentChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _model is not null && EnvironmentBox.SelectedItem is CloudChoice choice)
        {
            _model.EnvironmentId = choice.Id;
        }
    }

    private void OnModelChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _model is not null && ModelBox.SelectedItem is CloudChoice choice)
        {
            _model.ModelId = choice.Id.Length == 0 ? null : choice.Id;
        }
    }

    private void OnRepositoryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _model is { CanAddRepository: true } model)
        {
            e.Handled = true;
            _ = model.AddRepository();
        }
    }

    private void OnSecretKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _model is { CanAddSecretName: true } model)
        {
            e.Handled = true;
            _ = model.AddSecretName();
        }
    }

    private void OnAddRepositoryClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.AddRepository() == true)
        {
            RepositoryBox.FocusWhenReady();
        }
    }

    private void OnRemoveRepositoryClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null && (sender as Control)?.DataContext is CloudRepository repository && _model.RemoveRepository(repository.Address))
        {
            RepositoryBox.FocusWhenReady();
        }
    }

    private void OnAddSecretClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.AddSecretName() == true)
        {
            SecretBox.FocusWhenReady();
        }
    }

    private void OnAddKnownSecretClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null && KnownSecretBox.SelectedItem is string name && _model.AddKnownSecretName(name))
        {
            SecretBox.FocusWhenReady();
        }
    }

    private void OnRemoveSecretClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null && (sender as Control)?.DataContext is string name && _model.RemoveSecretName(name))
        {
            SecretBox.FocusWhenReady();
        }
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        // CreateAsync reports its expected failures in the model's result and never throws them, so the task is not observed here.
        _ = _model?.CreateAsync(CancellationToken.None);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _model?.Cancel();
}
