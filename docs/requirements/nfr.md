# Non-functional requirements T77d9-req

Five categories. Every target carries a number and a checker.

NFR-P1 kind: perf target: health and status responses answer in p95
under 300 ms at 50 requests per second with 0 timeouts in a 60 s window
verified_by: A08.load

NFR-S1 kind: security target: gitleaks reports 0 findings on 100 percent
of diffs and rate-limit partitions cover 100 percent of authenticated
routes verified_by: A10.sast

NFR-A1 kind: a11y target: scripted checks report 0 axe errors across 68
runs in light and dark at 1440, 1024 and 390 px verified_by: A08.a11y

NFR-I1 kind: i18n target: 100 percent of user-facing strings resolve
from the English resource table with 0 hardcoded literals in src
verified_by: A08.lint

NFR-C1 kind: compliance target: account erasure deletes 100 percent of
owner rows across 4 tables and tenant keys within 24 hours and leaves
0 email addresses in stored rows verified_by: A10.compliance
