// Helpers of the accessibility checks that run INSIDE the page. Every function below is self-contained: it references only globals of the
// page (document, getComputedStyle, matchMedia) and the other functions of this list, never a module-level binding, because the whole list is
// serialised with Function.prototype.toString and evaluated once per document (see pageLibSource). The pure ones (colour maths, focus ring
// classification, accessible name and role from a DOM-like tree) are also imported by the Node self-tests, which run them against a hand-made
// tree without a browser.
//
// What is approximated (and therefore reported as such, never as proof): the accessible name follows the accname algorithm for the common
// cases (aria-labelledby, aria-label, native label, alt, legend, caption, svg title, name from content, title, placeholder) and not for
// every corner; colour contrast of the focus ring is measured against the flattened background colours of the ancestors and ignores
// background images and gradients.

const NAME_FROM_CONTENT_ROLES = ["button", "link", "tab", "menuitem", "menuitemcheckbox", "menuitemradio", "option", "checkbox", "radio", "switch", "heading", "treeitem", "tooltip", "cell", "gridcell", "columnheader", "rowheader", "row"];

/** Parses a computed colour (rgb, rgba, the space-separated forms and color(srgb ...)) into 0..255 channels and an alpha of 0..1; null when unknown. */
export function parseColor(value) {
  if (typeof value !== "string") return null;
  const text = value.trim().toLowerCase();
  if (text === "transparent") return { r: 0, g: 0, b: 0, a: 0 };
  let match = /^rgba?\(([^)]+)\)$/.exec(text);
  if (match) {
    const parts = match[1].split(/[\s,/]+/).filter(Boolean);
    if (parts.length < 3) return null;
    const channel = (part) => (part.endsWith("%") ? (parseFloat(part) * 255) / 100 : parseFloat(part));
    const r = channel(parts[0]);
    const g = channel(parts[1]);
    const b = channel(parts[2]);
    const a = parts.length > 3 ? (parts[3].endsWith("%") ? parseFloat(parts[3]) / 100 : parseFloat(parts[3])) : 1;
    return [r, g, b, a].some((n) => Number.isNaN(n)) ? null : { r, g, b, a };
  }
  match = /^color\(\s*srgb\s+([^)]+)\)$/.exec(text);
  if (match) {
    const parts = match[1].split(/[\s/]+/).filter(Boolean);
    if (parts.length < 3) return null;
    const r = parseFloat(parts[0]) * 255;
    const g = parseFloat(parts[1]) * 255;
    const b = parseFloat(parts[2]) * 255;
    const a = parts.length > 3 ? parseFloat(parts[3]) : 1;
    return [r, g, b, a].some((n) => Number.isNaN(n)) ? null : { r, g, b, a };
  }
  return null;
}

/** Composites a colour with alpha over an opaque colour. */
export function compositeOver(top, bottom) {
  const alpha = top.a === undefined ? 1 : top.a;
  return {
    r: top.r * alpha + bottom.r * (1 - alpha),
    g: top.g * alpha + bottom.g * (1 - alpha),
    b: top.b * alpha + bottom.b * (1 - alpha),
    a: 1,
  };
}

/** The WCAG relative luminance of an opaque colour. */
export function relativeLuminance(colour) {
  const channel = (value) => {
    const x = value / 255;
    return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * channel(colour.r) + 0.7152 * channel(colour.g) + 0.0722 * channel(colour.b);
}

/** The WCAG contrast ratio of two opaque colours (1 to 21). */
export function contrastRatio(first, second) {
  const l1 = relativeLuminance(first);
  const l2 = relativeLuminance(second);
  return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
}

/** Splits a CSS value at the commas that are not inside parentheses. */
export function splitTopLevel(text) {
  const parts = [];
  let depth = 0;
  let current = "";
  for (const ch of String(text)) {
    if (ch === "(") depth += 1;
    if (ch === ")") depth -= 1;
    if (ch === "," && depth === 0) {
      parts.push(current.trim());
      current = "";
    } else {
      current += ch;
    }
  }
  if (current.trim() !== "") parts.push(current.trim());
  return parts;
}

/** Parses a computed box-shadow into layers {color, x, y, blur, spread, inset}; "none" gives an empty list. */
export function parseBoxShadow(value) {
  if (typeof value !== "string" || value.trim() === "" || value.trim() === "none") return [];
  return splitTopLevel(value).map((layer) => {
    const inset = /\binset\b/.test(layer);
    const colourMatch = /(rgba?\([^)]*\)|color\([^)]*\)|#[0-9a-f]{3,8}\b)/i.exec(layer);
    const rest = colourMatch ? layer.replace(colourMatch[0], " ") : layer;
    const numbers = (rest.match(/-?\d*\.?\d+(?:px)?/g) || []).map((n) => parseFloat(n));
    return { color: colourMatch ? colourMatch[0] : null, x: numbers[0] || 0, y: numbers[1] || 0, blur: numbers[2] || 0, spread: numbers[3] || 0, inset };
  });
}

