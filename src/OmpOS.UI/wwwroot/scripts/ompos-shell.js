// Focus trap for the off-canvas agent drawer. While the drawer is open below 840 px, Tab and Shift+Tab cycle inside it, the first
// control takes focus, and widening the window past the breakpoint hands the layout back to the desktop rail. Escape and the
// scrim close the drawer on the .NET side (AppShell).

const DRAWER_QUERY = "(max-width: 839.98px)";
const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
const traps = new Map();
let nextToken = 1;

function focusableWithin(container) {
  return Array.from(container.querySelectorAll(FOCUSABLE)).filter(
    (node) => node.getClientRects().length > 0 && getComputedStyle(node).visibility !== "hidden",
  );
}

export function trapFocus(container, dotnet) {
  const onKeyDown = (event) => {
    if (event.key !== "Tab") {
      return;
    }
    const items = focusableWithin(container);
    if (items.length === 0) {
      event.preventDefault();
      return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;
    const inside = container.contains(active);
    if (event.shiftKey && (active === first || !inside)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !inside)) {
      event.preventDefault();
      first.focus();
    }
  };
  const media = window.matchMedia(DRAWER_QUERY);
  const onMediaChange = (event) => {
    if (!event.matches) {
      dotnet.invokeMethodAsync("CloseFromViewport");
    }
  };
  document.addEventListener("keydown", onKeyDown, true);
  media.addEventListener("change", onMediaChange);

  const token = nextToken++;
  traps.set(token, { onKeyDown, media, onMediaChange });
  const items = focusableWithin(container);
  (items[0] ?? container).focus();
  return token;
}

export function releaseFocus(token) {
  const trap = traps.get(token);
  if (trap === undefined) {
    return;
  }
  document.removeEventListener("keydown", trap.onKeyDown, true);
  trap.media.removeEventListener("change", trap.onMediaChange);
  traps.delete(token);
}
