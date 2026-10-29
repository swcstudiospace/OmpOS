---
description: "How to verify by hand that Autonogrammer respects reduced motion, forced colors, increased contrast and the dark scheme on Windows, macOS and Linux, and what the scripted checks cover."
---

# Reduced motion and forced colors: manual verification

Status: Written, not yet carried out by a person on any of the three systems. The scripted emulation checks ran against the built bundles on 2026-10-29 with zero error findings ([evidence](../runbooks/evidence/web-accessibility-2026-10-29/README.md)); the emulation palette is not a Windows High Contrast theme (known issue K11).

Last verified: 2026-10-29

Applies to: the web head (Chrome, Edge, Firefox, Safari). The app reads the operating-system settings through CSS media features; it also has its own theme and motion choices (the theme switch in the top bar; `localStorage` keys `autonogrammer.theme` and `autonogrammer.motion` hold the theme and the motion preference, never a credential).

## What the scripted checks do

`scripts/web-smoke/a11y/media-emulation.mjs` asks Chrome to emulate each preference (DevTools command `Emulation.setEmulatedMedia`) and measures the page:

| Emulated feature | What is asserted | Not covered |
| --- | --- | --- |
| `forced-colors: active` | Every text field keeps a border; every button keeps a border or a text label (an icon-only button without a border is a warning); the first 30 focus stops show an outline of at least 1 px. | The real palettes of a system (the emulation uses Chrome's default), icons drawn with images, the terminal canvas. |
| `prefers-contrast: more` | The same boundary rule. | Whether contrast increases; that is a judgement for the manual check. |
| `prefers-reduced-motion: reduce` | No element or pseudo-element has a transition or animation longer than 0.01 s, no smooth scrolling, no running animation. | Motion drawn by script or by the terminal. |
| `prefers-color-scheme: dark` | With the theme set to "system" the page darkens, body text keeps 4.5:1 and axe finds no serious or critical violation. | Every colour pair on every screen. |

Screenshots of the Agents, Kanban, About and Settings views under each emulation are written with `--a11y-report DIR` (`scripts/web-smoke/run-smoke.sh`) for a person to look at.

## Windows: contrast themes (forced colors)

1. Settings > Accessibility > Contrast themes: choose "Aquatic", then repeat with "Night sky" and "Desert". (Windows 10: Settings > Ease of Access > High contrast.) Left Alt + Left Shift + Print Screen turns the last theme on and off.
2. Reload the web head in Edge or Chrome (and Firefox, which has its own "Colors" override: Settings > General > Language and Appearance > Colors > Manage Colors).
3. Walk the Agents view, a conversation, Kanban, Settings and the connect screen. Expected: all text is readable on the theme's background; every button, field and tab has a visible edge or is clearly a label; the focused element has a clearly visible outline in the theme's highlight colour; selected and current items (the selected tab, the current agent, the current Settings section) are still distinguishable; icons are visible; nothing is hidden or overlaps.
4. Record anything that disappears: an icon that is only a background image, a status that is only a colour, a focus ring that vanishes.

## macOS: Increase contrast and Reduce motion

1. System Settings > Accessibility > Display: turn on "Increase contrast", and separately "Reduce motion" and "Reduce transparency". Safari and Chrome pass these on as `prefers-contrast: more` and `prefers-reduced-motion: reduce`.
2. Reload. With Increase contrast, expected: borders and text are at least as strong as before, nothing relies on a pale hairline. With Reduce motion, expected: no sliding drawer, no spinning or pulsing indicators, no animated scrolling; state changes happen at once.
3. Forced colors are not offered by macOS; use a browser that has its own override (Firefox colors) if you need to test that mode there.

## Linux: GNOME and KDE

1. GNOME: Settings > Accessibility > Seeing: turn on "High Contrast" (the HighContrast theme) and "Reduce Animation" (Firefox maps it to `prefers-reduced-motion`; Chrome follows the GNOME setting on recent versions). KDE: System Settings > Workspace Behavior > General Behavior > "Animation speed" to the instant end.
2. Firefox: Settings > General > Colors > Manage Colors > "Override the colors specified by the page" set to "Always" gives a forced-colors page on any desktop.
3. Chrome and Chromium: start with `--force-prefers-reduced-motion` to ask for reduced motion without changing the desktop, and use DevTools > Rendering > "Emulate CSS media feature prefers-contrast", "prefers-reduced-motion", "forced-colors" and "prefers-color-scheme".
4. Repeat the walk described for Windows.

## Dark scheme

Set the app's theme switch to "System", then switch the operating system between light and dark (or use DevTools > Rendering > "Emulate CSS media feature prefers-color-scheme"). Expected: the page follows the system at once or after a reload; text and the focus ring keep their contrast in both.

## Record

| Date | Tester | System and browser | Mode | Views walked | Result | Defects |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |

## How verified

- The behaviour of the scripted emulation was exercised on 2026-10-29 in a local Chrome against the fixture page of `scripts/web-smoke/a11y/fixture-selftest.mjs`: `forced-colors: active` and `prefers-reduced-motion: reduce` were applied and measured (the fixture's one-second transition and its border-less icon button were found). It was not run against the app.
- The menu paths of the operating systems were written from memory of current releases and were not followed on a machine for this document; menu names change between versions.
- Not verified: any system palette, Safari and Firefox behaviour, and the app's own motion preference ("full" overrides the system setting) beyond reading `theme-boot.js`.
