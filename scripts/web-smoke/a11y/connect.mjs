// The connect screen of an owner-token deployment (real bundle only; the Demo has no sign-in).
//
// The smoke API runs in Development, where GET /auth/config says "development" and nobody has to sign in. To reach the connect screen without
// reconfiguring that API, this check intercepts two requests of its own page with the Fetch domain: GET /auth/config is answered with the
// owner-token mode (C1 of the program: {"mode":"owner-token","authority":null,"audience":null}) and GET /workspace, the request that verifies a typed
// token, is answered with 401. Every other request goes to the real API untouched. The text typed into the token field is a fixed dummy string that
// is not a credential of anything and is never printed or written.
//
// Asserted: the redirect from "/" to /connect, the title, one h1 with focus, the labelled password field (name, required, autocomplete, description),
// the Tab order and the focus indicators, the empty submit (announced error, aria-invalid, focus stays in the field), the rejected token (announced error,
// the typed value is gone, focus stays in the field), and axe, reflow, target size and text spacing on the screen in both states at both viewports.

import { analyzeRoute, analyzeStops } from "./logic.mjs";
import { finding } from "./report.mjs";
import { probeAxe } from "./states.mjs";
import { probeReflow } from "./reflow.mjs";
import { probeTargetSize } from "./target-size.mjs";
import { probeTextSpacing } from "./text-spacing.mjs";

const DUMMY_TOKEN = "a11y-smoke-dummy-input-not-a-credential";
const TOKEN_ID = "autonogrammer-connect-token";
const ALERT = ".autonogrammer-connect-card [role=alert]";
const CORS = [
  { name: "Access-Control-Allow-Origin", value: "*" },
  { name: "Access-Control-Allow-Headers", value: "authorization, content-type, accept" },
  { name: "Access-Control-Allow-Methods", value: "GET, POST, OPTIONS" },
];

function fulfil(page, requestId, status, body, extra = []) {
  const headers = [...CORS, ...extra];
  return page
    .send("Fetch.fulfillRequest", { requestId, responseCode: status, responseHeaders: headers, body: Buffer.from(body ?? "").toString("base64") })
    .catch(() => {});
}

async function installInterception(page) {
  await page.send("Fetch.enable", { patterns: [{ urlPattern: "*/auth/config", requestStage: "Request" }, { urlPattern: "*/workspace", requestStage: "Request" }] });
  const off = page.on("Fetch.requestPaused", (event) => {
    const { requestId, request } = event;
    const path = new URL(request.url).pathname;
    if (request.method === "OPTIONS") {
      void fulfil(page, requestId, 204, "");
    } else if (path.endsWith("/auth/config")) {
      void fulfil(page, requestId, 200, JSON.stringify({ mode: "owner-token", authority: null, audience: null }), [{ name: "Content-Type", value: "application/json" }, { name: "Cache-Control", value: "no-store" }]);
    } else if (path.endsWith("/workspace")) {
      void fulfil(page, requestId, 401, JSON.stringify({ type: "/problems/unauthorized", title: "Unauthorized", status: 401 }), [{ name: "Content-Type", value: "application/problem+json" }]);
    } else {
      void page.send("Fetch.continueRequest", { requestId }).catch(() => {});
    }
  });
  return async () => {
    off();
    await page.send("Fetch.disable").catch(() => {});
  };
}

/** What the page says about the token field. */
async function readField(page) {
  await page.lib("probeRouteState");
  return page.evalValue(`(() => {
    const lib = window.__a11yLib;
    const input = document.getElementById(${JSON.stringify(TOKEN_ID)});
    if (!input) return null;
    const describedBy = (input.getAttribute("aria-describedby") || "").split(/\\s+/).filter(Boolean);
    return {
      type: input.getAttribute("type"),
      name: lib.accessibleName(input),
      required: input.getAttribute("aria-required"),
      autocomplete: input.getAttribute("autocomplete"),
      invalid: input.getAttribute("aria-invalid"),
      describedBy,
      describedByResolved: describedBy.map((id) => { const el = document.getElementById(id); return !!el && (el.textContent || "").trim().length > 0; }),
      empty: input.value === "",
      focused: document.activeElement === input,
    };
  })()`);
}

async function alertText(page) {
  return page.evalValue(`(document.querySelector(${JSON.stringify(ALERT)})?.textContent ?? "").trim()`);
}

