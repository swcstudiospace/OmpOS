# Greptile.com typeface research (item 1)

Date read: 2026-10-22 (all sources opened that day).

## Verdict

greptile.com uses four open-licensed Google Fonts families, all self-hosted by the site from its own `/_next/static/media/` path (no Google Fonts or other CDN link appears in the homepage HTML). The body and most UI text use **DM Sans** (variable, weights 100–1000, normal and italic). Headings and the hero use **Anybody** (variable, weights 100–900, normal and italic; the hero h1 uses weight 700). Monospace labels use **Space Mono** (weights 400 and 700). **Nanum Pen Script** (weight 400) is loaded by the page but I found no element or utility class that uses it on the homepage. All four are licensed under the SIL Open Font License 1.1 (OFL). The OFL permits embedding in desktop and mobile apps and in web apps, provided the copyright notice and licence text ship with the app and no font is sold on its own. Recommendation: use the same families, taken unmodified from the upstream Google Fonts files, with an OFL notice in the About box. Do not use Nanum Pen Script unless a surface needs it. The font files were not decoded beyond their CSS family names, so binary name-table confirmation is unverified (see Unverified).

## Table

| # | Item | Claim | Evidence (URL) | Opened (verified) | Date read |
|---|------|-------|----------------|-------------------|-----------|
| 1 | Hosting | greptile.com loads fonts from its own origin: `<link rel="preload">` for `/_next/static/media/*.woff2` (7 files) and `@font-face` `src:url(../media/…)` in the first stylesheet. | https://www.greptile.com/ (raw HTML); https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css | yes | 2026-10-22 |
| 2 | CDN | No `fonts.googleapis.com`, `fonts.gstatic.com`, Typekit or other font CDN reference appears in the homepage HTML or in the stylesheets I read. | Same as row 1 (full raw HTML artifact, 28 lines, searched) | yes (search of full read output) | 2026-10-22 |
| 3 | Build origin | The `@font-face` set, `Fallback` faces and `--font-*` variables match Next.js self-hosted `next/font/google` output. This is an inference from the naming pattern (`*-s.p.<hash>.woff2`, `*_<hash>-module__…`), not a stated fact on the site. | https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css | yes (inference, marked [INFERENCE]) | 2026-10-22 |
| 4 | DM Sans family | `@font-face` family "DM Sans", `font-style: normal` and `italic`, `font-weight: 100 1000` (variable), `font-display: swap`. Woff2 files are latin and latin-ext subsets. | https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css | yes | 2026-10-22 |
| 5 | DM Sans usage | Body class `font-sans` on `<body>`; `.font-sans{font-family:var(--font-dm-sans),sans-serif}`; `--font-dm-sans:"DM Sans","DM Sans Fallback"`. | https://www.greptile.com/ (raw HTML, body tag); https://www.greptile.com/_next/static/chunks/8a34bd95f6f4410b.css (utility CSS) | yes | 2026-10-22 |
| 6 | DM Sans design | Google Fonts specimen: low-contrast geometric sans, designed by Colophon Foundry (UK), started from Latin Poppins; weight range expanded to 100–1000 in May 2023. | https://fonts.google.com/specimen/DM+Sans | yes | 2026-10-22 |
| 7 | DM Sans licence | Copyright 2014 The DM Sans Project Authors; SIL OFL 1.1; no Reserved Font Name declared. | https://raw.githubusercontent.com/google/fonts/main/ofl/dmsans/OFL.txt | yes | 2026-10-22 |
| 8 | Anybody family | `@font-face` family "Anybody", normal and italic, `font-weight: 100 900`, `font-stretch: 100%`, `font-display: swap`. | https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css | yes | 2026-10-22 |
| 9 | Anybody usage | Hero h1 class `font-anybody font-bold` (weight 700); testimonial role line `font-anybody font-medium`; `.font-anybody{font-family:var(--font-anybody),sans-serif}`. | https://www.greptile.com/ (raw HTML, h1 and testimonial markup); https://www.greptile.com/_next/static/chunks/8a34bd95f6f4410b.css | yes | 2026-10-22 |
| 10 | Anybody design | Google Fonts specimen: big family with Eurostile affinity, from Etcetera Type Co; 10-degree italic angle. | https://fonts.google.com/specimen/Anybody | yes | 2026-10-22 |
| 11 | Anybody licence | Copyright 2020 The Anybody Project Authors; SIL OFL 1.1; no Reserved Font Name declared. | https://raw.githubusercontent.com/google/fonts/main/ofl/anybody/OFL.txt | yes | 2026-10-22 |
| 12 | Space Mono family | `@font-face` family "Space Mono", normal style, `font-weight: 400` and `700`, `font-display: swap`. | https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css | yes | 2026-10-22 |
| 13 | Space Mono usage | Footer copyright `font-mono`; testimonial name `font-space-mono text-sm tracking-wider uppercase`; `.font-mono{font-family:var(--font-space-mono),monospace}`. | https://www.greptile.com/ (raw HTML footer and testimonial markup); https://www.greptile.com/_next/static/chunks/8a34bd95f6f4410b.css | yes | 2026-10-22 |
| 14 | Space Mono design | Google Fonts specimen: fixed-width family by Colophon Foundry for Google Design, Latin Extended support. | https://fonts.google.com/specimen/Space+Mono | yes | 2026-10-22 |
| 15 | Space Mono licence | Copyright 2016 The Space Mono Project Authors; SIL OFL 1.1; no Reserved Font Name declared. | https://raw.githubusercontent.com/google/fonts/main/ofl/spacemono/OFL.txt | yes | 2026-10-22 |
| 16 | Nanum Pen Script family | `@font-face` family "Nanum Pen Script", normal, `font-weight: 400`, many unicode-range subsets (Korean and Latin). `<html>` carries class `nanum_pen_script_…__variable`, which sets `--font-handwriting:"Nanum Pen Script","Nanum Pen Script Fallback"`. | https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css (artifact tail, line 10); https://www.greptile.com/ (html class) | yes | 2026-10-22 |
| 17 | Nanum Pen Script usage | No `font-handwriting` utility, no `var(--font-handwriting)` reference and no `nanum` reference in the homepage HTML or the utility stylesheet. Loaded but not used on the homepage. Other pages not checked. | Searches over the full read output of https://www.greptile.com/ and of https://www.greptile.com/_next/static/chunks/8a34bd95f6f4410b.css | yes (search) | 2026-10-22 |
| 18 | Nanum Pen Script design and publisher | Google Fonts specimen: Nanum fonts designed by Sandoll Communications and Fontrix; published by Naver; pen script for screen use. | https://fonts.google.com/specimen/Nanum+Pen+Script | yes | 2026-10-22 |
| 19 | Nanum Pen Script licence | Copyright 2010 NHN Corporation; SIL OFL 1.1; Reserved Font Names: "Nanum", "Naver Nanum", "NanumGothic", "Naver NanumGothic", "NanumMyeongjo", "Naver NanumMyeongjo", "NanumBrush", "Naver NanumBrush", "NanumPen", "Naver NanumPen". | https://raw.githubusercontent.com/google/fonts/main/ofl/nanumpenscript/OFL.txt | yes | 2026-10-22 |
| 20 | Font file format | The DM Sans latin woff2 was returned by the read tool as binary (36.1 KB) with no decoded name table. | https://www.greptile.com/_next/static/media/5c285b27cdda1fe8-s.p.a62025f2.woff2 | yes (binary metadata only) | 2026-10-22 |
| 21 | OFL: use in apps | Bundling OFL fonts in a mobile or desktop app is allowed. Minimum: copyright statement, licence notice and licence text; an About box or changelog mention is good practice; only the weights you ship need to be included. | https://openfontlicense.org/how-to-use-ofl-fonts/ (FAQ 1.20 linked from it) | yes | 2026-10-22 |
| 22 | OFL: use on the web | Webfonts via `@font-face` are allowed and are described as "explicitly allowed"; hosting on the same server or a separate webfont service is fine. | https://openfontlicense.org/how-to-use-ofl-fonts/ (FAQ 2.1) | yes | 2026-10-22 |
| 23 | OFL: what is restricted | The only restrictions are: do not sell the font by itself; keep copyright and licence information with the font files; modified versions may not use Reserved Font Names. | https://openfontlicense.org/how-to-use-ofl-fonts/; OFL.txt files in rows 7, 11, 15, 19 | yes | 2026-10-22 |
| 24 | OFL: rebuilds and format changes | The OFL "Modified Version" definition covers changing formats and porting, so a converted or subset copy (for example, to woff2) is a Modified Version. Reserved Font Names then apply where declared (row 19). OFL-FAQ 5.9 says rebuilds that change font data are Modified Versions. | https://raw.githubusercontent.com/google/fonts/main/ofl/dmsans/OFL.txt (Definitions); https://openfontlicense.org/ofl-reserved-font-names/; https://openfontlicense.org/ofl-faq/ (FAQ 5.9 in full read output) | yes | 2026-10-22 |
| 25 | OFL: Reserved Font Names | Only Nanum Pen Script declares RFNs (see row 19). The other three OFL.txt files declare none, so a renamed or converted copy of DM Sans, Anybody or Space Mono has no RFN constraint in the licence text. | OFL.txt files in rows 7, 11, 15, 19 | yes | 2026-10-22 |

