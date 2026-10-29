namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// The Keychain Services calls the macOS store needs, over generic password items with service <c>Autonogrammer</c> and the secret's name as
/// the account. Each method returns the <c>OSStatus</c> of the native call. A missing framework is a <see cref="DllNotFoundException"/>.
/// </summary>
internal interface IKeychainApi
{
    /// <summary>Adds an item (<c>SecItemAdd</c>).</summary>
    /// <param name="account">The secret's name.</param>
    /// <param name="secret">The secret.</param>
    /// <returns>The <c>OSStatus</c>.</returns>
    int Add(string account, SecretString secret);

    /// <summary>Replaces the data of an existing item (<c>SecItemUpdate</c>).</summary>
    /// <param name="account">The secret's name.</param>
    /// <param name="secret">The secret.</param>
    /// <returns>The <c>OSStatus</c>.</returns>
    int Update(string account, SecretString secret);

    /// <summary>Reads an item (<c>SecItemCopyMatching</c> returning data).</summary>
    /// <param name="account">The secret's name.</param>
    /// <param name="secret">The secret when the status is success; otherwise null.</param>
    /// <returns>The <c>OSStatus</c>.</returns>
    int Copy(string account, out SecretString? secret);

    /// <summary>Deletes an item (<c>SecItemDelete</c>).</summary>
    /// <param name="account">The secret's name.</param>
    /// <returns>The <c>OSStatus</c>.</returns>
    int Delete(string account);

    /// <summary>Lists the accounts of every item of the service (<c>SecItemCopyMatching</c> returning attributes).</summary>
    /// <param name="accounts">The accounts when the status is success; otherwise empty.</param>
    /// <returns>The <c>OSStatus</c>.</returns>
    int List(out IReadOnlyList<string> accounts);
}
