#!/usr/bin/env node
// Creates a throwaway omp agent directory that points omp at the scripted fake model server, and prints the
// environment to run omp (or the API that launches `omp acp`) with. It never touches ~/.omp and refuses a
// target that is inside it.
//
//   node scripts/hosted/prepare-omp-home.mjs [--dir <path>] [--fake-url <url>] [--model-id <id>]
//                                            [--approval-mode always-ask|always-ask-cached|write|yolo]
//                                            [--format env|export|json] [--shared]
//
// --shared makes the directories and files readable and writable by every user. Use it only where the user that
// prepares the directory differs from the one that runs omp (a container shell versus the service account); the
// directory holds no secret, only a dummy key.
//
// Files written (documented omp keys only; see omp docs "models.md", "settings.md", "approval-mode.md"):
//   <root>/agent/models.yml   providers.fake: baseUrl, api: openai-completions, apiKey (a dummy literal), one model
//   <root>/agent/config.yml   modelRoles.default: fake/<model id>, tools.approvalMode
//   <root>/home/              an empty HOME, so nothing is read from the real home directory
//
// Printed environment: PI_CODING_AGENT_DIR (relocates the default profile's agent directory), HOME and
// PI_NO_TITLE=1 (documented: disables the automatic session-title request, which would otherwise be a second
// model call for every first prompt).

import { chmodSync, mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { join, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import { DEFAULT_MODEL_ID } from "./fake-model-server.mjs";

const APPROVAL_MODES = ["always-ask", "always-ask-cached", "write", "yolo"];
const DUMMY_KEY = "fake-key-not-a-secret";

function parseArgs(argv) {
  const options = {
    dir: undefined,
    fakeUrl: process.env.FAKE_MODEL_URL ?? `http://127.0.0.1:${process.env.FAKE_MODEL_PORT ?? 18080}/v1`,
    modelId: process.env.FAKE_MODEL_ID ?? DEFAULT_MODEL_ID,
    approvalMode: "always-ask",
    format: "env",
    shared: false,
  };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const value = () => {
      if (i + 1 >= argv.length) throw new Error(`${arg} needs a value`);
      return argv[++i];
    };
    if (arg === "--dir") options.dir = value();
    else if (arg === "--fake-url") options.fakeUrl = value();
    else if (arg === "--model-id") options.modelId = value();
    else if (arg === "--approval-mode") options.approvalMode = value();
    else if (arg === "--format") options.format = value();
    else if (arg === "--shared") options.shared = true;
    else throw new Error(`unknown argument ${arg}`);
  }
  if (!APPROVAL_MODES.includes(options.approvalMode)) throw new Error(`--approval-mode must be one of ${APPROVAL_MODES.join(", ")}`);
  if (!["env", "export", "json"].includes(options.format)) throw new Error("--format must be env, export or json");
  if (!/^[A-Za-z0-9._-]+$/.test(options.modelId)) throw new Error("--model-id may contain only letters, digits, dot, underscore and hyphen");
  let url;
  try {
    url = new URL(options.fakeUrl);
  } catch {
    throw new Error(`--fake-url is not a URL: ${options.fakeUrl}`);
  }
  if (url.protocol !== "http:") throw new Error("--fake-url must be an http URL");
  options.fakeUrl = options.fakeUrl.replace(/\/+$/, "");
  return options;
}

function insideRealOmpHome(path) {
  const forbidden = resolve(homedir(), ".omp");
  const target = resolve(path);
  return target === forbidden || target.startsWith(forbidden + sep);
}

/**
 * Writes the throwaway agent directory.
 * @param {{dir?: string, fakeUrl: string, modelId: string, approvalMode: string, shared?: boolean}} options The options.
 * @returns {{root: string, agentDir: string, home: string, env: Record<string, string>}} The directories and the environment.
 */
export function prepareOmpHome(options) {
  const root = options.dir ? resolve(options.dir) : mkdtempSync(join(tmpdir(), "autonogrammer-omp-"));
  if (insideRealOmpHome(root)) throw new Error("refusing to write inside ~/.omp");
  const agentDir = join(root, "agent");
  const home = join(root, "home");
  mkdirSync(agentDir, { recursive: true, mode: 0o700 });
  mkdirSync(home, { recursive: true, mode: 0o700 });

  const modelsYml = [
    "providers:",
    "  fake:",
    `    baseUrl: ${options.fakeUrl}`,
    "    api: openai-completions",
    `    apiKey: ${DUMMY_KEY}`,
    "    models:",
    `      - id: ${options.modelId}`,
    "        name: Fake model (scripted)",
    "        reasoning: false",
    "        input: [text]",
    "        cost:",
    "          input: 0",
    "          output: 0",
    "          cacheRead: 0",
    "          cacheWrite: 0",
    "        contextWindow: 128000",
    "        maxTokens: 4096",
    "",
  ].join("\n");
  const configYml = ["modelRoles:", `  default: fake/${options.modelId}`, "tools:", `  approvalMode: ${options.approvalMode}`, ""].join("\n");
  writeFileSync(join(agentDir, "models.yml"), modelsYml, { mode: 0o600 });
  writeFileSync(join(agentDir, "config.yml"), configYml, { mode: 0o600 });
  if (options.shared) {
    for (const dir of [root, agentDir, home]) chmodSync(dir, 0o777);
    for (const file of ["models.yml", "config.yml"]) chmodSync(join(agentDir, file), 0o666);
  }

  return { root, agentDir, home, env: { PI_CODING_AGENT_DIR: agentDir, HOME: home, PI_NO_TITLE: "1" } };
}

const shellQuote = (value) => `'${String(value).replace(/'/g, `'\\''`)}'`;

function render(result, format) {
  if (format === "json") return `${JSON.stringify({ root: result.root, agentDir: result.agentDir, home: result.home, env: result.env }, null, 2)}\n`;
  const lines = Object.entries(result.env).map(([key, value]) => (format === "export" ? `export ${key}=${shellQuote(value)}` : `${key}=${value}`));
  return `${lines.join("\n")}\n`;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const options = parseArgs(process.argv.slice(2));
    const result = prepareOmpHome(options);
    process.stdout.write(render(result, options.format));
    process.stderr.write(`prepare-omp-home: wrote ${join(result.agentDir, "models.yml")} and config.yml (approvalMode ${options.approvalMode}, model fake/${options.modelId})\n`);
  } catch (error) {
    process.stderr.write(`prepare-omp-home: ${error.message}\n`);
    process.exit(1);
  }
}
