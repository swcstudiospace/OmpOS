// Auto-follow and scroll-pause support for SessionView. The document viewport is the scroll
// container: the shell does not bound the session region, so the page itself scrolls.

const BOTTOM_TOLERANCE_PX = 24;
const observers = new Map();
let nextToken = 1;

function viewport() {
  return document.scrollingElement ?? document.documentElement;
}

function isScrolledUp() {
  const v = viewport();
  return v.scrollHeight - v.scrollTop - v.clientHeight > BOTTOM_TOLERANCE_PX;
}

export function scrollToEnd() {
  const v = viewport();
  v.scrollTop = v.scrollHeight;
}

// Reports only transitions across the bottom edge, so ordinary scroll events do not cross the interop boundary.
export function observe(callback) {
  let scrolledUp = isScrolledUp();
  const onScroll = () => {
    const up = isScrolledUp();
    if (up !== scrolledUp) {
      scrolledUp = up;
      callback.invokeMethodAsync("ReportScrollPosition", up);
    }
  };
  const token = nextToken++;
  window.addEventListener("scroll", onScroll, { passive: true });
  observers.set(token, onScroll);
  return token;
}

export function unobserve(token) {
  const onScroll = observers.get(token);
  if (onScroll === undefined) {
    return;
  }

  window.removeEventListener("scroll", onScroll);
  observers.delete(token);
}
