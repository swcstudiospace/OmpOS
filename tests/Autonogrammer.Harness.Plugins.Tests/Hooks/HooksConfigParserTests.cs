using System.Text;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the parsing of <c>hooks.json</c> and Claude settings files, including every rejection rule. Parsing never throws.
/// </summary>
public sealed class HooksConfigParserTests
{
    private static HooksConfigDocument Parse(string json, HookConfigFormat format = HookConfigFormat.HooksJson) =>
        HooksConfigParser.Parse(Encoding.UTF8.GetBytes(json), format, "hooks.json");

    [Fact]
    public void TheCursorDocumentationExampleParsesWithEveryHookIntact()
    {
        var document = Parse("""
            {
              "version": 1,
              "hooks": {
                "sessionStart": [{ "command": "./session-init.sh" }],
                "preToolUse": [{ "command": "./hooks/validate-tool.sh", "matcher": "Shell|Read|Write" }],
                "beforeShellExecution": [{ "command": "./scripts/approve-network.sh", "timeout": 30, "matcher": "curl|wget|nc" }],
                "stop": [{ "command": "./audit.sh", "loop_limit": 10 }],
                "beforeTabFileRead": [{ "command": "./redact-secrets-tab.sh" }],
                "workspaceOpen": [{ "command": "./register-workspace-plugins.sh" }]
              }
            }
            """);

        document.IsValid.ShouldBeTrue();
        document.Diagnostics.ShouldBeEmpty();
        document.Version.ShouldBe(1);
        document.Hooks.Select(hook => hook.EventName).ShouldBe(
            ["sessionStart", "preToolUse", "beforeShellExecution", "stop", "beforeTabFileRead", "workspaceOpen"],
            ignoreOrder: false);
        var shell = document.Hooks.Single(hook => hook.Event == HookEvent.BeforeShellExecution);
        shell.Command.ShouldBe("./scripts/approve-network.sh");
        shell.TimeoutSeconds.ShouldBe(30d);
        shell.Matcher.ShouldBe("curl|wget|nc");
        shell.Type.ShouldBe(HookType.Command);
        shell.FailClosed.ShouldBeFalse();
        shell.Enabled.ShouldBeTrue();
        shell.RunsIn.ShouldBe(HookLocations.Both);
        var stop = document.Hooks.Single(hook => hook.Event == HookEvent.Stop);
        stop.LoopLimit.ShouldBe(10);
        stop.LoopLimitSpecified.ShouldBeTrue();
    }

    [Fact]
    public void AutonogrammerExtrasAndPromptHooksParse()
    {
        var document = Parse("""
            {
              "version": 1,
              "hooks": {
                "afterFileEdit": [
                  { "args": ["prettier", "--write", "${file_path}"], "id": "fmt", "enabled": false, "description": "format",
                    "runsIn": ["local"], "env": { "FOO": "bar" }, "failClosed": true }
                ],
                "preToolUse": [
                  { "type": "prompt", "prompt": "Only allow read-only operations. $ARGUMENTS", "model": "small", "timeout": 10 }
                ]
              }
            }
            """);

        document.IsValid.ShouldBeTrue();
        var edit = document.Hooks[0];
        edit.Args!.ShouldBe(["prettier", "--write", "${file_path}"], ignoreOrder: false);
        edit.Command.ShouldBeNull();
        edit.Id.ShouldBe("fmt");
        edit.Enabled.ShouldBeFalse();
        edit.RunsIn.ShouldBe(HookLocations.Local);
        edit.Env["FOO"].ShouldBe("bar");
        edit.FailClosed.ShouldBeTrue();
        var prompt = document.Hooks[1];
        prompt.Type.ShouldBe(HookType.Prompt);
        prompt.Prompt.ShouldBe("Only allow read-only operations. $ARGUMENTS");
        prompt.Model.ShouldBe("small");
        prompt.TimeoutSeconds.ShouldBe(10d);
    }

    [Fact]
    public void OmpSectionHooksAreKeptApartAndUnknownOmpEventsAreIgnoredWithAWarning()
    {
        var document = Parse("""
            {
              "version": 1,
              "hooks": {},
              "omp": {
                "tool_call": [{ "command": "./guard.sh", "matcher": "^(bash|write)$" }],
                "session_before_compact": [{ "command": "./keep.sh" }],
                "made_up_event": [{ "command": "./x.sh" }]
              }
            }
            """);

        document.Hooks.ShouldBeEmpty();
        document.OmpHooks.Select(hook => hook.EventName).ShouldBe(["tool_call", "session_before_compact"], ignoreOrder: false);
        document.OmpHooks.ShouldAllBe(hook => hook.IsOmpEvent && hook.Event == null);
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
        document.Diagnostics.Single().Location.ShouldBe("$.omp.made_up_event");
    }