/**
 * Classifies the focus indicator of an element from its computed style. An outline (a width of at least 2 px, 1 px under forced colours;
 * the "auto" style counts as 2 px) or a box-shadow ring of a spread of at least 2 px whose colour has a contrast of at least 3:1 with the
 * background it is drawn against is a visible indicator. background is an opaque {r,g,b} or null when it could not be measured; in that case
 * only the geometry is judged and contrast is null.
 */
export function describeFocusRing(style, background, forcedColors) {
  const minWidth = forcedColors ? 1 : 2;
  const measure = (colour) => {
    const parsed = parseColor(colour);
    return parsed && background ? Math.round(contrastRatio(compositeOver(parsed, background), background) * 100) / 100 : null;
  };
  const result = { visible: false, kind: "none", width: 0, contrast: null, reason: "no outline and no box-shadow ring" };
  const outlineStyle = style.outlineStyle || "none";
  const outlineWidth = parseFloat(style.outlineWidth) || 0;
  if (outlineStyle !== "none" && outlineStyle !== "hidden") {
    const width = outlineStyle === "auto" ? Math.max(outlineWidth, 2) : outlineWidth;
    const contrast = measure(style.outlineColor);
    result.kind = "outline";
    result.width = width;
    result.contrast = contrast;
    if (width < minWidth) {
      result.reason = `outline is ${width}px, at least ${minWidth}px is needed`;
    } else if (contrast !== null && contrast < 3) {
      result.reason = `outline contrast ${contrast}:1 is below 3:1`;
    } else {
      result.visible = true;
      result.reason = "";
      return result;
    }
  }
  if (!forcedColors) {
    for (const layer of parseBoxShadow(style.boxShadow)) {
      if (layer.inset || layer.spread < 2) continue;
      const contrast = measure(layer.color);
      if (contrast !== null && contrast < 3) continue;
      return { visible: true, kind: "box-shadow", width: layer.spread, contrast, reason: "" };
    }
  }
  return result;
}

/** The longest duration of a computed transition-duration or animation-duration list, in seconds. */
export function longestDuration(value) {
  if (typeof value !== "string" || value.trim() === "") return 0;
  let longest = 0;
  for (const part of value.split(",")) {
    const text = part.trim();
    const seconds = text.endsWith("ms") ? parseFloat(text) / 1000 : parseFloat(text);
    if (!Number.isNaN(seconds) && seconds > longest) longest = seconds;
  }
  return longest;
}

/** The area, in squared CSS pixels, of the intersection of two rectangles {left, top, right, bottom}. */
export function rectsOverlapArea(a, b) {
  const width = Math.min(a.right, b.right) - Math.max(a.left, b.left);
  const height = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
  return width > 0 && height > 0 ? width * height : 0;
}

/** True when a circle of radius r centred on (cx, cy) overlaps the rectangle; touching is not overlapping. */
export function circleIntersectsRect(cx, cy, r, rect) {
  const nearestX = Math.max(rect.left, Math.min(cx, rect.right));
  const nearestY = Math.max(rect.top, Math.min(cy, rect.bottom));
  return Math.hypot(cx - nearestX, cy - nearestY) < r;
}

function normaliseSpace(text) {
  return String(text).replace(/\s+/g, " ").trim();
}

