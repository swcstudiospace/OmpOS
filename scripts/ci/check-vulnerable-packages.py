#!/usr/bin/env python3
"""Fail when a `dotnet list ... package --vulnerable --format json` report lists a vulnerable package.

`dotnet list` exits 0 even when it finds vulnerabilities, so the parse of its JSON output is the gate.
Usage: check-vulnerable-packages.py REPORT.json [REPORT.json ...]
"""

import json
import sys


def vulnerable_packages(report):
    """Return every top-level or transitive package in the report that lists a vulnerability."""
    return [
        package
        for project in report["projects"]
        for framework in project.get("frameworks", [])
        for key in ("topLevelPackages", "transitivePackages")
        for package in framework.get(key, [])
        if package.get("vulnerabilities")
    ]


def main(paths):
    if not paths:
        print("usage: check-vulnerable-packages.py REPORT.json [REPORT.json ...]", file=sys.stderr)
        return 2

    hits = []
    for path in paths:
        with open(path, encoding="utf-8") as report_file:
            hits.extend(vulnerable_packages(json.load(report_file)))

    print(json.dumps(hits, indent=1))
    return 1 if hits else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
