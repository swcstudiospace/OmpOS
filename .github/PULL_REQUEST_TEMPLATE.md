<!--
Thank you for the pull request. Fill in the sections and tick the boxes that apply; delete the rest.
Never paste secrets (keys, tokens, .env contents) into the description, the diff or a screenshot.
Security fixes: do not open a public pull request for an undisclosed vulnerability; use the private
reporting described in SECURITY.md.
-->

## What and why

<!-- One or two sentences: what changes and which problem it solves. Link the issue: "Closes #123". -->

## Type of change

- [ ] Bug fix
- [ ] New feature or improvement
- [ ] Documentation only
- [ ] Dependency update
- [ ] Breaking change (a public contract, API route, setting or file format changes)
- [ ] Build, CI or release

## Checklist

- [ ] The gate is green: `./bootstrap.sh` exits 0 on this branch (build with warnings as errors, tests, `dotnet format`, web smoke).
- [ ] Tests cover the change (deterministic: no sleeps, no network, no real `omp` outside `Category=Live` tests), and no existing test was weakened.
- [ ] Documentation is updated (README, `docs/`, runbooks) and `CHANGELOG.md` has a line under `[Unreleased]` for a user-visible change.
- [ ] Every commit is signed off (`git commit -s`, the DCO), and I license the contribution under `AGPL-3.0-or-later`.
- [ ] New decisions, blockers or deferred items have a row in `docs/open-questions.md` (OQ rows).
- [ ] Screenshots in the light and dark themes are attached for a UI change.
- [ ] No secrets, no new `TODO` without an owner and an open-question link, no new dependency without a reason.
- [ ] Maintainers only: the commit bodies end with the `Refs SPE-xxxxx` trailer.

## How I tested it

<!-- Commands you ran and what you observed. -->

## Screenshots

<!-- UI changes: before and after, light and dark. -->

<!--
Dependency changes and lock files
---------------------------------
Every project commits a packages.lock.json and CI restores with --locked-mode, so a change to
Directory.Packages.props (including Dependabot pull requests) needs the lock files relocked, or
restore fails with NU1004. CI=true turns locked mode on locally as well, so relock with locked
mode switched off:

    dotnet restore OmpOS.Linux.slnf --force-evaluate -p:RestoreLockedMode=false

Commit the resulting packages.lock.json changes in the same pull request.
-->
