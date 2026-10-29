// Media emulation (CDP Emulation.setEmulatedMedia): forced-colors, prefers-contrast, prefers-reduced-motion and prefers-color-scheme (WCAG 1.4.3, 1.4.11,
// 2.3.3, 2.4.7). Each emulation is asserted on the DOM; the screenshots of the main views are kept for a human to look at (they are evidence for a
// review, not assertions).
//
//   forced-colors: active     every text field keeps a border, every button a border or a text label; the focus indicator of the first stops is an
//                             outline of at least 1 px (box-shadow rings are removed by the browser in this mode)
//   prefers-contrast: more    the same boundary rule
//   prefers-reduced-motion    no transition or animation longer than 0.01 s, no smooth scrolling, no running animation
//   prefers-color-scheme      with the theme set to "system", dark darkens the page and keeps body text at 4.5:1; axe runs on the dark views

import { analyzeAffordances, analyzeColourScheme, analyzeMotion, analyzeStops, analyzeAxe } from "./logic.mjs";
import { walk } from "./keyboard.mjs";
import { enterState, loadApp, runAxe, STATES } from "./states.mjs";

const VIEWS = ["agents", "kanban", "about", "settings-dialog"];
const THEME_KEY = "autonogrammer.theme";

async function eachView(page, report, group, visit) {
  for (const name of VIEWS) {
    const state = STATES.find((s) => s.name === name);
    await page.spaNavigate("/");
    if (!(await enterState(page, state, report, { group, viewport: "1440x900" }))) continue;
    await visit(name);
  }
}

async function shot(page, report, options, name) {
  if (options.reportDir) report.screenshot(name, await page.screenshot());
}

/** A short Tab walk under forced colours: the first stops must show an outline. */
async function forcedColoursFocus(page, report) {
  await page.spaNavigate("/");
  const focused = await page.evalFn(() => {
    const link = document.querySelector(".autonogrammer-skip-link");
    if (!link) return false;
    link.focus();
    return document.activeElement === link;
  });
  if (!focused) return;
  await page.waitForCalm(1000);
  const { stops } = await walk(page, "forward", await page.lib("probeStop"), 30);
  report.count("media:forced-colors-stops", stops.length);
  const findings = analyzeStops(stops, { state: "forced-colors walk", viewport: "1440x900" }).filter((f) => f.id === "focus-not-visible" || f.id === "stop-without-name");
  report.add(findings.map((f) => ({ ...f, check: "media" })));
}

/** Runs the four emulations. */
export async function checkMedia(page, report, options) {
  await page.setViewport(1440, 900);
  const ctx = { group: "media", viewport: "1440x900" };
  // Start from an empty storage of this isolated context, then ask for the "system" theme so the colour scheme of the emulation decides.
  await page.navigate(options.baseUrl);
  await page.evalValue(`localStorage.setItem(${JSON.stringify(THEME_KEY)}, "system"); true`);
  try {
    if (!(await loadApp(page, options.baseUrl, report, ctx))) return;

    for (const emulation of [
      { id: "forced-colors", features: [{ name: "forced-colors", value: "active" }] },
      { id: "prefers-contrast", features: [{ name: "prefers-contrast", value: "more" }] },
    ]) {
      await page.setMedia(emulation.features);
      await page.settle();
      await eachView(page, report, "media", async (view) => {
        const data = await page.lib("collectControlAffordances");
        report.count(`media:${emulation.id}:controls`, data.controls.length);
        report.add(analyzeAffordances(data, emulation.id === "forced-colors" ? "forced-colors" : "prefers-contrast", { ...ctx, state: `${emulation.id} ${view}` }));
        await shot(page, report, options, `${emulation.id}-${view}`);
      });
      if (emulation.id === "forced-colors") await forcedColoursFocus(page, report);
    }

    await page.setMedia([{ name: "prefers-reduced-motion", value: "reduce" }]);
    await page.settle();
    await eachView(page, report, "media", async (view) => {
      await page.waitForCalm(2000);
      const data = await page.lib("collectMotion");
      report.add(analyzeMotion(data, { ...ctx, state: `reduced-motion ${view}` }));
      report.count("media:reduced-motion:views");
      await shot(page, report, options, `reduced-motion-${view}`);
    });

    // Colour scheme: measure the light page, then the dark one after a reload (the theme boot script reads the setting once per load).
    await page.setMedia([{ name: "prefers-color-scheme", value: "light" }]);
    await page.settle();
    const light = await page.lib("probeThemeColours");
    await page.setMedia([{ name: "prefers-color-scheme", value: "dark" }]);
    if (await loadApp(page, options.baseUrl, report, ctx)) {
      const dark = await page.lib("probeThemeColours");
      report.add(analyzeColourScheme(light, dark, { ...ctx, state: "colour scheme" }));
      await eachView(page, report, "media", async (view) => {
        const run = await runAxe(page, options.axeJs);
        report.add(analyzeAxe(run, { ...ctx, state: `dark ${view}` }).map((f) => ({ ...f, check: "media" })));
        report.count("media:dark:axe-runs");
        await shot(page, report, options, `dark-${view}`);
      });
    }
  } finally {
    await page.setMedia([]);
    await page.evalValue(`localStorage.removeItem(${JSON.stringify(THEME_KEY)}); true`).catch(() => {});
  }
}
