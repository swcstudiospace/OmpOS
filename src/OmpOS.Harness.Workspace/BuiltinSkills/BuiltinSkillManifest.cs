namespace OmpOS.Workspace.BuiltinSkills;

/// <summary>
/// The list of skills the application ships (<see cref="Shipped"/>), in the order the folders appear under <c>assets/skills</c>, and the Cursor built-in skills it does not ship
/// (<see cref="NotApplicable"/>) with the reason. A test compares the list with the folders that are really bundled, so the two cannot drift apart.
/// </summary>
public static class BuiltinSkillManifest
{
    /// <summary>Gets the skills that ship, ordered by name.</summary>
    public static IReadOnlyList<BuiltinSkillEntry> Shipped { get; } =
    [
        new("agent-review", ["agent-review"], "Reviews your changes against the base branch in Quick or Deep depth and reports severity-ranked findings without editing files."),
        new("create-hook", ["create-hook"], "Writes a hook script and its hooks.json entry for an agent lifecycle event."),
        new("create-rule", ["create-rule"], "Writes a rule file or an AGENTS.md section with the right scope."),
        new("create-skill", ["create-skill"], "Creates an Agent Skill folder with a SKILL.md."),
        new("create-subagent", ["create-subagent"], "Writes a custom subagent definition with a focused role."),
        new("migrate-to-skills", ["migrate-to-skills"], "Converts custom commands and agent-decided rules into skills and keeps the originals until you confirm."),
        new("review", ["review"], "Reviews the working diff for bugs, regressions and missing tests and reports without editing files."),
        new("review-security", ["review-security"], "Reviews a change for security vulnerabilities and reports evidenced, ranked findings."),
        new("split-to-prs", ["split-to-prs"], "Plans and, after approval, creates a stack of small reviewable branches from one large change."),
        new("update-settings", ["update-settings"], "Explains where an OmpOS setting lives and changes it safely, with a backup. It replaces update-cursor-settings and update-cli-config."),
    ];

    /// <summary>Gets the Cursor built-in skills that OmpOS does not ship, ordered by token.</summary>
    public static IReadOnlyList<BuiltinSkillNotApplicable> NotApplicable { get; } =
    [
        new("automate", "Creates Cursor Automations, a Cursor cloud service; OmpOS has no automation service to target."),
        new("autopilot", "Drives a pull request through a Cursor cloud agent; it needs the cloud-agent service that OmpOS does not reproduce."),
        new("canvas", "Renders interactive artifacts in a Cursor pane; OmpOS has no canvas surface to render into."),
        new("cursor-blame", "Reads Cursor's server-side record of AI-authored changes and their prompts; OmpOS keeps no such record."),
        new("loop", "Needs a scheduler that re-runs a prompt; omp's own /loop works only in its terminal interface and is not exposed over ACP."),
        new("review-bugbot", "Bugbot is a Cursor-hosted reviewer; /review and /agent-review cover the local review."),
        new("sdk", "Helps build with the Cursor SDK; OmpOS has no SDK of its own."),
        new("shell", "Shell mode is a built-in terminal action of OmpOS (/shell), not a skill."),
        new("statusline", "Configures the Cursor command-line status line; OmpOS has no scriptable status line."),
        new("update-cli-config", "Edits the Cursor CLI's configuration file; OmpOS has no such file. /update-settings replaces it."),
        new("update-cursor-settings", "Edits Cursor and VS Code settings; OmpOS settings are handled by /update-settings and /config."),
    ];

    /// <summary>Finds the shipped skill that makes a slash token runnable.</summary>
    /// <param name="token">The slash token, without the slash.</param>
    /// <returns>The skill, or <see langword="null"/> when no shipped skill serves the token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is null.</exception>
    public static BuiltinSkillEntry? FindByToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        foreach (var entry in Shipped)
        {
            foreach (var served in entry.SlashTokens)
            {
                if (string.Equals(served, token, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
        }

        return null;
    }
}
