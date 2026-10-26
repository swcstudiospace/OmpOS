# ADR-0011: Logging split by layer

- Status: Accepted 2026-10-26
- Date: 2026-10-26
- Owner: ompos-maintainers
- Deciders: ompos-maintainers
- Scope: which diagnostic mechanism each layer uses to report failures: the services and harness layers, the native Avalonia head (`src/OmpOS.Native.Views`, `apps/OmpOS.Native`), and the Blazor web head (`apps/OmpOS.Web`, `src/OmpOS.UI`). Recorded for phase 25.5 (fix F-D, finding M-21, decision D1).

## Context

Failures in the code were reported through two mechanisms. The services and harness layers, and the web head, used `ILogger<T>`. The native head used `System.Diagnostics.Trace`. Some failures were logged through neither, so they stayed silent (findings M-2 to M-21 in phase 25.1). Without a recorded rule, each new catch site had to pick a mechanism from the nearest file, and the choice was not visible to a reviewer.

## Decision

Each layer uses one mechanism:

1. **Services and harness layers** (`Harness.*`, `EngineHost`, `Coordination.*`, `Sessions`, `Workflows`, `Api`) log through `ILogger<T>`. A new logger is an optional constructor parameter `ILogger<T>? logger = null`, stored as `logger ?? NullLogger<T>.Instance`, so existing callers keep compiling and DI-constructed services receive the real logger.
2. **Native Avalonia head** (`src/OmpOS.Native.Views`, `apps/OmpOS.Native`) reports through `Trace`. `App.Initialize` installs one `StderrTraceListener` that writes every trace line to the process's standard error, through `App.InstallStderrTraceListener`. Installing it again replaces the earlier listener, so at most one is present.
3. **Blazor web head** (`apps/OmpOS.Web` and the shared components in `src/OmpOS.UI`) injects `ILogger<T>` through DI. The browser's console logging provider receives the entries.

The split follows the code that already exists. The native head has no DI container for its windows and already used `Trace`; the web head has DI and a browser console provider; the services already take `ILogger<T>`.

## Consequences

- No layer writes to `Console` for diagnostics. The one exception is `App.Initialize`, which passes `Console.Error` to the stderr listener. In the desktop app that listener is the operator's output: a failure traced by the native head appears on the standard error of the process that launched it.
- Native head code that needs to report a failure calls `Trace.TraceError` or `Trace.TraceWarning`. It does not add an `ILogger` dependency.
- The web head's diagnostics appear in the browser console, not in a terminal.
- The native composition passes no logger to the services (no `ILogger` is referenced in `apps/OmpOS.Native` or `src/OmpOS.Native.Views`). Their `ILogger` output is discarded in the desktop app until a provider is wired. Wiring it is outside this ADR.
- Tests of native failures observe them by adding a `TraceListener`, or by calling `App.InstallStderrTraceListener` with a writer they control. Tests of web components supply an `ILogger<T>` through the bUnit service collection.

## Amendments

None.