function isHiddenForName(node) {
  if (node.nodeType !== 1) return false;
  if (node.hidden === true || node.getAttribute("aria-hidden") === "true") return true;
  if (typeof getComputedStyle === "function" && node.isConnected) {
    const style = getComputedStyle(node);
    return style.display === "none" || style.visibility === "hidden";
  }
  return false;
}

/** The accessible name of an element: a simplified accname computation (see the header of this file for what is covered). */
export function accessibleName(element) {
  const roleOfNode = (node) => {
    const explicit = (node.getAttribute("role") || "").trim().split(/\s+/)[0];
    return explicit ? explicit.toLowerCase() : "";
  };
  const fromContent = (node, visited) => {
    let text = "";
    for (const child of Array.from(node.childNodes)) {
      if (child.nodeType === 3) {
        text += child.nodeValue;
      } else if (child.nodeType === 1) {
        if (isHiddenForName(child)) continue;
        const inner = compute(child, visited, { reference: false, content: true });
        text += ` ${inner} `;
      }
    }
    return normaliseSpace(text);
  };
  const compute = (node, visited, mode) => {
    if (visited.has(node)) return "";
    visited.add(node);
    const tag = node.tagName.toLowerCase();
    if (!mode.reference && isHiddenForName(node)) return "";
    const labelledby = node.getAttribute("aria-labelledby");
    if (labelledby && !mode.reference && !mode.content) {
      const doc = node.ownerDocument;
      const names = labelledby
        .split(/\s+/)
        .filter(Boolean)
        .map((id) => doc.getElementById(id))
        .filter(Boolean)
        .map((target) => compute(target, new Set([node]), { reference: true, content: true }))
        .filter((name) => name !== "");
      if (names.length > 0) return normaliseSpace(names.join(" "));
    }
    const ariaLabel = normaliseSpace(node.getAttribute("aria-label") || "");
    if (ariaLabel !== "") return ariaLabel;
    const type = (node.getAttribute("type") || "").toLowerCase();
    if (tag === "img" || tag === "area" || (tag === "input" && type === "image")) {
      const alt = node.getAttribute("alt");
      if (alt !== null && alt !== undefined) return normaliseSpace(alt);
    }
    if (tag === "input" && ["button", "submit", "reset"].includes(type)) {
      const value = normaliseSpace(node.getAttribute("value") || "");
      if (value !== "") return value;
      return type === "submit" ? "Submit" : type === "reset" ? "Reset" : "";
    }
    if (!mode.content && (tag === "input" || tag === "textarea" || tag === "select" || tag === "meter" || tag === "progress")) {
      let labels = node.labels ? Array.from(node.labels) : null;
      if (labels === null) {
        const doc = node.ownerDocument;
        labels = Array.from(doc.querySelectorAll("label")).filter((label) => (node.id && label.getAttribute("for") === node.id) || label.contains(node));
      }
      const names = labels.map((label) => compute(label, new Set([node]), { reference: false, content: true })).filter((name) => name !== "");
      if (names.length > 0) return normaliseSpace(names.join(" "));
    }
    if (tag === "fieldset") {
      const legend = Array.from(node.children).find((child) => child.tagName.toLowerCase() === "legend");
      if (legend) return compute(legend, visited, { reference: false, content: true });
    }
    if (tag === "table") {
      const caption = Array.from(node.children).find((child) => child.tagName.toLowerCase() === "caption");
      if (caption) return compute(caption, visited, { reference: false, content: true });
    }
    if (tag === "svg") {
      const title = Array.from(node.children).find((child) => child.tagName.toLowerCase() === "title");
      if (title) return normaliseSpace(title.textContent || "");
    }
    if (tag === "figure") {
      const caption = Array.from(node.children).find((child) => child.tagName.toLowerCase() === "figcaption");
      if (caption) return compute(caption, visited, { reference: false, content: true });
    }
    const role = roleOfNode(node);
    if (mode.content || NAME_FROM_CONTENT_ROLES.includes(role) || (role === "" && ["button", "a", "summary", "h1", "h2", "h3", "h4", "h5", "h6", "option", "th", "td"].includes(tag))) {
      const text = fromContent(node, visited);
      if (text !== "") return text;
    }
    const title = normaliseSpace(node.getAttribute("title") || "");
    if (title !== "") return title;
    const placeholder = normaliseSpace(node.getAttribute("placeholder") || "");
    if (placeholder !== "" && (tag === "input" || tag === "textarea")) return placeholder;
    return "";
  };
  return compute(element, new Set(), { reference: false, content: false });
}

