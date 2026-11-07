import assert from "node:assert/strict";
import test from "node:test";
import { registerShortcuts } from "../../src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-keys.js";

test("Ctrl or Meta Shift Digit1 and Digit2 call the surface shortcuts", async () => {
  const calls = [];
  const listeners = [];
  globalThis.document = {
    addEventListener(type, listener) {
      listeners.push({ type, listener });
    },
    removeEventListener() {},
  };
  registerShortcuts({
    invokeMethodAsync(name, id) {
      calls.push([name, id]);
      return Promise.resolve();
    },
  });
  const keydown = listeners.find((entry) => entry.type === "keydown").listener;

  keydown(event({ code: "Digit1", key: "!", ctrlKey: true, shiftKey: true }));
  keydown(event({ code: "Digit2", key: "@", ctrlKey: true, shiftKey: true }));
  keydown(event({ code: "Digit1", key: "!", metaKey: true, shiftKey: true }));
  keydown(event({ code: "Digit2", key: "@", metaKey: true, shiftKey: true }));
  keydown(event({ code: "Digit1", key: "1", ctrlKey: true, shiftKey: false }));
  keydown(event({ code: "Digit2", key: "2", altKey: true, ctrlKey: true, shiftKey: true }));
  keydown(event({ code: "Digit1", key: "!", ctrlKey: true, shiftKey: true, isComposing: true }));

  assert.deepEqual(calls, [
    ["OnShortcut", "code"],
    ["OnShortcut", "cowork"],
    ["OnShortcut", "code"],
    ["OnShortcut", "cowork"],
  ]);
});

function event(fields) {
  return {
    code: fields.code,
    key: fields.key,
    ctrlKey: fields.ctrlKey === true,
    metaKey: fields.metaKey === true,
    altKey: fields.altKey === true,
    shiftKey: fields.shiftKey === true,
    isComposing: fields.isComposing === true,
    keyCode: 0,
    preventDefault() {},
  };
}
