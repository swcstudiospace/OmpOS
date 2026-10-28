// Keyboard support for the Kanban board. The arrow keys, Home and End move between cards on the .NET side (KanbanView); this module only
// stops the browser from also scrolling the page when one of those keys is pressed on a card control. Enter and Space keep their default
// action, so a focused card still opens its session.

const NAVIGATION_KEYS = new Set(["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End"]);
const attached = new Map();
let nextToken = 1;

export function attach(root) {
  const onKeyDown = (event) => {
    if (!NAVIGATION_KEYS.has(event.key) || event.altKey || event.ctrlKey || event.metaKey) {
      return;
    }
    if (event.target instanceof Element && event.target.closest("[data-kanban-nav]") !== null) {
      event.preventDefault();
    }
  };
  root.addEventListener("keydown", onKeyDown);
  const token = nextToken++;
  attached.set(token, { root, onKeyDown });
  return token;
}

export function detach(token) {
  const entry = attached.get(token);
  if (entry === undefined) {
    return;
  }
  entry.root.removeEventListener("keydown", entry.onKeyDown);
  attached.delete(token);
}
