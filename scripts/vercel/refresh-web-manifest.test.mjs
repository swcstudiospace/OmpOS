// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 SWC Studio
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { afterEach, describe, test } from "node:test";
import { fileURLToPath } from "node:url";

const SCRIPT = join(dirname(fileURLToPath(import.meta.url)), "refresh-web-manifest.mjs");
const sha = (text) => `sha256-${createHash("sha256").update(text).digest("base64")}`;

const FILES = {
  "appsettings.json": '{"Api":{"BaseUrl":"http://127.0.0.1:5187"}}\n',
  "css/app.css": "body { margin: 0; }\n",
  "_framework/blazor.boot.json": '{"resources":{}}\n',
};
const OLD_VERSION = "1i7m3O/Z";

const dirs = [];
afterEach(() => {
  for (const dir of dirs.splice(0)) {
    rmSync(dir, { recursive: true, force: true });
  }
});

/** Writes a tiny bundle in the published format: a 3-entry manifest, a worker with the version comment and the files. */
function makeBundle({ manifestVersion = OLD_VERSION, workerFirstLine = `/* Manifest version: ${OLD_VERSION} */` } = {}) {
  const dir = mkdtempSync(join(tmpdir(), "refresh-web-manifest-"));
  dirs.push(dir);
  for (const [name, text] of Object.entries(FILES)) {
    mkdirSync(dirname(join(dir, name)), { recursive: true });
    writeFileSync(join(dir, name), text);
  }
  const manifest = { version: manifestVersion, assets: Object.entries(FILES).map(([url, text]) => ({ hash: sha(text), url })) };
  if (manifestVersion === null) {
    delete manifest.version;
  }
  writeFileSync(join(dir, "service-worker-assets.js"), `self.assetsManifest = ${JSON.stringify(manifest, null, 2)};\n`);
  writeFileSync(join(dir, "service-worker.js"), `${workerFirstLine}\nself.addEventListener('install', () => {});\n`);
  return dir;
}

const run = (...args) => spawnSync(process.execPath, [SCRIPT, ...args], { encoding: "utf8" });
const readManifest = (dir) => {
  const text = readFileSync(join(dir, "service-worker-assets.js"), "utf8");
  return JSON.parse(text.slice(text.indexOf("{"), text.lastIndexOf("}") + 1));
};
const snapshot = (dir) => ({
  manifest: readFileSync(join(dir, "service-worker-assets.js"), "utf8"),
  worker: readFileSync(join(dir, "service-worker.js"), "utf8"),
});

