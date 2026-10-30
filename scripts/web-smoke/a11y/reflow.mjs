// Reflow (WCAG 1.4.10): at 320 CSS px wide no view scrolls sideways and nothing is wider than the viewport unless it sits in a scroll region that is
// declared as one (a focusable, named role=region, a pre, a table or the terminal). Three configurations:
//   320x568 at device pixel ratio 1   the smallest phone width the criterion names
//   320x256 at device pixel ratio 4   a 1280x1024 window at 400 percent browser zoom (1280/4 by 1024/4 CSS pixels)
//   1280x1024 at device pixel ratio 4 the literal "high density at 1280 px" case; it must behave like the desktop
// The viewport change is applied without a reload; the layout follows the media queries at once.

import { analyzeReflow } from "./logic.mjs";
import { visitStates } from "./states.mjs";

export const REFLOW_CONFIGS = [
  { name: "320x568@1x", width: 320, height: 568, deviceScaleFactor: 1 },
  { name: "320x256@4x (400% of 1280x1024)", width: 320, height: 256, deviceScaleFactor: 4 },
  { name: "1280x1024@4x", width: 1280, height: 1024, deviceScaleFactor: 4 },
];

/** Measures the current view and adds the findings. */
export async function probeReflow(page, report, ctx) {
  const data = await page.lib("collectReflow");
  report.count("reflow:views");
  report.add(analyzeReflow(data, ctx));
}

/** Reflow of every reachable state in the three configurations. */
export async function checkReflow(page, report, options) {
  await visitStates(page, report, { ...options, group: "reflow", viewports: REFLOW_CONFIGS }, (ctx) => probeReflow(page, report, ctx));
}
