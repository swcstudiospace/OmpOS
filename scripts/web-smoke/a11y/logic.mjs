// The judgement half of the accessibility checks: pure functions that turn what the page reported (page-lib.mjs) into findings. Nothing here
// touches a browser, so the Node self-tests run every rule with hand-made data.

import { circleIntersectsRect, rectsOverlapArea } from "./page-lib.mjs";
import { finding } from "./report.mjs";

const PRODUCT_TITLE = "Autonogrammer";
/** The narrowest viewport, in CSS pixels, at which the agent rail is a column and not a drawer. */
export const RAIL_COLUMN_MIN_WIDTH = 840;

const where = (ctx) => ({ state: ctx.state, viewport: ctx.viewport });

// ---- routes ------------------------------------------------------------------------------------------------

/**
 * Judges one route snapshot.
 * entry: { route, mode: "direct" | "inapp", layout: "shell" | "bare", status, snap (probeRouteState), previousTitle, expectFocus: ("h1" | "conversation" | "dialog" | "trigger")[], expectAnnouncement }
 */
export function analyzeRoute(entry) {
  const out = [];
  const at = { state: `route ${entry.route}`, viewport: entry.mode };
  const { snap } = entry;
  if (entry.mode === "direct" && entry.status !== 200) {
    out.push(finding("routes", "http-status", "error", `${entry.route} answered HTTP ${entry.status}, not 200 (the static host must rewrite deep links to index.html)`, { ...at, wcag: "2.4.2" }));
  }
  if (!snap.title || snap.title.trim() === "") {
    out.push(finding("routes", "title-missing", "error", `${entry.route} has an empty document.title`, { ...at, wcag: "2.4.2" }));
  } else if (snap.title.trim() === PRODUCT_TITLE) {
    out.push(finding("routes", "title-is-boot-title", "error", `${entry.route} still shows the boot title "${PRODUCT_TITLE}", not a page title`, { ...at, wcag: "2.4.2" }));
  }
  if (entry.mode === "inapp" && entry.previousTitle !== undefined && entry.previousTitle === snap.title) {
    out.push(finding("routes", "title-unchanged", "error", `the title did not change when navigating to ${entry.route} (still "${snap.title}")`, { ...at, wcag: "2.4.2" }));
  }
  if (snap.headings.length !== 1) {
    out.push(finding("routes", "h1-count", "error", `${entry.route} has ${snap.headings.length} visible h1 elements, exactly one is required`, { ...at, wcag: "1.3.1", detail: snap.headings.map((h) => h.text) }));
  }
  out.push(...analyzeLandmarks(snap, entry.layout, at, entry.route));
  if (entry.mode === "inapp") {
    const expected = entry.expectFocus ?? ["h1"];
    const landed = (snap.activeIsH1 && expected.includes("h1")) || (snap.activeIsConversation && expected.includes("conversation")) || (snap.activeInDialog && expected.includes("dialog")) || (snap.activeIsTrigger === true && expected.includes("trigger"));
    if (!landed) {
      out.push(finding("routes", "focus-not-moved", "error", `after navigating to ${entry.route} focus is on ${snap.active ?? "nothing"}, expected ${expected.join(" or ")}`, { ...at, wcag: "2.4.3", detail: snap.active }));
    }
    if (entry.expectAnnouncement !== false && snap.announcer !== snap.title) {
      out.push(finding("routes", "announcer-mismatch", "error", `the route announcer says "${snap.announcer ?? "(no announcer region)"}" but the title is "${snap.title}"`, { ...at, wcag: "4.1.3" }));
    }
  }
  return out;
}

