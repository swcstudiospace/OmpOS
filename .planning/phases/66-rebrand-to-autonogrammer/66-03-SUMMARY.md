---
phase: 66-rebrand-to-autonogrammer
plan: "03"
status: complete per git; verification not re-run here
---
# 66-03 Summary: regenerate after the rename

Done partly by the integration line and partly by this session (the later tool-hardening and evidence fixes).

## Commits (regenerate)
- 881f66ae chore: relock via `dotnet restore --force-evaluate`: 69 files, 967 insertions/deletions each (plan text expected 85 lock files; the count differs, [INFERENCE] some locks needed no change).
- b5f6bc1a style: dotnet format re-sorts usings (Autonogrammer sorts before Microsoft): 499 files, 852 each.
- 19812147 chore: third-party notices regenerated: 1 file, 63 each.
- d714acb4 chore: OpenAPI regenerated, brand strings only: 1 file, 9 each.
- d02bcbb3 fix: worker evidence file renamed to the name the rewritten links use (rename only, 0 line changes).

## Commits (tool support, this session)
- 3d7c722b: rebrand tool protects the Railway names of the staged worker (1 file, 1 insertion).
- 9d0b163e: rebrand:keep-start/keep-end markers (unbalanced is a hard error), glob character classes, new milestone planning dirs frozen, CHANGELOG lines wrapped. 5 files, 238 insertions, 4 deletions. Commit message reports 61 unit tests pass and verify clean.

## Verification evidence in git
- Only the 9d0b163e message (61 tests, verify clean). For the regenerate commits only the commit messages and stats.
- Not re-run here: locked-mode restore, build, format check, brand and brand-check, leak-scan fingerprints (.gitleaksignore), scanner state record.

## Known gaps
- Brand manifest/asset manifest regeneration and .gitleaksignore update are named in the plan; no matching commit among those inspected. Unconfirmed, verify in 66-07.
- Generated files mixed with the style commit are not checked for hand edits here.
