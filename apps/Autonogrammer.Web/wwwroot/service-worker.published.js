// Autonogrammer offline shell: the published service worker. The publish step copies this file to service-worker.js and appends the version of
// service-worker-assets.js, so a changed bundle is a changed worker and browsers install it.
//
// What it does
//   - Installs by downloading every static file the build lists in service-worker-assets.js into one cache named for that list's version.
//   - Answers a request for one of those files, and a page navigation (the page itself is index.html), from that cache; everything else is left to the browser.
//   - Never caches, and never answers from a cache: a request that is not GET, is cross-origin, carries an Authorization header, asks for an event stream
//     (SSE), is a range request, is /auth/config or anything under /auth/, or (apart from a page navigation) goes to an API path.
//     The worker never writes a response it fetched at run time into a cache, so a token, an API answer or a stream cannot end up in one.
//   - Replaces the running worker only when the user agrees: a new worker installs and waits. The page shows "Update available - reload" (register-service-worker.js)
//     and asks the waiting worker to take over with a SKIP_WAITING message. Activation deletes the caches of older versions.
//
// The host must send these Content-Security-Policy directives, so the worker and the manifest may load:
//     worker-src 'self'; manifest-src 'self'
// (worker-src falls back to script-src and manifest-src to default-src, both 'self' in the shipped policy, but a host that tightens either must keep them),
// and must serve service-worker.js and service-worker-assets.js with "Cache-Control: no-cache" so a new version is seen on the next visit.

self.importScripts("./service-worker-assets.js");

const CACHE_PREFIX = "autonogrammer-shell-";
const CACHE_NAME = CACHE_PREFIX + self.assetsManifest.version;
const SHELL_URL = "index.html";

// The files of the build that make the app start and run offline.
const CACHED_FILES = [/\.dll$/, /\.wasm$/, /\.html$/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.svg$/, /\.ico$/, /\.webmanifest$/, /\.blat$/, /\.dat$/];
const NOT_CACHED_FILES = [/^service-worker\.js$/, /^service-worker-assets\.js$/, /^appsettings\.demo\.json$/];

// index.html carries the build's fingerprint placeholders, which the publish step fills in. It is fetched without the manifest hash, so the install never depends on that step's order.
const WITHOUT_INTEGRITY = [/\.html$/];

// The paths that never come from a cache.
const AUTH_CONFIG_PATH = "/auth/config";
const AUTH_PATH_PREFIX = "/auth/";
// The roots of the API routes. A page navigation to one of these is a page of this app and still gets the shell (Settings has pages of its own).
const API_PATH_PREFIXES = ["/api/", "/sessions", "/providers", "/models", "/cloud", "/settings/", "/workspace", "/events", "/automations", "/board", "/hooks", "/source", "/health", "/metrics"];

const scopeUrl = new URL(self.registration.scope);
const shellFiles = self.assetsManifest.assets.filter(isCachedAsset);
const shellUrls = new Set(shellFiles.map(asset => new URL(asset.url, scopeUrl).href));

function isCachedAsset(asset) {
  return CACHED_FILES.some(pattern => pattern.test(asset.url)) && !NOT_CACHED_FILES.some(pattern => pattern.test(asset.url));
}

// True for every request that must reach the network and leave no copy: the worker neither reads nor writes a cache for it.
function isNeverCached(request) {
  if (request.method !== "GET") {
    return true;
  }

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) {
    return true;
  }

  if (request.headers.has("Authorization") || request.headers.has("Range")) {
    return true;
  }

  if ((request.headers.get("Accept") || "").includes("text/event-stream")) {
    return true;
  }

  if (url.pathname === AUTH_CONFIG_PATH || url.pathname.startsWith(AUTH_PATH_PREFIX)) {
    return true;
  }

  return request.mode !== "navigate" && API_PATH_PREFIXES.some(prefix => url.pathname.startsWith(prefix));
}

async function install() {
  const requests = shellFiles.map(asset => WITHOUT_INTEGRITY.some(pattern => pattern.test(asset.url))
    ? new Request(asset.url, { cache: "no-cache" })
    : new Request(asset.url, { cache: "no-cache", integrity: asset.hash }));
  const cache = await caches.open(CACHE_NAME);
  await cache.addAll(requests);
}

async function activate() {
  const names = await caches.keys();
  await Promise.all(names
    .filter(name => name.startsWith(CACHE_PREFIX) && name !== CACHE_NAME)
    .map(name => caches.delete(name)));
}

async function fromCache(key, request) {
  const cache = await caches.open(CACHE_NAME);
  const cached = await cache.match(key);
  return cached || fetch(request);
}

self.addEventListener("install", event => event.waitUntil(install()));
self.addEventListener("activate", event => event.waitUntil(activate()));

self.addEventListener("message", event => {
  if (event.data && event.data.type === "SKIP_WAITING") {
    self.skipWaiting();
  }
});

self.addEventListener("fetch", event => {
  const request = event.request;
  if (isNeverCached(request)) {
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(fromCache(SHELL_URL, request));
  } else if (shellUrls.has(request.url)) {
    event.respondWith(fromCache(request, request));
  }
});