describe("refresh-web-manifest", () => {
  test("updates the named hash to the real value and leaves the other entries untouched", () => {
    const dir = makeBundle();
    const before = readManifest(dir);
    const rewritten = '{"Api":{"BaseUrl":"https://api.example.com"}}\n';
    writeFileSync(join(dir, "appsettings.json"), rewritten);

    const result = run(dir, "appsettings.json");
    assert.equal(result.status, 0, result.stderr);

    const after = readManifest(dir);
    assert.equal(after.assets.find((a) => a.url === "appsettings.json").hash, sha(rewritten));
    for (const url of ["css/app.css", "_framework/blazor.boot.json"]) {
      assert.deepEqual(after.assets.find((a) => a.url === url), before.assets.find((a) => a.url === url));
    }
    assert.deepEqual(after.assets.map((a) => a.url), before.assets.map((a) => a.url));
  });

  test("changes the version in both files to the documented derivation", () => {
    const dir = makeBundle();
    const rewritten = '{"Api":{"BaseUrl":"https://api.example.com"}}\n';
    writeFileSync(join(dir, "appsettings.json"), rewritten);

    assert.equal(run(dir, "appsettings.json").status, 0);

    const expected = createHash("sha256")
      .update(`${OLD_VERSION}\nappsettings.json=${sha(rewritten)}`)
      .digest("base64")
      .slice(0, 8);
    assert.notEqual(expected, OLD_VERSION);
    assert.equal(readManifest(dir).version, expected);
    const worker = readFileSync(join(dir, "service-worker.js"), "utf8");
    assert.equal(worker.split("\n")[0], `/* Manifest version: ${expected} */`);
    assert.equal(worker.split("\n")[1], "self.addEventListener('install', () => {});");
  });

  test("sorts the changed entries by url in the version derivation", () => {
    const dir = makeBundle();
    writeFileSync(join(dir, "appsettings.json"), "{}\n");
    writeFileSync(join(dir, "css/app.css"), "a {}\n");

    assert.equal(run(dir, "css/app.css", "appsettings.json").status, 0);

    const lines = [`appsettings.json=${sha("{}\n")}`, `css/app.css=${sha("a {}\n")}`];
    const expected = createHash("sha256").update(`${OLD_VERSION}\n${lines.join("\n")}`).digest("base64").slice(0, 8);
    assert.equal(readManifest(dir).version, expected);
  });

  test("a second run changes nothing", () => {
    const dir = makeBundle();
    writeFileSync(join(dir, "appsettings.json"), '{"Api":{"BaseUrl":"https://api.example.com"}}\n');
    assert.equal(run(dir, "appsettings.json").status, 0);
    const first = snapshot(dir);

    const second = run(dir, "appsettings.json");
    assert.equal(second.status, 0, second.stderr);
    assert.deepEqual(snapshot(dir), first);
  });

  test("an unchanged file changes nothing, not even the version", () => {
    const dir = makeBundle();
    const before = snapshot(dir);
    assert.equal(run(dir, "appsettings.json").status, 0);
    assert.deepEqual(snapshot(dir), before);
  });

  test("a file that is not in the manifest is reported and is not an error", () => {
    const dir = makeBundle();
    writeFileSync(join(dir, "extra.txt"), "not shipped\n");
    const before = snapshot(dir);

    const result = run(dir, "extra.txt");
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stderr, /extra\.txt is not in the manifest/);
    assert.deepEqual(snapshot(dir), before);
  });

  test("a manifest without a version fails and writes nothing", () => {
    const dir = makeBundle({ manifestVersion: null });
    writeFileSync(join(dir, "appsettings.json"), "{}\n");
    const before = snapshot(dir);

    const result = run(dir, "appsettings.json");
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /version/);
    assert.deepEqual(snapshot(dir), before);
  });

  test("a worker without the first-line comment fails and writes nothing", () => {
    const dir = makeBundle({ workerFirstLine: "// no version here" });
    writeFileSync(join(dir, "appsettings.json"), "{}\n");
    const before = snapshot(dir);

    const result = run(dir, "appsettings.json");
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /service-worker\.js/);
    assert.deepEqual(snapshot(dir), before);
  });

  test("a named file that is listed but missing fails and writes nothing", () => {
    const dir = makeBundle();
    rmSync(join(dir, "css/app.css"));
    writeFileSync(join(dir, "appsettings.json"), "{}\n");
    const before = snapshot(dir);

    const result = run(dir, "appsettings.json", "css/app.css");
    assert.notEqual(result.status, 0);
    assert.deepEqual(snapshot(dir), before);
  });

  test("a path outside the bundle is refused", () => {
    const dir = makeBundle();
    const result = run(dir, "../escape.txt");
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /not a path inside the bundle/);
  });

  test("--verify fails on a changed file and passes after the refresh", () => {
    const dir = makeBundle();
    const clean = run("--verify", dir);
    assert.equal(clean.status, 0, clean.stderr);
    assert.match(clean.stdout, /3 assets/);

    writeFileSync(join(dir, "appsettings.json"), '{"Api":{"BaseUrl":"https://api.example.com"}}\n');
    const broken = run("--verify", dir);
    assert.equal(broken.status, 1);
    assert.match(broken.stderr, /appsettings\.json/);
    assert.doesNotMatch(broken.stderr, /app\.css/);

    assert.equal(run(dir, "appsettings.json").status, 0);
    const fixed = run("--verify", dir);
    assert.equal(fixed.status, 0, fixed.stderr);
  });

  test("--verify reports a missing file and a version that differs between worker and manifest", () => {
    const dir = makeBundle({ workerFirstLine: "/* Manifest version: other123 */" });
    rmSync(join(dir, "css/app.css"));

    const result = run("--verify", dir);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /css\/app\.css: missing/);
    assert.match(result.stderr, /other123/);
  });

  test("--verify skips the worker, the manifest and the demo settings", () => {
    const dir = makeBundle();
    const manifest = readManifest(dir);
    for (const url of ["service-worker.js", "service-worker-assets.js", "appsettings.demo.json"]) {
      manifest.assets.push({ hash: "sha256-unrelated", url });
    }
    writeFileSync(join(dir, "service-worker-assets.js"), `self.assetsManifest = ${JSON.stringify(manifest, null, 2)};\n`);

    const result = run("--verify", dir);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /3 assets/);
  });
});

const FINISH = join(dirname(fileURLToPath(import.meta.url)), "finish-web-bundle.sh");
const finish = (dir, origin) => {
  const env = { ...process.env };
  delete env.AUTONOGRAMMER_API_ORIGIN;
  if (origin !== undefined) {
    env.AUTONOGRAMMER_API_ORIGIN = origin;
  }
  return spawnSync("bash", [FINISH, dir], { encoding: "utf8", env });
};

describe("finish-web-bundle.sh", () => {
  test("writes the origin into appsettings.json and leaves a bundle whose manifest verifies", () => {
    const dir = makeBundle();

    const result = finish(dir, "https://api.example.com");
    assert.equal(result.status, 0, result.stderr);

    const settings = readFileSync(join(dir, "appsettings.json"), "utf8");
    assert.match(settings, /"BaseUrl": "https:\/\/api\.example\.com"/);
    assert.equal(readManifest(dir).assets.find((a) => a.url === "appsettings.json").hash, sha(settings));
    assert.notEqual(readManifest(dir).version, OLD_VERSION);
    assert.equal(run("--verify", dir).status, 0);
  });

  test("without an origin it fails before it changes anything", () => {
    const dir = makeBundle();
    const before = { ...snapshot(dir), settings: readFileSync(join(dir, "appsettings.json"), "utf8") };

    const result = finish(dir, undefined);
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /AUTONOGRAMMER_API_ORIGIN/);
    assert.deepEqual({ ...snapshot(dir), settings: readFileSync(join(dir, "appsettings.json"), "utf8") }, before);
  });

  test("a plain http origin outside loopback is refused", () => {
    const dir = makeBundle();
    const before = snapshot(dir);

    assert.notEqual(finish(dir, "http://api.example.com").status, 0);
    assert.deepEqual(snapshot(dir), before);
  });

  test("a bundle that carries the Demo settings file is refused", () => {
    const dir = makeBundle();
    writeFileSync(join(dir, "appsettings.demo.json"), "{}\n");

    const result = finish(dir, "https://api.example.com");
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Demo/);
  });
});
