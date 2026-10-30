namespace Autonogrammer.Presentation.Safety;

/// <summary>One choice in the run mode list: the mode, its name and the honest sentence about what it does in Autonogrammer.</summary>
/// <param name="Choice">The mode.</param>
/// <param name="Title">The name, as Cursor spells it.</param>
/// <param name="Summary">What it does in Autonogrammer.</param>
public sealed record RunModeOption(RunModeChoice Choice, string Title, string Summary);
