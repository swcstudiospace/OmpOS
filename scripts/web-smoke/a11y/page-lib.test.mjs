// Self-tests of the pure parts of page-lib.mjs: colour maths, focus indicator classification, accessible name and role. They need no browser:
//   node --test scripts/web-smoke/a11y/*.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import { documentOf, el } from "./fake-dom.mjs";
import {
  accessibleName,
  circleIntersectsRect,
  compositeOver,
  contrastRatio,
  describeFocusRing,
  longestDuration,
  pageLibSource,
  parseBoxShadow,
  parseColor,
  pathKey,
  rectsOverlapArea,
  relativeLuminance,
  roleOf,
  splitTopLevel,
} from "./page-lib.mjs";

const WHITE = { r: 255, g: 255, b: 255 };
const BLACK = { r: 0, g: 0, b: 0 };

// ---- colour --------------------------------------------------------------------------------------------------

test("parseColor reads rgb, rgba, the space syntax with alpha, color(srgb) and transparent", () => {
  assert.deepEqual(parseColor("rgb(10, 20, 30)"), { r: 10, g: 20, b: 30, a: 1 });
  assert.deepEqual(parseColor("rgba(10, 20, 30, 0.5)"), { r: 10, g: 20, b: 30, a: 0.5 });
  assert.deepEqual(parseColor("rgb(10 20 30 / 25%)"), { r: 10, g: 20, b: 30, a: 0.25 });
  assert.deepEqual(parseColor("color(srgb 1 0.5 0 / 0.5)"), { r: 255, g: 127.5, b: 0, a: 0.5 });
  assert.deepEqual(parseColor("transparent"), { r: 0, g: 0, b: 0, a: 0 });
  assert.equal(parseColor("oklch(0.5 0.1 200)"), null);
  assert.equal(parseColor(undefined), null);
  assert.equal(parseColor("rgb(1, 2)"), null);
});

test("contrastRatio matches the WCAG reference values", () => {
  assert.equal(Math.round(contrastRatio(BLACK, WHITE) * 100) / 100, 21);
  assert.equal(contrastRatio(WHITE, WHITE), 1);
  // #767676 on white is the lightest grey that reaches 4.5:1.
  assert.ok(Math.abs(contrastRatio({ r: 0x76, g: 0x76, b: 0x76 }, WHITE) - 4.54) < 0.01);
  assert.ok(relativeLuminance(WHITE) > 0.99 && relativeLuminance(BLACK) === 0);
});

test("compositeOver blends by alpha over an opaque colour", () => {
  assert.deepEqual(compositeOver({ r: 0, g: 0, b: 0, a: 0.5 }, WHITE), { r: 127.5, g: 127.5, b: 127.5, a: 1 });
  assert.deepEqual(compositeOver({ r: 10, g: 20, b: 30, a: 1 }, WHITE), { r: 10, g: 20, b: 30, a: 1 });
});

// ---- focus indicator -----------------------------------------------------------------------------------------

test("splitTopLevel keeps commas inside parentheses together", () => {
  assert.deepEqual(splitTopLevel("rgb(0, 0, 0) 0px 0px 0px 2px, rgba(1, 2, 3, 0.5) 0 0 4px"), ["rgb(0, 0, 0) 0px 0px 0px 2px", "rgba(1, 2, 3, 0.5) 0 0 4px"]);
});

test("parseBoxShadow reads colour, offsets, blur, spread and inset", () => {
  assert.deepEqual(parseBoxShadow("none"), []);
  const [ring, inner] = parseBoxShadow("rgb(0, 95, 204) 0px 0px 0px 3px, inset rgba(0, 0, 0, 0.2) 0px 1px 2px 0px");
  assert.deepEqual(ring, { color: "rgb(0, 95, 204)", x: 0, y: 0, blur: 0, spread: 3, inset: false });
  assert.equal(inner.inset, true);
  assert.equal(inner.y, 1);
});

test("describeFocusRing accepts an outline of 2 px or more with 3:1 contrast", () => {
  const ring = describeFocusRing({ outlineStyle: "solid", outlineWidth: "2px", outlineColor: "rgb(0, 0, 0)", boxShadow: "none" }, WHITE, false);
  assert.equal(ring.visible, true);
  assert.equal(ring.kind, "outline");
  assert.equal(ring.contrast, 21);
});