/** The ARIA role of an element: the first token of role when present, otherwise the implicit role of its tag. */
export function roleOf(element) {
  const explicit = (element.getAttribute("role") || "").trim().split(/\s+/)[0];
  if (explicit) return explicit.toLowerCase();
  const tag = element.tagName.toLowerCase();
  const type = (element.getAttribute("type") || "").toLowerCase();
  const insideSectioning = () => {
    for (let node = element.parentElement; node; node = node.parentElement) {
      if (["article", "aside", "main", "nav", "section"].includes(node.tagName.toLowerCase())) return true;
    }
    return false;
  };
  switch (tag) {
    case "a":
    case "area":
      return element.hasAttribute("href") ? "link" : "generic";
    case "button":
    case "summary":
      return "button";
    case "input":
      if (["button", "submit", "reset", "image"].includes(type)) return "button";
      if (type === "checkbox") return "checkbox";
      if (type === "radio") return "radio";
      if (type === "range") return "slider";
      if (type === "number") return "spinbutton";
      if (type === "search") return "searchbox";
      if (type === "hidden") return "none";
      return "textbox";
    case "textarea":
      return "textbox";
    case "select":
      return element.hasAttribute("multiple") ? "listbox" : "combobox";
    case "h1":
    case "h2":
    case "h3":
    case "h4":
    case "h5":
    case "h6":
      return "heading";
    case "nav":
      return "navigation";
    case "main":
      return "main";
    case "aside":
      return "complementary";
    case "header":
      return insideSectioning() ? "generic" : "banner";
    case "footer":
      return insideSectioning() ? "generic" : "contentinfo";
    case "form":
      return accessibleName(element) !== "" ? "form" : "generic";
    case "section":
      return accessibleName(element) !== "" ? "region" : "generic";
    case "ul":
    case "ol":
      return "list";
    case "li":
      return "listitem";
    case "table":
      return "table";
    case "dialog":
      return "dialog";
    case "img":
      return element.getAttribute("alt") === "" ? "presentation" : "img";
    default:
      return "generic";
  }
}

/** A selector-like path of the element from the root, such as main:3>div:1>button:2; equal for the same node while the DOM does not change. */
export function pathKey(element) {
  const parts = [];
  for (let node = element; node && node.nodeType === 1 && node.parentElement; node = node.parentElement) {
    const index = Array.prototype.indexOf.call(node.parentElement.children, node) + 1;
    parts.unshift(`${node.tagName.toLowerCase()}:${index}`);
  }
  return parts.join(">");
}

/** A short human description of an element: tag, id, classes and the start of its outer HTML. */
export function describeNode(element) {
  if (!element || element.nodeType !== 1) return "(none)";
  const id = element.id ? `#${element.id}` : "";
  const classes = typeof element.className === "string" && element.className.trim() ? `.${element.className.trim().split(/\s+/).slice(0, 3).join(".")}` : "";
  const html = String(element.outerHTML || "").replace(/\s+/g, " ").slice(0, 140);
  return `${element.tagName.toLowerCase()}${id}${classes} ${html}`.slice(0, 220);
}

/** True when neither the element nor an ancestor is hidden from rendering or from the accessibility tree. */
export function isExposed(element) {
  for (let node = element; node && node.nodeType === 1; node = node.parentElement) {
    if (node.hidden === true || node.getAttribute("aria-hidden") === "true") return false;
    if (typeof getComputedStyle === "function") {
      const style = getComputedStyle(node);
      if (style.display === "none") return false;
      if (node === element && (style.visibility === "hidden" || style.visibility === "collapse")) return false;
    }
  }
  return true;
}

/** True when the element is drawn so small or so clipped that it is meant for assistive technology only (the screen-reader-only utility). */
export function isVisuallyHidden(element) {
  const rect = element.getBoundingClientRect();
  if (rect.width <= 1 || rect.height <= 1) return true;
  const style = getComputedStyle(element);
  return style.clip === "rect(0px, 0px, 0px, 0px)" || style.clipPath === "inset(50%)";
}

