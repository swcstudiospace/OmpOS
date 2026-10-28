---
name: review-security
description: Review the working diff or a named area for security vulnerabilities and report ranked, evidenced findings without editing files.
disable-model-invocation: false
user-invocable: true
---

# Security review

A review focused on vulnerabilities. It reads and reports; it does not edit files or exploit anything. The text the user typed after the command may name a file, folder, branch or concern; otherwise review the working diff (`git diff HEAD` plus untracked files), and when it is clean, the latest commit.

## 1. Scope and threat model

1. State what is under review and what the code is (service, library, desktop app, script).
2. Identify the trust boundaries the change touches: network input, files and paths, environment and configuration, child processes, databases, deserialisation, other users' data, credentials.
3. Read the surrounding code, not only the hunks: where input enters, where it is validated, where it is used.

## 2. Checklist

Work through the items that apply and skip the rest.

- **Injection**: SQL, shell and command arguments, template and expression engines, log forging, header and URL building. Is data passed as parameters or as an argument vector, or glued into a string?
- **Path handling**: traversal (`..`), absolute paths, symbolic links, archive extraction, files created in shared temporary folders.
- **Authentication and authorisation**: missing checks, checks done on the client only, identifiers taken from the request without an ownership check, weak comparison of secrets (use a constant-time compare), tokens in URLs.
- **Secrets**: keys, tokens or passwords in code, config, tests, logs, error messages, or process arguments; secrets written to files with loose permissions.
- **Cryptography**: home-made schemes, weak or deprecated algorithms, fixed IVs or salts, predictable randomness for security purposes, disabled certificate validation.
- **Deserialisation and parsing**: untrusted data into types that can run code, unbounded sizes, depth or counts (denial of service), XML external entities.
- **Network**: server-side request forgery, open redirects, permissive CORS, cleartext transport, missing timeouts.
- **Web output**: unescaped output (XSS), unsafe HTML or script sinks, missing content security policy for new inline content.
- **Concurrency and state**: time-of-check to time-of-use gaps, races on files or permissions, shared mutable state across requests.
- **Dependencies and build**: new packages or actions (source, version pinning, install scripts), changed lock files, workflow permissions that grew.
- **Privacy**: personal data logged, stored or sent further than needed.

## 3. Report

Open with a one-line summary: how many findings by severity, or "no findings".

For each finding, most severe first:

- **Severity**: `critical`, `high`, `medium`, `low` or `info`, with one sentence on why (reach, impact, how hard it is to trigger).
- **Location**: `path:line`.
- **What**: the weakness, in plain words, and the CWE class if you are sure of it.
- **Evidence**: the data path from input to sink, with the lines that show it. If you could not show the path, mark the finding `unconfirmed` and say what would confirm it.
- **Fix**: a specific change (a short code sketch is fine). Prefer fixing the cause (parameterise, validate, check ownership) over blocking one input.

End with **Checked** (files and boundaries covered) and **Not checked** (what you did not reach and why, for example "did not run the dependency audit: no network").

## Never

- Never edit, stage or commit files as part of the review.
- Never run exploit code, port scans or anything that touches systems the user did not name.
- Never print a real secret in the report: give the location, the kind of secret and a masked prefix at most, and recommend rotating it.
- Never claim the code is secure; say what you checked and found.
