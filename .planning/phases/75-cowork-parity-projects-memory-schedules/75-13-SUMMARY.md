# 75-13 summary: project-aware Cowork session creation (native)

status: partial. Commit 63178542. Not complete; the phase is not claimed complete.

## Built and tested

- Build: 0 warnings, 0 errors.
- Native `CoworkProjectSessionSelection` tests: 8/8 passed.
- Presentation tests: 5/5 passed.
- Architecture tests: 110/111 passed. The sole failure is SEC-77-07.

## Done

- Session creation on the host is project-aware.
- The rail project picker and the New task action create sessions in the selected project.
- A `FolderMissing` notice is shown when a project's folder is missing.
- Account switch clears the project selection.

## Gaps

- The project editor is not connected to `ProjectsView`.
- Memory mode is the constant `MemoryOff=false`. No memory mode is user-selectable.
- Teardown of an open session is not implemented here. It belongs to 75-08.
- Rollback leaves a Failed row visible.

## Scope deviations (owner-approved)

- `CoworkView.axaml.cs` changed.
- `SurfaceHost.axaml.cs` changed (one wiring line).
- `CoworkAccountContext.cs` changed (account-scoped project read).

## Not claimed

- Phase 75 completion, and any milestone completion.
- A user-selectable memory mode.
- Connection of the project editor to `ProjectsView`.
- Open-session teardown on account switch (75-08).
