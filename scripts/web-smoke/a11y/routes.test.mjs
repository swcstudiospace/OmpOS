// The route check's decision when a restarted sequence also lands on the boot shell. A null second attempt must be an error:
// treating it as no entries would let the check pass without testing Back, Forward or Settings.
//   node --test scripts/web-smoke/a11y/routes.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import { bootShellOutcome } from "./routes.mjs";

test("a second attempt that produced entries adds no finding", () => {
  assert.equal(bootShellOutcome([{ route: "/kanban" }]), null);
});

test("a second attempt that also landed on the boot shell is an error, not a pass", () => {
  const outcome = bootShellOutcome(null);
  assert.equal(outcome.severity, "error");
  assert.equal(outcome.check, "routes");
  assert.equal(outcome.id, "sequence-boot-shell");
});
