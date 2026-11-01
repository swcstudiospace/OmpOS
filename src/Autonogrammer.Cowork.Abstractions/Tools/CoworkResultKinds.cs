namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Which kinds of content a tool may return.</summary>
[Flags]
public enum CoworkResultKinds
{
    /// <summary>The tool returns text.</summary>
    Text = 1,

    /// <summary>The tool returns images.</summary>
    Image = 2,
}
