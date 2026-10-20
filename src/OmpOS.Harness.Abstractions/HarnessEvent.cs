namespace OmpOS.Harness.Abstractions;

/// <summary>
/// One event emitted by an omp session while a prompt streams. The union is closed:
/// the constructor is private-protected, so only the derived records in this assembly
/// can exist. Consumers discriminate by pattern matching on the derived type.
/// </summary>
public abstract record HarnessEvent
{
    private protected HarnessEvent()
    {
    }
}