/** The background colour behind an element, flattened over its ancestors and the white canvas; includeSelf adds the element's own background. */
export function effectiveBackground(element, includeSelf) {
  const layers = [];
  for (let node = includeSelf ? element : element.parentElement; node && node.nodeType === 1; node = node.parentElement) {
    const colour = parseColor(getComputedStyle(node).backgroundColor);
    if (colour && colour.a > 0) layers.push(colour);
    if (colour && colour.a >= 1) break;
  }
  let result = { r: 255, g: 255, b: 255, a: 1 };
  for (let i = layers.length - 1; i >= 0; i -= 1) result = compositeOver(layers[i], result);
  return result;
}

/** True when an element is a declared scroll container: a named focusable region, a pre, a table or a terminal. */
export function isDeclaredScrollContainer(element) {
  const tag = element.tagName.toLowerCase();
  if (tag === "pre" || tag === "table") return true;
  if (element.classList && (element.classList.contains("xterm") || element.classList.contains("autonogrammer-terminal") || element.classList.contains("autonogrammer-terminal-host"))) return true;
  return element.getAttribute("role") === "region" && element.getAttribute("tabindex") === "0" && accessibleName(element) !== "";
}

/** Describes the focused element: identity, name, role, geometry, focus indicator and whether another element covers its centre. */
export function probeStop() {
  const active = document.activeElement;
  if (!active || active === document.body || active === document.documentElement) {
    return { left: true, activeTag: active ? active.tagName.toLowerCase() : null };
  }
  const forced = matchMedia("(forced-colors: active)").matches;
  const style = getComputedStyle(active);
  const offset = parseFloat(style.outlineOffset) || 0;
  const background = effectiveBackground(active, offset < 0);
  const ring = describeFocusRing(
    { outlineStyle: style.outlineStyle, outlineWidth: style.outlineWidth, outlineColor: style.outlineColor, boxShadow: style.boxShadow },
    background,
    forced,
  );
  const rects = active.getClientRects();
  const rect = rects.length > 0 ? rects[0] : active.getBoundingClientRect();
  const cx = rect.left + rect.width / 2;
  const cy = rect.top + rect.height / 2;
  const inViewport = cx >= 0 && cy >= 0 && cx <= window.innerWidth && cy <= window.innerHeight;
  let obscuredBy = null;
  if (inViewport) {
    const hit = document.elementFromPoint(cx, cy);
    const belongs = hit && (active.contains(hit) || (hit.tagName === "LABEL" && hit.control === active));
    obscuredBy = belongs ? null : describeNode(hit);
  }
  return {
    left: false,
    key: pathKey(active),
    tag: active.tagName.toLowerCase(),
    id: active.id || "",
    role: roleOf(active),
    name: accessibleName(active),
    hasAriaLabel: active.hasAttribute("aria-label"),
    visibleText: normaliseSpace(active.innerText || "").slice(0, 120),
    ring,
    inViewport,
    obscuredBy,
    rect: { left: rect.left, top: rect.top, width: rect.width, height: rect.height },
    description: describeNode(active),
    inert: !!active.closest("[inert]"),
    landmark: (active.closest("header, nav, main, footer, [role=dialog], [role=alertdialog]") || { tagName: "" }).tagName.toLowerCase(),
  };
}

/** The element a pathKey names in the current document, or null. */
export function elementByKey(key) {
  let node = document.documentElement;
  for (const part of String(key).split(">")) {
    const match = /^(.+):(\d+)$/.exec(part);
    if (!match) return null;
    node = node.children[Number(match[2]) - 1];
    if (!node || node.tagName.toLowerCase() !== match[1]) return null;
  }
  return node;
}

/** Focuses the element a pathKey names; true when it is the active element afterwards. */
export function focusByKey(key) {
  const element = elementByKey(key);
  if (!element) return false;
  element.focus();
  return document.activeElement === element;
}

