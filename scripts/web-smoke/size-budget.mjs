// Payload budget gate for the published web head. Walks a published wwwroot, sums the raw, gzip and Brotli bytes of the boot set and compares
// the sums with scripts/web-smoke/size-budget.json. Zero dependencies: node:fs, node:path, node:zlib and node:assert only.
//
// The boot set is what a first visit downloads before the app is usable:
//   - every file under _framework/ (the runtime, the assemblies, the boot configuration), except the .gz and .br copies a build can add beside them;
//   - every stylesheet, script and preloaded file that index.html references (stylesheet, preload and modulepreload links, script tags);
//   - what those stylesheets pull in: their @import files and the url() fonts and stylesheets.
// Icons, the web manifest, the service worker and the images of the page are not part of it.
//
// Usage
//   node scripts/web-smoke/size-budget.mjs [--budget FILE] [--write-baseline] WWWROOT
//   node scripts/web-smoke/size-budget.mjs --self-test
// Output: one JSON document on stdout, progress on stderr.
// Exit: 0 within budget, 1 over budget or a file index.html references is missing, 2 for a usage or read error.
// --write-baseline records the measured sizes plus 10 percent as the new limits (provisional: false); run it once after an intended change.

import assert from "node:assert/strict";
import { mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, posix, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { brotliCompressSync, constants, gzipSync } from "node:zlib";

const HEADROOM = 1.1;
const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const DEFAULT_BUDGET = join(SCRIPT_DIR, "size-budget.json");
const PRECOMPRESSED = /\.(gz|br)$/i;
const STYLESHEET = /\.css$/i;
const FONT = /\.(woff2?|ttf|otf)$/i;
const LINK_RELS = new Set(["stylesheet", "preload", "modulepreload"]);
const METRICS = ["rawBytes", "gzipBytes", "brotliBytes"];

/** The file system the measurement reads. The real one walks a directory; the self-test supplies a map. */
export function directoryFiles(root) {
  const files = new Map();
  const walk = (directory, prefix) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = prefix === "" ? entry.name : `${prefix}/${entry.name}`;
      if (entry.isDirectory()) {
        walk(join(directory, entry.name), path);
      } else if (entry.isFile()) {
        files.set(path, () => readFileSync(join(directory, entry.name)));
      }
    }
  };
  walk(root, "");
  return files;
}

/** Turns a value of a src or href attribute into a path under the root, or null when it is not a local file. */
export function localPath(value, fromDirectory) {
  const cleaned = value.replace(/#\[\.\{fingerprint\}\]/g, "").split("#")[0].split("?")[0].trim();
  if (cleaned === "" || /^[a-z][a-z0-9+.-]*:/i.test(cleaned) || cleaned.startsWith("//")) {
    return null;
  }

  const joined = cleaned.startsWith("/") ? posix.normalize(cleaned.slice(1)) : posix.normalize(posix.join(fromDirectory, cleaned));
  return joined.startsWith("..") || joined === "." ? null : joined;
}

/** Reads the files index.html loads: stylesheets, preloads and scripts. */
export function htmlReferences(html) {
  const found = [];
  for (const tag of html.matchAll(/<(script|link)\b[^>]*>/gi)) {
    const attributes = new Map();
    for (const attribute of tag[0].matchAll(/([a-zA-Z-]+)\s*=\s*"([^"]*)"/g)) {
      attributes.set(attribute[1].toLowerCase(), attribute[2]);
    }

    if (tag[1].toLowerCase() === "script") {
      if (attributes.has("src")) {
        found.push(attributes.get("src"));
      }
    } else if (attributes.has("href") && (attributes.get("rel") ?? "").toLowerCase().split(/\s+/).some(rel => LINK_RELS.has(rel))) {
      found.push(attributes.get("href"));
    }
  }

  return found;
}

