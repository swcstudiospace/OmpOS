#!/usr/bin/env node
// Prints one JSON object (probe schema 1) on stdout.
// Exit codes: 0 healthy, 2 degraded, 3 not installed. No network; the only process it spawns is
// `node <gsdHome>/bin/gsd-tools.cjs runtime-identity --raw` with a 5 s timeout.
import { exitCodeFor, probe } from "../src/gsd-home.mjs";

const result = probe({ spawn: true });
process.stdout.write(`${JSON.stringify(result)}\n`);
process.exitCode = exitCodeFor(result);
