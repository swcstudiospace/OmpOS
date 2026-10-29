// Registers the offline-shell service worker and tells the user when a new version is waiting. It is a file, not an inline script, because the CSP
// forbids inline script. The host's CSP must allow the worker: worker-src 'self' (see service-worker.published.js).
//
// A new version never reloads the page by itself. When one is installed and waiting, a polite status reads "Update available - reload" with a Reload
// button; the button asks the waiting worker to take over and the page reloads once that has happened.
(function () {
  "use strict";

  if (!("serviceWorker" in navigator)) {
    return;
  }

  const STATUS_ID = "autonogrammer-update-status";
  let reloading = false;

  // The status region is in the page before it has anything to say, because a live region only announces text that is added to one that already exists.
  function statusRegion() {
    let region = document.getElementById(STATUS_ID);
    if (region === null) {
      region = document.createElement("div");
      region.id = STATUS_ID;
      region.setAttribute("role", "status");
      region.setAttribute("aria-live", "polite");
      region.style.position = "fixed";
      region.style.right = "1rem";
      region.style.bottom = "1rem";
      region.style.maxWidth = "calc(100vw - 2rem)";
      region.style.zIndex = "1001";
      document.body.appendChild(region);
    }

    return region;
  }

  function offerUpdate(worker) {
    const region = statusRegion();
    if (region.firstChild !== null) {
      return;
    }

    // System colours keep the notice readable in light, dark and forced-colour modes without a stylesheet of its own.
    region.style.background = "Canvas";
    region.style.color = "CanvasText";
    region.style.border = "1px solid CanvasText";
    region.style.borderRadius = "0.25rem";
    region.style.padding = "0.5rem 0.75rem";
    region.style.display = "flex";
    region.style.flexWrap = "wrap";
    region.style.alignItems = "center";
    region.style.gap = "0.5rem";

    const message = document.createElement("span");
    message.textContent = "Update available - reload";
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "Reload";
    button.style.minHeight = "1.5rem";
    button.style.minWidth = "1.5rem";
    button.style.font = "inherit";
    button.addEventListener("click", function () {
      button.disabled = true;
      navigator.serviceWorker.addEventListener("controllerchange", function () {
        if (!reloading) {
          reloading = true;
          window.location.reload();
        }
      });
      worker.postMessage({ type: "SKIP_WAITING" });
    });
    region.append(message, button);
  }

  function watch(registration) {
    if (registration.waiting !== null && navigator.serviceWorker.controller !== null) {
      offerUpdate(registration.waiting);
    }

    registration.addEventListener("updatefound", function () {
      const installing = registration.installing;
      if (installing === null) {
        return;
      }

      installing.addEventListener("statechange", function () {
        // The first install has no controller yet: that is a first visit, not an update.
        if (installing.state === "installed" && navigator.serviceWorker.controller !== null) {
          offerUpdate(installing);
        }
      });
    });
  }

  window.addEventListener("load", function () {
    statusRegion();
    navigator.serviceWorker
      .register("service-worker.js", { updateViaCache: "none" })
      .then(watch)
      .catch(function (error) {
        console.warn("Autonogrammer: the offline shell is not available (" + (error && error.name) + ").");
      });
  });
})();
