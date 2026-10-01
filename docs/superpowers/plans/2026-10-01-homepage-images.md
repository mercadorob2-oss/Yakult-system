# Homepage Images Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Utilize all 5 portal banner images across Public landing and authenticated Home pages.

**Architecture:** Copy 4 unused banners into `wwwroot/images/` with clean names; add a vanilla-JS carousel to the Public hero plus thumbnail/strip art per section; add lazy thumbnails to Home system and mini cards. CSS-only overlays preserve text contrast.

**Tech Stack:** ASP.NET Core 8 Razor (`cshtml`), vanilla CSS/JS, `dotnet build`.

---

### Task 1: Copy and verify image assets

**Files:**
- Copy from: `Yakult.SystemsPortal/images/BANNER 2.jpg`, `EPR_BANNER.jpg`, `POSTER WEB BANNER.png`, `WEB BANNER VFT_1.png`
- Create: `Yakult.SystemsPortal/wwwroot/images/banner-2.jpg`, `epr-banner.jpg`, `poster-web-banner.png`, `web-banner-vft1.png`
- Existing: `Yakult.SystemsPortal/wwwroot/images/Yakult_background.jpg` (untouched)

- [ ] **Step 1: Copy files with clean names**

```powershell
$src = "Yakult.SystemsPortal/images"
$dst = "Yakult.SystemsPortal/wwwroot/images"
Copy-Item "$src/BANNER 2.jpg" "$dst/banner-2.jpg" -Force
Copy-Item "$src/EPR_BANNER.jpg" "$dst/epr-banner.jpg" -Force
Copy-Item "$src/POSTER WEB BANNER.png" "$dst/poster-web-banner.png" -Force
Copy-Item "$src/WEB BANNER VFT_1.png" "$dst/web-banner-vft1.png" -Force
Get-ChildItem $dst | Select-Object Name, Length
```

- [ ] **Step 2: Verify all 5 files exist**

```powershell
@("banner-2.jpg","epr-banner.jpg","poster-web-banner.png","web-banner-vft1.png","Yakult_background.jpg") | ForEach-Object { Test-Path "Yakult.SystemsPortal/wwwroot/images/$_" }
```

Expected: `True` five times.

---

### Task 2: Public hero carousel Razor

**Files:**
- Modify: `Yakult.SystemsPortal/Views/Public/Index.cshtml:4-41` (hero `<section class="portal-hero">` block)

- [ ] **Step 1: Insert carousel slides as first child of `.portal-hero`**

```html
<div class="portal-carousel" aria-hidden="true">
    <img class="portal-carousel__slide is-active" src="@Url.Content("~/images/banner-2.jpg")" alt="" onerror="this.style.display='none'" />
    <img class="portal-carousel__slide" src="@Url.Content("~/images/epr-banner.jpg")" alt="" loading="lazy" onerror="this.style.display='none'" />
    <img class="portal-carousel__slide" src="@Url.Content("~/images/poster-web-banner.png")" alt="" loading="lazy" onerror="this.style.display='none'" />
    <img class="portal-carousel__slide" src="@Url.Content("~/images/web-banner-vft1.png")" alt="" loading="lazy" onerror="this.style.display='none'" />
    <img class="portal-carousel__slide" src="@Url.Content("~/images/Yakult_background.jpg")" alt="" loading="lazy" onerror="this.style.display='none'" />
</div>
<div class="portal-carousel__dots" aria-hidden="true">
    <button type="button" data-slide="0" class="is-active" tabindex="-1"></button>
    <button type="button" data-slide="1" tabindex="-1"></button>
    <button type="button" data-slide="2" tabindex="-1"></button>
    <button type="button" data-slide="3" tabindex="-1"></button>
    <button type="button" data-slide="4" tabindex="-1"></button>
</div>
```

Placement: directly inside `<section class="portal-hero">`, before `<div class="portal-container portal-hero__grid">`. Keep existing grid content unchanged above the carousel via `position: relative; z-index: 1`.

