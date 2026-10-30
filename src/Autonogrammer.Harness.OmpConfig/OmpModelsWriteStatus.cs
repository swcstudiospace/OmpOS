namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The outcome of changing <c>models.yml</c>.</summary>
public enum OmpModelsWriteStatus
{
    /// <summary>The file was changed and omp accepted it.</summary>
    Written = 0,

    /// <summary>The file already says what was asked; nothing was written.</summary>
    Unchanged = 1,

    /// <summary>The id exists in <c>providers:</c> outside an Autonogrammer block. Nothing was written.</summary>
    Conflict = 2,

    /// <summary>The file has a layout this editor does not touch (flow style, tabs, malformed Autonogrammer markers, ...). Nothing was written; edit the file by hand.</summary>
    UnsupportedLayout = 3,

    /// <summary>The provider is not acceptable, or omp's own validation refused the change and the previous file was restored.</summary>
    Rejected = 4,

    /// <summary>The file could not be read or written, or omp could not be asked to validate; the previous file was restored where a write had happened.</summary>
    Failed = 5,
}
