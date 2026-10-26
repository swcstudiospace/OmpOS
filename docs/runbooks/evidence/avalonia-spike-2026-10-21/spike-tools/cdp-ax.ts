// Throwaway spike: open a served page in the running Chrome over CDP, wait for the WebAssembly runtime,
// then record the accessibility tree and a screenshot. Usage: bun cdp-ax.ts <url> <outPrefix> <waitMs>

type JsonObject = Record<string, unknown>;

interface Pending {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
}

interface AxNode {
  nodeId: string;
  role: string;
  name: string;
  ignored: boolean;
}

function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function readString(record: JsonObject, key: string): string | undefined {
  const candidate = record[key];
  return typeof candidate === "string" ? candidate : undefined;
}

function readTarget(value: unknown): { id: string; webSocketDebuggerUrl: string } {
  if (!isJsonObject(value)) {
    throw new Error("target is not an object");
  }
  const id = readString(value, "id");
  const webSocketDebuggerUrl = readString(value, "webSocketDebuggerUrl");
  if (id === undefined || webSocketDebuggerUrl === undefined) {
    throw new Error("target lacks id or webSocketDebuggerUrl");
  }
  return { id, webSocketDebuggerUrl };
}

function readProbe(result: unknown): string {
  if (isJsonObject(result) && isJsonObject(result.result)) {
    const value = readString(result.result, "value");
    if (value !== undefined) {
      return value;
    }
  }
  throw new Error("Runtime.evaluate returned no string value");
}

function readAxNodes(result: unknown): AxNode[] {
  if (!isJsonObject(result) || !Array.isArray(result.nodes)) {
    throw new Error("getFullAXTree returned no nodes array");
  }
  const nodes: AxNode[] = [];
  for (const raw of result.nodes) {
    if (!isJsonObject(raw)) {
      continue;
    }
    const role = isJsonObject(raw.role) ? (readString(raw.role, "value") ?? "") : "";
    const name = isJsonObject(raw.name) ? (readString(raw.name, "value") ?? "") : "";
    nodes.push({ nodeId: readString(raw, "nodeId") ?? "", role, name, ignored: raw.ignored === true });
  }
  return nodes;
}

function readScreenshot(result: unknown): Uint8Array {
  if (isJsonObject(result)) {
    const data = readString(result, "data");
    if (data !== undefined) {
      return Buffer.from(data, "base64");
    }
  }
  throw new Error("captureScreenshot returned no data");
}

const cdpBase = "http://127.0.0.1:9333";
const pageUrl = process.argv[2];
const outPrefix = process.argv[3];
const waitMs = Number(process.argv[4] ?? "20000");
if (pageUrl === undefined || outPrefix === undefined || !Number.isFinite(waitMs)) {
  throw new Error("usage: bun cdp-ax.ts <url> <outPrefix> <waitMs>");
}

const created = await fetch(`${cdpBase}/json/new?${encodeURIComponent(pageUrl)}`, { method: "PUT" });
if (!created.ok) {
  throw new Error(`json/new failed: ${created.status}`);
}
const target = readTarget(await created.json());

const socket = new WebSocket(target.webSocketDebuggerUrl);
const opened = Promise.withResolvers<void>();
socket.onopen = () => opened.resolve();
socket.onerror = () => opened.reject(new Error("socket error"));
await opened.promise;

let nextId = 0;
const pending = new Map<number, Pending>();
socket.onmessage = (event) => {
  const message: unknown = JSON.parse(String(event.data));
  if (!isJsonObject(message) || typeof message.id !== "number") {
    return;
  }
  const waiter = pending.get(message.id);
  if (waiter === undefined) {
    return;
  }
  pending.delete(message.id);
  if (isJsonObject(message.error)) {
    waiter.reject(new Error(JSON.stringify(message.error)));
  } else {
    waiter.resolve(message.result);
  }
};

function send(method: string, params: JsonObject = {}): Promise<unknown> {
  const id = ++nextId;
  const waiter = Promise.withResolvers<unknown>();
  pending.set(id, { resolve: waiter.resolve, reject: waiter.reject });
  socket.send(JSON.stringify({ id, method, params }));
  return waiter.promise;
}

await send("Page.enable");
await send("Runtime.enable");
await send("Accessibility.enable");
// Fixed wait for the WebAssembly runtime to start and render. This is a foreground wait, not a background-job poll.
await Bun.sleep(waitMs);

const probe = readProbe(
  await send("Runtime.evaluate", {
    expression:
      "JSON.stringify({ title: document.title, canvases: document.querySelectorAll('canvas').length, roleNodes: document.querySelectorAll('[role]').length, bodyChildren: document.body.children.length })",
    returnByValue: true,
  }),
);
const axResult = await send("Accessibility.getFullAXTree");
const nodes = readAxNodes(axResult);
const shot = readScreenshot(await send("Page.captureScreenshot", { format: "png" }));

const summary = nodes.filter((node) => !node.ignored && (node.role !== "" || node.name !== ""));
await Bun.write(`${outPrefix}-ax-full.json`, JSON.stringify(axResult, null, 2));
await Bun.write(`${outPrefix}-ax-summary.json`, JSON.stringify(summary, null, 2));
await Bun.write(`${outPrefix}-screen.png`, shot);
console.log(`probe=${probe}`);
console.log(`ax_nodes_total=${nodes.length} ax_nodes_visible_or_named=${summary.length}`);
for (const node of summary) {
  console.log(`${node.role} | ${node.name}`);
}

socket.close();
await fetch(`${cdpBase}/json/close/${target.id}`);