- [ ] **Step 2: Append carousel JS at end of file (after existing sections, before end)**

```html
<script>
(() => {
    const slides = Array.from(document.querySelectorAll('.portal-carousel__slide'));
    const dots = Array.from(document.querySelectorAll('.portal-carousel__dots button'));
    if (slides.length < 2) return;
    let current = 0, timer = null;
    const show = (i) => {
        slides[current].classList.remove('is-active');
        dots[current]?.classList.remove('is-active');
        current = (i + slides.length) % slides.length;
        slides[current].classList.add('is-active');
        dots[current]?.classList.add('is-active');
    };
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (!reduceMotion) {
        timer = setInterval(() => show(current + 1), 6000);
    }
    dots.forEach((d, i) => d.addEventListener('click', () => { show(i); }));
    const hero = document.querySelector('.portal-hero');
    hero?.addEventListener('mouseenter', () => timer && clearInterval(timer));
    hero?.addEventListener('mouseleave', () => {
        if (!reduceMotion && !timer) timer = setInterval(() => show(current + 1), 6000);
    });
})();
</script>
```

---

### Task 3: Public carousel + section art CSS

**Files:**
- Modify: `Yakult.SystemsPortal/wwwroot/css/public-portal.css` (append at end, after line 1042)

- [ ] **Step 1: Append carousel and section art styles**

```css
/* ── Homepage image utilization (2026-10-01) ── */
.portal-hero { position: relative; }
.portal-carousel__slide {
    position: absolute; inset: 0; width: 100%; height: 100%;
    object-fit: cover; opacity: 0; transition: opacity 1s ease; z-index: 0;
}
.portal-carousel__slide.is-active { opacity: 1; }
.portal-hero__grid, .portal-hero__panel { position: relative; z-index: 1; }
.portal-hero::after {
    content: ""; position: absolute; inset: 0; z-index: 0; pointer-events: none;
    background: linear-gradient(100deg, rgba(255,255,255,.98) 0%, rgba(255,255,255,.94) 42%, rgba(255,255,255,.35) 68%, rgba(255,255,255,.05) 100%);
}
.portal-carousel__dots { position: absolute; bottom: 14px; left: 50%; transform: translateX(-50%); display: flex; gap: 8px; z-index: 2; }
.portal-carousel__dots button { width: 10px; height: 10px; border-radius: 50%; border: 0; background: rgba(11,21,34,.25); cursor: pointer; padding: 0; }
.portal-carousel__dots button.is-active { background: var(--portal-red, #a90f16); }
.portal-priority-card__thumb { display: block; width: 100%; height: 120px; object-fit: cover; border-radius: 8px 8px 0 0; }
.portal-section--navy .learning-grid { position: relative; }
.portal-help-banner { position: relative; overflow: hidden; }
.portal-help-banner::before {
    content: ""; position: absolute; inset: 0; z-index: 0; opacity: .14;
    background: url('../images/epr-banner.jpg') center / cover no-repeat;
}
.portal-help-banner > * { position: relative; z-index: 1; }
```

- [ ] **Step 2: Verify CSS was appended**

```powershell
Get-Content Yakult.SystemsPortal/wwwroot/css/public-portal.css -Tail 5
```

Expected: last lines show the `.portal-help-banner > *` rule.

---

### Task 4: Public section thumbnails Razor

**Files:**
- Modify: `Yakult.SystemsPortal/Views/Public/Index.cshtml:60-80` (priority cards), `:111-125` (learning navy), `:172-175` (help banner)

- [ ] **Step 1: Add thumbnails to the 3 priority cards**

