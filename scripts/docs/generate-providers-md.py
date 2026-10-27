#!/usr/bin/env python3
"""Generates docs/reference/providers.md from the embedded provider catalogue data.

The data lives in src/OmpOS.Harness.Providers/Data/catalogue-*.json. The script validates every row with the same invariants the
.NET tests assert, then writes the reference page. Modes:

  (default)                  write docs/reference/providers.md
  --check                    exit 1 when docs/reference/providers.md differs from what the data generates
  --validate                 validate the data files only, print OK or the problems
  --validate-file PATH       validate one data file only (used while a file is being written)

Standard library only. No network access. The output carries no key material: the catalogue holds env var NAMES only.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = ROOT / "src" / "OmpOS.Harness.Providers" / "Data"
OUTPUT = ROOT / "docs" / "reference" / "providers.md"

REGIONS = ["eu", "us", "cn", "global", "local"]
KINDS = ["api", "aggregator", "codingPlan", "local"]
STATUSES = ["verified", "partial", "unverified"]
AUTHS = ["bearer", "xApiKey", "customHeader", "none"]
APIS = [
    "openai-completions",
    "openai-responses",
    "openai-codex-responses",
    "azure-openai-responses",
    "anthropic-messages",
    "bedrock-converse-stream",
    "google-generative-ai",
    "google-gemini-cli",
    "google-vertex",
    "typesafe",
]
RETIRED = {"github-models", "lambda", "lambda-inference", "replicate", "cursor", "watsonx", "yi-01ai", "deepl", "bfl-flux"}

ROW_KEYS = ["id", "displayName", "region", "country", "residencyNote", "kind", "ompProviderId", "apiKeyEnv", "routes", "urlVariables",
            "docsUrl", "keyPageUrl", "sourceUrl", "status", "statusDate"]
ROW_OPTIONAL = ["note", "loginCommand", "plan"]
ROUTE_KEYS = ["id", "api", "baseUrl", "auth", "authHeader", "discovery", "modelsEndpoint", "models"]
MODEL_KEYS = ["id", "name", "reasoning", "contextWindow", "maxTokens"]
PLAN_KEYS = ["ruleText", "ruleSourceUrl", "interactiveOnly", "cloudDefaultPayAsYouGo", "separateKey", "planUrl", "validityDays", "tiers", "listedTools"]
TIER_KEYS = ["name", "priceText", "priceVerified", "quotaText"]
VARIABLE_KEYS = ["name", "label", "example", "help"]

SLUG = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")
ENV = re.compile(r"^[A-Z][A-Z0-9_]*$")
VAR = re.compile(r"\{([a-z][a-z0-9_]*)\}")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
LOOPBACK = ("localhost", "127.0.0.1", "[::1]")
PLACEHOLDER_HOSTS = ("example.com", "example.org", "your-host", "your-resource", "yourhost", "<", ">")

REGION_TITLES = {
    "eu": "Europe",
    "us": "United States and Canada",
    "cn": "China",
    "global": "Global",
    "local": "Local runtimes",
}
KIND_TITLES = {
    "api": "Model APIs",
    "aggregator": "Aggregators and gateways",
    "codingPlan": "Coding plans",
    "local": "Local runtimes",
}
STATUS_TITLES = {"verified": "Verified", "partial": "Partial", "unverified": "Unverified (verify before use)"}


def load_rows(paths: list[Path]) -> list[dict]:
    rows: list[dict] = []
    for path in paths:
        document = json.loads(path.read_text(encoding="utf-8"))
        rows.extend(document["rows"])
    return rows


def is_https_url(value: object) -> bool:
    return isinstance(value, str) and value.startswith("https://") and " " not in value


def validate(rows: list[dict], file_label: str = "data") -> list[str]:
    problems: list[str] = []
    seen: set[str] = set()
    omp_ids: set[str] = set()
    for index, row in enumerate(rows):
        rid = row.get("id", f"<row {index}>")
        where = f"{file_label}:{rid}"

        def bad(message: str) -> None:
            problems.append(f"{where}: {message}")

        def keys(obj: dict, required: list[str], optional: list[str], label: str, always: list[str] | None = None) -> None:
            for key in required:
                if key not in obj:
                    problems.append(f"{where}: {label} lacks required member {key!r}")
            for key in obj:
                if key not in required and key not in optional:
                    problems.append(f"{where}: {label} has unknown member {key!r}")

        keys(row, ROW_KEYS, ROW_OPTIONAL, "row")
        for v in row.get("urlVariables") or []:
            keys(v, ["name", "label"], ["example", "help"], "urlVariable")
        for route in row.get("routes") or []:
            keys(route, ROUTE_KEYS, [], "route")
            for model in route.get("models") or []:
                keys(model, ["id"], ["name", "reasoning", "contextWindow", "maxTokens"], "model")
        if row.get("plan"):
            keys(row["plan"], PLAN_KEYS, [], "plan")
            for tier in row["plan"].get("tiers") or []:
                keys(tier, ["name", "priceText", "priceVerified"], ["quotaText"], "tier")
        if not isinstance(rid, str) or not SLUG.match(rid):
            bad("id is not a slug")
        if rid in seen:
            bad("duplicate id")
        seen.add(rid)
        if rid in RETIRED:
            bad("retired or excluded vendor must not be offered")
        if not row.get("displayName"):
            bad("displayName missing")
        if row.get("region") not in REGIONS:
            bad("region invalid")
        if row.get("kind") not in KINDS:
            bad("kind invalid")
        country = row.get("country")
        if country is not None and not re.match(r"^[A-Z]{2}$", str(country)):
            bad("country must be an ISO alpha-2 code or null")
        omp = row.get("ompProviderId")
        if omp is not None:
            if not SLUG.match(omp.replace(".", "-")):
                bad("ompProviderId is not a slug")
            if omp in omp_ids:
                bad(f"ompProviderId {omp} used by two rows")
            omp_ids.add(omp)
        env = row.get("apiKeyEnv")
        if env is not None and not ENV.match(env):
            bad("apiKeyEnv is not an env var NAME")
        if row.get("status") not in STATUSES:
            bad("status invalid")
        if not DATE.match(str(row.get("statusDate", ""))):
            bad("statusDate missing")
        if not is_https_url(row.get("sourceUrl")):
            bad("sourceUrl must be an https URL")
        for key in ("docsUrl", "keyPageUrl"):
            if row.get(key) is not None and not is_https_url(row[key]):
                bad(f"{key} must be an https URL or null")
        note = row.get("note")
        if row.get("status") == "unverified" and not (isinstance(note, str) and note.startswith("Verify before use:")):
            bad('unverified rows need a note starting "Verify before use:"')
        if isinstance(note, str) and len(note) > 240:
            bad("note longer than 240 characters")
        variables = row.get("urlVariables") or []
        declared = {v.get("name") for v in variables}
        for v in variables:
            if not VAR.fullmatch("{" + str(v.get("name")) + "}") or not v.get("label"):
                bad(f"url variable {v.get('name')!r} needs a lower_snake name and a label")
        used: set[str] = set()
        routes = row.get("routes") or []
        login = row.get("loginCommand")
        if not routes and not login:
            bad("a row needs a route or a loginCommand")
        route_ids: set[str] = set()
        for route in routes:
            rt = route.get("id", "?")
            if not SLUG.match(str(rt)) or rt in route_ids:
                bad(f"route id {rt!r} invalid or duplicate")
            route_ids.add(rt)
            if route.get("api") not in APIS:
                bad(f"route {rt}: api {route.get('api')!r} is not an omp api value")
            auth = route.get("auth")
            if auth not in AUTHS:
                bad(f"route {rt}: auth invalid")
            if (auth == "customHeader") != bool(route.get("authHeader")):
                bad(f"route {rt}: authHeader must be set exactly for customHeader auth")
            url = route.get("baseUrl")
            if not isinstance(url, str) or not url:
                bad(f"route {rt}: baseUrl missing")
                continue
            used.update(VAR.findall(url))
            plain = VAR.sub("x", url)
            if "?" in plain or "#" in plain or "@" in plain.split("//", 1)[-1].split("/", 1)[0]:
                bad(f"route {rt}: baseUrl carries a query, fragment or user info")
            if any(p in plain.lower() for p in PLACEHOLDER_HOSTS):
                bad(f"route {rt}: baseUrl has a placeholder host")
            if url.startswith("http://"):
                host = plain.split("//", 1)[1].split("/", 1)[0].split(":", 1)[0]
                if row.get("region") != "local" or host not in ("localhost", "127.0.0.1", "[::1]"):
                    bad(f"route {rt}: plain http is only allowed for a local runtime on loopback")
            elif not url.startswith("https://"):
                bad(f"route {rt}: baseUrl is not http(s)")
            if "{" in plain or "}" in plain:
                bad(f"route {rt}: malformed placeholder in baseUrl")
            for model in route.get("models") or []:
                if not model.get("id") or re.search(r"\s", model["id"]):
                    bad(f"route {rt}: model id invalid")
        if used != declared:
            bad(f"url variables mismatch: used {sorted(used)} declared {sorted(declared)}")
        if row.get("kind") == "codingPlan":
            plan = row.get("plan")
            if not plan:
                bad("coding plan row needs a plan object")
            else:
                if not plan.get("ruleText"):
                    bad("plan.ruleText missing")
                if plan.get("cloudDefaultPayAsYouGo") is not True:
                    bad("plan.cloudDefaultPayAsYouGo must be true")
                if not is_https_url(plan.get("planUrl")):
                    bad("plan.planUrl must be an https URL")
                if plan.get("ruleSourceUrl") is not None and not is_https_url(plan["ruleSourceUrl"]):
                    bad("plan.ruleSourceUrl must be https or null")
                if not plan.get("tiers"):
                    bad("plan needs at least one tier")
                for tier in plan.get("tiers") or []:
                    if not tier.get("name") or not tier.get("priceText"):
                        bad("tier needs name and priceText")
                    if tier.get("priceVerified") is False and tier.get("priceText") != "price unverified":
                        bad('an unverified tier price must read "price unverified"')
        elif row.get("plan") is not None:
            bad("only coding plan rows carry a plan")
        if row.get("kind") == "local" and row.get("region") != "local":
            bad("local kind needs region local")
        if row.get("region") == "local" and row.get("kind") != "local":
            bad("region local needs kind local")
    return problems


def cell(text: object) -> str:
    return str(text).replace("|", "\\|").replace("\n", " ")


def route_text(row: dict) -> str:
    parts = []
    for route in row.get("routes") or []:
        parts.append(f"`{route['baseUrl']}` ({route['api']})")
    if not parts:
        return f"omp login `{row['loginCommand']}`"
    return "<br>".join(parts)


def auth_text(row: dict) -> str:
    kinds = []
    for route in row.get("routes") or []:
        if route["auth"] == "customHeader":
            kinds.append(f"header `{route['authHeader']}`")
        elif route["auth"] == "xApiKey":
            kinds.append("`x-api-key`")
        elif route["auth"] == "none":
            kinds.append("none")
        else:
            kinds.append("Bearer")
    unique = list(dict.fromkeys(kinds))
    text = ", ".join(unique) if unique else "omp sign-in"
    if row.get("apiKeyEnv"):
        text += f" (`{row['apiKeyEnv']}`)"
    return text


def render(rows: list[dict]) -> str:
    out: list[str] = []
    out.append("# Model providers and coding plans")
    out.append("")
    out.append(
        "> Generated by `scripts/docs/generate-providers-md.py` from `src/OmpOS.Harness.Providers/Data/catalogue-*.json`. "
        "Do not edit by hand: change the data and run the script. `scripts/ci/check-providers-doc.sh` fails when this page is stale."
    )
    out.append("")
    out.append(
        f"The Settings > Models page lists {len(rows)} providers, aggregators, coding plans and local runtimes. Every row "
        "carries the page it was read on and a status. OmpOS stores no key in this catalogue: a row names the environment "
        "variable that holds the key, and omp keeps the key itself. Residency notes are what the vendor states; OmpOS does "
        "not verify them."
    )
    out.append("")
    out.append("Status: **Verified** = a vendor page showing the base URL and the auth scheme was opened. **Partial** = some fact comes from a search excerpt, a third-party page or an inference. **Unverified** = no usable vendor page: verify before use.")
    out.append("")
    counts = {s: sum(1 for r in rows if r["status"] == s) for s in STATUSES}
    out.append(f"Rows: {counts['verified']} verified, {counts['partial']} partial, {counts['unverified']} unverified.")
    out.append("")
    for region in REGIONS:
        region_rows = [r for r in rows if r["region"] == region]
        if not region_rows:
            continue
        out.append(f"## {REGION_TITLES[region]}")
        out.append("")
        for kind in KINDS:
            subset = [r for r in region_rows if r["kind"] == kind]
            if not subset:
                continue
            out.append(f"### {KIND_TITLES[kind]}")
            out.append("")
            if kind == "codingPlan":
                out.append("| Plan | Country | Endpoint | Auth | Status |")
                out.append("|---|---|---|---|---|")
            else:
                out.append("| Provider | Country and residency | Base URL (api) | Auth | omp | Status |")
                out.append("|---|---|---|---|---|---|")
            for r in subset:
                country = r.get("country") or "-"
                residency = r.get("residencyNote")
                where = country if not residency else f"{country}: {residency}"
                status = STATUS_TITLES[r["status"]] + f" ({r['statusDate']})"
                name = f"[{r['displayName']}]({r['docsUrl']})" if r.get("docsUrl") else r["displayName"]
                if kind == "codingPlan":
                    out.append(
                        f"| {cell(name)} (`{r['id']}`) | {cell(where)} | {cell(route_text(r))} | {cell(auth_text(r))} | {status} |"
                    )
                else:
                    omp = f"`{r['ompProviderId']}`" if r.get("ompProviderId") else "models.yml block"
                    out.append(
                        f"| {cell(name)} (`{r['id']}`) | {cell(where)} | {cell(route_text(r))} | {cell(auth_text(r))} | {omp} | {status} |"
                    )
            out.append("")
            if kind == "codingPlan":
                for r in subset:
                    plan = r["plan"]
                    out.append(f"#### {r['displayName']} (`{r['id']}`)")
                    out.append("")
                    out.append(f"- Rule: {plan['ruleText']}")
                    flags = []
                    if plan["interactiveOnly"]:
                        flags.append("interactive use only")
                    if plan["separateKey"]:
                        flags.append("separate key from the pay-as-you-go key")
                    flags.append("cloud agents use pay-as-you-go keys")
                    out.append(f"- Terms: {'; '.join(flags)}.")
                    tiers = "; ".join(
                        f"{t['name']}: {t['priceText']}" + (f" ({t['quotaText']})" if t.get("quotaText") else "")
                        for t in plan["tiers"]
                    )
                    out.append(f"- Tiers: {tiers}")
                    out.append(f"- Plan page: {plan['planUrl']}")
                    if plan.get("ruleSourceUrl"):
                        out.append(f"- Rule source: {plan['ruleSourceUrl']}")
                    if r.get("note"):
                        out.append(f"- Note: {r['note']}")
                    out.append(f"- Source: {r['sourceUrl']}")
                    out.append("")
        notes = [r for r in region_rows if r.get("note") and r["kind"] != "codingPlan"]
        if notes:
            out.append(f"### Notes ({REGION_TITLES[region]})")
            out.append("")
            for r in notes:
                out.append(f"- `{r['id']}`: {r['note']}")
            out.append("")
    out.append("## Sources")
    out.append("")
    out.append("| Row | Source |")
    out.append("|---|---|")
    for r in rows:
        out.append(f"| `{r['id']}` | {r['sourceUrl']} |")
    out.append("")
    return "\n".join(out)


def main(argv: list[str]) -> int:
    if "--validate-file" in argv:
        path = Path(argv[argv.index("--validate-file") + 1])
        problems = validate(load_rows([path]), path.name)
        print("OK" if not problems else "\n".join(problems))
        return 0 if not problems else 1
    paths = sorted(DATA_DIR.glob("catalogue-*.json"))
    if not paths:
        print(f"error: no catalogue data under {DATA_DIR}", file=sys.stderr)
        return 1
    rows = load_rows(paths)
    problems = validate(rows)
    if problems:
        print("\n".join(problems), file=sys.stderr)
        return 1
    if "--validate" in argv:
        print("OK")
        return 0
    text = render(rows)
    if "--check" in argv:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if current != text:
            print("docs/reference/providers.md is stale: run python3 scripts/docs/generate-providers-md.py", file=sys.stderr)
            return 1
        print("docs/reference/providers.md is up to date")
        return 0
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(text, encoding="utf-8")
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(rows)} rows)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