/** The state of the page a route check needs: title, headings, landmarks, the route announcer and the focused element. */
export function probeRouteState() {
  const exposed = (element) => isExposed(element) && !element.closest("noscript");
  const headings = Array.from(document.querySelectorAll("h1")).filter(exposed).map((h) => ({ text: normaliseSpace(h.textContent || ""), focused: document.activeElement === h, key: pathKey(h) }));
  const selector = "header, nav, main, footer, aside, section, form, [role]";
  const landmarkRoles = ["banner", "navigation", "main", "contentinfo", "complementary", "region", "search", "form"];
  const landmarks = Array.from(document.querySelectorAll(selector))
    .filter(exposed)
    .map((element) => ({ role: roleOf(element), name: accessibleName(element), tag: element.tagName.toLowerCase(), key: pathKey(element) }))
    .filter((item) => landmarkRoles.includes(item.role));
  const tablists = Array.from(document.querySelectorAll("[role=tablist]")).filter(exposed).length;
  const announcer = document.querySelector("[data-route-announcer]");
  const active = document.activeElement;
  return {
    path: location.pathname,
    title: document.title,
    headings,
    landmarks,
    tablists,
    announcer: announcer ? normaliseSpace(announcer.textContent || "") : null,
    active: active && active !== document.body ? describeNode(active) : null,
    activeIsH1: !!active && active.tagName === "H1",
    activeIsConversation: !!active && active.id === "autonogrammer-conversation",
    activeIsSkipLink: !!active && active.classList.contains("autonogrammer-skip-link"),
    activeInMain: !!active && !!active.closest("main"),
    activeInDialog: !!active && !!active.closest("[role=dialog], [role=alertdialog]"),
  };
}

/** The elements that must be at least 24 by 24 CSS pixels (WCAG 2.5.8), with their geometry. Disabled, hidden and visually hidden controls are left out. */
export function collectTargets() {
  const selector = "a[href], area[href], button, input:not([type=hidden]), select, textarea, summary, [role=button], [role=link], [role=tab], [role=checkbox], [role=radio], [role=switch], [role=menuitem], [role=option], [role=slider], [role=combobox]";
  const result = [];
  for (const element of document.querySelectorAll(selector)) {
    if (element.disabled === true) continue;
    if (!isExposed(element) || element.closest("[inert]")) continue;
    if (element.tagName === "INPUT" && element.type === "hidden") continue;
    const rect = element.getBoundingClientRect();
    if (rect.width === 0 && rect.height === 0) continue;
    if (isVisuallyHidden(element)) continue;
    const style = getComputedStyle(element);
    let inline = false;
    if (element.tagName === "A" && style.display === "inline") {
      const parent = element.parentElement;
      let outside = "";
      for (const child of Array.from(parent ? parent.childNodes : [])) {
        if (child.nodeType === 3) outside += child.nodeValue;
      }
      inline = normaliseSpace(outside) !== "";
    }
    result.push({
      key: pathKey(element),
      description: describeNode(element),
      name: accessibleName(element),
      inline,
      rect: { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom, width: rect.width, height: rect.height },
    });
  }
  return result;
}

/** Elements wider than the viewport and the scroll widths of the document, for the reflow check (WCAG 1.4.10). */
export function collectReflow() {
  const root = document.documentElement;
  const viewportWidth = root.clientWidth;
  const offenders = [];
  const isOffender = (element) => {
    const style = getComputedStyle(element);
    if (style.display === "none" || style.visibility === "hidden") return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 && rect.right > viewportWidth + 1;
  };
  for (const element of document.body.querySelectorAll("*")) {
    if (["script", "style", "link", "meta", "title", "path", "circle", "rect", "line", "g", "defs", "use"].includes(element.tagName.toLowerCase())) continue;
    if (!isOffender(element)) continue;
    if (element.parentElement && element.parentElement !== document.body && isOffender(element.parentElement)) continue;
    let clippedBy = null;
    let declared = false;
    for (let node = element; node && node !== document.body; node = node.parentElement) {
      if (isDeclaredScrollContainer(node)) declared = true;
      if (node !== element) {
        const style = getComputedStyle(node);
        if (["auto", "scroll", "hidden", "clip"].includes(style.overflowX)) {
          clippedBy = { description: describeNode(node), overflowX: style.overflowX };
          break;
        }
      }
    }
    const style = getComputedStyle(element);
    offenders.push({
      key: pathKey(element),
      description: describeNode(element),
      right: Math.round(element.getBoundingClientRect().right),
      fixed: style.position === "fixed",
      declared,
      clippedBy,
    });
    if (offenders.length >= 25) break;
  }
  return {
    viewportWidth,
    innerWidth: window.innerWidth,
    devicePixelRatio: window.devicePixelRatio,
    rootScrollWidth: root.scrollWidth,
    rootClientWidth: root.clientWidth,
    bodyScrollWidth: document.body.scrollWidth,
    bodyClientWidth: document.body.clientWidth,
    offenders,
  };
}

