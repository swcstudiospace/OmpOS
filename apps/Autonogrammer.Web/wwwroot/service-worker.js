// Development build of the service worker: it does nothing. A development run always fetches from the network, so a stale cached file never hides an edit.
// The published build is service-worker.published.js; the project file swaps it in under this name (ServiceWorker item in Autonogrammer.Web.csproj).
self.addEventListener("fetch", () => { });
