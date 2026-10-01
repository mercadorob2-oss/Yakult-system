# Camera Auto-Pause for Learning Video Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Auto-pause the tracked learning video when the watcher walks away (no face visible for ~3s) and offer one-tap resume when they return, with zero behavior change when the camera is unavailable.

**Architecture:** Pure frontend addition inside the existing tracked-player script in `Content.cshtml`. A small presence engine (`getUserMedia` + tiny face detector at ~3fps) calls the existing `togglePlay()` / `ytPlayer.pauseVideo()` paths, so anti-skip (`anchorSec`), progress POST (`/Public/Progress`), and completion logic are untouched. Camera OFF by default; HTTP/no-camera/denied falls back to visibility+idle auto-pause that already works on insecure origins.

**Tech Stack:** Razor view + vanilla JS, `face-api.js` tiny-face-detector via CDN, existing CSS in `public-portal.css`, ASP.NET Core 8 (`net8.0`), no DB migration, no new packages.

---

## File map

- Modify: `Yakult.SystemsPortal/Views/Public/Content.cshtml:138-168` — camera toggle + preview markup inside the tracked `HostedVideo` player block and the tracked YouTube controls block (`:120-124`), plus presence-engine JS appended inside the existing `isTracked` IIFE (`:259-481`).
- Modify: `Yakult.SystemsPortal/wwwroot/css/public-portal.css:743` (append near `.course-player__controls`) — styles for toggle, preview pip, status pill. No other CSS files touched.
- Ops (no code): IIS HTTPS binding for the portal (e.g. `portal.yakult.ph`, same pattern as `cadena.yakult.ph`) so `navigator.mediaDevices` exists. Code feature-detects and degrades on HTTP.

---

### Task 1: Camera controls markup + styles (no behavior yet)

**Files:**
- Modify: `Yakult.SystemsPortal/Views/Public/Content.cshtml:142-153`
- Modify: `Yakult.SystemsPortal/wwwroot/css/public-portal.css:743`

- [ ] **Step 1: Add the toggle + preview markup to the hosted tracked player**

In `Yakult.SystemsPortal/Views/Public/Content.cshtml`, inside the `isTracked` hosted block right after the existing controls div (`courseRewind`/`courseTime`/`courseFill`), insert:

```html
<div class="course-camera-row">
    <button type="button" class="course-camera__toggle" id="courseCameraToggle" aria-pressed="false" title="Auto-pause when you walk away (uses your camera locally)">📷 Auto-pause: Off</button>
    <span class="course-camera__status" id="courseCameraStatus" role="status" aria-live="polite"></span>
</div>
<div class="course-camera__preview" id="courseCameraPreview" hidden>
    <video id="courseCameraVideo" muted playsinline width="160" height="120"></video>
</div>
```

Placement rule: inside `div.course-player` (`:142`), after `div.course-player__controls` (`:147-153`), before `div.course-status` (`:154`). For the tracked YouTube block (`:120-124`), insert the same snippet after `div.course-player__controls--yt` so both player kinds get the toggle with identical IDs (only one player kind renders per page, so IDs stay unique).

- [ ] **Step 2: Add the styles**

Append to `Yakult.SystemsPortal/wwwroot/css/public-portal.css` after the `.course-player__controls` rule (`:743`):

```css
.course-camera-row { display: flex; align-items: center; gap: 10px; padding: 8px 2px 0; }
.course-camera__toggle { border: 1px solid var(--portal-line); background: #fff; border-radius: 999px; padding: 6px 12px; font-size: 12.5px; cursor: pointer; }
.course-camera__toggle[aria-pressed="true"] { background: #0b1522; color: #fff; border-color: #0b1522; }
.course-camera__status { font-size: 12px; color: #5b6b7c; }
.course-camera__preview { margin-top: 8px; }
.course-camera__preview video { border-radius: 8px; border: 1px solid var(--portal-line); transform: scaleX(-1); }
```

- [ ] **Step 3: Verify markup renders with no JS errors**

Run: `dotnet build Yakult.SystemsPortal/Yakult.SystemsPortal.csproj -c Release`
Expected: `Build succeeded.` Then open any tracked course page, confirm the "Auto-pause: Off" button renders under the player on both a `HostedVideo` item and a `YouTube` item, and existing play/pause still works.

- [ ] **Step 4: Commit**

```bash
git add Yakult.SystemsPortal/Views/Public/Content.cshtml Yakult.SystemsPortal/wwwroot/css/public-portal.css
git commit -m "feat(learning): add camera auto-pause toggle markup and styles"
```

---

### Task 2: Presence engine — face-absent pause, face-present resume prompt

**Files:**
- Modify: `Yakult.SystemsPortal/Views/Public/Content.cshtml:259-481` (inside the `isTracked` IIFE, after the `cta` handler at `:389-397`)

- [ ] **Step 1: Add the engine skeleton (camera OFF until toggled)**