test("describeFocusRing rejects a thin, a pale and an absent outline, with the reason", () => {
  const thin = describeFocusRing({ outlineStyle: "solid", outlineWidth: "1px", outlineColor: "rgb(0, 0, 0)", boxShadow: "none" }, WHITE, false);
  assert.equal(thin.visible, false);
  assert.match(thin.reason, /1px/);
  const pale = describeFocusRing({ outlineStyle: "solid", outlineWidth: "3px", outlineColor: "rgb(200, 200, 200)", boxShadow: "none" }, WHITE, false);
  assert.equal(pale.visible, false);
  assert.match(pale.reason, /below 3:1/);
  const none = describeFocusRing({ outlineStyle: "none", outlineWidth: "0px", outlineColor: "rgb(0, 0, 0)", boxShadow: "none" }, WHITE, false);
  assert.equal(none.visible, false);
  assert.equal(none.kind, "none");
});

test("describeFocusRing accepts the auto style and a box-shadow ring, but only outside forced colours", () => {
  assert.equal(describeFocusRing({ outlineStyle: "auto", outlineWidth: "1px", outlineColor: "rgb(16, 16, 16)", boxShadow: "none" }, WHITE, false).visible, true);
  const shadow = { outlineStyle: "none", outlineWidth: "0px", outlineColor: "rgb(0, 0, 0)", boxShadow: "rgb(0, 95, 204) 0px 0px 0px 3px" };
  assert.deepEqual(describeFocusRing(shadow, WHITE, false).kind, "box-shadow");
  assert.equal(describeFocusRing(shadow, WHITE, true).visible, false);
});

test("describeFocusRing under forced colours needs 1 px, and judges geometry only when the background is unknown", () => {
  assert.equal(describeFocusRing({ outlineStyle: "solid", outlineWidth: "1px", outlineColor: "rgb(0, 0, 0)", boxShadow: "none" }, WHITE, true).visible, true);
  const unknown = describeFocusRing({ outlineStyle: "solid", outlineWidth: "2px", outlineColor: "rgb(250, 250, 250)", boxShadow: "none" }, null, false);
  assert.equal(unknown.visible, true);
  assert.equal(unknown.contrast, null);
});

// ---- geometry ------------------------------------------------------------------------------------------------

test("longestDuration reads seconds and milliseconds lists", () => {
  assert.equal(longestDuration("0.15s, 200ms"), 0.2);
  assert.equal(longestDuration("0s"), 0);
  assert.equal(longestDuration(""), 0);
  assert.equal(longestDuration("1e-05s"), 0.00001);
});

test("rectsOverlapArea and circleIntersectsRect", () => {
  assert.equal(rectsOverlapArea({ left: 0, top: 0, right: 10, bottom: 10 }, { left: 5, top: 5, right: 15, bottom: 15 }), 25);
  assert.equal(rectsOverlapArea({ left: 0, top: 0, right: 10, bottom: 10 }, { left: 10, top: 0, right: 20, bottom: 10 }), 0);
  const box = { left: 20, top: 0, right: 40, bottom: 20 };
  assert.equal(circleIntersectsRect(10, 10, 12, box), true);
  assert.equal(circleIntersectsRect(10, 10, 10, box), false, "touching is not intersecting");
  assert.equal(circleIntersectsRect(30, 10, 1, box), true, "a centre inside the rectangle");
});

// ---- accessible name and role --------------------------------------------------------------------------------

function named(node) {
  documentOf(node);
  return accessibleName(node);
}

test("accessibleName: aria-label beats content; aria-labelledby beats aria-label and may name hidden text", () => {
  assert.equal(named(el("button", { "aria-label": "Close" }, ["x"])), "Close");
  const root = el("div", {}, [el("button", { "aria-labelledby": "a b", "aria-label": "ignored" }), el("span", { id: "a" }, ["First"]), el("span", { id: "b", hidden: "" }, ["Second"])]);
  documentOf(root);
  assert.equal(accessibleName(root.children[0]), "First Second");
});

test("accessibleName: a button takes its text, and an aria-hidden icon adds nothing", () => {
  const button = el("button", { type: "button" }, [el("svg", { "aria-hidden": "true" }, [el("title", {}, ["Icon"])]), "  New ", el("span", {}, ["conversation"])]);
  assert.equal(named(button), "New conversation");
  assert.equal(named(el("button", {}, [el("svg", {})])), "");
});

