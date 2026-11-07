// The sign-in explanation of an owner-token deployment (real bundle only; the Demo has no sign-in).
//
// The web head signs in with Supabase only, so an owner-token deployment has no sign-in form here: the gate explains that sign-in is not available
// in this web build and offers no field. The smoke API runs in Development, where GET /auth/config says "development" and nobody has to sign in. To
// reach the owner-token explanation without reconfiguring that API, this check intercepts one request of its own page with the Fetch domain: GET
// /auth/config is answered with the owner-token mode. Every other request goes to the real API untouched.
//
// Asserted: the page stays at "/" (no redirect to a sign-in form), the explanation is shown in the main landmark, no form field is offered, and axe,
// reflow, target size and text spacing on the explanation at both viewports.

import { finding } from "./report.mjs";
import { LOAD_TIMEOUT_MS, probeAxe } from "./states.mjs";
import { probeReflow } from "./reflow.mjs";
import { probeTargetSize } from "./target-size.mjs";
import { probeTextSpacing } from "./text-spacing.mjs";

const EXPLANATION = "not available in this web build";
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
  await page.send("Fetch.enable", { patterns: [{ urlPattern: "*/auth/config", requestStage: "Request" }] });
  const off = page.on("Fetch.requestPaused", (event) => {
    const { requestId, request } = event;
    const path = new URL(request.url).pathname;
    if (request.method === "OPTIONS") {
      void fulfil(page, requestId, 204, "");
    } else if (path.endsWith("/auth/config")) {
      void fulfil(page, requestId, 200, JSON.stringify({ mode: "owner-token", authority: null, audience: null }), [{ name: "Content-Type", value: "application/json" }, { name: "Cache-Control", value: "no-store" }]);
    } else {
      void page.send("Fetch.continueRequest", { requestId }).catch(() => {});
    }
  });
  return async () => {
    off();
    await page.send("Fetch.disable").catch(() => {});
  };
}

/** The sign-in explanation checks. */
export async function checkConnect(page, report, { baseUrl, axeJs }) {
  const state = "sign-in explanation";
  const at = { state, viewport: "1280x900" };
  await page.setViewport(1280, 900);
  const stopInterception = await installInterception(page);
  try {
    await page.navigate(baseUrl);
    const shown = await page.waitFor(`location.pathname === '/' && (document.querySelector('main')?.textContent ?? '').includes(${JSON.stringify(EXPLANATION)})`, LOAD_TIMEOUT_MS);
    if (!shown) {
      report.add(finding("connect", "explanation-not-shown", "error", `with an owner-token configuration ${baseUrl} did not show that sign-in is not available here (the page is at ${await page.evalValue("location.pathname")})`, at));
      return;
    }
    await page.settle();
    report.count("connect:views");

    const fields = await page.evalValue("document.querySelectorAll('input, textarea, select').length");
    if (fields !== 0) {
      report.add(finding("connect", "explanation-offers-field", "error", `the sign-in explanation offers ${fields} form field(s), but an owner-token deployment has no web sign-in`, { ...at, wcag: "3.3.2" }));
    }

    for (const viewport of [{ name: "1280x900", width: 1280, height: 900 }, { name: "320x568", width: 320, height: 568 }]) {
      await page.setViewport(viewport.width, viewport.height);
      await page.settle();
      await page.waitForCalm();
      const ctx = { group: "connect", state, viewport: viewport.name };
      await probeAxe(page, report, ctx, axeJs);
      await probeReflow(page, report, ctx);
      await probeTargetSize(page, report, ctx);
      await probeTextSpacing(page, report, ctx);
    }
  } finally {
    await stopInterception();
  }
}
