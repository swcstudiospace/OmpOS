namespace Autonogrammer.Web.Services;

/// <summary>The account a browser wallet returned: its address and the chain identifier in decimal.</summary>
/// <param name="Address">The address the wallet returned.</param>
/// <param name="ChainId">The chain identifier in decimal, for example <c>1</c>.</param>
internal sealed record WalletAccount(string Address, string ChainId);
