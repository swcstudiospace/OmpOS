// Text spacing (WCAG 1.4.12): the user's style sheet sets line height 1.5, letter spacing 0.12em, word spacing 0.16em and paragraph spacing 2em. The check
// measures every state before and after the styles are applied. Text containers that start to clip their text, and text boxes that start to overlap a
// sibling, are errors; clipping that was already there (single-line labels that end in an ellipsis on purpose) is a warning.

import { analyzeTextSpacing } from "./logic.mjs";
import { visitStates } from "./states.mjs";

export const TEXT_SPACING_CSS = "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } p { margin-bottom: 2em !important; }";
const STYLE_ID = "a11y-text-spacing";

async function measure(page) {
  return { clipping: await page.lib("collectTextClipping"), boxes: await page.lib("collectTextBoxes") };
}

/** Measures the current view before and after the spacing styles and adds the findings. */
export async function probeTextSpacing(page, report, ctx) {
  const before = await measure(page);
  await page.evalFn((css, id) => {
    const style = document.createElement("style");
    style.id = id;
    style.textContent = css;
    document.head.appendChild(style);
    return true;
  }, TEXT_SPACING_CSS, STYLE_ID);
  try {
    await page.settle();
    const after = await measure(page);
    report.count("text-spacing:views");
    report.add(analyzeTextSpacing(before, after, ctx));
  } finally {
    await page.evalFn((id) => document.getElementById(id)?.remove(), STYLE_ID);
  }
}

/** Text spacing of every reachable state at both viewports. */
export async function checkTextSpacing(page, report, options) {
  await visitStates(page, report, { ...options, group: "text-spacing" }, (ctx) => probeTextSpacing(page, report, ctx));
}