/** Text containers that clip their content (overflow hidden or clip) while the content is larger than the box. Used before and after the text-spacing styles. */
export function collectTextClipping() {
  const result = [];
  for (const element of document.body.querySelectorAll("*")) {
    const style = getComputedStyle(element);
    const clipsX = style.overflowX === "hidden" || style.overflowX === "clip";
    const clipsY = style.overflowY === "hidden" || style.overflowY === "clip";
    if (!clipsX && !clipsY) continue;
    if (style.display === "none" || !(element.textContent || "").trim()) continue;
    if (!isExposed(element)) continue;
    const tooWide = clipsX && element.scrollWidth > element.clientWidth + 1;
    const tooTall = clipsY && element.scrollHeight > element.clientHeight + 1;
    if (!tooWide && !tooTall) continue;
    result.push({
      key: pathKey(element),
      description: describeNode(element),
      axis: tooWide && tooTall ? "both" : tooWide ? "x" : "y",
      ellipsis: style.textOverflow === "ellipsis",
      scrollWidth: element.scrollWidth,
      clientWidth: element.clientWidth,
      scrollHeight: element.scrollHeight,
      clientHeight: element.clientHeight,
    });
    if (result.length >= 60) break;
  }
  return result;
}

/** The boxes of the elements that hold text directly, for the overlap test of the text-spacing check. */
export function collectTextBoxes() {
  const result = [];
  for (const element of document.body.querySelectorAll("*")) {
    let direct = "";
    for (const child of Array.from(element.childNodes)) {
      if (child.nodeType === 3) direct += child.nodeValue;
    }
    if (normaliseSpace(direct) === "" && !["INPUT", "TEXTAREA", "SELECT"].includes(element.tagName)) continue;
    if (!isExposed(element)) continue;
    const style = getComputedStyle(element);
    if (style.display === "contents") continue;
    const range = document.createRange();
    range.selectNodeContents(element);
    const rect = element.tagName === "INPUT" || element.tagName === "TEXTAREA" || element.tagName === "SELECT" ? element.getBoundingClientRect() : range.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) continue;
    if (isVisuallyHidden(element)) continue;
    let positioned = false;
    for (let node = element; node && node !== document.body; node = node.parentElement) {
      const position = getComputedStyle(node).position;
      if (position === "absolute" || position === "fixed" || position === "sticky") {
        positioned = true;
        break;
      }
    }
    result.push({
      key: pathKey(element),
      parentKey: element.parentElement ? pathKey(element.parentElement) : "",
      description: describeNode(element),
      positioned,
      rect: { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom },
    });
    if (result.length >= 1500) break;
  }
  return result;
}

/** The visible boundary and text affordances of the controls, for the forced-colors and prefers-contrast checks. */
export function collectControlAffordances() {
  const selector = "button, input:not([type=hidden]), select, textarea, [role=tab], [role=button]";
  const nativeTypes = ["checkbox", "radio", "range", "file", "color"];
  const result = [];
  for (const element of document.querySelectorAll(selector)) {
    if (!isExposed(element) || element.closest("[inert]") || isVisuallyHidden(element)) continue;
    const tag = element.tagName.toLowerCase();
    const type = (element.getAttribute("type") || "").toLowerCase();
    if (nativeTypes.includes(type)) continue;
    const isField = tag === "textarea" || tag === "select" || (tag === "input" && !["button", "submit", "reset", "image"].includes(type));
    const style = getComputedStyle(element);
    let border = false;
    for (const side of ["Top", "Right", "Bottom", "Left"]) {
      const width = parseFloat(style[`border${side}Width`]) || 0;
      const sideStyle = style[`border${side}Style`];
      const colour = parseColor(style[`border${side}Color`]);
      if (width > 0 && sideStyle !== "none" && sideStyle !== "hidden" && (!colour || colour.a > 0)) border = true;
    }
    const text = normaliseSpace(element.innerText || element.getAttribute("value") || "");
    result.push({
      key: pathKey(element),
      description: describeNode(element),
      kind: isField ? "field" : "button",
      border,
      hasText: text !== "",
      hasIcon: !!element.querySelector("svg, img"),
      outline: style.outlineStyle !== "none" && (parseFloat(style.outlineWidth) || 0) > 0,
    });
  }
  return { forcedColors: matchMedia("(forced-colors: active)").matches, contrastMore: matchMedia("(prefers-contrast: more)").matches, reducedMotion: matchMedia("(prefers-reduced-motion: reduce)").matches, dark: matchMedia("(prefers-color-scheme: dark)").matches, controls: result };
}

