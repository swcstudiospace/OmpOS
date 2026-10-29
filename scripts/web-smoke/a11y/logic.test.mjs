// Self-tests of logic.mjs: every rule of the accessibility checks is run on hand-made page data, so a rule that stops firing is noticed without a browser.
//   node --test scripts/web-smoke/a11y/*.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import {
  analyzeAffordances,
  analyzeAxe,
  analyzeColourScheme,
  analyzeDialog,
  analyzeMotion,
  analyzeReflow,
  analyzeRoute,
  analyzeStops,
  analyzeTargets,
  analyzeTextSpacing,
  analyzeTitleSet,
  analyzeWalk,
  diffClipping,
  findOverlaps,
  undersizedTargets,
} from "./logic.mjs";

const ids = (findings) => findings.map((f) => f.id);
const errorsOf = (findings) => findings.filter((f) => f.severity === "error");

// ---- routes --------------------------------------------------------------------------------------------------

const goodSnap = (overrides = {}) => ({
  path: "/kanban",
  title: "Kanban - Autonogrammer",
  headings: [{ text: "Kanban", focused: false, key: "main:1>h1:1" }],
  landmarks: [
    { role: "banner", name: "", tag: "header", key: "a" },
    { role: "navigation", name: "Agents", tag: "nav", key: "b" },
    { role: "main", name: "", tag: "main", key: "c" },
    { role: "contentinfo", name: "", tag: "footer", key: "d" },
  ],
  tablists: 1,
  announcer: "Kanban - Autonogrammer",
  active: "h1",
  activeIsH1: true,
  activeIsConversation: false,
  activeIsSkipLink: false,
  activeInMain: true,
  activeInDialog: false,
  ...overrides,
});

test("analyzeRoute: a correct direct load and a correct in-app navigation have no finding", () => {
  assert.deepEqual(analyzeRoute({ route: "/kanban", mode: "direct", layout: "shell", status: 200, snap: goodSnap() }), []);
  assert.deepEqual(analyzeRoute({ route: "/kanban", mode: "inapp", layout: "shell", snap: goodSnap(), previousTitle: "Agents - Autonogrammer", expectFocus: ["h1"] }), []);
});

test("analyzeRoute: status, empty and boot titles, h1 count", () => {
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 404, snap: goodSnap() })), ["http-status"]);
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: goodSnap({ title: "" }) })), ["title-missing"]);
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: goodSnap({ title: "Autonogrammer" }) })), ["title-is-boot-title"]);
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: goodSnap({ headings: [] }) })), ["h1-count"]);
  const two = goodSnap({ headings: [{ text: "A" }, { text: "B" }] });
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: two })), ["h1-count"]);
});

test("analyzeRoute: landmarks must exist once and be told apart by name", () => {
  const noBanner = goodSnap({ landmarks: goodSnap().landmarks.filter((l) => l.role !== "banner") });
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: noBanner })), ["landmark-missing"]);
  const twoMain = goodSnap({ landmarks: [...goodSnap().landmarks, { role: "main", name: "", tag: "div", key: "e" }] });
  assert.ok(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: twoMain })).includes("landmark-duplicated"));
  const twoNav = goodSnap({ landmarks: [...goodSnap().landmarks, { role: "navigation", name: "Agents", tag: "nav", key: "e" }] });
  assert.ok(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: twoNav })).includes("landmark-name-duplicated"));
  const noNavNoTabs = goodSnap({ landmarks: goodSnap().landmarks.filter((l) => l.role !== "navigation"), tablists: 0 });
  assert.deepEqual(ids(analyzeRoute({ route: "/x", mode: "direct", layout: "shell", status: 200, snap: noNavNoTabs })), ["landmark-missing"]);
  const bare = goodSnap({ landmarks: [{ role: "main", name: "", tag: "main", key: "c" }], tablists: 0 });
  assert.deepEqual(analyzeRoute({ route: "/nope", mode: "direct", layout: "bare", status: 200, snap: bare }), []);
});

