// Applies the saved surface to <html> before the first paint. index.html loads this as a plain blocking script
// from the head (a file, because the CSP script-src 'self' forbids inline script). It must stay dependency-free and must never throw:
// storage can be blocked, and a failure here would leave the page unstyled.
//
//   localStorage "autonogrammer.surface"   "code" | "cowork"   (default "code")
//
// The workbench calls window.autonogrammerSurface.read and write. paint sets the attribute without writing storage.
(function () {
  var root = document.documentElement;
  var key = "autonogrammer.surface";
  function read() {
    try {
      return window.localStorage.getItem(key);
    } catch (error) {
      return null;
    }
  }
  function paint(value) {
    root.setAttribute("data-surface", value === "cowork" ? "cowork" : "code");
  }
  function write(value) {
    var next = value === "cowork" ? "cowork" : "code";
    try {
      window.localStorage.setItem(key, next);
    } catch (error) {
      // Storage is unavailable: the attribute still records the choice for this paint.
    }
    paint(next);
  }
  paint(read());
  window.autonogrammerSurface = { read: read, write: write, paint: paint };
})();
