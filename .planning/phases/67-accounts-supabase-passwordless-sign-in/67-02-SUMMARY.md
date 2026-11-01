# Plan 67-02 Summary: Harness.Secrets.Os (libsecret, Keychain, DPAPI)

Status: code complete, offline suite green, Live keyring test written and NOT run (orchestrator runs it).
Commit: `58d5e85a feat(67-02): OS secret stores (libsecret, Keychain, DPAPI) with typed SecretStoreFailure`.
Requirement AG-67-10 (offline part): satisfied. Live part: gated, unrun.

## What was built

- `Autonogrammer.Harness.Secrets` (additive only): `SecretStoreFailure` enum (`Unavailable`, `Locked`, `Denied`, `Corrupt`, `Other`, in that order) and `SecretStoreException.Failure` (default `Other`; two new ctors `(message, failure)` and `(message, failure, inner)`). Existing ctors, `OwnerOnlyFileSecretStore` and `InMemorySessionSecretStore` are unchanged (the file store still reports `Other`).
- `Autonogrammer.Harness.Secrets.Os` (namespace `Autonogrammer.Harness.Secrets.Os`, L2):
  - `public sealed class OsSecretStore : ISecretStore` is the dispatcher plan 67-04 consumes. Ctor `OsSecretStore(ILogger<OsSecretStore>? logger = null, TimeProvider? timeProvider = null)`. Picks libsecret on Linux, Keychain on macOS, DPAPI on Windows, otherwise a store whose every call fails as `Unavailable`. Creating it touches nothing; the first call reports a missing service/library.
  - Failures are `SecretStoreException` with `Failure` set; the message is one fixed sentence per kind (`The Linux Secret Service is locked.` etc.), never a name, value or native text. Argument errors (null/blank name, null secret) and cancellation fault/cancel the returned task, never throw synchronously.
  - Internal: `LinuxLibsecretStore` (non-variadic `secret_password_storev_sync`, `lookupv_sync`, `clearv_sync`, `searchv_sync`, `secret_retrievable_get_attributes`; attributes `{app: autonogrammer, name}`; schema `org.autonogrammer.Secret` built by hand; default collection; `NativeLibrary.TryLoad` of libsecret/glib/gobject/gio; calls on a dedicated LongRunning thread with a `GCancellable` bound to the token; search flags ALL|UNLOCK), `MacosKeychainStore` (SecItemAdd/Update/CopyMatching/Delete over CoreFoundation, service `Autonogrammer`, account = name, duplicate on add falls back to update, OSStatus mapped: -25308 Locked; -128/-25293/-25292/-34018 Denied; -25291/-25294/-25307 Unavailable; -25295/-26275 Corrupt), `WindowsDpapiStore` (one file `<sha256(name)>.dpapi` per secret under `%LOCALAPPDATA%\Autonogrammer\secrets`, payload `AGD1` + name + value protected CurrentUser with fixed entropy `Autonogrammer.Harness.Secrets.Os/v1`, temp file + `File.Move(overwrite)`, name verified on read, unreadable file is `Corrupt`), `UnavailableSecretStore`, `BlockingSecretStore` (shared off-thread run, timing and logging), `NativeUtf8` (hand-marshalled UTF-8 zeroed on dispose, NUL and lone surrogates refused without quoting the text), `StoreGuards`, `OsStoreLogMessages` (EventIds 3050 to 3053 used, 3054 to 3069 free; carry store name, operation, failure kind and elapsed ms only).
  - Bindings: `DllImport` over `IntPtr`/`int`/`uint`/`byte` only, every import has `DefaultDllImportSearchPaths(SafeDirectories)` (39 imports, asserted by a reflection test), no `AllowUnsafeBlocks`, no `unsafe`, no function pointers (asserted by a source test). `CFData` that carries a secret is created mutable and zeroed before release; the data returned by `SecItemCopyMatching` cannot be zeroed (immutable, owned by CoreFoundation); the managed `string` inside `SecretString` is not zeroable either.
- Seams for tests: `ILibsecretApi`, `IKeychainApi`, `IDpapiProtector` (internal; `InternalsVisibleTo` to the test project already existed).

## Tests (the verify command, run as written)

