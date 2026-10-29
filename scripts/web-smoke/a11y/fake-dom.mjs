// A tiny DOM for the Node self-tests: just what accessibleName, roleOf and pathKey read (nodeType, tagName, attributes, children, text, ownerDocument,
// getElementById, querySelectorAll("label"), contains). It has no layout and no styles; that is why those functions guard their use of getComputedStyle.

class FakeText {
  constructor(value) {
    this.nodeType = 3;
    this.nodeValue = value;
    this.parentElement = null;
  }

  get textContent() {
    return this.nodeValue;
  }
}

class FakeElement {
  constructor(tag, attributes = {}, children = []) {
    this.nodeType = 1;
    this.tagName = tag.toUpperCase();
    this.attributes = { ...attributes };
    this.childNodes = children.map((child) => (typeof child === "string" ? new FakeText(child) : child));
    this.parentElement = null;
    this.ownerDocument = null;
    this.isConnected = false;
    for (const child of this.childNodes) child.parentElement = this;
  }

  get hidden() {
    return "hidden" in this.attributes;
  }

  get id() {
    return this.attributes.id ?? "";
  }

  get children() {
    return this.childNodes.filter((node) => node.nodeType === 1);
  }

  get textContent() {
    return this.childNodes.map((node) => node.textContent).join("");
  }

  getAttribute(name) {
    return name in this.attributes ? String(this.attributes[name]) : null;
  }

  hasAttribute(name) {
    return name in this.attributes;
  }

  contains(node) {
    for (let current = node; current; current = current.parentElement) if (current === this) return true;
    return false;
  }

  descendants() {
    return this.children.flatMap((child) => [child, ...child.descendants()]);
  }
}

/** Builds an element: el("button", {type: "button"}, ["text", el("span", ...)]). */
export function el(tag, attributes = {}, children = []) {
  return new FakeElement(tag, attributes, children);
}

/** Wraps a root element in a document and sets ownerDocument on every node. */
export function documentOf(root) {
  const doc = {
    documentElement: root,
    getElementById: (id) => [root, ...root.descendants()].find((node) => node.id === id) ?? null,
    querySelectorAll: (selector) => {
      if (selector !== "label") throw new Error(`the fake document only knows "label", not ${selector}`);
      return [root, ...root.descendants()].filter((node) => node.tagName === "LABEL");
    },
  };
  for (const node of [root, ...root.descendants()]) node.ownerDocument = doc;
  return doc;
}
