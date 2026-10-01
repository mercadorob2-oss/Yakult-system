# Homepage Images Design — 2026-10-01

## Goal
Utilize all unused banner images in `Yakult.SystemsPortal/images/` across the home pages.
Only `wwwroot/images/Yakult_background.jpg` is currently used (hero backgrounds via
`wwwroot/css/public-portal.css`, plus `Views/Home/Login.cshtml` and `Register.cshtml`).
Four banners are unused: `BANNER 2.jpg`, `EPR_BANNER.jpg`, `POSTER WEB BANNER.png`,
`WEB BANNER VFT_1.png`. Approved approach: B (carousel + thumbs), local-only changes.

## Assets
- Copy 4 banners from `/images/` to `wwwroot/images/` with clean names:
  - `BANNER 2.jpg` -> `wwwroot/images/banner-2.jpg`
  - `EPR_BANNER.jpg` -> `wwwroot/images/epr-banner.jpg`
  - `POSTER WEB BANNER.png` -> `wwwroot/images/poster-web-banner.png`
  - `WEB BANNER VFT_1.png` -> `wwwroot/images/web-banner-vft1.png`
- Keep originals in `/images/` untouched as backup.
- Reference via `~/images/<clean-name>` with `Url.Content`.
- No `.csproj` change (SDK-style web project auto-includes `wwwroot`).
- No publish step by implementer; user publishes via desktop shortcut.

## Public homepage (`Views/Public/Index.cshtml`) — all images top to bottom
- Hero (`.portal-hero`) becomes a rotating carousel of all 5 images in this order:
  `banner-2.jpg`, `epr-banner.jpg`, `poster-web-banner.png`,
  `web-banner-vft1.png`, `Yakult_background.jpg` (last = fallback).
- Existing hero content (search, links, access panel) stays overlaid on top.
- Behavior: auto-advance ~6s, dots + prev/next arrows, pause on hover,
  honor `prefers-reduced-motion` (no auto-advance).
- Sections below reuse images so the page reads top-to-bottom:
  - Priority cards: small top thumbnails (`banner-2`, `epr-banner`).
  - Services/directory headers: thin banner strip (`poster-web-banner`).
  - Learning navy section: faded side art (`web-banner-vft1`).
  - Help banner: faded background (`epr-banner`).
- Contrast: reuse existing white-gradient overlay pattern from
  `wwwroot/css/public-portal.css` (~line 1000) so text stays readable.
- Styles go in `wwwroot/css/public-portal.css` only.

## Authenticated Home (`Views/Home/Index.cshtml`) — 2 images per section
- No carousel (keep fast for daily users).
- `.systems-grid` cards: thumbnail header cycling 2 widest banners
  (`web-banner-vft1`, `banner-2` by index % 2).
- Quick-Access mini cards: thumbnails cycling other 2
  (`epr-banner`, `poster-web-banner` by index % 2).
- Thumbs: `<img loading="lazy">` in fixed 16:9 container, no layout shift.
- Styles go in `wwwroot/css/site.css` only.

## Error handling
- Every new `<img>` gets `onerror="this.style.display='none'"` so a missing
  file degrades to the current flat design, never a broken-image icon.
- Carousel JS is vanilla and guarded by `if (slides.length > 1)`.
- No DB, config, or auth changes.

## Testing
- `dotnet build` on `Yakult.SystemsPortal`.
- Run locally: check Public `/` (carousel rotates, no console errors,
  text readable on all slides) and `/Home/Index` (thumbs load, lazy, no shift).
- Verify missing-file fallback by temporarily renaming one banner.