/** Landmarks of one page: shell pages need banner, navigation or tablist, main and contentinfo exactly where they are unique; a bare page needs main. */
export function analyzeLandmarks(snap, layout, at, route) {
  const out = [];
  const count = (role) => snap.landmarks.filter((l) => l.role === role).length;
  for (const role of ["banner", "main", "contentinfo"]) {
    if (count(role) > 1) {
      out.push(finding("routes", "landmark-duplicated", "error", `${route} has ${count(role)} ${role} landmarks, at most one is allowed`, { ...at, wcag: "1.3.1" }));
    }
  }
  if (count("main") === 0) out.push(finding("routes", "landmark-missing", "error", `${route} has no main landmark`, { ...at, wcag: "1.3.1" }));
  if (layout === "shell") {
    if (count("banner") === 0) out.push(finding("routes", "landmark-missing", "error", `${route} has no banner landmark`, { ...at, wcag: "1.3.1" }));
    if (count("contentinfo") === 0) out.push(finding("routes", "landmark-missing", "error", `${route} has no contentinfo landmark`, { ...at, wcag: "1.3.1" }));
    if (count("navigation") === 0 && snap.tablists === 0) out.push(finding("routes", "landmark-missing", "error", `${route} has neither a navigation landmark nor a tablist`, { ...at, wcag: "1.3.1" }));
  }
  const seen = new Map();
  for (const l of snap.landmarks) {
    if (["banner", "main", "contentinfo"].includes(l.role)) continue;
    const id = `${l.role}\u0000${l.name}`;
    seen.set(id, (seen.get(id) ?? 0) + 1);
  }
  for (const [id, n] of seen) {
    if (n > 1) {
      const [role, name] = id.split("\u0000");
      out.push(finding("routes", "landmark-name-duplicated", "error", `${route} has ${n} ${role} landmarks named "${name}"; landmarks of one role need distinct names`, { ...at, wcag: "1.3.1" }));
    }
  }
  return out;
}

/** Titles of the distinct routes of one load style must differ from each other. */
export function analyzeTitleSet(entries) {
  const out = [];
  const byTitle = new Map();
  for (const e of entries) {
    const title = e.snap.title?.trim();
    if (!title) continue;
    byTitle.set(title, [...(byTitle.get(title) ?? []), e.route]);
  }
  for (const [title, routes] of byTitle) {
    if (routes.length > 1) {
      out.push(finding("routes", "title-duplicate", "error", `routes ${routes.join(", ")} share the title "${title}"`, { state: "routes", wcag: "2.4.2" }));
    }
  }
  return out;
}

// ---- keyboard ----------------------------------------------------------------------------------------------

/** Judges the focus stops one by one: a name, a role, a visible indicator, in the viewport and not covered. */
export function analyzeStops(stops, ctx) {
  const out = [];
  for (const stop of stops) {
    if (stop.left) continue;
    const target = stop.description;
    const base = { ...where(ctx), target };
    if (!stop.name || stop.name.trim() === "") {
      out.push(finding("keyboard", "stop-without-name", "error", `a focus stop has no accessible name: ${stop.description}`, { ...base, wcag: "4.1.2" }));
    }
    if (!stop.role || stop.role === "generic" || stop.role === "none" || stop.role === "presentation") {
      out.push(finding("keyboard", "stop-without-role", "warning", `a focus stop has no interactive role (${stop.role}): ${stop.description}`, { ...base, wcag: "4.1.2" }));
    }
    if (stop.hasAriaLabel && stop.visibleText && ["button", "link", "tab", "menuitem", "option", "checkbox", "radio", "switch"].includes(stop.role)) {
      const plain = (text) => text.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, " ").trim();
      if (!plain(stop.name).includes(plain(stop.visibleText))) {
        out.push(finding("keyboard", "label-in-name", "warning", `the accessible name "${stop.name}" does not contain the visible text "${stop.visibleText}" (speech-input users say the visible text)`, { ...base, wcag: "2.5.3" }));
      }
    }
    if (!stop.ring.visible) {
      out.push(finding("keyboard", "focus-not-visible", "error", `no visible focus indicator on ${stop.role} "${stop.name}": ${stop.ring.reason}`, { ...base, wcag: "2.4.7", detail: stop.ring }));
    }
    if (!stop.inViewport) {
      out.push(finding("keyboard", "focus-offscreen", "error", `the centre of the focused ${stop.role} "${stop.name}" is outside the viewport`, { ...base, wcag: "2.4.11", detail: stop.rect }));
    } else if (stop.obscuredBy) {
      out.push(finding("keyboard", "focus-obscured", "error", `the focused ${stop.role} "${stop.name}" is covered by ${stop.obscuredBy}`, { ...base, wcag: "2.4.11" }));
    }
    if (stop.inert) {
      out.push(finding("keyboard", "focus-in-inert", "error", `focus reached an element inside an inert subtree: ${stop.description}`, { ...base, wcag: "2.1.2" }));
    }
  }
  return out;
}