test("analyzeRoute: focus and the announcer after an in-app navigation", () => {
  const base = { route: "/kanban", mode: "inapp", layout: "shell", previousTitle: "Agents - Autonogrammer", expectFocus: ["h1"] };
  assert.deepEqual(ids(analyzeRoute({ ...base, snap: goodSnap({ activeIsH1: false, active: "button#x" }) })), ["focus-not-moved"]);
  assert.deepEqual(ids(analyzeRoute({ ...base, snap: goodSnap({ announcer: "Agents - Autonogrammer" }) })), ["announcer-mismatch"]);
  assert.deepEqual(ids(analyzeRoute({ ...base, snap: goodSnap({ announcer: null }) })), ["announcer-mismatch"]);
  assert.deepEqual(ids(analyzeRoute({ ...base, previousTitle: "Kanban - Autonogrammer", snap: goodSnap() })), ["title-unchanged"]);
  assert.deepEqual(analyzeRoute({ ...base, expectFocus: ["trigger"], snap: goodSnap({ activeIsH1: false, activeIsTrigger: true }) }), []);
  assert.deepEqual(analyzeRoute({ ...base, expectFocus: ["dialog"], snap: goodSnap({ activeIsH1: false, activeInDialog: true }) }), []);
  assert.deepEqual(analyzeRoute({ ...base, expectFocus: ["conversation", "h1"], snap: goodSnap({ activeIsH1: false, activeIsConversation: true }) }), []);
});

test("analyzeTitleSet: two routes must not share a title", () => {
  const entry = (route, title) => ({ route, snap: goodSnap({ title }) });
  assert.deepEqual(analyzeTitleSet([entry("/", "A"), entry("/kanban", "B")]), []);
  const dup = analyzeTitleSet([entry("/", "A"), entry("/kanban", "A"), entry("/about", "C")]);
  assert.deepEqual(ids(dup), ["title-duplicate"]);
  assert.match(dup[0].message, /\/, \/kanban/);
});

// ---- keyboard ------------------------------------------------------------------------------------------------

const stop = (key, over = {}) => ({
  left: false,
  key,
  tag: "button",
  id: "",
  role: "button",
  name: key,
  hasAriaLabel: false,
  visibleText: "",
  ring: { visible: true, kind: "outline", width: 2, contrast: 8, reason: "" },
  inViewport: true,
  obscuredBy: null,
  rect: { left: 0, top: 0, width: 40, height: 40 },
  description: `button#${key}`,
  inert: false,
  landmark: "main",
  ...over,
});

test("analyzeStops: name, role, visible focus, viewport, covered, inert and label in name", () => {
  const ctx = { state: "agents view", viewport: "1440x900" };
  assert.deepEqual(analyzeStops([stop("a")], ctx), []);
  assert.deepEqual(ids(analyzeStops([stop("a", { name: " " })], ctx)), ["stop-without-name"]);
  assert.deepEqual(ids(analyzeStops([stop("a", { role: "generic" })], ctx)), ["stop-without-role"]);
  assert.deepEqual(ids(analyzeStops([stop("a", { ring: { visible: false, kind: "none", width: 0, contrast: null, reason: "no outline" } })], ctx)), ["focus-not-visible"]);
  assert.deepEqual(ids(analyzeStops([stop("a", { inViewport: false })], ctx)), ["focus-offscreen"]);
  assert.deepEqual(ids(analyzeStops([stop("a", { obscuredBy: "div.sticky-bar" })], ctx)), ["focus-obscured"]);
  assert.deepEqual(ids(analyzeStops([stop("a", { inert: true })], ctx)), ["focus-in-inert"]);
  const mismatch = analyzeStops([stop("a", { hasAriaLabel: true, name: "Open session 3", visibleText: "Refactor checkout" })], ctx);
  assert.deepEqual(ids(mismatch), ["label-in-name"]);
  assert.equal(mismatch[0].severity, "warning");
  assert.deepEqual(analyzeStops([stop("a", { hasAriaLabel: true, name: "Settings", visibleText: "settings" })], ctx), []);
  assert.deepEqual(analyzeStops([stop("a", { hasAriaLabel: true, name: "Refactor checkout, 2m ago, Running", visibleText: "Refactor checkout 2m ago Running" })], ctx), [], "punctuation between the parts of a name does not matter");
  assert.deepEqual(analyzeStops([{ left: true }], ctx), []);
});

const skip = stop("s", { description: "a.autonogrammer-skip-link", landmark: "" });
const walkCtx = { state: "agents view", viewport: "1440x900", forwardEnded: true, backwardEnded: true, requiredLandmarks: ["header", "main", "footer"] };
const forward = [skip, stop("h", { landmark: "header" }), stop("m", { landmark: "main" }), stop("f", { landmark: "footer" })];

