namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The outcome of <see cref="OmpDefaultModel.SetDefaultAsync"/>.</summary>
public enum OmpDefaultModelStatus
{
    /// <summary>The default role now holds the selector, and omp reports it back.</summary>
    Set = 0,

    /// <summary>The selector does not have the <c>provider/model-id</c> shape. Nothing was written.</summary>
    InvalidSelector = 1,

    /// <summary>omp's complete model listing does not contain the selector. Nothing was written.</summary>
    UnknownModel = 2,

    /// <summary>omp could not be asked, refused the write, or did not report the new value. See the message.</summary>
    Failed = 3,
}
