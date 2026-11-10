// The route check's decision when a restarted sequence also lands on the boot shell. A null second attempt must be an error:
// treating it as no entries would let the check pass without testing Back, Forward or Settings.
//   node --test scripts/web-smoke/a11y/routes.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import { bootShellOutcome, readyFor, waitForInitialTitle } from "./routes.mjs";

test("a second attempt that produced entries adds no finding", () => {
  assert.equal(bootShellOutcome([{ route: "/kanban" }]), null);
});

test("a second attempt that also landed on the boot shell is an error, not a pass", () => {
  const outcome = bootShellOutcome(null);
  assert.equal(outcome.severity, "error");
  assert.equal(outcome.check, "routes");
  assert.equal(outcome.id, "sequence-boot-shell");
});

test("initial navigation waits for a non-boot title and fails closed on timeout", async () => {
  const findings = [];
  let expression;
  const report = { add: (finding) => findings.push(finding) };
  const page = { waitFor: async (condition) => { expression = condition; return false; } };
  assert.equal(await waitForInitialTitle(page, report), false);
  const ready = new Function("document", `return ${expression}`);
  assert.equal(ready({ title: "Autonogrammer" }), false);
  assert.equal(ready({ title: "" }), false);
  assert.equal(ready({ title: "Agents - Autonogrammer" }), true);
  assert.equal(findings[0].id, "initial-title-not-ready");
  assert.equal(findings[0].severity, "error");
});

test("dynamic session readiness waits for the requested thread and its title", () => {
  const ready = new Function("document", `return ${readyFor("/sessions/session%20one")}`);
  const document = {
    title: "Session - Autonogrammer",
    querySelector: () => null,
  };
  assert.equal(!!ready(document), false);
  document.querySelector = () => ({ getAttribute: () => "other" });
  document.title = "Other - Autonogrammer";
  assert.equal(ready(document), false);
  document.querySelector = () => ({ getAttribute: () => "session one" });
  document.title = "Session - Autonogrammer";
  assert.equal(ready(document), false);
  document.title = "Session one - Autonogrammer";
  assert.equal(ready(document), true);
});
