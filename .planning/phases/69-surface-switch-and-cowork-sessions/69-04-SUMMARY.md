# 69-04 Summary

Web shell switches Code | Cowork by pointer, keyboard and `/cowork`, with a Cowork workbench, rail and signed-out, Demo, empty and error states.

## What landed

- `SurfaceTabs` is a Workspace tablist (Code / Cowork, roving tabindex, arrows/Home/End) in a Surface fragment before the view tabs.
- `/cowork` selects Cowork. A user switch navigates with `replace: true`. `/` restores a stored `cowork` only when `ICoworkAccess.IsAvailable`.
- `surface-boot.js` is the only new file that names `localStorage` (`autonogrammer.surface`). It sets `data-surface` before first paint and never throws when storage is blocked.
- The rail reads `GET account/sessions`, keeps rows labelled `cowork`, and does not fetch transcripts. Titles are plain text, capped at 200 characters.
- `--autonogrammer-cowork-dock-width: 420px` twins the dock width. `surface.css` is imported before `a11y.css`.
- `AccountMenu.razor` is not on disk, so `HeaderEndSlot` does not place it and `SignOutButton` is unchanged for heads without that menu.

## Open items

1. Phase 67 `WebAccountSession` (`IAuthSession`) must keep `HasToken` true exactly while a session is held.
2. Phase 67 built `AccountMenu.razor` unplaced. This plan places it only when that type exists and an account host is registered. The type is not on disk, so the head still shows `SignOutButton`.
3. `GET /account/sessions` row shape is `AccountSessionList` / `AccountSessionItem` (`Session` plus a `surface` string), read from the contract phase 67 shipped.
4. `surface-boot.js` is on the display-preference allow-list in `SessionKeyHolderTests`. No `.cs` or `.razor` file names a storage API.


The implementation commit landed as `8680ec9b` because a concurrent commit consumed the already-staged 69-04 paths. A follow-up commit carries the `feat(69-04)` message.
## Verification

Targeted UI and Web filters passed via `/tmp/ag-bin/ag-dotnet`. `node --test scripts/web-smoke/surface-keys.test.mjs` passed. `node --check scripts/web-smoke/a11y/routes.mjs` passed.