Card 1 (Services) insert as first child of `<a class="portal-priority-card" asp-action="Services">`:
```html
<img class="portal-priority-card__thumb" src="@Url.Content("~/images/banner-2.jpg")" alt="" loading="lazy" onerror="this.style.display='none'" />
```
Card 2 (Employee resources) first child of `<a class="portal-priority-card" asp-route="EmployeeResourcesIndex">`:
```html
<img class="portal-priority-card__thumb" src="@Url.Content("~/images/epr-banner.jpg")" alt="" loading="lazy" onerror="this.style.display='none'" />
```
Card 3 (IT Help) first child of `<a class="portal-priority-card" asp-action="Help">`:
```html
<img class="portal-priority-card__thumb" src="@Url.Content("~/images/poster-web-banner.png")" alt="" loading="lazy" onerror="this.style.display='none'" />
```

- [ ] **Step 2: Add faded side art to learning navy section**

Insert directly inside `<section class="portal-section portal-section--navy">` before `<div class="portal-container">`:
```html
<img src="@Url.Content("~/images/web-banner-vft1.png")" alt="" loading="lazy" aria-hidden="true" style="position:absolute;right:0;top:0;height:100%;opacity:.12;object-fit:cover;pointer-events:none;" onerror="this.style.display='none'" />
```

---

### Task 5: Authenticated Home card thumbnails Razor

**Files:**
- Modify: `Yakult.SystemsPortal/Views/Home/Index.cshtml:187-194` (system card `.card-visual`), `:282-287` (mini card icon block)

- [ ] **Step 1: System cards — thumbnail header cycling 2 banners**

Replace inside `<div class="card-visual">`, before `<div class="card-icon">`:
```html
@{
    var thumbImages = new[] { "web-banner-vft1.png", "banner-2.jpg" };
    var thumb = thumbImages[cardIndex % thumbImages.Length];
}
<img class="system-card__thumb" src="@Url.Content("~/images/" + thumb)" alt="" loading="lazy" onerror="this.style.display='none'" />
```
Note: `cardIndex` is already incremented per card in the existing loop (line 174-185), so `% 2` alternates banners across cards.

- [ ] **Step 2: Mini cards — thumbnails cycling other 2 banners**

Replace `<div class="mini-card-icon">...</div>` block opener by inserting before it:
```html
@{
    var miniThumbs = new[] { "epr-banner.jpg", "poster-web-banner.png" };
}
```
Inside the `@foreach (var mini in Model.MiniCards)` loop, track index with the existing pattern (declare `@{ var miniIndex = 0; }` before loop, use `miniThumbs[miniIndex % miniThumbs.Length]`, increment `miniIndex++` at loop end):
```html
<img class="mini-card__thumb" src="@Url.Content("~/images/" + miniThumbs[miniIndex % miniThumbs.Length])" alt="" loading="lazy" onerror="this.style.display='none'" />
```

---

### Task 6: Home thumbnail CSS

**Files:**
- Modify: `Yakult.SystemsPortal/wwwroot/css/site.css` (append at end)

- [ ] **Step 1: Append thumb styles**

```css
/* ── Homepage image utilization (2026-10-01) ── */
.system-card__thumb { display: block; width: 100%; aspect-ratio: 16 / 9; object-fit: cover; border-radius: 12px; }
.mini-card__thumb { display: block; width: 100%; aspect-ratio: 16 / 9; object-fit: cover; border-radius: 10px 10px 0 0; }
```

---

### Task 7: Build and verify

**Files:** none (verification only)

- [ ] **Step 1: Build**

```powershell
dotnet build Yakult.SystemsPortal/Yakult.SystemsPortal.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 2: Run locally and browse**

```powershell
dotnet run --project Yakult.SystemsPortal/Yakult.SystemsPortal.csproj
```

Expected: Public `/` shows hero carousel rotating through 5 slides with dots; text readable; priority cards show thumbs; `/Home/Index` (after login) shows system/mini card thumbs; browser console has no 404s for `/images/`. Temporarily rename one banner to confirm `onerror` fallback degrades gracefully, then restore.
