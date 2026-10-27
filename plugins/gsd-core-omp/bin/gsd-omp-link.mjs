#!/usr/bin/env node
// Maintains the generated symlinks in <plugin>/agents (same code path as the session_start handler).
// Usage: gsd-omp-link.mjs [--dry-run]
// Prints one JSON object. Exit codes: 0 ok, 1 link problems, 3 GSD-Core not installed, 64 bad usage.
import { syncAgents } from "../src/gsd-home.mjs";

const args = process.argv.slice(2);
const unknown = args.filter((a) => a !== "--dry-run");
if (unknown.length > 0) {
  process.stderr.write(`gsd-omp-link: unknown argument(s): ${unknown.join(" ")}\nusage: gsd-omp-link.mjs [--dry-run]\n`);
  process.exit(64);
}

const { probe, link } = syncAgents({ dryRun: args.includes("--dry-run") });
process.stdout.write(`${JSON.stringify({ schema: 1, status: probe.status, gsdHome: probe.gsdHome, link })}\n`);
process.exitCode = link === null ? 3 : link.errors.length > 0 ? 1 : 0;
