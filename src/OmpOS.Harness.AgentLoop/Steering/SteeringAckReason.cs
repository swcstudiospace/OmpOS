namespace OmpOS.Harness.AgentLoop.Steering;

/// <summary>Why the omp plugin did not deliver a directive. The wire codes are <c>invalid-json</c>, <c>invalid-schema</c>, <c>too-large</c>, <c>bad-id</c>, <c>duplicate</c>, <c>blank-text</c> and <c>deliver-error</c>.</summary>
public enum SteeringAckReason
{
    /// <summary>A code this reader does not know.</summary>
    Unknown = 0,

    /// <summary>The directive file was not valid JSON.</summary>
    InvalidJson = 1,

    /// <summary>The directive did not match the directive schema.</summary>
    InvalidSchema = 2,

    /// <summary>The directive file or its text was too large.</summary>
    TooLarge = 3,

    /// <summary>The identifier was not valid.</summary>
    BadId = 4,

    /// <summary>The identifier was already used.</summary>
    Duplicate = 5,

    /// <summary>The text was blank.</summary>
    BlankText = 6,

    /// <summary>omp refused or failed to take the message.</summary>
    DeliverError = 7,
}