    [Fact]
    public void AnUnknownEventIsKeptWithAWarningAndDoesNotFailTheFile()
    {
        var document = Parse("""{ "version": 1, "hooks": { "futureEvent": [{ "command": "./x.sh" }], "stop": [{ "command": "./y.sh" }] } }""");

        document.IsValid.ShouldBeTrue();
        document.Hooks.Count.ShouldBe(2);
        var unknown = document.Hooks.Single(hook => string.Equals(hook.EventName, "futureEvent", StringComparison.Ordinal));
        unknown.Event.ShouldBeNull();
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
    }

    [Fact]
    public void UnknownKeysAreWarnedAndIgnored()
    {
        var document = Parse("""{ "version": 1, "extra": 3, "hooks": { "stop": [{ "command": "./y.sh", "colour": "red" }] } }""");

        document.IsValid.ShouldBeTrue();
        document.Hooks.Count.ShouldBe(1);
        document.Diagnostics.Select(d => d.Location).ShouldBe(["$.extra", "$.hooks.stop[0].colour"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("""{ "hooks": {} }""", "$.version")]
    [InlineData("""{ "version": 2, "hooks": {} }""", "$.version")]
    [InlineData("""{ "version": "1", "hooks": {} }""", "$.version")]
    [InlineData("""{ "version": 1.5, "hooks": {} }""", "$.version")]
    [InlineData("[]", "$")]
    [InlineData("""{ "version": 1, "hooks": [] }""", "$.hooks")]
    [InlineData("""{ "version": 1, "hooks": { "stop": {} } }""", "$.hooks.stop")]
    public void InvalidDocumentsBecomeErrorsAndNoHooks(string json, string location)
    {
        var document = Parse(json);

        document.IsValid.ShouldBeFalse();
        document.Hooks.ShouldBeEmpty();
        document.Diagnostics.ShouldContain(d => d.Severity == HookDiagnosticSeverity.Error && d.Location == location);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("""{ "version": 1, /* comment */ "hooks": {} }""")]
    [InlineData("""{ "version": 1, "hooks": {}, }""")]
    public void MalformedJsonNeverThrows(string json)
    {
        var document = Parse(json);

        document.IsValid.ShouldBeFalse();
        document.Diagnostics.Single().Location.ShouldBe("$");
    }

    [Fact]
    public void ADuplicatedKeyRejectsTheWholeFile()
    {
        var document = Parse("""{ "version": 1, "hooks": { "stop": [{ "command": "./a.sh", "command": "./b.sh" }] } }""");

        document.IsValid.ShouldBeFalse();
        document.Hooks.ShouldBeEmpty();
        document.Diagnostics.ShouldContain(d => d.Message.Contains("more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSizeLimitIsEnforced()
    {
        var padding = new string('a', HookConfigLimits.MaxHooksFileBytes);
        var document = Parse($$"""{ "version": 1, "hooks": { "stop": [{ "command": "./a.sh", "description": "{{padding}}" }] } }""");

        document.IsValid.ShouldBeFalse();
        document.Diagnostics.Single().Message.ShouldContain("limit", Case.Sensitive);
    }

    [Fact]
    public void TheDepthLimitIsEnforced()
    {
        var nested = string.Concat(Enumerable.Repeat("[", 30)) + string.Concat(Enumerable.Repeat("]", 30));
        var document = Parse($$"""{ "version": 1, "hooks": {}, "x": {{nested}} }""");

        document.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{ "command": "./a.sh", "args": ["a"] }""", "command")]
    [InlineData("""{ }""", "command")]
    [InlineData("""{ "command": "" }""", "command")]
    [InlineData("""{ "command": 4 }""", "command")]
    [InlineData("""{ "args": [] }""", "args")]
    [InlineData("""{ "args": ["", "x"] }""", "args")]
    [InlineData("""{ "command": "./a.sh", "type": "script" }""", "type")]
    [InlineData("""{ "type": "prompt" }""", "prompt")]
    [InlineData("""{ "command": "./a.sh", "timeout": 0 }""", "timeout")]
    [InlineData("""{ "command": "./a.sh", "timeout": "5" }""", "timeout")]
    [InlineData("""{ "command": "./a.sh", "failClosed": "yes" }""", "failClosed")]
    [InlineData("""{ "command": "./a.sh", "env": { "A": 1 } }""", "env")]
    [InlineData("""{ "command": "./a.sh", "env": { "A=B": "1" } }""", "env")]
    [InlineData("""{ "command": "./a.sh", "runsIn": ["cloud"] }""", "runsIn")]
    [InlineData("""{ "command": "./a.sh", "loop_limit": -1 }""", "loop_limit")]
    [InlineData("""{ "command": "./a.sh", "loop_limit": 1.5 }""", "loop_limit")]
    public void AnInvalidHookIsDroppedWithAnErrorNamingItsField(string hook, string field)
    {
        var document = Parse($$"""{ "version": 1, "hooks": { "stop": [ {{hook}}, { "command": "./ok.sh" } ] } }""");

        document.Hooks.Select(h => h.Command).ShouldBe(["./ok.sh"], ignoreOrder: false);
        document.Diagnostics.ShouldContain(d => d.Severity == HookDiagnosticSeverity.Error && d.Location.EndsWith("." + field, StringComparison.Ordinal));
    }

    [Fact]
    public void ATooLargeTimeoutIsClampedWithAWarning()
    {
        var document = Parse("""{ "version": 1, "hooks": { "stop": [{ "command": "./a.sh", "timeout": 100000 }] } }""");

        document.IsValid.ShouldBeTrue();
        document.Hooks.Single().TimeoutSeconds.ShouldBe(HookConfigLimits.MaxTimeoutSeconds);
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
    }

    [Fact]
    public void LoopLimitNullMeansNoCapAndIsDistinguishedFromAbsent()
    {
        var document = Parse("""{ "version": 1, "hooks": { "stop": [{ "command": "./a.sh", "loop_limit": null }, { "command": "./b.sh" }] } }""");

        document.Hooks[0].LoopLimit.ShouldBeNull();
        document.Hooks[0].LoopLimitSpecified.ShouldBeTrue();
        document.Hooks[1].LoopLimitSpecified.ShouldBeFalse();
    }

    [Fact]
    public void LoopLimitOnAnotherEventIsWarnedAsHavingNoEffect()
    {
        var document = Parse("""{ "version": 1, "hooks": { "preToolUse": [{ "command": "./a.sh", "loop_limit": 3 }] } }""");

        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
        document.Diagnostics.Single().Location.ShouldEndWith(".loop_limit", Case.Sensitive);
    }

    [Fact]
    public void DuplicateIdsInOneFileDropTheLaterHook()
    {
        var document = Parse("""{ "version": 1, "hooks": { "stop": [{ "command": "./a.sh", "id": "x" }, { "command": "./b.sh", "id": "x" }] } }""");

        document.Hooks.Select(h => h.Command).ShouldBe(["./a.sh"], ignoreOrder: false);
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Error);
    }

    [Fact]
    public void AnInvalidMatcherRegexKeepsTheHookAndWarns()
    {
        var document = Parse("""{ "version": 1, "hooks": { "preToolUse": [{ "command": "./a.sh", "matcher": "([" }] } }""");

        document.Hooks.Count.ShouldBe(1);
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
        document.Diagnostics.Single().Location.ShouldEndWith(".matcher", Case.Sensitive);
    }

    [Fact]
    public void ClaudeSettingsMapTheFiveEventsAndTranslateToolNames()
    {
        var document = Parse(
            """
            {
              "permissions": { "allow": ["Bash(ls)"] },
              "hooks": {
                "PreToolUse": [{ "matcher": "Bash|Edit", "hooks": [{ "type": "command", "command": "./pre.sh", "timeout": 5 }] }],
                "PostToolUse": [{ "matcher": "Read", "hooks": [{ "type": "command", "command": "./post.sh" }] }],
                "UserPromptSubmit": [{ "hooks": [{ "type": "command", "command": "./prompt.sh" }] }],
                "Stop": [{ "hooks": [{ "type": "command", "command": "./stop.sh" }] }],
                "SubagentStop": [{ "hooks": [{ "type": "command", "command": "./sub.sh" }] }],
                "Notification": [{ "hooks": [{ "type": "command", "command": "./n.sh" }] }]
              }
            }
            """,
            HookConfigFormat.ClaudeSettings);

        document.Hooks.Select(h => h.EventName).ShouldBe(
            ["preToolUse", "postToolUse", "beforeSubmitPrompt", "stop", "subagentStop"],
            ignoreOrder: false);
        document.Hooks[0].Matcher.ShouldBe("Shell|Write");
        document.Hooks[0].TimeoutSeconds.ShouldBe(5d);
        document.Hooks.ShouldAllBe(h => h.LoopLimitSpecified && h.LoopLimit == null);
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
        document.Diagnostics.Single().Location.ShouldBe("$.hooks.Notification");
    }

    [Fact]
    public void ClaudeMatchersOnNonToolEventsAreIgnoredWithAWarning()
    {
        var document = Parse(
            """{ "hooks": { "Stop": [{ "matcher": "Bash", "hooks": [{ "type": "command", "command": "./stop.sh" }] }] } }""",
            HookConfigFormat.ClaudeSettings);

        document.Hooks.Single().Matcher.ShouldBeNull();
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
    }

    [Fact]
    public void ClaudeSettingsWithoutAHooksKeyYieldNothing()
    {
        var document = Parse("""{ "model": "x" }""", HookConfigFormat.ClaudeSettings);

        document.Hooks.ShouldBeEmpty();
        document.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ClaudePromptTypeHooksAreNotImported()
    {
        var document = Parse(
            """{ "hooks": { "Stop": [{ "hooks": [{ "type": "prompt", "prompt": "x" }] }] } }""",
            HookConfigFormat.ClaudeSettings);

        document.Hooks.ShouldBeEmpty();
        document.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Warning);
    }
}
