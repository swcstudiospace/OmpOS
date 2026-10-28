namespace OmpOS.Workspace.Ignore;

/// <summary>
/// The built-in ignore list. It is OmpOS's reading of the list Cursor applies before any ignore file (research AC-047): lock files, environment files, version-control
/// folders, caches, binaries, images, archives, fonts and private keys. Patterns use gitignore syntax and rank lowest, so a <c>!pattern</c> in the global list or in any ignore file
/// overrides one, for example <c>!.env.example</c> or <c>!package-lock.json</c>. Folders whose names can hold source (<c>dist</c>, <c>bin</c>, <c>obj</c>) and text formats (<c>svg</c>) are left out on purpose.
/// </summary>
public static class IgnoreDefaults
{
    /// <summary>Gets the built-in patterns in rank order.</summary>
    public static IReadOnlyList<string> Patterns { get; } =
    [
        "package-lock.json",
        "yarn.lock",
        "pnpm-lock.yaml",
        "Cargo.lock",
        "poetry.lock",
        "packages.lock.json",
        "*.lock",
        "go.sum",
        ".env",
        ".env.*",
        ".git/",
        ".hg/",
        ".svn/",
        "node_modules/",
        "__pycache__/",
        ".venv/",
        "venv/",
        ".cache/",
        ".next/",
        "*.exe",
        "*.dll",
        "*.so",
        "*.dylib",
        "*.o",
        "*.a",
        "*.class",
        "*.jar",
        "*.wasm",
        "*.pyc",
        "*.png",
        "*.jpg",
        "*.jpeg",
        "*.gif",
        "*.bmp",
        "*.ico",
        "*.webp",
        "*.tiff",
        "*.zip",
        "*.tar",
        "*.gz",
        "*.tgz",
        "*.bz2",
        "*.xz",
        "*.7z",
        "*.rar",
        "*.ttf",
        "*.otf",
        "*.woff",
        "*.woff2",
        "*.eot",
        "*.pem",
        "*.key",
        "id_rsa",
        "id_ed25519",
        "id_ecdsa",
    ];
}
