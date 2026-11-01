
namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Action class a tier allows. The model cannot create a grant.</summary>
public enum ComputerActionClass
{
    /// <summary>Screenshot.</summary>
    Screenshot = 0,

    /// <summary>Zoom.</summary>
    Zoom = 1,

    /// <summary>Window listing.</summary>
    ListWindows = 2,

    /// <summary>Pointer move.</summary>
    PointerMove = 3,

    /// <summary>Click.</summary>
    Click = 4,

    /// <summary>Drag.</summary>
    Drag = 5,

    /// <summary>Scroll.</summary>
    Scroll = 6,

    /// <summary>Type.</summary>
    Type = 7,

    /// <summary>Chord.</summary>
    Chord = 8,

    /// <summary>Clipboard read or write.</summary>
    Clipboard = 9,
}
