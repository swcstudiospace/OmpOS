#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 SWC Studio
"""Report breaking changes in docs/api/openapi.json between a base ref and HEAD (stdlib only).

Breaking: removed path or method, removed or newly required parameter, removed response code, removed response or
request property, property type change, newly required request property.
Usage: python3 scripts/ci/check-openapi-breaking.py [--base REF] [--head REF|WORKTREE] [--file PATH]
  --base defaults to origin/main; --head defaults to the working tree file.
Exit 1 on breaking changes unless ALLOW_BREAKING_API=<reason> is set or the HEAD commit message contains
"BREAKING-API:" (the text after it is the reason). Exit 2 when a document cannot be read.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys

METHODS = ("get", "put", "post", "delete", "options", "head", "patch", "trace")


def resolve(schema, doc, depth=0):
    """Follow a local $ref (bounded)."""
    while isinstance(schema, dict) and "$ref" in schema and depth < 20:
        node = doc
        for part in schema["$ref"].lstrip("#/").split("/"):
            node = node.get(part.replace("~1", "/").replace("~0", "~"), {}) if isinstance(node, dict) else {}
        schema, depth = node, depth + 1
    return schema if isinstance(schema, dict) else {}


def compare_schema(old, new, old_doc, new_doc, where, request, out, active):
    """Compare two schemas recursively; request=True means required additions are breaking.

    `active` holds the (old, new, request) schema pairs of the current descent, not of the display path, so a schema that refers to itself,
    directly or through others, ends the descent at its second visit instead of recursing without end.
    """
    old, new = resolve(old, old_doc), resolve(new, new_doc)
    if not old:
        return
    key = (id(old), id(new), request)
    if key in active:
        return
    active.add(key)
    try:
        if old.get("type") != new.get("type") and old.get("type") is not None:
            out.append(f"{where}: type changed from {old.get('type')} to {new.get('type')}")
            return
        old_props, new_props = old.get("properties", {}) or {}, new.get("properties", {}) or {}
        for name in sorted(old_props):
            if name not in new_props:
                out.append(f"{where}: property '{name}' removed")
            else:
                compare_schema(old_props[name], new_props[name], old_doc, new_doc, f"{where}.{name}", request, out, active)
        if request:
            for name in sorted(set(new.get("required", []) or []) - set(old.get("required", []) or [])):
                out.append(f"{where}: property '{name}' is newly required")
        if "items" in old and "items" in new:
            compare_schema(old["items"], new["items"], old_doc, new_doc, f"{where}[]", request, out, active)
    finally:
        active.discard(key)


def content_schema(node, doc):
    """First JSON-ish content schema of a requestBody/response object, or None."""
    node = resolve(node, doc)
    for media in sorted((node.get("content") or {})):
        return (node["content"][media] or {}).get("schema")
    return None


def param_map(item, doc):
    """Map (in, name) -> parameter for path-level plus operation-level parameters."""
    return {(p.get("in"), p.get("name")): p for p in (resolve(x, doc) for x in item)}


def diff(old_doc: dict, new_doc: dict) -> list[str]:
    """List of human-readable breaking changes."""
    out: list[str] = []
    active: set = set()
    old_paths, new_paths = old_doc.get("paths", {}), new_doc.get("paths", {})
    for path in sorted(old_paths):
        if path not in new_paths:
            out.append(f"{path}: path removed")
            continue
        for method in METHODS:
            if method not in old_paths[path]:
                continue
            tag = f"{method.upper()} {path}"
            if method not in new_paths[path]:
                out.append(f"{tag}: method removed")
                continue
            old_op, new_op = old_paths[path][method], new_paths[path][method]
            old_params = param_map((old_paths[path].get("parameters") or []) + (old_op.get("parameters") or []), old_doc)
            new_params = param_map((new_paths[path].get("parameters") or []) + (new_op.get("parameters") or []), new_doc)
            for key in sorted(old_params, key=str):
                if key not in new_params:
                    out.append(f"{tag}: parameter '{key[1]}' ({key[0]}) removed")
            for key in sorted(new_params, key=str):
                if new_params[key].get("required") and not (key in old_params and old_params[key].get("required")):
                    out.append(f"{tag}: parameter '{key[1]}' ({key[0]}) is newly required" if key in old_params else f"{tag}: new required parameter '{key[1]}' ({key[0]})")
                elif key in old_params:
                    ot, nt = resolve(old_params[key].get("schema"), old_doc).get("type"), resolve(new_params[key].get("schema"), new_doc).get("type")
                    if ot and ot != nt:
                        out.append(f"{tag}: parameter '{key[1]}' type changed from {ot} to {nt}")
            for code in sorted(old_op.get("responses", {}) or {}):
                if code not in (new_op.get("responses") or {}):
                    out.append(f"{tag}: response {code} removed")
                else:
                    old_schema = content_schema(old_op["responses"][code], old_doc)
                    new_schema = content_schema(new_op["responses"][code], new_doc)
                    if old_schema is not None and new_schema is None:
                        out.append(f"{tag} response {code}: response body removed")
                    elif old_schema is not None:
                        compare_schema(old_schema, new_schema, old_doc, new_doc, f"{tag} response {code}", False, out, active)
            old_body, new_body = content_schema(old_op.get("requestBody") or {}, old_doc), content_schema(new_op.get("requestBody") or {}, new_doc)
            if old_body is not None and new_body is not None:
                compare_schema(old_body, new_body, old_doc, new_doc, f"{tag} request", True, out, active)
            elif new_body is not None and resolve(new_op.get("requestBody"), new_doc).get("required") and old_body is None:
                out.append(f"{tag}: request body is newly required")
    return out


def git_show(ref: str, path: str) -> str:
    """Contents of path at ref."""
    return subprocess.run(["git", "show", f"{ref}:{path}"], check=True, capture_output=True, text=True).stdout


def allowance(env: dict, message: str) -> str | None:
    """Reason that permits breaking changes, from ALLOW_BREAKING_API or a BREAKING-API: line in the commit message."""
    reason = env.get("ALLOW_BREAKING_API", "").strip()
    if reason:
        return reason
    for line in message.splitlines():
        if line.strip().startswith("BREAKING-API:") and line.split(":", 1)[1].strip():
            return line.split(":", 1)[1].strip()
    return None


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--base", default="origin/main")
    ap.add_argument("--head", default="WORKTREE")
    ap.add_argument("--file", default="docs/api/openapi.json")
    args = ap.parse_args(argv)
    try:
        old = json.loads(git_show(args.base, args.file))
        if args.head == "WORKTREE":
            with open(args.file, encoding="utf-8") as handle:
                new = json.load(handle)
        else:
            new = json.loads(git_show(args.head, args.file))
        # A pull-request checkout is a merge commit; the author's message lives on its second parent.
        parents = subprocess.run(["git", "rev-list", "--parents", "-n", "1", "HEAD"], check=True, capture_output=True, text=True).stdout.split()
        tip = "HEAD^2" if len(parents) > 2 else "HEAD"
        message = subprocess.run(["git", "log", "-1", "--format=%B", tip], check=True, capture_output=True, text=True).stdout
    except (OSError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        print(f"error: cannot read the OpenAPI documents: {error}", file=sys.stderr)
        return 2
    changes = diff(old, new)
    if not changes:
        print("openapi: no breaking changes")
        return 0
    print("\n".join(f"breaking: {c}" for c in changes), file=sys.stderr)
    reason = allowance(os.environ, message)
    if reason:
        print(f"openapi: {len(changes)} breaking change(s) allowed: {reason}")
        return 0
    print("openapi: breaking changes; set ALLOW_BREAKING_API=<reason> or add a 'BREAKING-API: <reason>' line to the commit message", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
