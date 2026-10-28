namespace OmpOS.Api.Services.Providers;

/// <summary>The result of checking a custom endpoint request.</summary>
/// <param name="Record">The endpoint to write, or null when the request is refused.</param>
/// <param name="Problem">The sentence that says what is wrong, or null. It never repeats a value the caller sent.</param>
internal sealed record CustomProviderValidation(CustomProviderRecord? Record, string? Problem);
