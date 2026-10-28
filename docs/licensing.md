---
description: "What the AGPL-3.0-or-later licence of OmpOS means for users, hosts and contributors, the source offer, third-party licences and open licence questions."
---

# Licensing

Status: Built (the licence is in effect; this page is engineering documentation, not legal advice)

Last verified: 2026-10-28

Applies to: v0.15.0

This page describes what the OmpOS licence means for people who use, host or contribute to OmpOS. It is engineering documentation, not legal advice. The research behind it is `docs/plans/v0.15.0-research/11-agpl-audit.md`; the decision is [ADR-0018](decisions/ADR-0018-licence-agpl-and-source-offer.md).

## The licence

OmpOS is licensed under the GNU Affero General Public License, version 3 or, at your option, any later version. The SPDX identifier is `AGPL-3.0-or-later`. The copyright line is `Copyright (C) 2026 SWC Studio`.

| File | What it is |
|---|---|
| [LICENSE](https://github.com/swcstudiospace/OmpOS/blob/main/LICENSE) | The full licence text, unmodified. |
| [NOTICE](https://github.com/swcstudiospace/OmpOS/blob/main/NOTICE) | Copyright, the licence notice, the source URL, the submodule statement and the name notice. |
| [THIRD-PARTY-NOTICES.md](https://github.com/swcstudiospace/OmpOS/blob/main/THIRD-PARTY-NOTICES.md) | Licences and copyright lines of everything third-party that OmpOS uses or conveys. Generated, never edited by hand. |
| [REUSE.toml](https://github.com/swcstudiospace/OmpOS/blob/main/REUSE.toml) and `LICENSES/` | Machine-readable licence data (REUSE 3.3): one annotation table, no per-file headers, licence texts by SPDX identifier. |

What that means in practice:

- You may use, study, copy, modify and redistribute OmpOS, including commercially, as long as every copy or modified version you convey stays under the same licence terms, carries the notices above and comes with (or offers) its Corresponding Source.
- Using OmpOS does not put your own work under the AGPL. Code you write with OmpOS, the repositories you open in it and the files agents produce for you belong to you.
- The "or later" option lets a later GNU licence of the same family apply at your choice. The project maintainers can narrow this by publishing a proxy statement (AGPL section 14); none exists.

## Running OmpOS as a hosted service (AGPL section 13)

The AGPL adds one duty to the GPL: if you modify OmpOS and let other people interact with your modified version remotely through a computer network, you must prominently offer those users the Corresponding Source of your version, at no charge, from a network server. The offer is only legally required for modified versions, but OmpOS treats every deployment as modified and always makes it, so no operator has to decide whether their build counts.

How the offer is made:

| Surface | What the user sees |
|---|---|
| API (`services/OmpOS.Api`) | An anonymous `GET /source` that needs no sign-in (the response body is below). |
| Web head | A footer link "Source code (AGPL-3.0-or-later)" on every screen, signed in or not, and an About page that shows the version, the revision and the links to the source tree, LICENSE and THIRD-PARTY-NOTICES. |
| Desktop head | The About dialog shows licence, version, revision and the source URL. |
| Container image | OCI labels `org.opencontainers.image.licenses`, `.source`, `.revision` and `.version`; LICENSE, NOTICE and THIRD-PARTY-NOTICES.md under `/licenses`. |

The `GET /source` response is exactly this shape:

```json
{
  "licence": "AGPL-3.0-or-later",
  "licenceUrl": "https://www.gnu.org/licenses/agpl-3.0.html",
  "sourceUrl": "https://github.com/swcstudiospace/OmpOS",
  "revision": "<git sha or unknown>",
  "version": "0.15.0"
}
```

`revision` is the git commit the running build was made from, or `unknown` when the build did not record one. `version` comes from `VersionPrefix` in `Directory.Build.props`, which is the single version source.

Operator duties if you fork and host a modified OmpOS:

1. Publish your modified source (a public fork is enough) and point `sourceUrl` at it, not at this repository, together with the revision you deploy. The link must lead to the code that is actually running.
2. Keep the footer link, the About page and `GET /source` working and reachable without sign-in.
3. Keep LICENSE, NOTICE and THIRD-PARTY-NOTICES.md with every installer, APK, image and web payload you convey.
4. Rename the product if you do not want to be mistaken for the upstream project (see Name and logo).

Building images: `podman build -f deploy/api.Containerfile --build-arg OMPOS_REVISION="$(git rev-parse HEAD)" -t ompos-api:local .` sets the revision label; without the argument the label is `unknown`.

## Third-party licences

Every package in the dependency graph (about 230 NuGet packages across shipped, build-time, test and spike scope) is permissive (MIT, BSD-2-Clause, BSD-3-Clause, Apache-2.0, PostgreSQL) and compatible with the AGPL version 3. The list, with scope, licence expression and copyright line per package, and the licence and notice files that ship inside packages (for example the Skia and HarfBuzz notices), is [THIRD-PARTY-NOTICES.md](https://github.com/swcstudiospace/OmpOS/blob/main/THIRD-PARTY-NOTICES.md). Two packages declare their licence as a file inside the package; their licences were checked against the vendor repository and are recorded in the generator script (`Avalonia.Angle.Windows.Natives` BSD-3-Clause, `Unicode.net` MIT).

Regenerate and check:

```sh
python3 scripts/legal/generate-third-party-notices.py          # rewrite THIRD-PARTY-NOTICES.md
python3 scripts/legal/generate-third-party-notices.py --check  # exit 1 when it is stale, exit 2 when a licence is undetermined
bash scripts/ci/check-licences.sh                              # the whole licence gate
```

The generator reads every `packages.lock.json` and the licence data of the restored packages from the local NuGet cache, so run `./bootstrap.sh` first. It never guesses: a package whose licence it cannot determine is listed and the run fails. Adding a package therefore needs a regenerated notices file in the same commit.

What the generator cannot confirm:

- `TextMateSharp.Grammars` embeds many third-party TextMate grammars and themes. The package carries no licence file for them, so their individual licences are not recorded.
- The nuspec of `DiffPlex` was not available when the audit was written (it was not locked yet); it appears in the generated file once it is.
- omp's own npm dependency tree is resolved when the API image is built and is not audited.
- The Android SDK and workload licences are build-time tooling and are not read.

## Fonts and images

The desktop head embeds three font families under the SIL Open Font License 1.1: DM Sans, Anybody (SemiBold) and Space Mono. Each directory under `src/OmpOS.Native.Views/Assets/Fonts/` keeps its `OFL.txt`; `scripts/ci/check-licences.sh` fails when a directory holding font files has none. The fonts are used unmodified. If a converted copy (for example WOFF2 for the web head) is added, put an `OFL.txt` next to it and keep the source named; whether a format conversion is a "Modified Version" under the OFL was not settled by the FAQ and is not assumed either way.

Brand artwork under `assets/` is authored for OmpOS. Two web icons (`favicon.png`, `icon-192.png`) have no recorded provenance and look like the stock Blazor template; replace them with OmpOS artwork before the repository is described as fully REUSE-clean.

## Submodules and plugins

`plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate` are separate repositories pinned as git submodules. OmpOS launches them as omp extensions and never copies, edits or links their source (ADR-0013 rule 6). They carry their own terms, not OmpOS's:

| Submodule | Licence |
|---|---|
| claude-ultrathink | AGPL-3.0-or-later (LICENSE read in that repository) |
| agent-swarm | none published |
| agent-substrate | none published; private repository |

Submodules are not included in GitHub source archives of OmpOS, so they are not part of the source offer. `plugins/gsd-core-omp` is OmpOS code and is AGPL-3.0-or-later. omp itself (`@oh-my-pi/pi-coding-agent`, MIT) is a separate program that OmpOS starts over ACP/RPC stdio; the API image conveys it with its LICENSE under `/licenses`. Whether that boundary counts as mere aggregation is a legal judgement this page does not make.

## Contributing

Contributions are licensed under AGPL-3.0-or-later. Every commit carries a Developer Certificate of Origin sign-off (`git commit -s` adds `Signed-off-by:`); there is no contributor licence agreement. Under AGPL-3.0-or-later plus DCO the maintainers cannot later relicense contributed code as proprietary; a CLA would be needed only for dual licensing, and none is planned. See [CONTRIBUTING.md](https://github.com/swcstudiospace/OmpOS/blob/main/CONTRIBUTING.md).

## Name and logo

The licence grants no right to the name "OmpOS" or to its logo, banner or other brand artwork (AGPL section 7(e)); [NOTICE](https://github.com/swcstudiospace/OmpOS/blob/main/NOTICE) says so. Forks should use a different name. No trademark search for "OmpOS" has been made, so the name is not cleared.

## What the owner still has to do

| Item | Why | Reference |
|---|---|---|
| Confirm `AGPL-3.0-or-later` (not `-only`) | The default taken for "AGL 3.0" reads it as the GNU Affero licence with the "or later" option. Changing it means editing LICENSE-adjacent notices, package metadata and README together. | OQ-93 |
| Add a LICENSE to `agent-swarm` and `agent-substrate` and bump the pins, or keep them private and say so | Without a licence all rights are reserved, and `agent-substrate` is private, so public AGPL users cannot obtain it. | OQ-102 |
| Confirm SWC Studio owns all existing code | The git history was written by people and agents; ownership and the absence of third-party snippets were not verified. Review `git shortlog` before the first AGPL tag. | audit section 6.8 |
| Decide DCO versus CLA | DCO is the default; a CLA is only needed for dual licensing. | audit section 6.7 |
| Search the name "OmpOS" before relying on it as a mark | Not cleared. | audit section 6.6 |
| Decide app-store publication of the Android build | AGPL section 10 forbids further restrictions; store terms were not read. Ship through GitHub Releases or F-Droid first. | audit section 6.5 |
| Replace the two web icons of unknown provenance | See Fonts and images. | audit section 4 |