Insert this block after the `cta` click handler and before the `visibilitychange` handler (`:399`):

```html
<script>
/* appended inside the existing isTracked IIFE in Content.cshtml */
var camToggle = document.getElementById('courseCameraToggle');
var camStatus = document.getElementById('courseCameraStatus');
var camPreview = document.getElementById('courseCameraPreview');
var camVideo = document.getElementById('courseCameraVideo');
var camOn = false, camStream = null, camTimer = null, faceDetectorReady = false;
var ABSENT_MS = 3000, CHECK_MS = 350, lastFaceSeenAt = Date.now(), wasAutoPaused = false;

function camSetStatus(t) { if (camStatus) camStatus.textContent = t; }
function camIsSecure() { return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia); }

function camPauseForAbsence() {
    if (video && !video.paused) { video.pause(); wasAutoPaused = true; camSetStatus('Paused — you walked away'); }
    else if (ytPlayer && ytPlayer.pauseVideo && ytPlayer.getPlayerState && ytPlayer.getPlayerState() === 1) {
        try { ytPlayer.pauseVideo(); } catch (_) { }
        wasAutoPaused = true; camSetStatus('Paused — you walked away');
    }
}
function camOfferResume() {
    if (!wasAutoPaused) return;
    camSetStatus('Welcome back — press play to resume');
}
</script>
```

Behavior contract: `camPauseForAbsence` uses the same `pause` path as manual pause, so the existing `pause` listener (`:366`, saves progress) and YouTube polling stop run unchanged. No direct calls to `save()` here.

- [ ] **Step 2: Add toggle on/off + detector load**

```javascript
function camLoadDetector(done) {
    if (faceDetectorReady) return done(true);
    if (window.faceapi && window.faceapi.nets && window.faceapi.nets.tinyFaceDetector) {
        if (faceapi.nets.tinyFaceDetector.isLoaded) { faceDetectorReady = true; return done(true); }
    }
    var s = document.createElement('script');
    s.src = 'https://cdn.jsdelivr.net/npm/face-api.js@0.22.2/dist/face-api.min.js';
    s.onload = function () {
        faceapi.nets.tinyFaceDetector.loadFromUri('https://cdn.jsdelivr.net/npm/face-api.js@0.22.2/weights')
            .then(function () { faceDetectorReady = true; done(true); })
            .catch(function () { done(false); });
    };
    s.onerror = function () { done(false); };
    document.head.appendChild(s);
}

if (camToggle) camToggle.addEventListener('click', function () {
    if (!camOn) {
        if (!camIsSecure()) { camSetStatus('Camera needs HTTPS — use idle auto-pause instead'); return; }
        camLoadDetector(function (ok) {
            if (!ok) { camSetStatus('Face detector failed to load — use manual controls'); return; }
            navigator.mediaDevices.getUserMedia({ video: { width: 160, height: 120 }, audio: false })
                .then(function (stream) {
                    camStream = stream; camOn = true; wasAutoPaused = false;
                    camToggle.setAttribute('aria-pressed', 'true');
                    camToggle.textContent = '📷 Auto-pause: On';
                    if (camPreview) camPreview.hidden = false;
                    if (camVideo) { camVideo.srcObject = stream; camVideo.play().catch(function () { }); }
                    lastFaceSeenAt = Date.now();
                    camSetStatus('Watching for you…');
                    camTimer = window.setInterval(camTick, CHECK_MS);
                })
                .catch(function () { camSetStatus('Camera blocked — allow access or use manual controls'); });
        });
    } else {
        camStop('Auto-pause off');
    }
});

function camStop(msg) {
    camOn = false; wasAutoPaused = false;
    if (camTimer) { window.clearInterval(camTimer); camTimer = null; }
    if (camStream) { camStream.getTracks().forEach(function (t) { t.stop(); }); camStream = null; }
    if (camToggle) { camToggle.setAttribute('aria-pressed', 'false'); camToggle.textContent = '📷 Auto-pause: Off'; }
    if (camPreview) camPreview.hidden = true;
    if (msg) camSetStatus(msg);
}
```

Rules: stream is `160x120`, never recorded, never POSTed anywhere. Stopping releases all tracks (browser indicator turns off). Toggle text/pressed state is the only UI state.

- [ ] **Step 3: Add the sampling tick**

```javascript
function camTick() {
    if (!camOn || !camVideo || camVideo.readyState < 2 || !faceDetectorReady) return;
    faceapi.detectSingleFace(camVideo, new faceapi.TinyFaceDetectorOptions({ inputSize: 160, scoreThreshold: 0.4 }))
        .then(function (det) {
            if (!camOn) return;
            if (det) {
                var justBack = (Date.now() - lastFaceSeenAt) >= ABSENT_MS;
                lastFaceSeenAt = Date.now();
                if (justBack) camOfferResume();
            } else if (Date.now() - lastFaceSeenAt >= ABSENT_MS) {
                camPauseForAbsence();
            }
        })
        .catch(function () { });
}
```