/**
 * Judges a forward and a backward walk.
 * forward: the stops from the first (the skip link) to the last one before Tab leaves the page or wraps; backward: the stops met by Shift+Tab from the last forward stop,
 * starting with that stop itself. result flags: forwardEnded (the walk ended by wrapping or leaving the page) and backwardEnded.
 */
export function analyzeWalk(forward, backward, ctx) {
  const out = [];
  const base = where(ctx);
  const stops = forward.filter((s) => !s.left);
  if (!ctx.forwardEnded) {
    out.push(finding("keyboard", "walk-no-end", "error", `Tab did not leave or wrap the page within ${stops.length} stops: a focus loop or a very long page`, { ...base, wcag: "2.1.2" }));
  }
  if (stops.length < 3) {
    out.push(finding("keyboard", "walk-too-short", "error", `only ${stops.length} focus stops were found`, { ...base, wcag: "2.1.1" }));
    return out;
  }
  const seen = new Map();
  stops.forEach((s, index) => {
    if (seen.has(s.key)) {
      out.push(finding("keyboard", "focus-trap", "error", `the focus returned to ${s.description} at stop ${index + 1} after stop ${seen.get(s.key) + 1} without leaving the page: a keyboard trap or a loop`, { ...base, wcag: "2.1.2" }));
    } else {
      seen.set(s.key, index);
    }
  });
  for (const landmark of ctx.requiredLandmarks ?? []) {
    if (!stops.some((s) => s.landmark === landmark)) {
      out.push(finding("keyboard", "landmark-unreachable", "error", `no focus stop inside <${landmark}>: it cannot be reached with Tab`, { ...base, wcag: "2.1.1" }));
    }
  }
  if (!stops[0].description.includes("autonogrammer-skip-link")) {
    out.push(finding("keyboard", "skip-link-not-first", "error", `the first focus stop is ${stops[0].description}, not the skip link`, { ...base, wcag: "2.4.1" }));
  }
  if (!ctx.backwardEnded) {
    out.push(finding("keyboard", "backward-walk-no-end", "error", "Shift+Tab did not leave or wrap the page: a focus loop", { ...base, wcag: "2.1.2" }));
  }
  const expected = stops.slice(0, -1).map((s) => s.key).reverse();
  const actual = backward.filter((s) => !s.left).slice(1).map((s) => s.key);
  const length = Math.max(expected.length, actual.length);
  for (let index = 0; index < length; index += 1) {
    if (expected[index] !== actual[index]) {
      out.push(finding("keyboard", "backward-path-differs", "error", `Shift+Tab does not return along the Tab path: at step ${index + 1} it reached ${actual[index] ?? "(the end)"}, expected ${expected[index] ?? "(the end)"}`, { ...base, wcag: "2.4.3", detail: { expected: expected.slice(index, index + 3), actual: actual.slice(index, index + 3) } }));
      break;
    }
  }
  return out;
}

