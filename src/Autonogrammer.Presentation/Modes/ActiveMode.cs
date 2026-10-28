namespace Autonogrammer.Presentation.Modes;

/// <summary>The skill a session has pinned as its mode, with the body that goes in front of every prompt.</summary>
/// <param name="Skill">The skill.</param>
/// <param name="Body">The skill's body, read when the mode was started.</param>
public sealed record ActiveMode(ModeSkill Skill, string Body);
