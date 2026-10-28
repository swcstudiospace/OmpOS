// Keyboard helpers the .NET components cannot do alone. Blazor decides on a key only after a round trip, so it cannot cancel the
// browser's default action (a newline in the composer, a page scroll on an arrow key); these guards cancel it in the browser. All
// the behaviour itself stays in the C# components: a guard never sends, selects or closes anything.
//
//   guardComposer(textarea)        Enter (without Shift alone) never inserts a newline; while data-slash-open="true" the palette keys
//                                  (ArrowUp, ArrowDown, Tab) never move the caret or the focus; a key pressed during IME composition
//                                  is hidden from Blazor, so a composition never sends a message.
//   guardList(element)             ArrowUp, ArrowDown, Home and End never scroll the page while the focus is in the list.
//   registerShortcuts(dotnet)      Ctrl or Command with Shift+A, Shift+K and the comma call dotnet.OnShortcut("agents"|"kanban"|"settings").
//   watchOutside(container, dotnet)  Calls dotnet.OnOutside() when a pointer goes down, or the focus lands, outside the container (popups).
//   ensureVisible(id)              Scrolls the element with that id into view inside its scroll container, without animation.
//   resetScroll()                  Scrolls the document to the top (a view switch starts at the top).
//   focusId(id)                    Moves the focus to the element with that id (the thread after an agent was opened).
//   focusItem(container, id)       Moves the focus to the element of the container whose data-row-id is id (roving tabindex lists).
//   release(token)                 Removes the listeners of any registration above.

const guards = new Map();
let nextToken = 1;

function composing(event) {
  return event.isComposing === true || event.keyCode === 229;
}

function modifierFree(event) {
  return !event.ctrlKey && !event.metaKey && !event.altKey && !event.shiftKey;
}

function track(target, type, listener) {
  return trackAll([{ target, type, listener }]);
}

function trackAll(entries) {
  for (const entry of entries) {
    entry.target.addEventListener(entry.type, entry.listener);
  }
  const token = nextToken++;
  guards.set(token, entries);
  return token;
}

export function guardComposer(textarea) {
  return track(textarea, "keydown", (event) => {
    if (composing(event)) {
      event.stopPropagation();
      return;
    }
    if (event.key === "Enter") {
      // Shift+Enter alone is the newline; Enter, Ctrl+Enter and Ctrl+Shift+Enter are commands.
      if (!(event.shiftKey && !event.ctrlKey && !event.metaKey)) {
        event.preventDefault();
      }
      return;
    }
    if (textarea.dataset.slashOpen === "true" && modifierFree(event) && (event.key === "ArrowUp" || event.key === "ArrowDown" || event.key === "Tab")) {
      event.preventDefault();
    }
  });
}

export function guardList(element) {
  return track(element, "keydown", (event) => {
    if (modifierFree(event) && (event.key === "ArrowUp" || event.key === "ArrowDown" || event.key === "Home" || event.key === "End")) {
      event.preventDefault();
    }
  });
}

export function registerShortcuts(dotnet) {
  return track(document, "keydown", (event) => {
    if (composing(event) || !(event.ctrlKey || event.metaKey) || event.altKey) {
      return;
    }
    const key = event.key.toLowerCase();
    let id = null;
    if (event.shiftKey && key === "a") {
      id = "agents";
    } else if (event.shiftKey && key === "k") {
      id = "kanban";
    } else if (!event.shiftKey && key === ",") {
      id = "settings";
    }
    if (id !== null) {
      event.preventDefault();
      dotnet.invokeMethodAsync("OnShortcut", id).catch(ignoreDisposed);
    }
  });
}

// The component can be disposed while an event is in flight, and the callback then finds no object to call. There is nobody left to tell, so that
// one rejection is dropped; any other failure of the handler still surfaces as an unhandled rejection.
function ignoreDisposed(error) {
  if (!String(error && error.message).includes("no tracked object")) {
    throw error;
  }
}

export function watchOutside(container, dotnet) {
  const leave = (event) => {
    if (!container.contains(event.target)) {
      dotnet.invokeMethodAsync("OnOutside").catch(ignoreDisposed);
    }
  };
  return trackAll([
    { target: document, type: "pointerdown", listener: leave },
    { target: document, type: "focusin", listener: leave },
  ]);
}

export function ensureVisible(id) {
  const element = document.getElementById(id);
  if (element !== null) {
    element.scrollIntoView({ block: "nearest", inline: "nearest" });
  }
}

export function resetScroll() {
  window.scrollTo(0, 0);
}

export function focusId(id) {
  const element = document.getElementById(id);
  if (element !== null) {
    element.focus();
  }
}

export function focusItem(container, id) {
  const items = container.querySelectorAll("[data-row-id]");
  for (const item of items) {
    if (item.getAttribute("data-row-id") === id) {
      item.focus();
      return;
    }
  }
}

export function release(token) {
  const entries = guards.get(token);
  if (entries === undefined) {
    return;
  }
  for (const entry of entries) {
    entry.target.removeEventListener(entry.type, entry.listener);
  }
  guards.delete(token);
}