/** Reads what a stylesheet pulls in: its @import files and the stylesheets and fonts its url() values name. Comments are not code and name nothing. */
export function cssReferences(css) {
  const found = [];
  const code = css.replace(/\/\*[\s\S]*?\*\//g, "");
  for (const match of code.matchAll(/@import\s+(?:url\(\s*)?["']?([^"')\s;]+)["']?/gi)) {
    found.push(match[1]);
  }

  for (const match of code.matchAll(/url\(\s*["']?([^"')\s]+)["']?\s*\)/gi)) {
    const value = match[1].split("#")[0].split("?")[0];
    if (FONT.test(value) || STYLESHEET.test(value)) {
      found.push(match[1]);
    }
  }

  return found;
}

/** Finds the boot set of a published wwwroot. */
export function bootSet(files) {
  const set = new Set();
  const missing = [];
  for (const path of files.keys()) {
    if (path.startsWith("_framework/") && !PRECOMPRESSED.test(path)) {
      set.add(path);
    }
  }

  const queue = [];
  const add = (path, from) => {
    if (path === null || set.has(path)) {
      return;
    }

    if (!files.has(path)) {
      missing.push({ path, referencedBy: from });
      return;
    }

    set.add(path);
    if (STYLESHEET.test(path)) {
      queue.push(path);
    }
  };

  const index = files.get("index.html");
  if (index === undefined) {
    missing.push({ path: "index.html", referencedBy: "(the published wwwroot)" });
  } else {
    for (const reference of htmlReferences(index().toString("utf8"))) {
      add(localPath(reference, ""), "index.html");
    }
  }

  while (queue.length > 0) {
    const stylesheet = queue.shift();
    const directory = posix.dirname(stylesheet) === "." ? "" : posix.dirname(stylesheet);
    for (const reference of cssReferences(files.get(stylesheet)().toString("utf8"))) {
      add(localPath(reference, directory), stylesheet);
    }
  }

  return { paths: [...set].sort(), missing };
}

/** Measures the raw, gzip (level 9) and Brotli (quality 11) bytes of a set of files. */
export function measure(files, paths) {
  const total = { rawBytes: 0, gzipBytes: 0, brotliBytes: 0 };
  const perFile = [];
  for (const path of paths) {
    const bytes = files.get(path)();
    const sizes = {
      rawBytes: bytes.length,
      gzipBytes: gzipSync(bytes, { level: 9 }).length,
      brotliBytes: brotliCompressSync(bytes, {
        params: { [constants.BROTLI_PARAM_QUALITY]: 11, [constants.BROTLI_PARAM_SIZE_HINT]: bytes.length },
      }).length,
    };
    for (const metric of METRICS) {
      total[metric] += sizes[metric];
    }

    perFile.push({ path, ...sizes });
  }

  return { total, perFile };
}

/** Compares the measured totals with the budget limits. */
export function compare(total, limits) {
  const over = [];
  for (const metric of METRICS) {
    const limit = limits[metric];
    if (!Number.isInteger(limit) || limit <= 0) {
      throw new Error(`the budget has no positive integer limit for ${metric}`);
    }

    if (total[metric] > limit) {
      over.push({ metric, limit, measured: total[metric], overBy: total[metric] - limit });
    }
  }

  return over;
}

/** Runs the whole gate on a file map and a budget; returns the summary and the exit code. */
export function check(files, budget) {
  const { paths, missing } = bootSet(files);
  const { total, perFile } = measure(files, paths);
  const over = compare(total, budget.limits);
  const largest = [...perFile].sort((a, b) => b.brotliBytes - a.brotliBytes).slice(0, 10);
  const ok = over.length === 0 && missing.length === 0;
  return {
    exitCode: ok ? 0 : 1,
    summary: {
      ok,
      provisional: budget.provisional === true,
      basis: budget.basis,
      bootFiles: paths.length,
      measured: total,
      limits: budget.limits,
      over,
      missing,
      largest,
    },
  };
}

function readBudget(path) {
  const budget = JSON.parse(readFileSync(path, "utf8"));
  if (typeof budget !== "object" || budget === null || typeof budget.limits !== "object" || budget.limits === null) {
    throw new Error(`${path} must hold an object with a "limits" object`);
  }

  return budget;
}

function selfTest() {
  const text = value => () => Buffer.from(value, "utf8");
  const filler = length => () => Buffer.from("autonogrammer ".repeat(Math.ceil(length / 14)).slice(0, length), "utf8");
  const files = new Map([
    ["index.html", text(`<!DOCTYPE html><html><head>
      <link rel="preload" id="webassembly" />
      <script src="_content/Lib/scripts/theme-boot.js"></script>
      <link rel="preload" href="_content/Lib/fonts/a.woff2" as="font" type="font/woff2" crossorigin />
      <link rel="stylesheet" href="_content/Lib/styles/main.css" />
      <link rel="stylesheet" href="css/app.css?v=1" />
      <link rel="icon" href="favicon.ico" />
      <link rel="manifest" href="site.webmanifest" />
      <link rel="stylesheet" href="https://cdn.example/other.css" />
      </head><body><script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>
      <script src="register-service-worker.js" defer></script></body></html>`)],
    ["_framework/blazor.webassembly.js", text("export const boot = 1;")],
    ["_framework/dotnet.native.wasm", filler(4000)],
    ["_framework/dotnet.native.wasm.br", filler(100)],
    ["_framework/dotnet.native.wasm.gz", filler(100)],
    ["_content/Lib/scripts/theme-boot.js", text("document.documentElement.dataset.theme = 'light';")],
    ["_content/Lib/scripts/unused.js", filler(5000)],
    ["_content/Lib/fonts/a.woff2", filler(800)],
    ["_content/Lib/fonts/b.woff2", filler(900)],
    ["_content/Lib/styles/main.css", text("/* The @import urls are relative to this file; url(missing.woff2) in a comment is not a reference. */ @import \"tokens.css\"; @import url('./base.css'); @font-face { src: url('../fonts/b.woff2') format('woff2'); } .x { background: url(../img/x.png); }")],
    ["_content/Lib/styles/tokens.css", text(":root { --a: 1; }")],
    ["_content/Lib/styles/base.css", text("body { margin: 0; }")],
    ["_content/Lib/img/x.png", filler(7000)],
    ["css/app.css", text("#app { display: block; }")],
    ["register-service-worker.js", text("(function () {})();")],
    ["favicon.ico", filler(3000)],
    ["site.webmanifest", text("{}")],
  ]);

  const { paths, missing } = bootSet(files);
  assert.deepEqual(paths, [
    "_content/Lib/fonts/a.woff2",
    "_content/Lib/fonts/b.woff2",
    "_content/Lib/scripts/theme-boot.js",
    "_content/Lib/styles/base.css",
    "_content/Lib/styles/main.css",
    "_content/Lib/styles/tokens.css",
    "_framework/blazor.webassembly.js",
    "_framework/dotnet.native.wasm",
    "css/app.css",
    "register-service-worker.js",
  ]);
  assert.deepEqual(missing, []);

  const { total, perFile } = measure(files, paths);
  assert.equal(perFile.length, paths.length);
  assert.equal(total.rawBytes, perFile.reduce((sum, file) => sum + file.rawBytes, 0));
  assert.ok(total.gzipBytes > 0 && total.gzipBytes < total.rawBytes, "repetitive text must compress");
  assert.ok(total.brotliBytes > 0 && total.brotliBytes <= total.gzipBytes, "brotli must not be larger than gzip on repetitive text");

  const within = { limits: { rawBytes: total.rawBytes, gzipBytes: total.gzipBytes, brotliBytes: total.brotliBytes }, provisional: false, basis: "self-test" };
  const passed = check(files, within);
  assert.equal(passed.exitCode, 0);
  assert.equal(passed.summary.ok, true);

  const tight = { limits: { ...within.limits, gzipBytes: total.gzipBytes - 1 }, provisional: false, basis: "self-test" };
  const failed = check(files, tight);
  assert.equal(failed.exitCode, 1);
  assert.deepEqual(failed.summary.over.map(entry => [entry.metric, entry.overBy]), [["gzipBytes", 1]]);

  const broken = new Map(files);
  broken.delete("css/app.css");
  const brokenResult = check(broken, within);
  assert.equal(brokenResult.exitCode, 1);
  assert.deepEqual(brokenResult.summary.missing, [{ path: "css/app.css", referencedBy: "index.html" }]);

  assert.equal(localPath("../../etc/passwd", "a"), null);
  assert.equal(localPath("https://example.org/x.js", ""), null);
  assert.equal(localPath("data:text/plain,hi", ""), null);
  assert.equal(localPath("/css/app.css", "deep/dir"), "css/app.css");
  assert.throws(() => compare(total, { rawBytes: 1, gzipBytes: 1 }), /brotliBytes/);

  process.stderr.write("size-budget self-test: ok\n");
  process.stdout.write(`${JSON.stringify({ selfTest: "ok", bootFiles: paths.length })}\n`);
  return 0;
}

function parseArguments(argv) {
  const options = { budget: DEFAULT_BUDGET, writeBaseline: false, selfTest: false, root: null };
  for (let i = 0; i < argv.length; i++) {
    const argument = argv[i];
    if (argument === "--self-test") {
      options.selfTest = true;
    } else if (argument === "--write-baseline") {
      options.writeBaseline = true;
    } else if (argument === "--budget") {
      options.budget = argv[++i];
    } else if (argument.startsWith("--") || options.root !== null) {
      throw new Error(`unexpected argument ${argument}`);
    } else {
      options.root = argument;
    }
  }

  if (!options.selfTest && (options.root === null || options.budget === undefined)) {
    throw new Error("usage: size-budget.mjs [--budget FILE] [--write-baseline] WWWROOT | --self-test");
  }

  return options;
}

function main(argv) {
  let options;
  try {
    options = parseArguments(argv);
    if (options.selfTest) {
      return selfTest();
    }

    const root = resolve(options.root);
    if (!statSync(root).isDirectory()) {
      throw new Error(`${root} is not a directory`);
    }

    const files = directoryFiles(root);
    const budget = readBudget(options.budget);
    if (options.writeBaseline) {
      const { paths } = bootSet(files);
      const { total } = measure(files, paths);
      const limits = Object.fromEntries(METRICS.map(metric => [metric, Math.ceil(total[metric] * HEADROOM)]));
      const next = {
        schema: 1,
        provisional: false,
        basis: `Measured on a published wwwroot (${paths.length} boot files, raw ${total.rawBytes}, gzip ${total.gzipBytes}, brotli ${total.brotliBytes} bytes) plus 10 percent.`,
        limits,
      };
      mkdirSync(dirname(resolve(options.budget)), { recursive: true });
      writeFileSync(options.budget, `${JSON.stringify(next, null, 2)}\n`);
      process.stderr.write(`size-budget: wrote ${options.budget}\n`);
      process.stdout.write(`${JSON.stringify({ ok: true, wroteBaseline: true, measured: total, limits })}\n`);
      return 0;
    }

    const { exitCode, summary } = check(files, budget);
    summary.wwwroot = root.split(sep).join("/");
    process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
    if (exitCode !== 0) {
      process.stderr.write(`size-budget: FAILED (${summary.over.length} limit(s) over, ${summary.missing.length} file(s) missing)\n`);
    } else if (summary.provisional) {
      process.stderr.write("size-budget: within a PROVISIONAL budget; run with --write-baseline after the first measured publish\n");
    }

    return exitCode;
  } catch (error) {
    process.stderr.write(`size-budget: ${error.message}\n`);
    return 2;
  }
}

if (process.argv[1] !== undefined && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  process.exitCode = main(process.argv.slice(2));
}
