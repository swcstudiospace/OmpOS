// A small Chrome DevTools Protocol client for the accessibility checks (WebSocket from Node, no dependencies). The checks run on their OWN page in
// their OWN browser context, so they share nothing with the page the smoke script drives (no local storage, no focus, no emulation).
//
// Library functions of page-lib.mjs are called with page.lib("name", ...args); the library is installed once per document and again after every
// full navigation. Nothing here sleeps for a fixed time: every wait polls a condition of the page with a deadline.

import { readFileSync } from "node:fs";
import { pageLibSource } from "./page-lib.mjs";

const KEYS = {
  Tab: { code: "Tab", vk: 9 },
  Escape: { code: "Escape", vk: 27 },
  Enter: { code: "Enter", vk: 13, text: "\r" },
  Space: { code: "Space", vk: 32, text: " ", key: " " },
  ArrowDown: { code: "ArrowDown", vk: 40 },
  ArrowRight: { code: "ArrowRight", vk: 39 },
};

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** One page of the browser the smoke script started, driven over CDP. */
export class Page {
  constructor(socket) {
    this.socket = socket;
    this.nextId = 1;
    this.pending = new Map();
    this.listeners = new Set();
    this.sessionId = null;
    this.browserContextId = null;
    this.targetId = null;
    this.libInstalled = false;
    this.axeInstalled = false;
    this.documentStatuses = new Map();
    socket.addEventListener("message", (event) => this.#onMessage(JSON.parse(event.data)));
  }

  /** Opens a new page in a new, empty browser context of the Chrome listening on cdpPort. */
  static async open({ cdpPort, width = 1440, height = 900 }) {
    const meta = await (await fetch(`http://127.0.0.1:${cdpPort}/json/version`)).json();
    const socket = new WebSocket(meta.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
      socket.addEventListener("open", resolve, { once: true });
      socket.addEventListener("error", () => reject(new Error("CDP WebSocket error")), { once: true });
    });
    const page = new Page(socket);
    const context = await page.send("Target.createBrowserContext", {}, false);
    page.browserContextId = context.browserContextId;
    const target = await page.send("Target.createTarget", { url: "about:blank", browserContextId: page.browserContextId }, false);
    page.targetId = target.targetId;
    const attached = await page.send("Target.attachToTarget", { targetId: page.targetId, flatten: true }, false);
    page.sessionId = attached.sessionId;
    page.on("Page.frameNavigated", (params) => {
      if (!params.frame.parentId) {
        page.libInstalled = false;
        page.axeInstalled = false;
      }
    });
    page.on("Network.responseReceived", (params) => {
      if (params.type === "Document") page.documentStatuses.set(params.response.url, params.response.status);
    });
    await page.send("Page.enable");
    await page.send("Runtime.enable");
    await page.send("Network.enable");
    await page.send("Emulation.setFocusEmulationEnabled", { enabled: true });
    await page.send("Target.activateTarget", { targetId: page.targetId }, false);
    await page.setViewport(width, height);
    return page;
  }

