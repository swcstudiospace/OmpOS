#!/usr/bin/env python3
"""Drives `omp acp` over stdio with NO model prompt, to learn how multi-root (additional directories) behaves.

Safety: every omp child runs inside `unshare -rn` (empty network namespace), HOME and PI_CODING_AGENT_DIR point at a
sandbox, and the only credential is a fake key. A prompt that were forwarded to a model could not leave the machine.
Only slash commands are sent as prompts.

Usage: probe.py <scenario> ; scenarios: flag, slash, overlay, edge
"""
import json, os, queue, subprocess, sys, threading, time

SANDBOX = "/tmp/multirepo-home"
FIX = "/tmp/multirepo-fixture"
PRIMARY, DIR_B, DIR_C = f"{FIX}/primary", f"{FIX}/b", f"{FIX}/c"
ENV = dict(os.environ, HOME=SANDBOX, PI_CODING_AGENT_DIR=f"{SANDBOX}/.omp/agent", ANTHROPIC_API_KEY="sk-fake-not-a-key")


class Acp:
    def __init__(self, args, cwd):
        cmd = ["unshare", "-rn", "omp", "acp", *args]
        self.proc = subprocess.Popen(cmd, cwd=cwd, env=ENV, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                     stderr=subprocess.PIPE, text=True, bufsize=1)
        self.frames, self.next_id, self.q = [], 1, queue.Queue()
        threading.Thread(target=self._pump, daemon=True).start()
        self.stderr = []
        threading.Thread(target=lambda: self.stderr.extend(self.proc.stderr), daemon=True).start()

    def _pump(self):
        for line in self.proc.stdout:
            try:
                self.q.put(json.loads(line))
            except ValueError:
                self.q.put({"nonJson": line.rstrip()})

    def request(self, method, params, timeout=25):
        rid = self.next_id
        self.next_id += 1
        self.proc.stdin.write(json.dumps({"jsonrpc": "2.0", "id": rid, "method": method, "params": params}) + "\n")
        self.proc.stdin.flush()
        out, deadline = [], time.time() + timeout
        while time.time() < deadline:
            try:
                frame = self.q.get(timeout=0.5)
            except queue.Empty:
                continue
            out.append(frame)
            if frame.get("id") == rid and ("result" in frame or "error" in frame):
                # drain trailing notifications
                time.sleep(0.5)
                while not self.q.empty():
                    out.append(self.q.get())
                return out
        out.append({"probeTimeout": method})
        return out

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(timeout=5)
        except Exception:
            self.proc.kill()


def trim(result):
    """Keeps the answer readable: config option menus are reduced to id and current value."""
    if isinstance(result, dict) and "configOptions" in result:
        result = dict(result, configOptions=[{"id": o.get("id"), "currentValue": o.get("currentValue")} for o in result["configOptions"]])
    return result


def brief(frames):
    rows = []
    for f in frames:
        if "method" in f and f["method"] == "session/update":
            u = f["params"]["update"]
            kind = u.get("sessionUpdate")
            if kind == "available_commands_update":
                names = [c["name"] for c in u.get("availableCommands", [])]
                rows.append({"update": kind, "commands": names})
            else:
                rows.append({"update": kind, "content": u.get("content")})
        elif "result" in f or "error" in f:
            rows.append({"id": f.get("id"), "result": trim(f.get("result")), "error": f.get("error")})
        else:
            rows.append(f)
    return rows


def session(args, prompts, new_params_extra=None):
    a = Acp(args, PRIMARY)
    log = {"args": args, "steps": []}
    log["steps"].append({"initialize": brief(a.request("initialize", {"protocolVersion": 1, "clientCapabilities": {}}))})
    new = a.request("session/new", {"cwd": PRIMARY, "mcpServers": [], **(new_params_extra or {})})
    log["steps"].append({"session/new": brief(new)})
    sid = next((f["result"]["sessionId"] for f in new if "result" in f and "sessionId" in f.get("result", {})), None)
    for text in prompts:
        frames = a.request("session/prompt", {"sessionId": sid, "prompt": [{"type": "text", "text": text}]})
        log["steps"].append({f"prompt {text!r}": brief(frames)})
    a.close()
    log["stderrTail"] = a.stderr[-15:]
    return log


def session_headers():
    base = f"{SANDBOX}/.omp/agent/sessions"
    found = {}
    for root, _dirs, files in os.walk(base):
        for name in files:
            if name.endswith(".jsonl"):
                with open(os.path.join(root, name)) as fh:
                    lines = [fh.readline() for _ in range(3)]
                for line in lines:
                    try:
                        entry = json.loads(line)
                    except ValueError:
                        continue
                    if entry.get("type") == "session":
                        found[os.path.relpath(os.path.join(root, name), base)] = entry
    return found


if __name__ == "__main__":
    scenario = sys.argv[1]
    if scenario == "flag":
        result = session(["--add-dir", DIR_B, "--add-dir", DIR_C], ["/dirs"])
    elif scenario == "slash":
        result = session([], ["/dirs", f"/add-dir {DIR_B}", "/dirs", f"/remove-dir {DIR_B}", "/dirs"])
    elif scenario == "overlay":
        overlay = f"{SANDBOX}/overlay.yml"
        with open(overlay, "w") as fh:
            fh.write(f"workspace:\n  additionalDirectories:\n    - {DIR_B}\n    - {DIR_C}\n")
        result = session(["--config", overlay], ["/dirs"])
    elif scenario == "edge":
        os.makedirs(f"{DIR_B}/nested", exist_ok=True)
        result = session(["--add-dir", "../b", "--add-dir", f"{FIX}/does-not-exist", "--add-dir", DIR_B],
                         ["/dirs", f"/add-dir {DIR_B}/nested", f"/add-dir {FIX}/also-missing", "/add-dir ../c", "/dirs"])
    else:
        sys.exit("unknown scenario")
    result["sessionHeaders"] = session_headers()
    print(json.dumps(result, indent=2))
