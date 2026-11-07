namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>The GLib error domain of a <c>GError</c> that libsecret returned.</summary>
internal enum LibsecretErrorDomain
{
    /// <summary>A GLib domain this store does not know.</summary>
    Other,

    /// <summary>The libsecret domain (<c>SECRET_ERROR</c>).</summary>
    Secret,

    /// <summary>The GIO domain (<c>G_IO_ERROR</c>).</summary>
    Gio,

    /// <summary>The D-Bus domain (<c>G_DBUS_ERROR</c>).</summary>
    DBus,
}
