// Modal dialog support for the Settings dialog: Tab and Shift+Tab cycle inside the dialog, the page behind it does not scroll while it is
// open, and closing it returns the focus to the control that had it when the dialog opened. Escape and the unsaved-changes prompt are handled
// on the .NET side (SettingsDialog).

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
const traps = new Map();
let nextToken = 1;
let openDialogs = 0;

function focusableWithin(container) {
  return Array.from(container.querySelectorAll(FOCUSABLE)).filter(
    (node) => node.getClientRects().length > 0 && getComputedStyle(node).visibility !== "hidden",
  );
}

export function trapFocus(container) {
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
  const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  document.addEventListener("keydown", onKeyDown, true);
  openDialogs += 1;
  document.documentElement.setAttribute("data-dialog-open", "true");
  const token = nextToken++;
  traps.set(token, { onKeyDown, opener });
  return token;
}

export function releaseFocus(token) {
  const trap = traps.get(token);
  if (trap === undefined) {
    return;
  }
  document.removeEventListener("keydown", trap.onKeyDown, true);
  traps.delete(token);
  openDialogs -= 1;
  if (openDialogs <= 0) {
    openDialogs = 0;
    document.documentElement.removeAttribute("data-dialog-open");
  }
  if (trap.opener !== null && trap.opener.isConnected) {
    trap.opener.focus();
  }
}
