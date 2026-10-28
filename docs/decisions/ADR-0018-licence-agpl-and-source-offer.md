# ADR-0018: Licence AGPL-3.0-or-later and the network source offer

- Status: Accepted 2026-10-27 with the default taken for OQ-93 (the owner may still change the identifier; see Open risks). Closes OQ-62.
- Date: 2026-10-27
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: which licence Autonogrammer carries, how the copyright and third-party notices are kept, how contributions are licensed, and how a running Autonogrammer makes the Corresponding Source available to its network users (milestone v0.15.0, phase 51, requirements PX-03 and PX-04). Does not cover the licences of the submodules `agent-swarm` and `agent-substrate` (OQ-102) or the clearance of the name "Autonogrammer".

## Context

Autonogrammer had no LICENSE file (OQ-62). The owner direction of 2026-10-27 asks for an "AGL 3.0" licence; no licence has that name, so it is read as the GNU Affero General Public License version 3 (OQ-93). The closest plugin, `claude-ultrathink`, is already `AGPL-3.0-or-later` (its LICENSE, `package.json` and README grant "either version 3 of the License, or (at your option) any later version"). Autonogrammer is also run as a hosted service (the ASP.NET Core API on Railway, the Blazor WebAssembly head in browsers), which is the case the AGPL's section 13 targets.

Facts read for this record (research `docs/plans/v0.15.0-research/11-agpl-audit.md`, 2026-10-27):

- Every licence in the locked NuGet graph (about 230 packages: MIT, BSD-2-Clause, BSD-3-Clause, Apache-2.0, PostgreSQL), the three bundled fonts (SIL OFL 1.1) and vendored Bootstrap (MIT) is compatible with version 3 of the GNU GPL and therefore of the AGPL. Apache-2.0 is compatible with GPL version 3 only, so a version-3 licence is required.
- Two packages declare their licence as a file inside the package (`Avalonia.Angle.Windows.Natives`, `Unicode.net`); the vendor repositories give BSD-3-Clause and MIT.
- The submodules `agent-swarm` and `agent-substrate` have no LICENSE file; `agent-substrate` is private.
- AGPL section 13 requires an operator of a modified version to prominently offer remote users the Corresponding Source from a network server at no charge, and section 6 requires the source (or an offer) with object code: desktop installers, the APK, container images and the WebAssembly payload sent to browsers.

## Candidate chosen

The licence is `AGPL-3.0-or-later`, with copyright `Copyright (C) 2026 SWC Studio` (the holder named in the sibling plugin repositories; ownership of all existing code is not verified).

Parts:

1. **Files.** `LICENSE` (canonical text), `NOTICE`, generated `THIRD-PARTY-NOTICES.md`, `REUSE.toml` with `LICENSES/`, and package metadata in `Directory.Build.props` (`PackageLicenseExpression`, `Authors`, `Copyright`, `RepositoryUrl`, `VersionPrefix`). No per-file headers: REUSE annotations by glob.
2. **Third-party notices are generated**, not written: `scripts/legal/generate-third-party-notices.py` reads every `packages.lock.json`, the licence data of the restored packages and the font, image and container pins; it never guesses, and its `--check` mode is run by `scripts/ci/check-licences.sh`.
3. **Contributions:** DCO sign-off, no CLA.
4. **Source offer.** One contract on every surface (below).

### The source-offer contract

The API answers an anonymous `GET /source` (no sign-in, the same treatment as `/health/live`) with exactly this JSON shape:

```json
{
  "licence": "AGPL-3.0-or-later",
  "licenceUrl": "https://www.gnu.org/licenses/agpl-3.0.html",
  "sourceUrl": "https://github.com/swcstudiospace/OmpOS",
  "revision": "<git sha or unknown>",
  "version": "0.15.0"
}
```

`version` is `VersionPrefix` of `Directory.Build.props`; `revision` is the commit the build was made from, or `unknown`; `sourceUrl` is the repository URL, which a fork changes to its own. The web footer shows "Source code (AGPL-3.0-or-later)" on every screen including signed-out ones and links to an About page; the desktop About dialog shows the same fields; container images carry the OCI labels `org.opencontainers.image.licenses`, `.source`, `.revision` and `.version` and the licence files under `/licenses`.

## Licence

