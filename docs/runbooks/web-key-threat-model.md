# Web provider-key threat model

Phase 43 (VX-10). Scope: a provider key (for example a model API key) that the user types into the WebAssembly head (`apps/OmpOS.Web`, shared UI in `src/OmpOS.UI`) so the browser can call a provider directly.

## Decision

The key lives in memory for one page session and nowhere else. The default recorded for this phase is **session-only**: closing, reloading or navigating away from the page loses the key and the user enters it again. Persisting a key across sessions is a different product decision and is not made here.

## Mechanism

`OmpOS.UI.Security.SessionKeyHolder` is the only place a web key is held.

- Memory only: the key is kept in a private `char[]`. The holder has no static state, no serialisation (its JSON converter throws), and no web-storage, cookie, database or file access. `NoWebHeadSourceUsesBrowserStorage` fails the build when any `src/OmpOS.UI` or `apps/OmpOS.Web` source uses `localStorage`, `sessionStorage`, `indexedDB` or `document.cookie`.
- Cleared on dispose: `Clear()` and `Dispose()` overwrite the buffer with zeros; replacing the key overwrites the previous one. A reload creates a new holder, which starts empty.
- Never rendered: `ToString()`, string interpolation, string formatting, the debugger display and exception messages show `[redacted]`. `Reveal()` is the single member that returns the key, so it is the one call to audit.

## What is protected

| Threat | Protected | How |
| --- | --- | --- |
| Key survives a reload or a closed tab | Yes | Memory only; nothing writes browser storage. |
| Another page of the same origin reads the key from storage | Yes | No storage entry exists to read. |
| Key written to a log, transcript, crash report or JSON document by OmpOS code | Yes | Redacting `ToString`, throwing JSON converter, canary test `KeyNeverReachesALogOrTranscriptCanary`. |
| Key left in the holder's buffer after it is no longer needed | Yes | Buffer overwritten on `Clear`, replace and `Dispose`. |

## What is not protected

- **Cross-site scripting.** Script running in the page can call `Reveal()` through the same runtime, read the DOM, or hook network calls. The mitigation is the page's content security policy and the Markdown/UI sanitisation covered by `MarkdownSanitizationTests` and `SurfaceSafetyTests`; this holder adds no defence against it.
- **Strings handed out by `Reveal()`.** A .NET string is immutable and the runtime may copy it; it stays in WebAssembly linear memory until garbage collected, and clearing the holder cannot clear it. The key is also placed in the `Authorization` header of the request that uses it.
- **WebAssembly linear memory.** The key is plain bytes in the page's memory. A browser extension, a debugger, or a memory snapshot taken by the user's own tools can read it.
- **The provider and the network.** Transport security is TLS between the browser and the provider. The provider sees the key by design.
- **Browser features outside the page's control.** Form autofill and password managers can store a key typed into an `<input>`; the key input must use `autocomplete="off"` and no `name` that a password manager recognises (the key-entry component must do this when it is built).
- **Hosts other than the browser.** Native and mobile heads use the credential envelope and `SecretString` (`OmpOS.Harness.Secrets`), not this holder.

## Remaining check (not verified)

The unit tests prove the holder and scan the source. They do not prove that a running browser wrote nothing. That needs a browser-run check: load the web head in a headless browser, enter a canary key, drive a provider call against a stub, then assert that `localStorage`, `sessionStorage`, IndexedDB and `document.cookie` hold no entry containing the canary, that the canary is absent from the browser console log, and that a reload leaves the key field empty. It belongs in `tests/OmpOS.Web.Tests` and runs only where a headless browser is available; no headless browser is part of the repository gate today, so this check has not been run and the claim above rests on the unit tests and the source scan.
