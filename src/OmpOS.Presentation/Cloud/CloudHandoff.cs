using System.Globalization;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Presentation.Cloud;

/// <summary>
/// The <see cref="ICloudHandoff"/> over an <see cref="ICloudAgentsHost"/>. It checks every entry with <see cref="CloudInputValidator"/>, needs
/// the cost confirmation, and only then reads the setup and creates the agent.
/// </summary>
public sealed class CloudHandoff : ICloudHandoff
{
    private const int MaxDerivedNameChars = 59;
    private const int MaxMessageNameChars = 60;

    private readonly ICloudAgentsHost _host;

    /// <summary>Initializes a new instance of the <see cref="CloudHandoff"/> class.</summary>
    /// <param name="host">Where the agent is created.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public CloudHandoff(ICloudAgentsHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
    }

    /// <inheritdoc/>
    public async Task<CloudHandoffResult> StartAsync(CloudHandoffRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (CheckEntries(request) is { } problem)
        {
            return new CloudHandoffResult(CloudHandoffOutcome.NeedsInput, problem);
        }

        if (!request.CostConfirmed)
        {
            return new CloudHandoffResult(CloudHandoffOutcome.NeedsCostConfirmation, CloudCopy.CostNotConfirmed);
        }

        try
        {
            var setup = await _host.GetSetupAsync(cancellationToken).ConfigureAwait(true);
            if (!setup.IsConfigured)
            {
                return new CloudHandoffResult(CloudHandoffOutcome.NotAvailable, setup.NotConfiguredReason is { Length: > 0 } reason ? reason : CloudCopy.NotConfiguredShort);
            }

            var idle = request.IdleMinutes ?? setup.DefaultIdleMinutes;
            if (CloudInputValidator.ValidateIdleMinutes(idle.ToString(CultureInfo.InvariantCulture), setup.MaxIdleMinutes, out _) is { } idleError)
            {
                return new CloudHandoffResult(CloudHandoffOutcome.NeedsInput, idleError);
            }

            var agent = await _host.CreateAsync(
                new CloudAgentRequest
                {
                    Name = NameOf(request),
                    Repositories = [.. request.Repositories],
                    Branch = Blank(request.Branch),
                    BaseBranch = Blank(request.BaseBranch),
                    EnvironmentId = request.EnvironmentId ?? setup.DefaultEnvironmentId,
                    SecretNames = [.. request.SecretNames],
                    IdleMinutes = idle,
                    Model = request.Model,
                    Prompt = request.Prompt.Trim(),
                },
                cancellationToken).ConfigureAwait(true);
            return new CloudHandoffResult(CloudHandoffOutcome.Started, CloudCopy.HandoffStarted(PanelText.Plain(agent.Name, MaxMessageNameChars)), agent);
        }
        catch (OperationCanceledException)
        {
            return new CloudHandoffResult(CloudHandoffOutcome.Failed, CloudCopy.CreateCancelled);
        }
        catch (Exception fault) when (CloudFailures.IsHandled(fault))
        {
            return Describe(fault);
        }
    }

    private static CloudHandoffResult Describe(Exception fault) => CloudFailures.KindOf(fault) switch
    {
        CloudFailureKind.LimitReached => new CloudHandoffResult(CloudHandoffOutcome.LimitReached, CloudFailures.MessageOf(fault, CloudCopy.LimitReached)),
        CloudFailureKind.QuotaExceeded => new CloudHandoffResult(CloudHandoffOutcome.QuotaExceeded, CloudFailures.MessageOf(fault, CloudCopy.QuotaExceeded)),
        CloudFailureKind.Rejected or CloudFailureKind.Conflict or CloudFailureKind.NotFound => new CloudHandoffResult(CloudHandoffOutcome.Rejected, CloudFailures.MessageOf(fault, CloudCopy.CreateRejected)),
        CloudFailureKind.NotConfigured => new CloudHandoffResult(CloudHandoffOutcome.NotAvailable, CloudFailures.MessageOf(fault, CloudCopy.NotConfiguredShort)),
        CloudFailureKind.Unauthorized => new CloudHandoffResult(CloudHandoffOutcome.NotAvailable, CloudFailures.MessageOf(fault, CloudCopy.SignInNeeded)),
        _ => new CloudHandoffResult(CloudHandoffOutcome.Failed, CloudFailures.MessageOf(fault, CloudCopy.CreateFailed)),
    };

    private static string? CheckEntries(CloudHandoffRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return CloudCopy.HandoffNeedsPrompt;
        }

        if (CloudInputValidator.ValidatePrompt(request.Prompt) is { } promptError)
        {
            return promptError;
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && CloudInputValidator.ValidateName(request.Name) is { } nameError)
        {
            return nameError;
        }

        if (request.Repositories.Count == 0)
        {
            return CloudCopy.RepositoryNeeded;
        }

        if (request.Repositories.Count > CloudInputValidator.MaxRepositories)
        {
            return CloudCopy.TooManyRepositories(CloudInputValidator.MaxRepositories);
        }

        foreach (var repository in request.Repositories)
        {
            if (CloudInputValidator.ValidateRepositoryAddress(repository.Address, out _) is { } repositoryError)
            {
                return repositoryError;
            }
        }

        if (request.SecretNames.Count > CloudInputValidator.MaxSecretNames)
        {
            return CloudCopy.TooManySecretNames(CloudInputValidator.MaxSecretNames);
        }

        foreach (var name in request.SecretNames)
        {
            if (CloudInputValidator.ValidateSecretName(name) is { } secretError)
            {
                return secretError;
            }
        }

        return CloudInputValidator.ValidateBranch(request.Branch) ?? CloudInputValidator.ValidateBranch(request.BaseBranch);
    }

    private static string NameOf(CloudHandoffRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            return request.Name.Trim();
        }

        // The cut adds one ellipsis character, so a derived name is at most 60 characters.
        var firstLine = request.Prompt.Split('\n', 2)[0];
        var derived = PanelText.Plain(firstLine, MaxDerivedNameChars);
        return derived.Length == 0 ? CloudCopy.UntitledAgent : derived;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