test("accessibleName: labels by for and by wrapping, then placeholder and title as the last resort", () => {
  const form = el("form", {}, [el("label", { for: "t" }, ["Access token"]), el("input", { id: "t", type: "password" })]);
  documentOf(form);
  assert.equal(accessibleName(form.children[1]), "Access token");
  const wrapped = el("div", {}, [el("label", {}, ["Name ", el("input", { type: "text" })])]);
  documentOf(wrapped);
  assert.equal(accessibleName(wrapped.children[0].children[0]), "Name");
  assert.equal(named(el("input", { type: "text", placeholder: "Search" })), "Search");
  assert.equal(named(el("a", { href: "/", title: "Home" })), "Home");
  assert.equal(named(el("input", { type: "text" })), "");
});

test("accessibleName: images, submit buttons, headings, svg titles", () => {
  assert.equal(named(el("img", { alt: "Logo" })), "Logo");
  assert.equal(named(el("img", { alt: "" })), "");
  assert.equal(named(el("input", { type: "submit" })), "Submit");
  assert.equal(named(el("input", { type: "submit", value: "Connect" })), "Connect");
  assert.equal(named(el("h1", {}, ["Connect to Autonogrammer"])), "Connect to Autonogrammer");
  assert.equal(named(el("svg", {}, [el("title", {}, ["Warning"])])), "Warning");
  assert.equal(named(el("a", { href: "/x" }, [el("span", { hidden: "" }, ["secret"]), "Visible"])), "Visible");
});

test("roleOf: explicit role, native roles, and the scoping of header and footer", () => {
  assert.equal(roleOf(el("div", { role: "tab button" })), "tab");
  assert.equal(roleOf(el("button")), "button");
  assert.equal(roleOf(el("a", { href: "/" })), "link");
  assert.equal(roleOf(el("a")), "generic");
  assert.equal(roleOf(el("input", { type: "password" })), "textbox");
  assert.equal(roleOf(el("input", { type: "checkbox" })), "checkbox");
  assert.equal(roleOf(el("select")), "combobox");
  assert.equal(roleOf(el("h3")), "heading");
  const page = el("div", {}, [el("header"), el("main", {}, [el("header"), el("section", { "aria-label": "Session" }), el("section")]), el("footer")]);
  documentOf(page);
  const [top, main, bottom] = page.children;
  assert.equal(roleOf(top), "banner");
  assert.equal(roleOf(bottom), "contentinfo");
  assert.equal(roleOf(main), "main");
  assert.equal(roleOf(main.children[0]), "generic", "a header inside main is not a banner");
  assert.equal(roleOf(main.children[1]), "region");
  assert.equal(roleOf(main.children[2]), "generic");
});

test("pathKey is hierarchical and stable", () => {
  const body = el("body", {}, [el("div"), el("div", {}, [el("button"), el("button")])]);
  const html = el("html", {}, [body]);
  documentOf(html);
  assert.equal(pathKey(body.children[1].children[1]), "body:1>div:2>button:2");
});

// ---- serialisation -------------------------------------------------------------------------------------------

test("the library serialises into one self-contained scope that still works", () => {
  const holder = { window: {} };
  const lib = new Function("window", `return ${pageLibSource()};`)(holder.window);
  for (const name of ["parseColor", "accessibleName", "roleOf", "describeFocusRing", "probeStop", "probeRouteState", "collectTargets", "collectReflow", "collectMotion", "focusByKey"]) {
    assert.equal(typeof lib[name], "function", name);
  }
  assert.equal(holder.window.__a11yLib, lib, "installed once on window");
  const button = el("button", { type: "button" }, ["Save ", el("span", {}, ["now"])]);
  documentOf(button);
  assert.equal(lib.accessibleName(button), "Save now");
  assert.equal(lib.roleOf(button), "button");
  assert.equal(lib.describeFocusRing({ outlineStyle: "solid", outlineWidth: "2px", outlineColor: "rgb(0, 0, 0)", boxShadow: "none" }, WHITE, false).visible, true);
  assert.equal(new Function("window", `return ${pageLibSource()};`)(holder.window), lib, "a second evaluation reuses the installed library");
});
