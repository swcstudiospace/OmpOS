// Focus support for SessionView's "Show earlier messages". After earlier messages are added, focus moves to the first of them, so a keyboard or
// screen reader user continues reading where the new content starts instead of being left on a control that moved.

export function focusFirstBlock(container) {
  const first = container?.firstElementChild;
  if (first === null || first === undefined) {
    return false;
  }

  if (!first.hasAttribute("tabindex")) {
    first.setAttribute("tabindex", "-1");
  }

  first.focus();
  return true;
}
