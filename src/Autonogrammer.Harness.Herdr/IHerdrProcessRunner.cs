namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// Runs one executable with an argument array. No shell is involved, so each element reaches the process unchanged.
/// </summary>
public interface IHerdrProcessRunner
{
    /// <summary>Runs the executable and waits for it to exit.</summary>
    /// <param name="executablePath">The path of the executable to run.</param>
    /// <param name="arguments">The arguments, one element each.</param>
    /// <param name="cancellationToken">Cancels the wait and ends the process.</param>
    /// <returns>The exit code and both output streams.</returns>
    Task<HerdrProcessResult> RunAsync(string executablePath, string[] arguments, CancellationToken cancellationToken);
}