Thresholds: `CHECK_MS=350` (~3fps, cheap), `ABSENT_MS=3000` (walk-away, not a blink). Resume is prompt-only, never auto-plays (browsers block autoplay with sound; auto-play would also fight the completion/anchor logic).

- [ ] **Step 4: Verify no regressions on HTTP (camera correctly refuses)**

Run: `dotnet build Yakult.SystemsPortal/Yakult.SystemsPortal.csproj -c Release`
Expected: `Build succeeded.` Then on the HTTP dev URL: click toggle → status reads `Camera needs HTTPS — use idle auto-pause instead`, player otherwise normal, progress POSTs still fire on manual pause.

- [ ] **Step 5: Commit**

```bash
git add Yakult.SystemsPortal/Views/Public/Content.cshtml
git commit -m "feat(learning): face-absent auto-pause engine with resume prompt"
```

---

### Task 3: HTTP fallback — visibility + idle auto-pause (works without camera)

**Files:**
- Modify: `Yakult.SystemsPortal/Views/Public/Content.cshtml:399-406` (same IIFE)

- [ ] **Step 1: Pause on tab-hide and window blur through the same path**

Extend the existing `visibilitychange` handler (`:399`) and add blur/idle:

```javascript
var idleTimer = null;
function armIdlePause() {
    if (idleTimer) window.clearTimeout(idleTimer);
    idleTimer = window.setTimeout(function () {
        if (video && !video.paused) video.pause();
        else if (ytPlayer && ytPlayer.pauseVideo && ytPlayer.getPlayerState && ytPlayer.getPlayerState() === 1) {
            try { ytPlayer.pauseVideo(); } catch (_) { }
        }
    }, 60000);
}
['mousemove', 'keydown', 'touchstart'].forEach(function (ev) {
    document.addEventListener(ev, armIdlePause, { passive: true });
});
window.addEventListener('blur', function () {
    if (video && !video.paused) video.pause();
});
armIdlePause();
```

This intentionally duplicates the camera's pause effect via the native `pause` event, so `save(true)` on pause (`:366`) and the anchor logic (`snapBackToAnchor`, `:330`) behave identically for camera, idle, and manual pauses. 60s idle is deliberately longer than the camera's 3s so the two never fight.

- [ ] **Step 2: Verify fallback on HTTP**

Run: `dotnet build Yakult.SystemsPortal/Yakult.SystemsPortal.csproj -c Release`
Expected: `Build succeeded.` Manual matrix: play hosted video → switch tab → returns paused with progress saved; idle 60s → pauses; Alt-Tab (blur) → pauses. YouTube item: same via `pauseVideo`.

- [ ] **Step 3: Commit**

```bash
git add Yakult.SystemsPortal/Views/Public/Content.cshtml
git commit -m "feat(learning): visibility and idle auto-pause fallback for HTTP"
```

---

### Task 4: Acceptance matrix (camera + no-camera, both player kinds)

- [ ] **Step 1: HTTPS camera matrix**

On an HTTPS host (or `localhost`): allow camera → toggle On, preview mirrors, status `Watching for you…`. Cover lens / walk out of frame 3s+ → pauses + `Paused — you walked away`. Uncover → `Welcome back — press play to resume`, press play → resumes from `anchorSec` (no skip-ahead possible). Deny permission → `Camera blocked` + manual controls unaffected. Toggle Off → browser camera indicator turns off.

- [ ] **Step 2: HTTP + YouTube matrix**

On `http://192.168.100.186:7016`: toggle shows HTTPS notice; hosted + YouTube items still play, pause, rewind 10s, resume from `My Learning` timestamp, and complete at ≥90% exactly as before. Confirm network tab still POSTs `{contentId, positionSec, durationSec}` to `/Public/Progress` (`PublicController.cs:301`) on pause.

- [ ] **Step 3: Commit nothing (verification only)** — file any deviation as a fixup commit before sign-off.

---

## Self-review

1. Spec coverage: (a) walk-away auto-pause → Tasks 2+3 (camera on HTTPS, visibility/idle on HTTP); anti-skip + progress intact → Task 2 reuses `pause` path, Task 4 verifies `/Public/Progress`; privacy (local-only, opt-in, indicator off on stop) → Task 2; HTTPS prerequisite → header ops note + `camIsSecure` guard; (b) gesture and (c) identity explicitly out of scope.
2. Placeholder scan: no TBD/TODO; all selectors (`courseCameraToggle`, `courseVideo`, `courseYoutube`, `courseRewind`, `courseCta`) match IDs already in `Content.cshtml`; `face-api.js@0.22.2` pinned with exact CDN paths.
3. Type consistency: `camTick`/`camPauseForAbsence`/`camOfferResume`/`camStop` signatures used identically across steps; `video`/`ytPlayer` reuse the IIFE's existing variables (`:263-275`), no new player instances.
