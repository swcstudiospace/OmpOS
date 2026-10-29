// Self-tests of report.mjs.
//   node --test scripts/web-smoke/a11y/*.test.mjs

import assert from "node:assert/strict";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { finding, formatTable, Report } from "./report.mjs";

test("finding rejects an unknown severity", () => {
  assert.throws(() => finding("axe", "x", "fatal", "m"), /unknown severity/);
  assert.deepEqual(finding("axe", "x", "error", "m", { state: "s" }), { check: "axe", id: "x", severity: "error", message: "m", state: "s" });
});

test("Report keeps an identical finding once and counts errors per group", () => {
  const report = new Report();
  report.add(finding("axe", "a", "error", "m", { state: "s", viewport: "v", target: "t" }));
  report.add(finding("axe", "a", "error", "m", { state: "s", viewport: "v", target: "t" }));
  report.add([finding("axe", "a", "error", "m", { state: "s2" }), finding("axe", "b", "warning", "w"), finding("keyboard", "k", "info", "i")]);
  assert.equal(report.findings.length, 4);
  assert.equal(report.errors().length, 2);
  assert.equal(report.errors("keyboard").length, 0);
  assert.deepEqual(report.summary(), { axe: { errors: 2, warnings: 1, infos: 0 }, keyboard: { errors: 0, warnings: 0, infos: 1 } });
});

test("formatTable lists errors and warnings, not information, and says so when there are none", () => {
  assert.equal(formatTable([finding("axe", "a", "info", "i")]), "no errors or warnings");
  const table = formatTable([finding("axe", "axe-region", "warning", "a ".repeat(100), { state: "kanban", viewport: "320x568" })]);
  assert.match(table, /severity \| check/);
  assert.match(table, /warning\s+\| axe\s+\| kanban @ 320x568/);
  assert.match(table, /…/, "long messages are cut");
});

test("Report.write puts the JSON and the screenshots in the directory, and does nothing without one", () => {
  const report = new Report();
  report.add(finding("axe", "a", "error", "m"));
  report.count("axe:runs", 3);
  report.skip("routes", "no SPA rewrite");
  report.screenshot("forced-colors-agents", Buffer.from("png").toString("base64"));
  assert.equal(report.write(""), null);
  const directory = mkdtempSync(join(tmpdir(), "a11y-report-"));
  try {
    const file = report.write(join(directory, "out"), { kind: "demo" });
    const written = JSON.parse(readFileSync(file, "utf8"));
    assert.equal(written.kind, "demo");
    assert.equal(written.findings.length, 1);
    assert.equal(written.findings[0]._key, undefined);
    assert.deepEqual(written.counters, { "axe:runs": 3 });
    assert.deepEqual(written.skipped, [{ check: "routes", reason: "no SPA rewrite" }]);
    assert.deepEqual(written.screenshotFiles, ["screenshots/forced-colors-agents.png"]);
    assert.equal(existsSync(join(directory, "out", "screenshots", "forced-colors-agents.png")), true);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
