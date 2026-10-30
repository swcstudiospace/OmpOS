# Plan 71-02 SUMMARY: Safe content pipeline

Status: artifacts rendering tests green. Markdown and Code renderers stay in 71-05 and 71-06 (amendment 2).

## Results

| Command | Result |
| --- | --- |
| `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests/Autonogrammer.Cowork.Artifacts.Tests.csproj -c Release --filter-not-trait "Category=Live"` | Passed: total 28, failed 0 |

## What landed

- `CsvReader`: RFC 4180 quotes, embedded newlines, BOM, caps (2000 rows, 50 columns, 2000 characters, 1 MiB), formula-like marks, `Showing the first 2000 rows`.
- `ImageValidator`: header-only PNG/JPEG/GIF/WebP checks, 8192 and 50-megapixel caps, `Cowork:Artifacts:MaxImageBytes`, never-SVG.
- `SvgSanitizer` plus `CssCheck`: DTD prohibited, allowlist walk, removals as findings, rejected documents render as source, output only through `XmlWriter`.
- `ArtifactDocumentPolicy`: ADR-0033 D7.2 strings, origins never in `script-src`, `RenderResponseHeaders` with sandbox, frame-ancestors, and the permissions-policy deny list. No `X-Frame-Options`.
- `HtmlDocumentWrapper` and `MermaidDocumentBuilder` / `IMermaidAssetProvider`. Missing bundle is `MermaidAssetMissing` with source.
- Hostile fixtures under `Fixtures/hostile/` and `HostileCorpusTests`, which fails if a fixture file is not named by a test.

## Deviations

1. Result types are nested and internal so MA0048 and CA1034 hold without extra files outside the plan list. Tests see them through `InternalsVisibleTo`.
2. Markdown, Code, `md/script-link.md`, and tool fixtures are not in this plan (amendments 2 and 5).
3. `bidi-title.md` is refused by `ArtifactService.IsTitleValid` and by `CreateAsync` as `title_invalid`.

## Gaps

- The vendored Mermaid bundle is still 71-06. The builder only consumes `IMermaidAssetProvider`.
- Desktop and web surfaces that call these renderers are 71-05 and 71-06.