/** Judges the Settings dialog test. observed: { opened, focusInside, stayedInside (bool), escapeClosed, returnedToOpener, leaks: string[] }. */
export function analyzeDialog(observed, ctx) {
  const out = [];
  const base = { ...where(ctx), wcag: "2.1.2" };
  if (!observed.opened) {
    out.push(finding("dialog", "dialog-not-opened", "error", "the Settings dialog did not open from the keyboard", base));
    return out;
  }
  if (!observed.focusInside) out.push(finding("dialog", "focus-not-in-dialog", "error", `focus is on ${observed.activeAtOpen} after the dialog opened, not inside it`, { ...base, wcag: "2.4.3" }));
  if (!observed.stayedInside) out.push(finding("dialog", "focus-leaves-dialog", "error", `Tab or Shift+Tab moved focus out of the open dialog to ${observed.leaks[0]}`, base));
  if (!observed.escapeClosed) out.push(finding("dialog", "escape-does-not-close", "error", "Escape did not close the dialog", { ...base, wcag: "2.1.1" }));
  else if (!observed.returnedToOpener) out.push(finding("dialog", "focus-not-returned", "error", `after Escape focus is on ${observed.activeAfterClose}, not on the control that opened the dialog`, { ...base, wcag: "2.4.3" }));
  return out;
}

// ---- target size -------------------------------------------------------------------------------------------

const related = (a, b) => a === b || a.startsWith(`${b}>`) || b.startsWith(`${a}>`);

/**
 * WCAG 2.5.8: a target smaller than 24 by 24 CSS pixels passes when a 24 px circle centred on its box touches no other target (or the circle of
 * another undersized target). Inline links in a sentence are exempt as targets to size, and count as ordinary boxes for the spacing of their neighbours. Returns the failing targets with the neighbour that breaks the spacing.
 */
export function undersizedTargets(targets, minimum = 24) {
  const small = (t) => t.rect.width < minimum || t.rect.height < minimum;
  const failing = [];
  for (const target of targets) {
    if (!small(target) || target.inline) continue;
    const cx = target.rect.left + target.rect.width / 2;
    const cy = target.rect.top + target.rect.height / 2;
    let neighbour = null;
    for (const other of targets) {
      if (other === target || related(other.key, target.key)) continue;
      const hit = small(other) && !other.inline
        ? Math.hypot(cx - (other.rect.left + other.rect.width / 2), cy - (other.rect.top + other.rect.height / 2)) < minimum
        : circleIntersectsRect(cx, cy, minimum / 2, other.rect);
      if (hit) {
        neighbour = other;
        break;
      }
    }
    if (neighbour) failing.push({ target, neighbour });
  }
  return failing;
}

/** Findings of the target-size check for one state. */
export function analyzeTargets(targets, ctx) {
  return undersizedTargets(targets).map(({ target, neighbour }) =>
    finding("target-size", "target-too-small", "error", `${Math.round(target.rect.width)}x${Math.round(target.rect.height)} px target "${target.name}" is under 24x24 and a 24 px circle on it touches "${neighbour.name}"`, {
      ...where(ctx),
      target: target.description,
      wcag: "2.5.8",
      detail: { size: [target.rect.width, target.rect.height], neighbour: neighbour.description },
    }),
  );
}

// ---- reflow ------------------------------------------------------------------------------------------------

/** Findings of the reflow check (WCAG 1.4.10) for one view and one viewport. */
export function analyzeReflow(data, ctx) {
  const out = [];
  const base = { ...where(ctx), wcag: "1.4.10" };
  if (data.rootScrollWidth > data.rootClientWidth) {
    out.push(finding("reflow", "page-scrolls-sideways", "error", `the document is ${data.rootScrollWidth}px wide in a ${data.rootClientWidth}px viewport`, { ...base, detail: data.offenders.slice(0, 5).map((o) => o.description) }));
  }
  if (data.bodyScrollWidth > data.bodyClientWidth + 1) {
    out.push(finding("reflow", "body-scrolls-sideways", "error", `the body is ${data.bodyScrollWidth}px wide in a ${data.bodyClientWidth}px box`, base));
  }
  for (const o of data.offenders) {
    if (o.declared) continue;
    if (o.fixed && !o.clippedBy && data.rootScrollWidth <= data.rootClientWidth) {
      out.push(finding("reflow", "fixed-element-wider-than-viewport", "error", `a fixed element reaches ${o.right}px in a ${data.viewportWidth}px viewport: ${o.description}`, { ...base, target: o.description }));
    } else if (!o.clippedBy) {
      out.push(finding("reflow", "element-wider-than-viewport", "error", `an element reaches ${o.right}px in a ${data.viewportWidth}px viewport and nothing scrolls it: ${o.description}`, { ...base, target: o.description }));
    } else {
      out.push(finding("reflow", "wide-content-in-undeclared-container", "warning", `content reaches ${o.right}px inside ${o.clippedBy.description} (overflow-x ${o.clippedBy.overflowX}) that is not a named, focusable scroll region`, { ...base, target: o.description }));
    }
  }
  return out;
}