The licence chosen is `AGPL-3.0-or-later`; text and wording: https://www.gnu.org/licenses/agpl-3.0.txt (sections 6, 13 and 14), identical to the licence of the `claude-ultrathink` submodule. Third-party licences found in the graph are MIT, BSD-2-Clause, BSD-3-Clause, Apache-2.0, PostgreSQL and the SIL Open Font License 1.1 (https://openfontlicense.org/), listed with copyright lines in `THIRD-PARTY-NOTICES.md`. GNU compatibility list: https://www.gnu.org/licenses/license-list.html. `-or-later` is preferred to `-only` because it keeps one licence family with the plugin and lets the FSF successor apply without a relicensing round; `-only` is the better choice only if the owner wants to freeze the terms.

## Decision

1. Autonogrammer is licensed under `AGPL-3.0-or-later`; `LICENSE` holds the canonical text and the SPDX expression is set once in `Directory.Build.props`. No project overrides it (`scripts/ci/check-licences.sh` fails when one does).
2. `THIRD-PARTY-NOTICES.md` is generated and checked for staleness in the gate; a package whose licence cannot be determined fails the generator.
3. Fonts keep their `OFL.txt` next to the font files; a directory with font files and no `OFL.txt` fails the gate.
4. Contributions are accepted under `AGPL-3.0-or-later` with a DCO sign-off and no CLA. A CLA is needed only for dual licensing, and none is planned.
5. Every deployment is treated as modified: the source offer is always made, through the contract above, and always points at the deployed revision.
6. The plugin submodules and omp stay separate programs with their own terms (ADR-0013 rule 6); the source offer does not cover submodules, which GitHub source archives never contain.
7. The licence grants no trademark right (AGPL section 7(e)); the name and artwork are reserved in `NOTICE`.

## Verification route

Run by the orchestrator; nothing below was run when this record was written except the first line.

- `bash scripts/ci/check-licences.sh`: LICENSE length and SHA-256, notices freshness (`python3 scripts/legal/generate-third-party-notices.py --check`), font `OFL.txt` presence, plugin `package.json` licence, README, package metadata.
- The source-offer endpoint and pages are verified by the tests of the agents that implement them (the API test of `GET /source` without credentials; the web and desktop About pages): see `docs/licensing.md` for the contract they must match.
- `podman build -f deploy/api.Containerfile --build-arg AUTONOGRAMMER_REVISION="$(git rev-parse HEAD)" -t autonogrammer-api:local .` then `podman inspect autonogrammer-api:local` shows the four OCI labels and `/licenses` holds LICENSE, NOTICE, THIRD-PARTY-NOTICES.md, the omp LICENSE and the Bun notice.
- Optional: `reuse lint` once the REUSE tool is installed (not available on the research host).

## Open risks

- The owner may have meant `AGPL-3.0-only` (OQ-93): the change touches LICENSE-adjacent notices, `PackageLicenseExpression`, the README and `docs/licensing.md` together.
- Ownership of all existing code by SWC Studio and the absence of third-party snippets in the git history are unverified; review before the first AGPL tag.
- `agent-swarm` and `agent-substrate` have no licence (OQ-102); until that changes they are outside the source offer, and the private `agent-substrate` cannot be obtained by users of a public build.
- `TextMateSharp.Grammars` embeds third-party grammars whose licences the package does not state; the packages' native-code notices are reproduced from the packages but not otherwise audited.
- Two web icons (`favicon.png`, `icon-192.png`) have unknown provenance and the name "Autonogrammer" is not cleared as a trademark.
- App-store terms (Google Play) may conflict with AGPL section 10; the Android build ships through GitHub Releases or F-Droid until that is read.
- This record is engineering analysis, not legal advice; whether process boundaries (omp over ACP) count as aggregation is a legal judgement it does not make.

## Alternatives considered and rejected

- `AGPL-3.0-only`: freezes the terms; no dependency forces it. Kept as the fallback in OQ-93.
- GPL-3.0-or-later: no section 13 duty, so a hosted fork could keep its changes private; the owner asked for the Affero licence.
- A permissive licence (MIT or Apache-2.0): compatible with the dependencies but conflicts with the sibling AGPL plugin family and gives no network copyleft.
- A CLA: only useful for later relicensing or dual licensing, which is not planned; it adds friction for contributors.

## Related

- [ADR-0013](ADR-0013-plugin-isolation.md): plugin boundary; the licence table there names Autonogrammer as NOASSERTION and is superseded by this record.
- `docs/licensing.md`: the user and operator guide; `docs/plans/v0.15.0-research/11-agpl-audit.md`: the audit.