test("analyzeWalk: a clean forward and backward walk has no finding", () => {
  const backward = [forward[3], forward[2], forward[1], forward[0]];
  assert.deepEqual(analyzeWalk(forward, backward, walkCtx), []);
});

test("analyzeWalk: loops, a missing end, a short walk, unreachable landmarks and a backward path that differs", () => {
  const backward = [forward[3], forward[2], forward[1], forward[0]];
  assert.ok(ids(analyzeWalk(forward, backward, { ...walkCtx, forwardEnded: false })).includes("walk-no-end"));
  assert.ok(ids(analyzeWalk(forward, backward, { ...walkCtx, backwardEnded: false })).includes("backward-walk-no-end"));
  assert.deepEqual(ids(analyzeWalk(forward.slice(0, 2), [], walkCtx)), ["walk-too-short"]);
  const trap = [...forward, stop("m", { landmark: "main" })];
  assert.ok(ids(analyzeWalk(trap, backward, walkCtx)).includes("focus-trap"));
  const noFooter = forward.map((s) => (s.landmark === "footer" ? { ...s, landmark: "main" } : s));
  assert.ok(ids(analyzeWalk(noFooter, [noFooter[3], noFooter[2], noFooter[1], noFooter[0]], walkCtx)).includes("landmark-unreachable"));
  assert.ok(ids(analyzeWalk([stop("h", { landmark: "header" }), ...forward.slice(1)], backward, walkCtx)).includes("skip-link-not-first"));
  const skewed = [forward[3], forward[1], forward[2], forward[0]];
  const differs = analyzeWalk(forward, skewed, walkCtx);
  assert.deepEqual(ids(differs), ["backward-path-differs"]);
  assert.match(differs[0].message, /step 1/);
});

test("analyzeDialog: each way the dialog can fail", () => {
  const ok = { opened: true, focusInside: true, stayedInside: true, leaks: [], escapeClosed: true, returnedToOpener: true };
  const ctx = { state: "settings dialog", viewport: "1440x900" };
  assert.deepEqual(analyzeDialog(ok, ctx), []);
  assert.deepEqual(ids(analyzeDialog({ ...ok, opened: false }, ctx)), ["dialog-not-opened"]);
  assert.deepEqual(ids(analyzeDialog({ ...ok, focusInside: false, activeAtOpen: "body" }, ctx)), ["focus-not-in-dialog"]);
  assert.deepEqual(ids(analyzeDialog({ ...ok, stayedInside: false, leaks: ["a#x"] }, ctx)), ["focus-leaves-dialog"]);
  assert.deepEqual(ids(analyzeDialog({ ...ok, escapeClosed: false }, ctx)), ["escape-does-not-close"]);
  assert.deepEqual(ids(analyzeDialog({ ...ok, returnedToOpener: false, activeAfterClose: "body" }, ctx)), ["focus-not-returned"]);
});

// ---- target size ---------------------------------------------------------------------------------------------

const target = (key, left, top, width, height, over = {}) => ({ key, name: key, description: key, inline: false, rect: { left, top, right: left + width, bottom: top + height, width, height }, ...over });

test("undersizedTargets: 24 px passes; a small target passes with space around it and fails next to a neighbour", () => {
  assert.deepEqual(undersizedTargets([target("a", 0, 0, 24, 24), target("b", 24, 0, 24, 24)]), []);
  assert.deepEqual(undersizedTargets([target("a", 0, 0, 16, 16)]), []);
  // Two 20 px targets whose centres are 22 px apart: the 24 px circles intersect.
  const crowded = undersizedTargets([target("a", 0, 0, 20, 20), target("b", 22, 0, 20, 20)]);
  assert.deepEqual(crowded.map((c) => c.target.key), ["a", "b"]);
  // Centres exactly 24 px apart touch but do not intersect.
  assert.deepEqual(undersizedTargets([target("a", 0, 0, 20, 20), target("b", 24, 0, 20, 20)]), []);
  // A small target next to a large one: the circle must stay clear of the large target's box.
  assert.deepEqual(undersizedTargets([target("a", 0, 0, 16, 16), target("big", 18, 0, 40, 40)]).map((c) => c.target.key), ["a"]);
  assert.deepEqual(undersizedTargets([target("a", 0, 0, 16, 16), target("big", 30, 0, 40, 40)]), []);
});