// ---- text spacing ------------------------------------------------------------------------------------------

/** Text boxes that overlap a sibling's text box by more than minArea CSS px squared; absolutely positioned content is left out on purpose. */
export function findOverlaps(boxes, minArea = 6) {
  const byParent = new Map();
  for (const box of boxes) {
    if (box.positioned) continue;
    const list = byParent.get(box.parentKey) ?? [];
    list.push(box);
    byParent.set(box.parentKey, list);
  }
  const result = [];
  for (const list of byParent.values()) {
    const cap = Math.min(list.length, 60);
    for (let i = 0; i < cap; i += 1) {
      for (let j = i + 1; j < cap; j += 1) {
        if (related(list[i].key, list[j].key)) continue;
        const area = rectsOverlapArea(list[i].rect, list[j].rect);
        if (area > minArea) result.push({ a: list[i], b: list[j], area });
      }
    }
  }
  return result;
}

/** Splits clipped containers into the ones the spacing introduced and the ones that clipped before. */
export function diffClipping(before, after) {
  const known = new Set(before.map((c) => c.key));
  return { introduced: after.filter((c) => !known.has(c.key)), preexisting: after.filter((c) => known.has(c.key)) };
}

/** Findings of the text-spacing check (WCAG 1.4.12): clipping and overlap that only the spacing caused are errors; the rest are warnings. */
export function analyzeTextSpacing(before, after, ctx) {
  const out = [];
  const base = { ...where(ctx), wcag: "1.4.12" };
  const clip = diffClipping(before.clipping, after.clipping);
  for (const c of clip.introduced) {
    out.push(finding("text-spacing", "text-clipped-by-spacing", "error", `text is cut off once the text-spacing styles apply (${c.axis}: scroll ${c.scrollWidth}x${c.scrollHeight} in a ${c.clientWidth}x${c.clientHeight} box): ${c.description}`, { ...base, target: c.description, detail: c }));
  }
  for (const c of clip.preexisting) {
    out.push(finding("text-spacing", "text-clipped", "warning", `text is already cut off or truncated before the styles apply (${c.ellipsis ? "ellipsis" : "overflow hidden"}): ${c.description}`, { ...base, target: c.description }));
  }
  const baseline = new Set(findOverlaps(before.boxes).map((o) => `${o.a.key}|${o.b.key}`));
  for (const o of findOverlaps(after.boxes)) {
    const was = baseline.has(`${o.a.key}|${o.b.key}`);
    out.push(finding("text-spacing", was ? "text-boxes-overlap" : "text-boxes-overlap-by-spacing", was ? "warning" : "error", `text of ${o.a.description} and ${o.b.description} overlaps by ${Math.round(o.area)} px squared${was ? " (already before the styles)" : " after the styles"}`, { ...base, target: o.a.description }));
  }
  return out;
}

// ---- media emulation ---------------------------------------------------------------------------------------

