// The route check's decision when a restarted sequence also lands on the boot shell. A null second attempt must be an error:
// treating it as no entries would let the check pass without testing Back, Forward or Settings.
//   node --test scripts/web-smoke/a11y/routes.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import { bootShellOutcome, DIRECT_ROUTES, readyFor, waitForInitialTitle } from "./routes.mjs";

test("a second attempt that produced entries adds no finding", () => {
  assert.equal(bootShellOutcome([{ route: "/kanban" }]), null);
});

test("a second attempt that also landed on the boot shell is an error, not a pass", () => {
  const outcome = bootShellOutcome(null);
  assert.equal(outcome.severity, "error");
  assert.equal(outcome.check, "routes");
  assert.equal(outcome.id, "sequence-boot-shell");
});

test("initial navigation waits for a non-boot title and a rendered main heading, and fails closed on timeout", async () => {
  const findings = [];
  let expression;
  const report = { add: (finding) => findings.push(finding), count: () => {} };
  const page = { waitFor: async (condition) => { expression = condition; return false; } };
  assert.equal(await waitForInitialTitle(page, report), false);
  const ready = new Function("document", `return ${expression}`);
  const rendered = (title, { heading = true, loading = false } = {}) => ({
    title,
    querySelector: (selector) => (selector === "main h1" ? (heading ? {} : null) : selector === '[data-state="loading"]' ? (loading ? {} : null) : null),
  });
  assert.equal(ready(rendered("Autonogrammer")), false);
  assert.equal(ready(rendered("")), false);
  assert.equal(ready(rendered("Agents - Autonogrammer", { heading: false })), false);
  assert.equal(ready(rendered("Agents - Autonogrammer", { loading: true })), false);
  assert.equal(ready(rendered("Agents - Autonogrammer")), true);
  assert.equal(findings[0].id, "initial-title-not-ready");
  assert.equal(findings[0].severity, "error");
});

test("dynamic session readiness waits for a rendered main heading and no loading state", () => {
  const ready = new Function("document", `return ${readyFor("/sessions/session%20one")}`);
  const document = (heading, loading) => ({
    querySelector: (selector) => (selector === "main h1" ? (heading ? {} : null) : selector === '[data-state="loading"]' ? (loading ? {} : null) : null),
  });
  assert.equal(ready(document(false, false)), false);
  assert.equal(ready(document(true, true)), false);
  assert.equal(ready(document(true, false)), true);
});

test("the unknown path is checked as the bare NotFound page and is ready only once its own heading renders", () => {
  const spec = DIRECT_ROUTES.find((r) => r.route === "/does-not-exist-a11y");
  assert.equal(spec.layout, "bare");
  assert.equal(readyFor("/does-not-exist-a11y"), spec.ready);
  const ready = new Function("document", `return ${spec.ready}`);
  const document = (...present) => ({ querySelector: (selector) => (present.includes(selector) ? {} : null) });
  assert.equal(ready(document()), false);
  assert.equal(ready(document(".autonogrammer-notfound-page")), false);
  assert.equal(ready(document(".autonogrammer-notfound-page h1")), true);
});

test("a route with its own ready state keeps it, and an unknown in-app path has none", () => {
  const kanban = new Function("document", `return ${readyFor("/kanban")}`);
  assert.equal(kanban({ querySelector: (selector) => (selector === ".autonogrammer-kanban" ? {} : null) }), true);
  assert.equal(kanban({ querySelector: () => null }), false);
  assert.equal(readyFor("/somewhere-else"), null);
});
