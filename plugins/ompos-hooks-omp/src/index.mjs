import { createHookBridge } from "./bridge.mjs";

/**
 * omp extension entry. Registers handlers only; touches neither fs nor network at load time.
 * @param {{ on: (event: string, handler: (event: any, ctx: any) => any) => void, getAllTools?: () => any[] }} pi
 */
export default function ompHooksExtension(pi) {
  const bridge = createHookBridge({ getAllTools: () => (typeof pi.getAllTools === "function" ? pi.getAllTools() : []) });
  for (const name of bridge.eventNames) {
    pi.on(name, (event, ctx) => bridge.handle(name, event, ctx));
  }
}

export { createHookBridge };
