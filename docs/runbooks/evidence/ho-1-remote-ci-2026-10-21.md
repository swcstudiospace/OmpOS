# HO-1: remote CI for the pushed heads (2026-10-21)

Remote: `origin` = `https://github.com/swcstudiospace/OmpOS.git`. Branch: `feat/v0.9.2-hardening`.

Checks run on 2026-10-21:

- `git push origin feat/v0.9.2-hardening` fast-forwarded `6dc3387..dcd0599` (commits `b90fcff` and `dcd0599`).
- `git push --dry-run` at `6dc3387` returned `Everything up-to-date`, so the authenticated push path works.
- `gh run list` and `gh run view` on the repository returned the runs below.

| Run | Head | Conclusion | Jobs |
|-----|------|------------|------|
| 39675418242 | `dcd0599` | success | build-test success; shells success |
| 39668253843 | `6dc3387` | success | build-test success; shells success |
| 39662981858 | `a33c2f7` | failure | build-test failed at "Secret scan (gitleaks CLI, pinned)". Fixed by `6dc3387`, which allowlists the tokenizer identifier flagged in the live RPC capture |
| 39661140106, 39659074355, 39655121968 | earlier heads from the same day | failure | conclusion recorded; the failing step was not inspected |

Limits:

- The audit's HO-1 item names head `0b67f4b`. That head is superseded. This record covers the pushed heads `6dc3387` and `dcd0599`.
- Local commits `a6484a0` (planning context) and `adc260b` (HO-03 evidence) are not pushed yet. Their CI is not recorded here.