test("undersizedTargets: inline links and nested targets are not neighbours", () => {
  assert.deepEqual(undersizedTargets([target("a", 0, 0, 40, 12, { inline: true }), target("b", 0, 14, 20, 20)]).map((c) => c.target.key), []);
  assert.deepEqual(undersizedTargets([target("div:1>a:1", 0, 0, 16, 16), target("div:1>a:1>span:1", 2, 2, 10, 10)]), []);
});

test("analyzeTargets words the finding with the size and the neighbour", () => {
  const findings = analyzeTargets([target("a", 0, 0, 20, 20, { name: "Close" }), target("b", 22, 0, 20, 20, { name: "Rename" })], { state: "agents", viewport: "1440x900" });
  assert.equal(findings.length, 2);
  assert.match(findings[0].message, /20x20 px target "Close".*"Rename"/);
  assert.equal(findings[0].wcag, "2.5.8");
});

// ---- reflow and text spacing ---------------------------------------------------------------------------------

const reflow = (over = {}) => ({ viewportWidth: 320, innerWidth: 320, devicePixelRatio: 1, rootScrollWidth: 320, rootClientWidth: 320, bodyScrollWidth: 320, bodyClientWidth: 320, offenders: [], ...over });

test("analyzeReflow: sideways scroll, uncontained and contained wide content", () => {
  const ctx = { state: "kanban", viewport: "320x568@1x" };
  assert.deepEqual(analyzeReflow(reflow(), ctx), []);
  assert.deepEqual(ids(analyzeReflow(reflow({ rootScrollWidth: 380 }), ctx)), ["page-scrolls-sideways"]);
  assert.deepEqual(ids(analyzeReflow(reflow({ offenders: [{ description: "div.board", right: 900, fixed: false, declared: false, clippedBy: null }] }), ctx)), ["element-wider-than-viewport"]);
  assert.deepEqual(analyzeReflow(reflow({ offenders: [{ description: "table", right: 900, fixed: false, declared: true, clippedBy: { description: "div", overflowX: "auto" } }] }), ctx), []);
  const undeclared = analyzeReflow(reflow({ offenders: [{ description: "div.row", right: 900, fixed: false, declared: false, clippedBy: { description: "div.wrap", overflowX: "auto" } }] }), ctx);
  assert.deepEqual(ids(undeclared), ["wide-content-in-undeclared-container"]);
  assert.equal(undeclared[0].severity, "warning");
});

const box = (key, parentKey, left, top, right, bottom, positioned = false) => ({ key, parentKey, description: key, positioned, rect: { left, top, right, bottom } });

test("findOverlaps: siblings overlapping, positioned and related boxes ignored", () => {
  assert.equal(findOverlaps([box("p>a", "p", 0, 0, 100, 20), box("p>b", "p", 0, 10, 100, 30)]).length, 1);
  assert.equal(findOverlaps([box("p>a", "p", 0, 0, 100, 20), box("p>b", "p", 0, 20, 100, 40)]).length, 0, "touching edges");
  assert.equal(findOverlaps([box("p>a", "p", 0, 0, 100, 20), box("p>b", "p", 0, 10, 100, 30, true)]).length, 0, "an overlay is meant to overlap");
  assert.equal(findOverlaps([box("p>a", "p", 0, 0, 100, 20), box("q>b", "q", 0, 10, 100, 30)]).length, 0, "different parents are not compared");
  assert.equal(findOverlaps([box("p>a", "p", 0, 0, 100, 20), box("p>a>b", "p", 0, 10, 100, 30)]).length, 0, "an element and its descendant");
});

test("diffClipping and analyzeTextSpacing: only what the spacing introduced is an error", () => {
  const clip = (key, over = {}) => ({ key, description: key, axis: "x", ellipsis: false, scrollWidth: 120, clientWidth: 100, scrollHeight: 20, clientHeight: 20, ...over });
  const split = diffClipping([clip("old")], [clip("old"), clip("new")]);
  assert.deepEqual(split.introduced.map((c) => c.key), ["new"]);
  assert.deepEqual(split.preexisting.map((c) => c.key), ["old"]);
  const before = { clipping: [clip("old", { ellipsis: true })], boxes: [box("p>a", "p", 0, 0, 100, 20), box("p>b", "p", 0, 30, 100, 50)] };
  const after = { clipping: [clip("old", { ellipsis: true }), clip("new")], boxes: [box("p>a", "p", 0, 0, 100, 40), box("p>b", "p", 0, 30, 100, 50)] };
  const findings = analyzeTextSpacing(before, after, { state: "agents", viewport: "1440x900" });
  assert.deepEqual(errorsOf(findings).map((f) => f.id).sort(), ["text-boxes-overlap-by-spacing", "text-clipped-by-spacing"]);
  assert.deepEqual(findings.filter((f) => f.severity === "warning").map((f) => f.id), ["text-clipped"]);
});

