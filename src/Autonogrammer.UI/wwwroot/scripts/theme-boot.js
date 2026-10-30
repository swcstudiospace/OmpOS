// Applies the saved theme and motion preference to <html> before the first paint. index.html loads this as a plain blocking script
// from the head (a file, because the CSP script-src 'self' forbids inline script). It must stay dependency-free and must never throw:
// localStorage can be blocked, and a failure here would leave the page unstyled.
//
//   localStorage "autonogrammer.theme"   "system" | "light" | "dark"   (default "light")
//   localStorage "autonogrammer.motion"  "system" | "reduce" | "full"  (default "system": the operating-system setting decides)
//
// The autonogrammer-theme.js module writes the same keys when the user changes the choice.
(function () {
  var root = document.documentElement;
  var theme = "light";
  var motion = "system";
  try {
    var savedTheme = window.localStorage.getItem("autonogrammer.theme");
    if (savedTheme === "system" || savedTheme === "light" || savedTheme === "dark") {
      theme = savedTheme;
    }
    var savedMotion = window.localStorage.getItem("autonogrammer.motion");
    if (savedMotion === "reduce" || savedMotion === "full") {
      motion = savedMotion;
    }
  } catch (error) {
    // Storage is unavailable (private mode or blocked): the defaults apply.
  }
  root.setAttribute("data-theme", theme);
  if (motion === "system") {
    root.removeAttribute("data-motion");
  } else {
    root.setAttribute("data-motion", motion);
  }
})();