`ag-dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --filter-not-trait "Category=Live"`: total 78, passed 78, failed 0, skipped 0.
Also run: `--no-build --filter-trait "Category=Live"` without the env gate: 1 skipped with reason "Set AUTONOGRAMMER_LIVE_KEYRING=1 to run the live round trip against the real Linux Secret Service." And `tests/Autonogrammer.Harness.Secrets.Tests` (project I touched): 69 passed, 0 failed.
Coverage: the ISecretStore contract (round trip, replace, missing, remove, ordinal case-sensitive listing, argument faults, cancellation, off-caller-thread, blocked prompt ended by cancellation) over three backends (libsecret fake, Keychain fake, DPAPI over a temp dir with a reversible fake protector); typed failures with value-free messages and logs; Linux domain/code classification (locked to Locked, absent service to Unavailable); Keychain OSStatus mapping; DPAPI corruption and moved-file detection; real macOS and Windows stores report `Unavailable` on this Linux host; dispatcher selection; DllImport and unsafe-code source guards.

## Live keyring test (for the orchestrator)

`AUTONOGRAMMER_LIVE_KEYRING=1 /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --no-build --filter-trait "Category=Live"` (needs the session bus, e.g. `DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/0/bus`, and an unlocked gnome-keyring). `LiveKeyringTests.AThrowawayItemIsStoredReadListedAndRemoved` stores, reads, lists and removes one item under a unique `app` attribute `autonogrammer-live-<guid>` (never touches real items, cleans up in `finally`); it skips with the stated reason on `Locked`, `Unavailable` or `Denied`. This is the only check of the real libsecret marshalling (schema layout, GHashTable, GError, GList); until it passes that code is unexercised. The Keychain and DPAPI native paths are compile-checked only.

## Deviations and notes

- [Rule 3] `SecretStoreFailure` is in its own file `SecretStoreFailure.cs` (Meziantou MA0048 rejects two types in `SecretStoreException.cs`); `SecretStoreException.cs` only gained the property and ctors.
- Extra new files in the new Os project beyond the plan's list (all inside the project I own): `BlockingSecretStore`, `NativeUtf8`, `StoreGuards`, `ILibsecretApi`, `IKeychainApi`, `IDpapiProtector`, `ProtectedDataProtector`, `LibsecretNativeApi`, `LibsecretNativeMethods`, `LibsecretAttributes`, `LibsecretErrorDomain`, `KeychainNativeApi`, `KeychainNativeMethods`, `UnavailableSecretStore` (one type per file is enforced by the analyzers).
- Native libsecret errors are reduced to a typed `SecretStoreException` at the binding (an internal exception type was rejected by CA1064); domain, code and GLib text are dropped.
- `CA2216` is suppressed with a justification on two private scope types (`AttributeTable`, `CancellableScope`): a finalizer would call into GLib from the finalizer thread.
- csproj carve-out (D-62) applied: `Microsoft.Extensions.Logging.Abstractions` (no version) added to the Os csproj; `restore --force-evaluate` run for the Os project and the Os test project only; both lock files changed by that reference only and are committed. Dependents that record the Os project (`apps/Autonogrammer.Native`, `Autonogrammer.Accounts.Desktop` and its tests, per plan) are NOT relocked here; plan 67-04 relocks them once after its own carve-out.
- GIO errors other than cancelled and permission-denied (including an unregistered remote D-Bus error) are classified `Unavailable` [INFERENCE: libsecret/GIO do not expose the D-Bus error name without parsing free text, so this is the safe degrade; unverified against a real failing bus].
- The Linux search uses `SECRET_SEARCH_ALL | SECRET_SEARCH_UNLOCK`, so listing can show an unlock prompt as a read does [INFERENCE from the libsecret API docs; not exercised].

## Public names later plans use

`Autonogrammer.Harness.Secrets.SecretStoreFailure`, `SecretStoreException.Failure`, `Autonogrammer.Harness.Secrets.Os.OsSecretStore` (only public type of the Os project besides `AssemblyMarker`). No settings keys, no secret names, no new packages.

## Files changed

`src/Autonogrammer.Harness.Secrets/{SecretStoreException.cs,SecretStoreFailure.cs}`; `src/Autonogrammer.Harness.Secrets.Os/` (csproj, packages.lock.json, 19 new .cs files); `tests/Autonogrammer.Harness.Secrets.Os.Tests/` (packages.lock.json, `OsSecretStoreContractTests.cs`, `LinuxLibsecretTests.cs`, `LiveKeyringTests.cs`, `ManualTimeProvider.cs`, `Support/InMemoryLibsecretFake.cs`).