// ---- media emulation and axe ---------------------------------------------------------------------------------

const ctl = (kind, over = {}) => ({ key: "k", description: `${kind}#x`, kind, border: true, hasText: true, hasIcon: false, outline: false, ...over });

test("analyzeAffordances: the emulation must be on; fields need a border; buttons a border or a label", () => {
  const ctx = { state: "forced-colors agents", viewport: "1440x900" };
  const data = (controls, over = {}) => ({ forcedColors: true, contrastMore: false, reducedMotion: false, dark: false, controls, ...over });
  assert.deepEqual(analyzeAffordances(data([ctl("field"), ctl("button")]), "forced-colors", ctx), []);
  assert.deepEqual(ids(analyzeAffordances(data([], { forcedColors: false }), "forced-colors", ctx)), ["emulation-not-applied"]);
  assert.deepEqual(ids(analyzeAffordances(data([], { contrastMore: true, forcedColors: false }), "prefers-contrast", ctx)), []);
  assert.deepEqual(ids(analyzeAffordances(data([ctl("field", { border: false })]), "forced-colors", ctx)), ["field-without-boundary"]);
  assert.deepEqual(analyzeAffordances(data([ctl("button", { border: false })]), "forced-colors", ctx), [], "a button with a text label is perceivable");
  assert.deepEqual(ids(analyzeAffordances(data([ctl("button", { border: false, hasText: false, hasIcon: true })]), "forced-colors", ctx)), ["icon-button-without-boundary"]);
  assert.deepEqual(ids(analyzeAffordances(data([ctl("button", { border: false, hasText: false })]), "forced-colors", ctx)), ["button-without-affordance"]);
});

test("analyzeMotion and analyzeColourScheme", () => {
  const ctx = { state: "reduced-motion agents", viewport: "1440x900" };
  assert.deepEqual(analyzeMotion({ offenders: [], runningAnimations: 0, reducedMotion: true }, ctx), []);
  assert.deepEqual(ids(analyzeMotion({ offenders: [], runningAnimations: 0, reducedMotion: false }, ctx)), ["emulation-not-applied"]);
  assert.deepEqual(ids(analyzeMotion({ offenders: [{ description: "div.spinner", transition: 0, animation: 1, smoothScroll: false }], runningAnimations: 2, reducedMotion: true }, ctx)), ["motion-not-reduced", "animation-running"]);
  assert.deepEqual(analyzeColourScheme({ luminance: 0.95 }, { luminance: 0.02, textContrast: 15 }, ctx), []);
  assert.deepEqual(ids(analyzeColourScheme({ luminance: 0.95 }, { luminance: 0.95, textContrast: 15 }, ctx)), ["dark-scheme-not-applied"]);
  assert.deepEqual(ids(analyzeColourScheme({ luminance: 0.95 }, { luminance: 0.02, textContrast: 3 }, ctx)), ["dark-body-contrast"]);
});

test("analyzeAxe: serious and critical are errors, moderate and minor warnings, incomplete information", () => {
  const node = (target) => ({ target, html: "<x>" });
  const run = {
    violations: [
      { id: "color-contrast", impact: "serious", help: "Contrast", tags: ["wcag2aa"], nodes: [node("#a")] },
      { id: "region", impact: "moderate", help: "Region", tags: ["best-practice"], nodes: [node("#b")] },
      { id: "aria-x", impact: "critical", help: "X", tags: [], nodes: [node("#c")] },
    ],
    incomplete: [{ id: "color-contrast", nodes: [node("#d")] }],
  };
  const findings = analyzeAxe(run, { state: "kanban", viewport: "320x568" });
  assert.deepEqual(findings.map((f) => [f.id, f.severity]), [["axe-color-contrast", "error"], ["axe-region", "warning"], ["axe-aria-x", "error"], ["axe-incomplete-color-contrast", "info"]]);
  assert.equal(findings[0].target, "#a");
});
