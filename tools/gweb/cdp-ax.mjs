// Records the Chromium accessibility tree of one served page over CDP (Accessibility.getFullAXTree).
// Usage: node cdp-ax.mjs <cdpPort> <pageUrl> <outPrefix> <waitMs>
// Writes <outPrefix>-ax-full.json, <outPrefix>-ax-summary.json and <outPrefix>-screen.png.
// roleNodes = accessibility nodes that are not ignored and carry a non-empty role.

import { writeFile } from "node:fs/promises";

const [cdpPort, pageUrl, outPrefix, waitArg] = process.argv.slice(2);
const waitMs = Number(waitArg);
if (!cdpPort || !pageUrl || !outPrefix || !Number.isFinite(waitMs)) {
  throw new Error("usage: node cdp-ax.mjs <cdpPort> <pageUrl> <outPrefix> <waitMs>");
}

const cdpBase = `http://127.0.0.1:${cdpPort}`;

function roleOf(node) {
  return node.role && typeof node.role.value === "string" ? node.role.value : "";
}

function nameOf(node) {
  return node.name && typeof node.name.value === "string" ? node.name.value : "";
}

async function main() {
  const created = await fetch(`${cdpBase}/json/new?${encodeURIComponent(pageUrl)}`, { method: "PUT" });
  if (!created.ok) {
    throw new Error(`json/new failed: ${created.status}`);
  }
  const target = await created.json();

  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", () => resolve(), { once: true });
    socket.addEventListener("error", () => reject(new Error("CDP socket error")), { once: true });
  });

  let nextId = 0;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(String(event.data));
    if (typeof message.id !== "number") {
      return;
    }
    const waiter = pending.get(message.id);
    if (waiter === undefined) {
      return;
    }
    pending.delete(message.id);
    if (message.error !== undefined) {
      waiter.reject(new Error(`${waiter.method}: ${JSON.stringify(message.error)}`));
    } else {
      waiter.resolve(message.result);
    }
  });

  function send(method, params = {}) {
    const id = ++nextId;
    socket.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => pending.set(id, { method, resolve, reject }));
  }

  await send("Page.enable");
  await send("Runtime.enable");
  await send("Accessibility.enable");
  // Fixed foreground wait for the WebAssembly runtime to start and render the first frame.
  await new Promise((resolve) => setTimeout(resolve, waitMs));

  const probe = await send("Runtime.evaluate", {
    expression: `JSON.stringify({
      title: document.title,
      canvasElements: document.querySelectorAll("canvas").length,
      domElementsWithRoleAttribute: document.querySelectorAll("[role]").length,
      readyState: document.readyState
    })`,
    returnByValue: true,
  });
  const probeValue = probe.result.value;

  const ax = await send("Accessibility.getFullAXTree");
  const shot = await send("Page.captureScreenshot", { format: "png" });

  const nodes = ax.nodes;
  const roleNodes = nodes.filter((node) => !node.ignored && roleOf(node) !== "");
  const summary = nodes.filter((node) => !node.ignored && (roleOf(node) !== "" || nameOf(node) !== ""));
  const textboxNodes = nodes.filter((node) => !node.ignored && roleOf(node) === "textbox");
  const namedTextboxNodes = textboxNodes.filter((node) => nameOf(node) !== "");
  const roleCounts = {};
  for (const node of roleNodes) {
    roleCounts[roleOf(node)] = (roleCounts[roleOf(node)] ?? 0) + 1;
  }

  await writeFile(`${outPrefix}-ax-full.json`, JSON.stringify(ax, null, 2));
  await writeFile(`${outPrefix}-ax-summary.json`, JSON.stringify(summary, null, 2));
  await writeFile(`${outPrefix}-screen.png`, Buffer.from(shot.data, "base64"));

  console.log(`probe=${probeValue}`);
  console.log(`ax_nodes_total=${nodes.length}`);
  console.log(`roleNodes=${roleNodes.length}`);
  console.log(`textboxNodes=${textboxNodes.length}`);
  console.log(`namedTextboxNodes=${namedTextboxNodes.length}`);
  console.log(`roleCounts=${JSON.stringify(roleCounts)}`);
  for (const node of summary) {
    console.log(`node ${node.nodeId}: role=${roleOf(node)} name=${JSON.stringify(nameOf(node))}`);
  }

  socket.close();
  await fetch(`${cdpBase}/json/close/${target.id}`);
}

await main();
