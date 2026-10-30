// Theme and motion preference for the top-bar ThemeSwitch and the Settings page. The values and keys match theme-boot.js, which
// applies them before the first paint; this module reads and changes them while the app runs.

const THEME_KEY = "autonogrammer.theme";
const MOTION_KEY = "autonogrammer.motion";
const THEMES = ["system", "light", "dark"];
const MOTIONS = ["system", "reduce", "full"];

function read(key, allowed, fallback) {
  try {
    const value = window.localStorage.getItem(key);
    return allowed.includes(value) ? value : fallback;
  } catch {
    return fallback;
  }
}

function write(key, value) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Storage is unavailable: the choice applies to this page view only.
  }
}

export function getTheme() {
  return read(THEME_KEY, THEMES, "light");
}

export function setTheme(theme) {
  if (!THEMES.includes(theme)) {
    throw new RangeError(`unknown theme "${theme}"`);
  }
  write(THEME_KEY, theme);
  document.documentElement.setAttribute("data-theme", theme);
}

export function getMotion() {
  return read(MOTION_KEY, MOTIONS, "system");
}

export function setMotion(motion) {
  if (!MOTIONS.includes(motion)) {
    throw new RangeError(`unknown motion preference "${motion}"`);
  }
  write(MOTION_KEY, motion);
  if (motion === "system") {
    document.documentElement.removeAttribute("data-motion");
  } else {
    document.documentElement.setAttribute("data-motion", motion);
  }
}
