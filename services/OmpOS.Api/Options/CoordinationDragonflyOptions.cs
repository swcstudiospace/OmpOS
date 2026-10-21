namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Coordination:Dragonfly</c> configuration section. Startup validation requires <see cref="ConnectionString"/>
/// when <c>Coordination:Store</c> is <c>Dragonfly</c>; the value is never logged.
/// </summary>
internal sealed class CoordinationDragonflyOptions
{
    /// <summary>Gets or sets the StackExchange.Redis configuration string, for example <c>host:port,password=...,abortConnect=false</c>.</summary>
    public string? ConnectionString { get; set; }
}