  #onMessage(message) {
    if (message.id !== undefined && this.pending.has(message.id)) {
      const { resolve, reject, method } = this.pending.get(message.id);
      this.pending.delete(message.id);
      if (message.error) reject(new Error(`${method}: ${message.error.message}`));
      else resolve(message.result);
      return;
    }
    if (message.method && (message.sessionId === undefined || message.sessionId === this.sessionId)) {
      for (const listener of [...this.listeners]) listener(message.method, message.params ?? {});
    }
  }

  /** Sends a protocol command to the page session (or to the browser when scoped is false). */
  send(method, params = {}, scoped = true) {
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject, method });
      const body = scoped && this.sessionId ? { id, method, params, sessionId: this.sessionId } : { id, method, params };
      this.socket.send(JSON.stringify(body));
    });
  }

  /** Subscribes to a protocol event of this page; returns the function that unsubscribes. */
  on(method, handler) {
    const listener = (name, params) => {
      if (name === method) handler(params);
    };
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /** Evaluates an expression in the page and returns its value; a thrown exception becomes an Error. */
  async evalValue(expression) {
    const result = await this.send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) {
      const detail = result.exceptionDetails.exception?.description ?? result.exceptionDetails.text ?? "";
      throw new Error(`page evaluation failed: ${String(detail).slice(0, 300)}`);
    }
    return result.result.value;
  }

  /** Evaluates a function (serialised with toString) with JSON arguments in the page. */
  evalFn(fn, ...args) {
    return this.evalValue(`(${fn.toString()})(...${JSON.stringify(args)})`);
  }

  /** Calls a function of page-lib.mjs in the page, installing the library first when the document does not have it yet. */
  async lib(name, ...args) {
    if (!this.libInstalled) {
      await this.evalValue(`(${pageLibSource()}, true)`);
      this.libInstalled = true;
    }
    return this.evalValue(`window.__a11yLib.${name}(...${JSON.stringify(args)})`);
  }

  /** Installs the pinned axe-core engine in the document when it is not there. */
  async ensureAxe(axeJsPath) {
    if (this.axeInstalled && (await this.evalValue("typeof axe === 'object'"))) return;
    const injected = await this.send("Runtime.evaluate", { expression: readFileSync(axeJsPath, "utf8"), returnByValue: false });
    if (injected.exceptionDetails) throw new Error("axe-core failed to load in the page");
    this.axeInstalled = true;
  }

  /** Polls a JavaScript expression until it is truthy; false when the deadline passes. */
  async waitFor(expression, timeoutMs = 10000) {
    const deadline = Date.now() + timeoutMs;
    for (;;) {
      try {
        if (await this.evalValue(expression)) return true;
      } catch {
        // The document may be replaced or still booting; the next poll retries until the deadline.
      }
      if (Date.now() > deadline) return false;
      await sleep(100);
    }
  }

  /** Waits for two animation frames, so layout and style changes of the last action are applied. */
  settle() {
    return this.evalValue("new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve(true))))");
  }

  /** Waits until no CSS transition or animation is running, so computed styles read next are final (at most timeoutMs). */
  async waitForCalm(timeoutMs = 1500) {
    await this.waitFor("document.getAnimations().length === 0", timeoutMs);
  }

  /** Presses and releases a key (Tab, Escape, Enter, Space, ArrowDown, ArrowRight), optionally with Shift. */
  async key(name, { shift = false } = {}) {
    const spec = KEYS[name];
    if (!spec) throw new Error(`unknown key ${name}`);
    const base = { key: spec.key ?? name, code: spec.code, windowsVirtualKeyCode: spec.vk, nativeVirtualKeyCode: spec.vk, modifiers: shift ? 8 : 0 };
    await this.send("Input.dispatchKeyEvent", { type: "keyDown", ...base, ...(spec.text ? { text: spec.text } : {}) });
    await this.send("Input.dispatchKeyEvent", { type: "keyUp", ...base });
  }

  /** Types text into the focused element. */
  async type(text) {
    await this.send("Input.insertText", { text });
  }

  /** Loads a URL and waits for the load event; resolves to the HTTP status of the document and the URL after any redirect. */
  async navigate(url, timeoutMs = 90000) {
    this.documentStatuses.delete(url);
    const loaded = new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        off();
        reject(new Error(`timed out loading ${url}`));
      }, timeoutMs);
      const off = this.on("Page.loadEventFired", () => {
        clearTimeout(timer);
        off();
        resolve();
      });
    });
    const result = await this.send("Page.navigate", { url });
    if (result.errorText) throw new Error(`navigation to ${url} failed: ${result.errorText}`);
    await loaded;
    return { status: this.documentStatuses.get(url) ?? null, url: await this.evalValue("location.href") };
  }

  /** Sets the viewport in CSS pixels and the device pixel ratio (the page layout follows at once, no reload). */
  async setViewport(width, height, deviceScaleFactor = 1, mobile = width < 700) {
    await this.send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor, mobile });
  }

  /** Emulates media features, for example [{name: "forced-colors", value: "active"}]; an empty list clears them. */
  async setMedia(features = []) {
    await this.send("Emulation.setEmulatedMedia", { features });
  }

  /** Navigates inside the running app the way a click on a link does: a synthetic anchor is clicked and Blazor routes it without a reload. */
  async spaNavigate(path) {
    await this.evalFn((target) => {
      const anchor = document.createElement("a");
      anchor.setAttribute("href", target);
      anchor.style.cssText = "position:fixed;left:-9999px;top:0;width:1px;height:1px";
      anchor.textContent = "navigate";
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      return true;
    }, path);
    return this.waitFor(`location.pathname === ${JSON.stringify(path)}`, 10000);
  }

  /** A PNG screenshot of the viewport as base64. */
  async screenshot() {
    const { data } = await this.send("Page.captureScreenshot", { format: "png" });
    return data;
  }

  /** Closes the page and its browser context and the connection. */
  async close() {
    try {
      if (this.targetId) await this.send("Target.closeTarget", { targetId: this.targetId }, false);
      if (this.browserContextId) await this.send("Target.disposeBrowserContext", { browserContextId: this.browserContextId }, false);
    } catch {
      // The browser is stopped by the caller anyway.
    }
    this.socket.close();
  }
}