/** Findings for forced colours and increased contrast: every field keeps a border; a button keeps a border or a text label. */
export function analyzeAffordances(data, emulation, ctx) {
  const out = [];
  const base = { ...where(ctx), wcag: "1.4.11" };
  const applied = emulation === "forced-colors" ? data.forcedColors : data.contrastMore;
  if (!applied) {
    out.push(finding("media", "emulation-not-applied", "error", `the emulated ${emulation} media feature is not active in the page, so this run proves nothing for it`, base));
    return out;
  }
  const fields = data.controls.filter((c) => c.kind === "field" && !c.border);
  const iconOnly = data.controls.filter((c) => c.kind === "button" && !c.border && !c.hasText && c.hasIcon);
  const bare = data.controls.filter((c) => c.kind === "button" && !c.border && !c.hasText && !c.hasIcon);
  if (fields.length > 0) out.push(finding("media", "field-without-boundary", "error", `${fields.length} text field(s) have no border under ${emulation}: ${fields[0].description}`, { ...base, target: fields[0].description, detail: fields.slice(0, 5).map((c) => c.description) }));
  if (bare.length > 0) out.push(finding("media", "button-without-affordance", "error", `${bare.length} button(s) have no border, no text and no icon under ${emulation}: ${bare[0].description}`, { ...base, target: bare[0].description, detail: bare.slice(0, 5).map((c) => c.description) }));
  if (iconOnly.length > 0) out.push(finding("media", "icon-button-without-boundary", "warning", `${iconOnly.length} icon-only button(s) have no border under ${emulation}; they rely on the icon and the focus outline: ${iconOnly[0].description}`, { ...base, target: iconOnly[0].description, detail: iconOnly.slice(0, 5).map((c) => c.description) }));
  return out;
}

/** Findings for reduced motion: no transition or animation longer than 0.01 s, no smooth scrolling, nothing running. */
export function analyzeMotion(data, ctx) {
  const out = [];
  const base = { ...where(ctx), wcag: "2.3.3" };
  if (!data.reducedMotion) {
    out.push(finding("media", "emulation-not-applied", "error", "the emulated prefers-reduced-motion media feature is not active in the page", base));
    return out;
  }
  if (data.offenders.length > 0) {
    const first = data.offenders[0];
    out.push(finding("media", "motion-not-reduced", "error", `${data.offenders.length} element(s) still animate under reduced motion (first: ${first.description}, transition ${first.transition}s, animation ${first.animation}s${first.smoothScroll ? ", smooth scroll" : ""})`, { ...base, target: first.description, detail: data.offenders.slice(0, 8) }));
  }
  if (data.runningAnimations > 0) {
    out.push(finding("media", "animation-running", "error", `${data.runningAnimations} CSS animation(s) longer than 0.01 s are running under reduced motion`, base));
  }
  return out;
}

/** The dark colour scheme must change the page colours and keep body text at 4.5:1 or more. */
export function analyzeColourScheme(light, dark, ctx) {
  const out = [];
  const base = { ...where(ctx), wcag: "1.4.3" };
  if (!(dark.luminance < 0.3 && light.luminance > dark.luminance)) {
    out.push(finding("media", "dark-scheme-not-applied", "error", `prefers-color-scheme: dark did not darken the page (background luminance ${dark.luminance.toFixed(2)} dark, ${light.luminance.toFixed(2)} light)`, base));
  }
  if (dark.textContrast !== null && dark.textContrast < 4.5) {
    out.push(finding("media", "dark-body-contrast", "error", `body text contrast is ${dark.textContrast.toFixed(2)}:1 in the dark scheme`, base));
  }
  return out;
}

// ---- axe ---------------------------------------------------------------------------------------------------

/** Findings of one axe run: serious and critical violations are errors, the rest warnings; incomplete results are information. */
export function analyzeAxe(run, ctx) {
  const out = [];
  for (const v of run.violations) {
    const severe = v.impact === "serious" || v.impact === "critical";
    out.push(finding("axe", `axe-${v.id}`, severe ? "error" : "warning", `${v.impact} ${v.id}: ${v.help} (${v.nodes.length} node(s), first ${v.nodes[0]?.target ?? "?"})`, { ...where(ctx), target: v.nodes[0]?.target, detail: { tags: v.tags, nodes: v.nodes.slice(0, 5) } }));
  }
  for (const i of run.incomplete) {
    out.push(finding("axe", `axe-incomplete-${i.id}`, "info", `axe could not decide ${i.id} on ${i.nodes.length} node(s)`, { ...where(ctx), target: i.nodes[0]?.target }));
  }
  return out;
}
