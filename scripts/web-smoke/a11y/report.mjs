// Structured findings of the accessibility checks. A check never throws for a defect it found: it adds a finding. The smoke fails when a
// finding has the severity "error"; "warning" and "info" are printed and written to the report but do not fail the run.

import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

/** The severities, most serious first. */
export const SEVERITIES = ["error", "warning", "info"];

/**
 * Creates a finding.
 * @param {string} check The check group: routes, first-load-focus, keyboard, dialog, media, reflow, text-spacing, target-size, axe or connect.
 * @param {string} id A stable identifier of the kind of defect, such as "title-missing".
 * @param {"error"|"warning"|"info"} severity The severity.
 * @param {string} message One sentence that says what is wrong and where.
 * @param {{state?: string, viewport?: string, target?: string, wcag?: string, detail?: unknown}} [extra] Where it was found and the evidence.
 * @returns {object} The finding.
 */
export function finding(check, id, severity, message, extra = {}) {
  if (!SEVERITIES.includes(severity)) throw new Error(`unknown severity ${severity}`);
  return { check, id, severity, message, ...extra };
}

/** Pads or cuts a cell of the text table. */
function cell(value, width) {
  const text = String(value ?? "").replace(/\s+/g, " ");
  return text.length > width ? `${text.slice(0, width - 1)}…` : text.padEnd(width);
}

/** Formats findings as a fixed-width table; informational findings are counted, not listed. */
export function formatTable(findings) {
  const shown = findings.filter((f) => f.severity !== "info");
  if (shown.length === 0) return "no errors or warnings";
  const rows = shown.map((f) => [f.severity, f.check, [f.state, f.viewport].filter(Boolean).join(" @ "), f.id, f.message]);
  const header = ["severity", "check", "where", "id", "message"];
  const widths = [8, 16, 28, 26, 120];
  const line = (columns) => columns.map((value, index) => cell(value, widths[index])).join(" | ").trimEnd();
  return [line(header), line(widths.map((w) => "-".repeat(Math.min(w, 24)))), ...rows.map(line)].join("\n");
}

/** Collects findings, per-check counters and screenshots of one run. */
export class Report {
  constructor() {
    this.findings = [];
    this.counters = new Map();
    this.screenshots = [];
    this.skipped = [];
  }

  /** Adds one finding or a list of them; an identical finding (same check, id, state, viewport and target) is kept once. */
  add(items) {
    for (const item of Array.isArray(items) ? items : [items]) {
      const key = [item.check, item.id, item.state ?? "", item.viewport ?? "", item.target ?? "", item.severity].join("\u0000");
      if (this.findings.some((existing) => existing._key === key)) continue;
      this.findings.push({ ...item, _key: key });
    }
  }

  /** Counts something a check did (stops walked, states visited, elements measured), so a run that checked nothing is visible. */
  count(name, amount = 1) {
    this.counters.set(name, (this.counters.get(name) ?? 0) + amount);
  }

  /** Records that a part of a check was not run, with the reason; the run shows it and the report keeps it. */
  skip(check, reason) {
    this.skipped.push({ check, reason });
  }

  /** Keeps a screenshot (base64 PNG) for the report directory. */
  screenshot(name, base64) {
    this.screenshots.push({ name, base64 });
  }

  /** The findings of the severity error, optionally of one check group. */
  errors(check) {
    return this.findings.filter((f) => f.severity === "error" && (check === undefined || f.check === check));
  }

  /** The number of errors and warnings of each check group that ran. */
  summary() {
    const groups = {};
    for (const f of this.findings) {
      groups[f.check] ??= { errors: 0, warnings: 0, infos: 0 };
      if (f.severity === "error") groups[f.check].errors += 1;
      else if (f.severity === "warning") groups[f.check].warnings += 1;
      else groups[f.check].infos += 1;
    }
    return groups;
  }

  /** The report as a plain object. */
  toJSON() {
    return {
      findings: this.findings.map(({ _key, ...rest }) => rest),
      counters: Object.fromEntries(this.counters),
      skipped: this.skipped,
      summary: this.summary(),
    };
  }

  /** Writes a11y-report.json and the screenshots into the directory (created when missing); does nothing without a directory. */
  write(directory, meta = {}) {
    if (!directory) return null;
    mkdirSync(join(directory, "screenshots"), { recursive: true });
    for (const shot of this.screenshots) {
      writeFileSync(join(directory, "screenshots", `${shot.name}.png`), Buffer.from(shot.base64, "base64"));
    }
    const file = join(directory, "a11y-report.json");
    writeFileSync(file, `${JSON.stringify({ ...meta, ...this.toJSON(), screenshotFiles: this.screenshots.map((s) => `screenshots/${s.name}.png`) }, null, 2)}\n`);
    return file;
  }

  /** The table of errors and warnings. */
  table() {
    return formatTable(this.findings);
  }
}
