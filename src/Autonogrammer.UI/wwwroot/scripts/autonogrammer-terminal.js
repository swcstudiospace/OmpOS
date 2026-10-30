// xterm.js host for the omp terminal view. The .NET side (TerminalView) owns the byte stream: it numbers, dedupes and orders the chunks
// and calls write() only with chunks it accepted, so a reconnect never prints the same output twice. This module owns the screen: the
// theme comes from the CSS tokens (so Light, Dark and System follow the rest of the app), the face is Space Mono 13/17, and screen-reader
// mode is on so the output is announced. The terminal is read-only (stdin disabled): steering goes through the composer below it.
import { Terminal } from "../lib/xterm/xterm.mjs";
import { FitAddon } from "../lib/xterm/addon-fit.mjs";

const instances = new Map();
let nextToken = 1;

function tokenValue(styles, name) {
  const value = styles.getPropertyValue(name).trim();
  return value.length > 0 ? value : undefined;
}

// Reads the colours, face and size from the same custom properties the stylesheets use. Nothing here is a literal colour.
export function readTheme(styles) {
  const surface = tokenValue(styles, "--autonogrammer-surface-sunken");
  const text = tokenValue(styles, "--autonogrammer-text");
  const muted = tokenValue(styles, "--autonogrammer-text-secondary");
  const accent = tokenValue(styles, "--autonogrammer-accent");
  const focus = tokenValue(styles, "--autonogrammer-focus");
  const error = tokenValue(styles, "--autonogrammer-error");
  const success = tokenValue(styles, "--autonogrammer-success");
  const warning = tokenValue(styles, "--autonogrammer-warning");
  return {
    background: surface,
    foreground: text,
    cursor: accent,
    cursorAccent: surface,
    selectionBackground: tokenValue(styles, "--autonogrammer-selected"),
    selectionForeground: text,
    black: tokenValue(styles, "--autonogrammer-border-strong"),
    red: error,
    green: success,
    yellow: warning,
    blue: accent,
    magenta: accent,
    cyan: focus,
    white: text,
    brightBlack: muted,
    brightRed: error,
    brightGreen: success,
    brightYellow: warning,
    brightBlue: accent,
    brightMagenta: accent,
    brightCyan: focus,
    brightWhite: text,
  };
}

function readFont(styles) {
  const family = tokenValue(styles, "--autonogrammer-font-mono");
  const size = Number.parseFloat(tokenValue(styles, "--autonogrammer-font-size-terminal") ?? "13");
  const lineHeight = Number.parseFloat(tokenValue(styles, "--autonogrammer-line-height-mono") ?? "17");
  return { family, size, ratio: size > 0 ? lineHeight / size : 1 };
}

export function create(element) {
  const styles = getComputedStyle(document.documentElement);
  const font = readFont(styles);
  const terminal = new Terminal({
    screenReaderMode: true,
    disableStdin: true,
    cursorBlink: false,
    cursorInactiveStyle: "outline",
    scrollback: 5000,
    fontFamily: font.family,
    fontSize: font.size,
    lineHeight: font.ratio,
    theme: readTheme(styles),
  });
  const fit = new FitAddon();
  terminal.loadAddon(fit);
  terminal.open(element);
  const refit = () => {
    try {
      fit.fit();
    } catch {
      // The element has no layout yet (hidden or detached); the next resize fits it.
    }
  };
  refit();

  const resizeObserver = new ResizeObserver(() => window.requestAnimationFrame(refit));
  resizeObserver.observe(element);
  const applyTheme = () => {
    terminal.options.theme = readTheme(getComputedStyle(document.documentElement));
  };
  const themeObserver = new MutationObserver(applyTheme);
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  media.addEventListener("change", applyTheme);

  const token = nextToken++;
  instances.set(token, { terminal, resizeObserver, themeObserver, media, applyTheme });
  return token;
}

export function write(token, text) {
  instances.get(token)?.terminal.write(text);
}

export function focus(token) {
  instances.get(token)?.terminal.focus();
}

export function dispose(token) {
  const instance = instances.get(token);
  if (instance === undefined) {
    return;
  }
  instance.resizeObserver.disconnect();
  instance.themeObserver.disconnect();
  instance.media.removeEventListener("change", instance.applyTheme);
  instance.terminal.dispose();
  instances.delete(token);
}