## Owner decisions

1. Typeface for the desktop and mobile app (and web head): which option?
   - (a) Use the same families as greptile.com: DM Sans for body, Anybody for headings, Space Mono for mono labels. Licence-compatible under OFL.
   - (b) Use a different OFL family of your choice (for example a licence-compatible equivalent). Owner must pick the family.
   - (c) Ask the Greptile team. Not needed for licence compliance, since OFL does not require permission.
   Recommendation: (a), using unmodified upstream files from Google Fonts or the foundry repositories, not files converted from the greptile.com copies.
2. Nanum Pen Script: include it or drop it?
   - (a) Drop it. Greptile loads it but did not show a use on the homepage (row 17).
   - (b) Include it under OFL, and comply with its RFN rule (names "NanumPen" and "Naver NanumPen" may not be used by a modified version).
   Recommendation: (a) unless a handwriting surface is planned.
3. Brand look: is copying Greptile's visual identity (the same type system) an intent, or only the typeface choice?
   - (a) Typeface choice only. The OFL does not restrict using a font's design in a different product; this research did not check trademark law for the Greptile name or trade dress.
   - (b) Full visual match. Owner should check Greptile's own brand terms before copying its logo, lizard illustration, or trade dress. This research did not check those terms.
4. Format: use the woff2 files from greptile.com directly, or obtain upstream TTF/WOFF2 from Google Fonts?
   - (a) Upstream Google Fonts files: no Modified Version question arises.
   - (b) greptile.com copies: fetch-and-redistribute a copy of someone else's build. Still OFL, but keep the copyright and licence with it. Not recommended.

