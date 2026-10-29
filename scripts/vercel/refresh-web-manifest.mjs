// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 SWC Studio
//
// Keeps the Blazor service-worker asset manifest honest after a published web bundle has been edited, and checks it.
//
// `dotnet publish` writes service-worker-assets.js (self.assetsManifest = { "version", "assets": [{ "hash", "url" }] }) and the
// published service worker (service-worker.js, first line `/* Manifest version: <version> */`) pre-caches every manifest asset
// it matches with `integrity: asset.hash`. build-web.sh rewrites Api:BaseUrl in appsettings.json after the publish, so the
// file no longer has the manifest hash: cache.addAll rejects and the worker never installs. The worker's cache name uses the
// manifest version, so the version has to change whenever a file changes.
//
// Usage:
//   node refresh-web-manifest.mjs <bundle-dir> <relative-file>...   re-hash the named files in the manifest, bump the version
//   node refresh-web-manifest.mjs --verify <bundle-dir>             compare every manifest asset with the file on disk
//
// Node standard library only.
import { createHash } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { isAbsolute, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

export const MANIFEST_FILE = "service-worker-assets.js";
export const WORKER_FILE = "service-worker.js";
// Assets the worker does not pre-cache with an integrity check, or that are not shipped in a real bundle.
export const VERIFY_SKIPPED = new Set([WORKER_FILE, MANIFEST_FILE, "appsettings.demo.json"]);

const MANIFEST_ASSIGNMENT = "self.assetsManifest =";
const WORKER_FIRST_LINE = /^\/\* Manifest version: (.+) \*\/$/;

/** `sha256-` plus the base64 SHA-256 of the bytes, the format of the manifest hash. */
export function integrityOf(bytes) {
  return `sha256-${createHash("sha256").update(bytes).digest("base64")}`;
}

/** The new manifest version: the first 8 base64 characters of SHA-256 over the old version, a newline and the sorted `url=hash` lines joined by newlines. */
export function deriveVersion(oldVersion, changed) {
  const lines = changed.map(({ url, hash }) => `${url}=${hash}`).sort();
  const digest = createHash("sha256").update(`${oldVersion}\n${lines.join("\n")}`).digest("base64");
  return digest.slice(0, 8);
}

/**
 * Splits service-worker-assets.js into the text around the object literal and the parsed manifest. The object literal is the
 * JSON between the first `{` after `self.assetsManifest =` and the last `}`.
 */
export function parseManifest(text) {
  const assignment = text.indexOf(MANIFEST_ASSIGNMENT);
  const start = assignment < 0 ? -1 : text.indexOf("{", assignment);
  const end = text.lastIndexOf("}");
  if (start < 0 || end < start) {
    throw new Error(`${MANIFEST_FILE}: no "${MANIFEST_ASSIGNMENT} { ... }" object literal found`);
  }
  let manifest;
  try {
    manifest = JSON.parse(text.slice(start, end + 1));
  } catch (error) {
    throw new Error(`${MANIFEST_FILE}: the object literal is not JSON (${error.message})`);
  }
  if (typeof manifest.version !== "string" || manifest.version === "") {
    throw new Error(`${MANIFEST_FILE}: no "version" string in the manifest`);
  }
  if (!Array.isArray(manifest.assets)) {
    throw new Error(`${MANIFEST_FILE}: no "assets" array in the manifest`);
  }
  const head = text.slice(0, start);
  const tail = text.slice(end + 1);
  // The rewrite serialises the parsed manifest; it is only safe when that reproduces the published text exactly.
  if (head + JSON.stringify(manifest, null, 2) + tail !== text) {
    throw new Error(`${MANIFEST_FILE}: not in the published format (two-space JSON); refusing to rewrite it`);
  }
  return { head, tail, manifest };
}

/** Reads the version from the first line of service-worker.js and returns it with the text after that line. */
export function parseWorker(text) {
  const newline = text.indexOf("\n");
  const firstLine = newline < 0 ? text : text.slice(0, newline);
  const rest = newline < 0 ? "" : text.slice(newline);
  const carriageReturn = firstLine.endsWith("\r") ? "\r" : "";
  const match = WORKER_FIRST_LINE.exec(carriageReturn ? firstLine.slice(0, -1) : firstLine);
  if (!match) {
    throw new Error(`${WORKER_FILE}: the first line is not "/* Manifest version: <version> */"`);
  }
  return { version: match[1], carriageReturn, rest };
}

/** Resolves a bundle-relative path and refuses anything that leaves the bundle directory. */
function insideBundle(bundleDir, file) {
  const full = resolve(bundleDir, file);
  const rel = relative(resolve(bundleDir), full);
  if (rel === "" || rel.startsWith("..") || isAbsolute(rel)) {
    throw new Error(`${file}: not a path inside the bundle`);
  }
  return { full, url: rel.split(sep).join("/") };
}

/**
 * Computes the refresh without writing anything. Returns the files to write (empty when no hash changed), the changed urls and
 * the named files that have no manifest entry. Throws when the bundle cannot be refreshed safely.
 */
export function planRefresh(bundleDir, files) {
  const manifestPath = join(bundleDir, MANIFEST_FILE);
  const workerPath = join(bundleDir, WORKER_FILE);
  const { head, tail, manifest } = parseManifest(readFileSync(manifestPath, "utf8"));
  const worker = parseWorker(readFileSync(workerPath, "utf8"));

  const changed = [];
  const notInManifest = [];
  for (const file of files) {
    const { full, url } = insideBundle(bundleDir, file);
    const entries = manifest.assets.filter((asset) => asset.url === url);
    if (entries.length === 0) {
      notInManifest.push(url);
      continue;
    }
    if (entries.length > 1) {
      throw new Error(`${MANIFEST_FILE}: ${entries.length} entries for ${url}`);
    }
    if (!existsSync(full)) {
      throw new Error(`${url}: listed in the manifest but missing from the bundle`);
    }
    const hash = integrityOf(readFileSync(full));
    if (entries[0].hash !== hash) {
      entries[0].hash = hash;
      changed.push({ url, hash });
    }
  }
  if (changed.length === 0) {
    return { writes: [], changed, notInManifest, version: manifest.version };
  }

  const version = deriveVersion(manifest.version, changed);
  manifest.version = version;
  const writes = [
    { path: manifestPath, text: head + JSON.stringify(manifest, null, 2) + tail },
    { path: workerPath, text: `/* Manifest version: ${version} */${worker.carriageReturn}${worker.rest}` },
  ];
  return { writes, changed, notInManifest, version };
}

/** Compares every checked manifest asset with the file on disk. Returns one message per problem and the number of assets checked. */
export function verifyBundle(bundleDir) {
  const problems = [];
  const { manifest } = parseManifest(readFileSync(join(bundleDir, MANIFEST_FILE), "utf8"));
  let checked = 0;
  for (const asset of manifest.assets) {
    if (VERIFY_SKIPPED.has(asset.url)) {
      continue;
    }
    checked += 1;
    let full;
    try {
      full = insideBundle(bundleDir, asset.url).full;
    } catch (error) {
      problems.push(`${asset.url}: ${error.message}`);
      continue;
    }
    if (!existsSync(full)) {
      problems.push(`${asset.url}: missing from the bundle`);
    } else if (integrityOf(readFileSync(full)) !== asset.hash) {
      problems.push(`${asset.url}: hash differs from the manifest`);
    }
  }
  const workerVersion = parseWorker(readFileSync(join(bundleDir, WORKER_FILE), "utf8")).version;
  if (workerVersion !== manifest.version) {
    problems.push(`${WORKER_FILE}: version "${workerVersion}" differs from the manifest version "${manifest.version}"`);
  }
  return { problems, checked };
}

/** Runs the command line and returns the exit code. */
export function main(argv, out = process.stdout, err = process.stderr) {
  try {
    if (argv[0] === "--verify") {
      if (argv.length !== 2) {
        err.write("usage: refresh-web-manifest.mjs --verify <bundle-dir>\n");
        return 2;
      }
      const { problems, checked } = verifyBundle(argv[1]);
      if (problems.length > 0) {
        for (const problem of problems) {
          err.write(`${problem}\n`);
        }
        return 1;
      }
      out.write(`refresh-web-manifest: ${checked} assets match the manifest\n`);
      return 0;
    }
    if (argv.length < 2) {
      err.write("usage: refresh-web-manifest.mjs <bundle-dir> <relative-file>...\n       refresh-web-manifest.mjs --verify <bundle-dir>\n");
      return 2;
    }
    const [bundleDir, ...files] = argv;
    const plan = planRefresh(bundleDir, files);
    for (const url of plan.notInManifest) {
      err.write(`refresh-web-manifest: ${url} is not in the manifest, left alone\n`);
    }
    for (const write of plan.writes) {
      writeFileSync(write.path, write.text);
    }
    if (plan.writes.length === 0) {
      out.write("refresh-web-manifest: manifest already matches, nothing changed\n");
    } else {
      out.write(`refresh-web-manifest: updated ${plan.changed.map((entry) => entry.url).join(", ")}; manifest version is now ${plan.version}\n`);
    }
    return 0;
  } catch (error) {
    err.write(`refresh-web-manifest: ${error.message}\n`);
    return 1;
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = main(process.argv.slice(2));
}
