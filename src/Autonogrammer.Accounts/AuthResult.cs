using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Accounts;

/// <summary>
/// The outcome of a call that can fail in an expected way: a value or an <see cref="AuthFailure"/>. Expected failures are never exceptions.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public sealed class AuthResult<T>
{
    private readonly T? _value;
    private readonly AuthFailure? _failure;

    internal AuthResult(T value)
    {
        _value = value;
    }

    internal AuthResult(AuthFailure failure)
    {
        _failure = failure;
    }

    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => _failure is null;

    /// <summary>Gets the value of a successful call.</summary>
    /// <exception cref="InvalidOperationException">The call failed.</exception>
    public T Value => _failure is null ? _value! : throw new InvalidOperationException("The call failed; read Failure instead of Value.");

    /// <summary>Gets why the call failed.</summary>
    /// <exception cref="InvalidOperationException">The call succeeded.</exception>
    public AuthFailure Failure => _failure ?? throw new InvalidOperationException("The call succeeded; read Value instead of Failure.");
}

/// <summary>
/// Creates <see cref="AuthResult{T}"/> instances.
/// </summary>
public static class AuthResult
{
    /// <summary>Creates a successful result.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static AuthResult<T> Success<T>(T value) => new(value);

    /// <summary>Creates a failed result.</summary>
    /// <typeparam name="T">The type of the value the call would have returned.</typeparam>
    /// <param name="failure">Why the call failed.</param>
    /// <returns>The result.</returns>
    public static AuthResult<T> Fail<T>(AuthFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new AuthResult<T>(failure);
    }

    /// <summary>Creates a failed result from a kind alone.</summary>
    /// <typeparam name="T">The type of the value the call would have returned.</typeparam>
    /// <param name="kind">The failure kind.</param>
    /// <returns>The result.</returns>
    public static AuthResult<T> Fail<T>(AuthErrorKind kind) => new(new AuthFailure(kind));
}