/** The connect screen checks. */
export async function checkConnect(page, report, { baseUrl, axeJs }) {
  const state = "connect screen";
  const at = { state, viewport: "1280x900" };
  await page.setViewport(1280, 900);
  const stopInterception = await installInterception(page);
  try {
    await page.navigate(baseUrl);
    const reached = await page.waitFor("location.pathname === '/connect' && !!document.querySelector('main h1') && !!document.getElementById('autonogrammer-connect-token')", 150000);
    if (!reached) {
      report.add(finding("connect", "connect-not-reached", "error", `with an owner-token configuration ${baseUrl} did not lead to /connect with a token field (the page is at ${await page.evalValue("location.pathname")})`, at));
      return;
    }
    await page.waitFor("(document.querySelector('[data-route-announcer]')?.textContent ?? '').trim() === document.title", 5000);
    await page.waitFor("document.activeElement && document.activeElement.tagName === 'H1'", 5000);
    await page.settle();
    report.count("connect:views");

    const snap = await page.lib("probeRouteState");
    report.add(analyzeRoute({ route: "/connect", mode: "inapp", layout: "bare", snap, expectFocus: ["h1"] }).map((f) => ({ ...f, check: "connect", state })));

    const field = await readField(page);
    const bad = (id, message, wcag) => report.add(finding("connect", id, "error", message, { ...at, wcag, target: `#${TOKEN_ID}` }));
    if (field.type !== "password") bad("token-field-type", `the token field has type ${field.type}, a password field is required so the token is not shown`, "3.3.2");
    if (!field.name) bad("token-field-name", "the token field has no accessible name", "4.1.2");
    if (field.required !== "true") bad("token-field-required", "the token field does not say it is required (aria-required)", "3.3.2");
    if (field.autocomplete !== "current-password") bad("token-field-autocomplete", `the token field has autocomplete ${field.autocomplete}, current-password lets a password manager fill it`, "1.3.5");
    if (field.describedBy.length === 0 || field.describedByResolved.some((ok) => !ok)) bad("token-field-description", "the token field's aria-describedby does not point to existing, non-empty text", "3.3.2");

    // Tab order and focus indicators: h1 (focused) -> token field -> Connect button.
    const stops = [];
    for (let step = 0; step < 2; step += 1) {
      await page.key("Tab");
      await page.waitForCalm(1000);
      stops.push(await page.lib("probeStop"));
    }
    report.add(analyzeStops(stops, { state, viewport: "1280x900" }).map((f) => ({ ...f, check: "connect" })));
    const order = stops.map((s) => (s.left ? "(page)" : s.id || s.tag));
    if (order[0] !== TOKEN_ID || stops[1].role !== "button") {
      report.add(finding("connect", "tab-order", "error", `Tab from the heading reached ${order.join(" then ")}, expected the token field then the Connect button`, { ...at, wcag: "2.4.3" }));
    }

    // Empty submit with Enter in the field.
    await page.evalFn((id) => document.getElementById(id)?.focus(), TOKEN_ID);
    await page.key("Enter");
    const emptyAlert = await page.waitFor(`!!document.querySelector(${JSON.stringify(ALERT)})`, 5000);
    if (!emptyAlert) {
      bad("empty-submit-not-announced", "submitting an empty token showed no alert", "3.3.1");
    } else {
      await page.settle();
      const afterEmpty = await readField(page);
      if (afterEmpty.invalid !== "true") bad("empty-submit-not-invalid", "after an empty submit the field is not aria-invalid", "3.3.1");
      if (!afterEmpty.focused) bad("empty-submit-focus", "after an empty submit focus is not on the token field", "3.3.1");
      if (afterEmpty.describedByResolved.length < 2 || afterEmpty.describedByResolved.some((ok) => !ok)) bad("empty-submit-description", "after an empty submit the field's description does not include the error text", "3.3.1");
    }

    // A rejected token: the API answers 401. The typed text must be gone and the error announced.
    const firstAlert = await alertText(page);
    await page.evalFn((id) => document.getElementById(id)?.focus(), TOKEN_ID);
    await page.type(DUMMY_TOKEN);
    await page.key("Enter");
    const answered = await page.waitFor(`(document.querySelector(${JSON.stringify(ALERT)})?.textContent ?? "").trim() !== ${JSON.stringify(firstAlert)} && !document.querySelector('.autonogrammer-connect-status')`, 15000);
    if (!answered) {
      bad("rejected-token-not-announced", "a token the API rejects (401) produced no new alert", "3.3.1");
    } else {
      await page.settle();
      const afterReject = await readField(page);
      if (!afterReject.empty) bad("rejected-token-kept", "the rejected token is still in the field; it must be cleared so a wrong token is never shown again", "3.3.1");
      if (!afterReject.focused) bad("rejected-token-focus", "after a rejected token focus is not on the token field", "3.3.1");
      if (afterReject.invalid !== "true") bad("rejected-token-not-invalid", "after a rejected token the field is not aria-invalid", "3.3.1");
    }

    // axe, reflow, target size and text spacing in both viewports, with the alert shown.
    for (const viewport of [{ name: "1280x900", width: 1280, height: 900 }, { name: "320x568", width: 320, height: 568 }]) {
      await page.setViewport(viewport.width, viewport.height);
      await page.settle();
      await page.waitForCalm();
      const ctx = { group: "connect", state: `${state} (error shown)`, viewport: viewport.name };
      await probeAxe(page, report, ctx, axeJs);
      await probeReflow(page, report, ctx);
      await probeTargetSize(page, report, ctx);
      await probeTextSpacing(page, report, ctx);
    }
  } finally {
    await stopInterception();
  }
}
