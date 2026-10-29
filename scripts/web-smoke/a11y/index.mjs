// Entry of the accessibility checks. Both smoke scripts call runA11yChecks after their own assertions:
//
//   smoke-demo.mjs  kind "demo"  the Demo bundle: routes (direct loads and in-app), keyboard, axe on every state, media emulation, reflow, target size,
//                                text spacing
//   smoke.mjs       kind "api"   the real bundle against the in-memory API: the connect screen first, then the same checks over the states that exist there
//
// Every step runs on its own page in its own empty browser context, so no step inherits storage, focus, emulation or a half-finished interception from
// another, and a step that throws becomes one "check-crashed" error finding instead of stopping the run. The result carries one assertion per check group
// (true when the group has no finding of severity error) for the smoke's JSON, the table of findings, and the report object.

import { fileURLToPath } from "node:url";
import { Page } from "./cdp.mjs";
import { checkConnect } from "./connect.mjs";
import { checkKeyboard } from "./keyboard.mjs";
import { checkMedia } from "./media-emulation.mjs";
import { checkReflow } from "./reflow.mjs";
import { finding, Report } from "./report.mjs";
import { checkRoutes } from "./routes.mjs";
import { checkAxe } from "./states.mjs";
import { checkTargetSize } from "./target-size.mjs";
import { checkTextSpacing } from "./text-spacing.mjs";

/** The assertion name of each check group in the smoke JSON. */
export const ASSERTIONS = {
  connect: "a11yConnect",
  routes: "a11yRoutes",
  "first-load-focus": "a11yFirstLoadFocus",
  keyboard: "a11yKeyboard",
  dialog: "a11yDialog",
  axe: "a11yAxe",
  media: "a11yMedia",
  reflow: "a11yReflow",
  "target-size": "a11yTargetSize",
  "text-spacing": "a11yTextSpacing",
};

/** The steps, in order, with the check groups each one reports and the bundle kinds it applies to. */
const STEPS = [
  { name: "connect", groups: ["connect", "axe", "reflow", "target-size", "text-spacing"], kinds: ["api"], run: checkConnect },
  { name: "routes", groups: ["routes"], kinds: ["demo", "api"], run: checkRoutes },
  { name: "keyboard", groups: ["first-load-focus", "keyboard", "dialog"], kinds: ["demo", "api"], run: checkKeyboard },
  { name: "axe", groups: ["axe"], kinds: ["demo", "api"], run: checkAxe },
  { name: "media", groups: ["media"], kinds: ["demo", "api"], run: checkMedia },
  { name: "reflow", groups: ["reflow"], kinds: ["demo", "api"], run: checkReflow },
  { name: "target-size", groups: ["target-size"], kinds: ["demo", "api"], run: checkTargetSize },
  { name: "text-spacing", groups: ["text-spacing"], kinds: ["demo", "api"], run: checkTextSpacing },
];

/** The report directory named by --a11y-report in argv, or by A11Y_REPORT_DIR; empty when none. */
export function reportDirFromArgs(argv = process.argv, env = process.env) {
  const index = argv.indexOf("--a11y-report");
  if (index >= 0 && argv[index + 1]) return argv[index + 1];
  return env.A11Y_REPORT_DIR || "";
}

/**
 * Runs the accessibility checks.
 * @param {{cdpPort: string|number, baseUrl: string, kind: "demo"|"api", axeJs: string, reportDir?: string, groups?: string[], log?: (message: string) => void, meta?: object}} options
 * @returns {Promise<{report: Report, assertions: Record<string, boolean>, failures: string[], table: string, passed: boolean}>}
 */
export async function runA11yChecks(options) {
  const { cdpPort, baseUrl, kind, axeJs, reportDir = "", groups, log = () => {}, meta = {} } = options;
  const report = new Report();
  const only = groups && groups.length > 0 ? new Set(groups) : null;
  const steps = STEPS.filter((s) => s.kinds.includes(kind) && (!only || only.has(s.name)));
  const ran = new Set();
  for (const step of steps) {
    log(`a11y step ${step.name}`);
    let page = null;
    try {
      page = await Page.open({ cdpPort });
      await step.run(page, report, { kind, baseUrl, axeJs, reportDir });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      report.add(finding(step.groups[0], "check-crashed", "error", `the ${step.name} step stopped with an exception: ${message.slice(0, 300)}`, { state: step.name }));
    } finally {
      if (page) await page.close();
    }
    for (const group of step.groups) ran.add(group);
  }

  const assertions = {};
  const failures = [];
  for (const group of ran) {
    const errors = report.errors(group);
    assertions[ASSERTIONS[group]] = errors.length === 0;
    for (const e of errors.slice(0, 3)) failures.push(`${ASSERTIONS[group]}: ${[e.state, e.viewport].filter(Boolean).join(" @ ")}: ${e.message}`.slice(0, 400));
    if (errors.length > 3) failures.push(`${ASSERTIONS[group]}: and ${errors.length - 3} more error finding(s); see the report`);
  }
  const table = report.table();
  log(`a11y findings:\n${table}`);
  log(`a11y counters: ${JSON.stringify(Object.fromEntries(report.counters))}`);
  for (const skipped of report.skipped) log(`a11y skipped (${skipped.check}): ${skipped.reason}`);
  const file = report.write(reportDir, { kind, baseUrl, axeCore: "4.14.0", ...meta });
  if (file) log(`a11y report written to ${file}`);
  return { report, assertions, failures, table, passed: Object.values(assertions).every(Boolean) };
}

// Direct use: node scripts/web-smoke/a11y/index.mjs   (env CDP_PORT, BASE_URL, KIND, AXE_JS; optional --a11y-report DIR, A11Y_GROUPS=routes,keyboard)
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const need = (name) => {
    if (!process.env[name]) throw new Error(`missing environment variable ${name}`);
    return process.env[name];
  };
  const result = await runA11yChecks({
    cdpPort: need("CDP_PORT"),
    baseUrl: need("BASE_URL"),
    kind: need("KIND"),
    axeJs: need("AXE_JS"),
    reportDir: reportDirFromArgs(),
    groups: (process.env.A11Y_GROUPS ?? "").split(",").filter(Boolean),
    log: (message) => process.stderr.write(`${new Date().toISOString()} ${message}\n`),
  });
  process.stdout.write(`${JSON.stringify({ passed: result.passed, assertions: result.assertions, failures: result.failures }, null, 2)}\n`);
  process.exit(result.passed ? 0 : 1);
}
