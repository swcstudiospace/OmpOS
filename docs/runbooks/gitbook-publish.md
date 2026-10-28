---
description: "Owner steps to publish the OmpOS documentation with GitBook and GitHub Git Sync: create the site, install the GitHub app, sync docs/, set brand colours and logo, register the OpenAPI document and verify."
---

# Publish the docs with GitBook

Status: Blocked (the documentation is not published: it needs the owner's GitBook account; no GitBook credential exists in this repository or on the build host)

Last verified: 2026-10-28

Applies to: v0.15.0

**The site is NOT published until a live URL is confirmed.** Until then the Markdown in `docs/` is the documentation, readable on GitHub. Do not link a GitBook URL anywhere (README, repository About box, `SUPPORT.md`) before step 11 below.

The repository already carries everything GitBook reads: `.gitbook.yaml` at the repository root (`root: ./docs/`, landing `README.md`, navigation `SUMMARY.md`), `docs/README.md`, `docs/SUMMARY.md` and the pages. GitHub is the source of truth: edit in the repository, never merge change requests in GitBook (GitBook commits would race your pushes). The design behind this page is `docs/plans/v0.15.0-research/12-github-gitbook-plan.md`.

## What the repository gives GitBook

| File | Role |
|---|---|
| `.gitbook.yaml` | `root: ./docs/`; `structure.readme: README.md`; `structure.summary: SUMMARY.md`. |
| `docs/README.md` | Landing page. |
| `docs/SUMMARY.md` | Navigation. Each Markdown file appears once. |
| `docs/assets/` | Diagrams and screenshots referenced by pages. |
| `docs/plans/**`, `docs/runbooks/evidence/**`, `docs/api/**` | Not in `SUMMARY.md`: research, recorded evidence and internal change requests. They sit under the root; whether GitBook publishes files that are not in the summary is not stated in its documentation (UNVERIFIED: after the first sync, check the site for pages you did not list). |

Check the structure before you connect anything:

```sh
bash scripts/ci/check-docs.sh
```

It exits non-zero on a missing SUMMARY entry, an orphan page, a broken link or anchor, an unquoted `description:`, a second H1, a page without its status line, or unbalanced `{% %}` blocks. `OMPOS_RELEASE=1 bash scripts/ci/check-docs.sh` also fails when a pinned screenshot is missing.

## Steps (only a human with the accounts can do these)

1. **Sign in** at <https://app.gitbook.com> with GitHub. If GitBook shows "Potential duplicated accounts", unlink the GitHub identity from the other account first.
2. **Create the site.** Create an organisation if you have none, then a docs site named "OmpOS". Choose the plan: Git Sync is included in the Free plan; a custom domain is **not** (paid plans only, checked on gitbook.com/pricing on 2026-10-27, UNVERIFIED since). A `*.gitbook.io` URL is the guaranteed free outcome.
3. **Install the GitBook GitHub app** (<https://github.com/apps/gitbook-com>) on the `swcstudiospace` organisation; this needs an organisation owner. Grant access to the `OmpOS` repository only.
4. **Land the docs on `main` first** (merge this branch), so the first import has real content.
5. **Connect Git Sync.** In the site, open Git Sync, choose GitHub, repository `swcstudiospace/OmpOS`, branch `main`, and set the **initial direction GitHub to GitBook** (use "Swap direction" if GitBook offers the other; a non-empty GitBook space is overwritten). Leave the project directory empty: `.gitbook.yaml` at the repository root already points GitBook at `./docs/`. Only administrators and creators can configure Git Sync.
6. **Commit what GitBook writes.** If GitBook adds a `docs.yaml` or changes `SUMMARY.md`, review and commit it; the schema of `docs.yaml` is GitBook's, do not hand-write it.
7. **Customize** (these are UI settings, not files):
   - Colours: primary colour `#6d28d9` (the White-and-Purple accent); neutral surfaces `#fafaf9` (Light) and `#181818` (Dark); accent in Dark `#a78bfa`. Source: [Brand](../brand.md).
   - Logo: upload `assets/brand/ompos-logo.svg` for Light and `assets/brand/ompos-logo-dark.svg` for Dark (GitBook recommends a PNG at least 600 px wide: if the SVG is not accepted, render a PNG with `cairosvg`). Icon (132 by 132, used as favicon): downscale `assets/brand/ompos-icon-1024.png`. Social image: `assets/brand/ompos-social-preview.png` (1280 by 640).
   - Fonts: custom fonts accept only `.woff` or `.woff2`, with a regular and a bold file each, one standard family and one monospace family. DM Sans (standard) and Space Mono (monospace) fit; the repository holds them as `.woff2` under `src/OmpOS.UI/wwwroot/fonts/`. **Anybody cannot be a GitBook body font**: it is used only inside the logo and banner images, and only at 20 px and above. DM Sans has no bundled Bold file (SemiBold is the nearest); whether the plan allows custom fonts must be checked in the Customize screen.
8. **OpenAPI.** In the site, open OpenAPI, Add specification, name it `ompos-api`, choose URL and enter the raw GitHub URL of the document: `https://raw.githubusercontent.com/swcstudiospace/OmpOS/main/docs/api/openapi.json`. GitBook refreshes a URL source about every six hours (or press Check for updates). Then add an OpenAPI Reference page to the "HTTP API" area and commit the pages GitBook generates. The document has no `servers` entry beyond `/` and no security scheme, so "Test it" cannot work until a hosted API URL exists. This is the page: [HTTP API](../reference/api.md).
9. **Edit on GitHub**: turn the toggle on in the site settings; preview; then press **Publish**. Sites are public by default; Settings, Audience changes it.
10. **Custom domain** (paid plan only): Settings, Domain and URL; add the CNAME GitBook shows at your DNS provider (not an A record; no Cloudflare proxy); SSL is automatic.
11. **Record the live URL.** Only after you opened it and it matches the checklist below: `gh repo edit swcstudiospace/OmpOS -h <url>`, then put the URL in the README, `SUPPORT.md` and the issue form contact links, and replace the placeholder sentence in `README.md` and `docs/README.md`.

## Verification checklist

- [ ] `bash scripts/ci/check-docs.sh` passes (and `OMPOS_RELEASE=1 bash scripts/ci/check-docs.sh` once the screenshots are in `docs/assets/screens/`).
- [ ] The site's first page is the landing page and its navigation matches `docs/SUMMARY.md` (sections, order and nesting).
- [ ] Open five pages with front matter: no raw `---` block shows (a silent Git Sync failure means an unquoted `description`).
- [ ] The architecture diagram and every screenshot load, and each has alt text.
- [ ] A link from a guide into a reference page and into an ADR works; no page links to a file outside `docs/` (those links are dead on the site: the check warns).
- [ ] The generated pages for the providers and the slash commands render their tables.
- [ ] The OpenAPI reference lists the routes of `docs/api/openapi.json`.
- [ ] Search finds "Hooks" and "Remote environments".
- [ ] Light and Dark logos both show; colours match the brand.
- [ ] No page shows a pre-release claim: every page's status line says what is built, partial or blocked.
- [ ] A change pushed to `main` appears on the site after the sync, and a change committed by GitBook is reviewed in git before the next push.

## Interplay with CI

Git Sync commits to `main`, and every commit triggers `ci.yml` on the self-hosted pool. Treat GitHub as the only editing surface. If GitBook commits anyway, `git pull --rebase` before your next push. If branch protection is ever added to `main`, allow the `gitbook-com` app to bypass pull-request and push restrictions, or the sync fails with "Missing permissions to push".

## What is not decided here

The GitBook plan, the custom domain, the site title and visibility are the owner's choices; no GitBook credential, site or domain was created by this repository ([OQ-101](../open-questions.md)).