## Unverified

- Binary font metadata: the read tool returned the woff2 file as binary without a decoded name table, so the family name inside each font file is not confirmed. The family names come from the CSS `@font-face` declarations (rows 4, 8, 12, 16).
- Exact weights used per element beyond the homepage: weight ranges come from the `@font-face` declarations; per-element weights were read from class names on the homepage only. Other greptile.com pages were not opened.
- Nanum Pen Script usage on other pages: not checked (row 17 covers the homepage and the utility stylesheet only).
- Next.js self-hosting: the origin of the font files (`next/font/google`) is an inference from naming (row 3). I did not open the Next.js documentation page to confirm.
- Google Fonts specimen pages for DM Sans, Space Mono and Nanum Pen Script rendered only the description text; their weight tables are client-rendered and were not read. Weights come from the `@font-face` declarations and the OFL files.
- Greptile brand or trademark terms: not opened; not in scope.
- No raw Greptile licence statement for its fonts was found; the licence relied on is the upstream OFL.txt for each family.

## Sources

Opened 2026-10-22 unless noted. All "verified" marks refer to these URLs.

- https://www.greptile.com/ (raw HTML, read via the read tool; full output searched)
- https://www.greptile.com/_next/static/chunks/859d5c346047a53c.css (font-face stylesheet)
- https://www.greptile.com/_next/static/chunks/8a34bd95f6f4410b.css (utility stylesheet; font-family utilities)
- https://www.greptile.com/_next/static/media/5c285b27cdda1fe8-s.p.a62025f2.woff2 (binary; metadata only)
- https://fonts.google.com/specimen/DM+Sans
- https://fonts.google.com/specimen/Anybody
- https://fonts.google.com/specimen/Space+Mono
- https://fonts.google.com/specimen/Nanum+Pen+Script
- https://raw.githubusercontent.com/google/fonts/main/ofl/dmsans/OFL.txt
- https://raw.githubusercontent.com/google/fonts/main/ofl/anybody/OFL.txt
- https://raw.githubusercontent.com/google/fonts/main/ofl/spacemono/OFL.txt
- https://raw.githubusercontent.com/google/fonts/main/ofl/nanumpenscript/OFL.txt
- https://openfontlicense.org/how-to-use-ofl-fonts/
- https://openfontlicense.org/ofl-reserved-font-names/
- https://openfontlicense.org/ofl-faq/ (first 300 lines read; FAQ 5.9 located in full read output)
