using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Api.Security;
using OmpOS.Api.Services;
using OmpOS.Contracts.Controls;
using OmpOS.Harness.Abstractions;
using OmpOS.Sessions;

namespace OmpOS.Api.Endpoints;

/// <summary>
/// The engine's own configuration of a session: its model, mode and thinking level (<c>GET</c> and <c>PUT /sessions/{id}/config-options</c>)
/// and the slash commands it advertises (<c>GET /sessions/{id}/commands</c>). The engine owns both lists. The API starts one engine session per
/// prompt, so a change is possible only while a run holds a live engine session; between runs the last list the engine reported is
/// returned with <c>live: false</c>, and a change answers 409 <c>config_options_unavailable</c>. A successful change is appended to the session log as a
/// <c>config_option_update</c> event.
/// </summary>
internal static class SessionConfigEndpoints
{
    private const int MaxOptionIdLength = 128;

    /// <summary>Maps the configuration and command endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapSessionConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/sessions/{id}/config-options", OptionsAsync)
            .WithTags("Session configuration")
            .WithSummary("Reads the model, mode and thinking options the engine reports for the session.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPut("/sessions/{id}/config-options/{optionId}", SetAsync)
            .WithTags("Session configuration")
            .WithSummary("Changes one option of a session whose run holds a live engine session.")
            .Accepts<SetConfigOptionRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        endpoints.MapGet("/sessions/{id}/commands", CommandsAsync)
            .WithTags("Session configuration")
            .WithSummary("Lists the slash commands the engine advertises for the session.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<Results<Ok<SessionConfigOptionsResponse>, ProblemHttpResult>> OptionsAsync(
        string id,
        ISessionRepository repository,
        LiveSessionTracker tracker,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        return session is null ? SessionEndpoints.SessionNotFound(id) : TypedResults.Ok(ReadOptions(id, tracker));
    }

    private static async Task<Results<Ok<SessionCommandsResponse>, ProblemHttpResult>> CommandsAsync(
        string id,
        ISessionRepository repository,
        LiveSessionTracker tracker,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        if (tracker.TryGetLive(id) is { } live)
        {
            var commands = ConfigOptionMapper.CommandsOf(live);
            if (commands.Count > 0)
            {
                return TypedResults.Ok(new SessionCommandsResponse { SessionId = id, Available = true, Live = true, Commands = commands });
            }
        }

        if (tracker.LastKnown(id) is { Commands.Count: > 0 } last)
        {
            return TypedResults.Ok(new SessionCommandsResponse
            {
                SessionId = id,
                Available = true,
                Live = false,
                Reason = "No run is active. These are the commands the last run advertised.",
                Commands = last.Commands,
            });
        }

        return TypedResults.Ok(new SessionCommandsResponse
        {
            SessionId = id,
            Available = false,
            Live = false,
            Reason = "The engine has not advertised commands for this session. The API starts the engine when a prompt runs.",
            Commands = [],
        });
    }

    private static async Task<Results<Ok<SessionConfigOptionsResponse>, ProblemHttpResult>> SetAsync(
        HttpContext context,
        string id,
        string optionId,
        ISessionRepository repository,
        LiveSessionTracker tracker,
        SessionLogAppender appender,
        IOptions<ApiOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, repository, user, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionEndpoints.SessionNotFound(id);
        }

        var body = await WebBodyReader.ReadAsync(context, options.Value, SessionControlsJsonContext.Default.SetConfigOptionRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var value = body.Value!.Value;
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(optionId) || optionId.Length > MaxOptionIdLength)
        {
            return SessionEndpoints.ProblemResponse(400, "Invalid option change.", "The body needs a non-blank 'value', and the option identifier must be 1 to 128 characters.");
        }

        if (tracker.TryGetLive(id) is not { } live || !ConfigOptionMapper.HasControls(live))
        {
            return WebProblems.Of(409, "config_options_unavailable", "Options cannot be changed now.", "No run holds a live engine session for this session. The API starts the engine when a prompt runs.");
        }

        var failure = await ApplyAsync(live, optionId, value, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        tracker.Capture(id, live);
        var current = ConfigOptionMapper.OptionsOf(live).FirstOrDefault(option => string.Equals(option.Id, optionId, StringComparison.Ordinal));
        _ = await appender.AppendAsync(
            id,
            StreamEventPayloads.CreateConfigOptionUpdate(new ConfigOptionUpdateEvent { OptionId = optionId, CurrentValue = current?.CurrentValue ?? value }),
            CancellationToken.None).ConfigureAwait(false);
        return TypedResults.Ok(ReadOptions(id, tracker));
    }

    private static async Task<ProblemHttpResult?> ApplyAsync(IOmpSession live, string optionId, string value, CancellationToken cancellationToken)
    {
        if (live is IOmpSessionOptions engineOptions && engineOptions.Options.Any(option => string.Equals(option.Id, optionId, StringComparison.Ordinal)))
        {
            var result = await engineOptions.SetOptionAsync(optionId, value, cancellationToken).ConfigureAwait(false);
            return result.Outcome switch
            {
                OmpOptionSetOutcome.Set => null,
                OmpOptionSetOutcome.NotOpen => WebProblems.Of(409, "config_options_unavailable", "Options cannot be changed now.", "The engine session is not open."),
                OmpOptionSetOutcome.Busy => WebProblems.Of(409, "config_option_busy", "Option is busy.", "A prompt or another change is running."),
                OmpOptionSetOutcome.UnknownOption => NotFound(),
                OmpOptionSetOutcome.UnknownValue => InvalidValue(),
                _ => WebProblems.Of(502, "engine_rejected", "The engine did not confirm the option.", "The engine refused the change or did not answer."),
            };
        }

        if (string.Equals(optionId, ConfigOptionMapper.ModelOptionId, StringComparison.Ordinal) && live is IOmpModelControl { ModelChoices.Count: > 0 } model)
        {
            var result = await model.SetModelAsync(value, cancellationToken).ConfigureAwait(false);
            return result.Outcome switch
            {
                OmpModelSwitchOutcome.Switched => null,
                OmpModelSwitchOutcome.NotOpen => WebProblems.Of(409, "config_options_unavailable", "Options cannot be changed now.", "The engine session is not open."),
                OmpModelSwitchOutcome.Busy => WebProblems.Of(409, "config_option_busy", "Option is busy.", "A prompt or another change is running."),
                OmpModelSwitchOutcome.NoModelOption => NotFound(),
                OmpModelSwitchOutcome.UnknownModel => InvalidValue(),
                _ => WebProblems.Of(502, "engine_rejected", "The engine did not confirm the option.", "The engine refused the change or did not answer."),
            };
        }

        return NotFound();
    }

    private static ProblemHttpResult NotFound() =>
        WebProblems.Of(404, "config_option_not_found", "Option not found.", "The engine offers no option with that identifier.");

    private static ProblemHttpResult InvalidValue() =>
        WebProblems.Of(400, "invalid_config_value", "Invalid option value.", "The option does not offer that value.");

    private static SessionConfigOptionsResponse ReadOptions(string id, LiveSessionTracker tracker)
    {
        if (tracker.TryGetLive(id) is { } live)
        {
            var options = ConfigOptionMapper.OptionsOf(live);
            if (options.Count > 0)
            {
                return new SessionConfigOptionsResponse { SessionId = id, Available = true, Live = true, Options = options };
            }
        }

        if (tracker.LastKnown(id) is { Options.Count: > 0 } last)
        {
            return new SessionConfigOptionsResponse
            {
                SessionId = id,
                Available = true,
                Live = false,
                Reason = "No run is active. These are the options the last run reported; a change needs an active run.",
                Options = last.Options,
            };
        }

        return new SessionConfigOptionsResponse
        {
            SessionId = id,
            Available = false,
            Live = false,
            Reason = "The engine has not reported options for this session. The API starts the engine when a prompt runs.",
            Options = [],
        };
    }
}