/** Transitions, animations and smooth scrolling that last longer than 0.01 s, for the reduced-motion check. */
export function collectMotion() {
  const offenders = [];
  const threshold = 0.01;
  const check = (element, pseudo) => {
    const style = getComputedStyle(element, pseudo || null);
    const transitionProperties = style.transitionProperty || "";
    const transition = transitionProperties.trim() === "none" ? 0 : longestDuration(style.transitionDuration);
    const animation = style.animationName && style.animationName !== "none" ? longestDuration(style.animationDuration) : 0;
    const smooth = !pseudo && style.scrollBehavior === "smooth";
    if (transition > threshold || animation > threshold || smooth) {
      offenders.push({ key: pathKey(element), description: describeNode(element), pseudo: pseudo || "", transition, animation, smoothScroll: smooth });
    }
  };
  for (const element of [document.documentElement, ...document.body.querySelectorAll("*")]) {
    check(element, "");
    check(element, "::before");
    check(element, "::after");
    if (offenders.length >= 40) break;
  }
  const running = document.getAnimations().filter((animation) => {
    const timing = animation.effect && animation.effect.getComputedTiming ? animation.effect.getComputedTiming() : null;
    return timing && (timing.duration > threshold * 1000) && animation.playState === "running";
  }).length;
  return { offenders, runningAnimations: running, reducedMotion: matchMedia("(prefers-reduced-motion: reduce)").matches };
}

/** The flattened background colour of the page and its luminance, to tell the light and dark themes apart. */
export function probeThemeColours() {
  const background = effectiveBackground(document.body, true);
  const text = parseColor(getComputedStyle(document.body).color);
  return { dataTheme: document.documentElement.getAttribute("data-theme"), background, luminance: relativeLuminance(background), text, textContrast: text ? contrastRatio(compositeOver(text, background), background) : null };
}

/** True when no CSS animation or transition is running, so a computed style is final. */
export function animationsSettled() {
  return document.getAnimations().length === 0;
}

const LIBRARY = [
  parseColor,
  compositeOver,
  relativeLuminance,
  contrastRatio,
  splitTopLevel,
  parseBoxShadow,
  describeFocusRing,
  longestDuration,
  rectsOverlapArea,
  circleIntersectsRect,
  normaliseSpace,
  isHiddenForName,
  accessibleName,
  roleOf,
  pathKey,
  elementByKey,
  focusByKey,
  describeNode,
  isExposed,
  isVisuallyHidden,
  effectiveBackground,
  isDeclaredScrollContainer,
  probeStop,
  probeRouteState,
  collectTargets,
  collectReflow,
  collectTextClipping,
  collectTextBoxes,
  collectControlAffordances,
  collectMotion,
  probeThemeColours,
  animationsSettled,
];

/** The source of an expression that installs the library once on window.__a11yLib and evaluates to it. */
export function pageLibSource() {
  const names = LIBRARY.map((fn) => fn.name);
  const body = `const NAME_FROM_CONTENT_ROLES = ${JSON.stringify(NAME_FROM_CONTENT_ROLES)};\n${LIBRARY.map((fn) => fn.toString()).join("\n")}\nreturn { ${names.join(", ")} };`;
  return `(window.__a11yLib || (window.__a11yLib = (() => { ${body} })()))`;
}
