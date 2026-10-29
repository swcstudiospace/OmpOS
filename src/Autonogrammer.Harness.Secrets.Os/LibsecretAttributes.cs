namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>The libsecret item attributes and schema the store uses.</summary>
internal static class LibsecretAttributes
{
    /// <summary>The attribute that names the owning application.</summary>
    public const string AppKey = "app";

    /// <summary>The value of the <see cref="AppKey"/> attribute on every item the store writes.</summary>
    public const string AppValue = "autonogrammer";

    /// <summary>The attribute that holds the secret's name.</summary>
    public const string NameKey = "name";

    /// <summary>The schema name stored in the <c>xdg:schema</c> attribute.</summary>
    public const string SchemaName = "org.autonogrammer.Secret";

    /// <summary>Returns the attribute set that identifies one secret.</summary>
    /// <param name="name">The secret's name.</param>
    /// <param name="app">The value of the <c>app</c> attribute. Production code uses <see cref="AppValue"/>; the live test uses a unique value so it never touches a real item.</param>
    /// <returns>The attributes in the order <c>app</c>, <c>name</c>.</returns>
    public static (string Key, string Value)[] For(string name, string app = AppValue) => [(AppKey, app), (NameKey, name)];

    /// <summary>Returns the attribute set that matches every secret of the store.</summary>
    /// <param name="app">The value of the <c>app</c> attribute.</param>
    /// <returns>The single attribute <c>app</c>.</returns>
    public static (string Key, string Value)[] ForAll(string app = AppValue) => [(AppKey, app)];
}
