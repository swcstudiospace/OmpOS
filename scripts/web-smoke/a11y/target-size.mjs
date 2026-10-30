// Target size, minimum (WCAG 2.5.8): every visible interactive element is at least 24 by 24 CSS pixels, or a 24 px circle centred on it touches no other
// target. Inline links inside a sentence, disabled controls, hidden controls and the screen-reader-only skip link at rest are outside the criterion.

import { analyzeTargets } from "./logic.mjs";
import { visitStates } from "./states.mjs";

/** Measures the targets of the current view and adds the findings. */
export async function probeTargetSize(page, report, ctx) {
  const targets = await page.lib("collectTargets");
  report.count("target-size:targets", targets.length);
  report.add(analyzeTargets(targets, ctx));
}

/** Target size of every reachable state at both viewports. */
export async function checkTargetSize(page, report, options) {
  await visitStates(page, report, { ...options, group: "target-size" }, (ctx) => probeTargetSize(page, report, ctx));
}
