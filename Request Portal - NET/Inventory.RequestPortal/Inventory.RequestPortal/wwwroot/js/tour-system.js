/**
 * YakultTour — centralized guided-tour manager for the Request Portal.
 *
 * Design goals:
 *  • Reusable: any page registers its steps; the manager handles state.
 *  • Extensible: adding an approver or admin tour only requires a new
 *    registration call — no changes to this file.
 *  • Persistent: completion is stored in both localStorage (fast) and
 *    the server DB (durable across devices / cleared caches).
 *
 * Usage (per-page JS file):
 *   YakultTour.register('my-tour-key', steps, optionalOverrides);
 *   YakultTour.autoStart('my-tour-key');    // auto-launch if not done
 *   YakultTour.startTour('my-tour-key');    // force-launch (help button)
 */
(function (global) {
    'use strict';

    var REGISTRY = {}; // tourKey -> { steps, options }
    var LS_PREFIX = 'yakult_tour_';

    // Set to true only when the driver is destroyed from the LAST step
    // (i.e. user clicked Finish/Continue, not X or click-away).
    // Read by _advanceChain() to guard against accidental chain advances.
    var _lastTourReachedEnd = false;

    // ── localStorage helpers ───────────────────────────────────────────
    function lsKey(tourKey) { return LS_PREFIX + tourKey; }

    function isCompletedLocally(tourKey) {
        try { return localStorage.getItem(lsKey(tourKey)) === 'done'; }
        catch (e) { return false; }
    }

    function markCompletedLocally(tourKey) {
        try { localStorage.setItem(lsKey(tourKey), 'done'); }
        catch (e) { /* storage unavailable – ignore */ }
    }

    function clearLocalCompletion(tourKey) {
        try { localStorage.removeItem(lsKey(tourKey)); }
        catch (e) { /* ignore */ }
    }

    // ── Server helpers ─────────────────────────────────────────────────
    function serverHasCompleted(tourKey) {
        return fetch('/Tour/HasCompleted?tourKey=' + encodeURIComponent(tourKey), {
            credentials: 'same-origin'
        })
        .then(function (r) {
            if (!r.ok) {
                console.warn('[YakultTour] /Tour/HasCompleted returned HTTP ' + r.status + '. Tour DB table may not be created yet — run the migration script. Treating as not completed.');
                return { completed: false };
            }
            return r.json();
        })
        .then(function (data) {
            if (data.completed) markCompletedLocally(tourKey);
            return !!data.completed;
        })
        .catch(function (err) {
            console.warn('[YakultTour] Could not reach /Tour/HasCompleted:', err);
            return false;
        });
    }

    function serverMarkCompleted(tourKey) {
        // Fire-and-forget – localStorage is already updated before this call.
        fetch('/Tour/MarkCompleted', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: 'tourKey=' + encodeURIComponent(tourKey)
        }).catch(function () { /* non-fatal */ });
    }

    // ── Driver.js accessor ─────────────────────────────────────────────
    function getDriver() {
        // Driver.js v1.x IIFE sets:  this.driver = {}; this.driver.js = <module>;
        // So the correct path is window.driver.js.driver  (NOT window['driver.js'])
        var ns = global.driver && global.driver.js;
        if (!ns) {
            console.error('[YakultTour] Driver.js did not load — window.driver.js is undefined. Ensure ~/lib/driver.js/driver.js.iife.js is served.');
            return null;
        }
        if (typeof ns.driver !== 'function') {
            console.error('[YakultTour] Driver.js loaded but window.driver.js.driver is not a function. Actual keys:', Object.keys(ns));
            return null;
        }
        return ns.driver;
    }

    // ── Core: build and launch a Driver instance ───────────────────────
    function buildDriver(tourKey) {
        var entry = REGISTRY[tourKey];
        if (!entry) {
            console.warn('[YakultTour] No tour registered for key:', tourKey);
            return null;
        }

        var driverFn = getDriver();
        if (!driverFn) {
            console.warn('[YakultTour] Driver.js not loaded.');
            return null;
        }

        // Give the page a chance to prepare (e.g. inject demo data) before element filtering.
        if (entry.options && typeof entry.options.beforeStart === 'function') {
            entry.options.beforeStart();
        }

        // Filter out steps whose target element doesn't exist in the DOM.
        // Steps marked dynamic:true are exempt — their element is loaded async
        // (e.g. modal content fetched from server) and will exist by the time
        // the step is reached via onHighlightStarted Promise resolution.
        var steps = entry.steps.filter(function (step) {
            if (!step.element) return true; // popup/center steps have no element
            if (step.dynamic) return true;   // async-loaded elements: skip check
            return document.querySelector(step.element) !== null;
        });

        if (steps.length === 0) return null;

        var defaults = {
            showProgress: true,
            animate: true,
            overlayOpacity: 0.45,
            allowClose: true,
            showButtons: ['next', 'previous', 'close'],
            nextBtnText: 'Next →',
            prevBtnText: '← Back',
            doneBtnText: 'Finish'
        };

        var config = Object.assign({}, defaults, entry.options || {}, {
            steps: steps
        });

        // Capture and remove onDestroyed from the config so we can fire it
        // immediately inside onDestroyStarted (before the Driver.js fade-out
        // animation completes). This eliminates the ~300ms overlay animation
        // delay before chain navigation kicks in.
        var userOnDestroyed = config.onDestroyed || null;
        delete config.onDestroyed;

        var completed        = false;
        var destroyedFired   = false;

        function _fireDestroyed() {
            if (destroyedFired) return;
            destroyedFired = true;
            if (userOnDestroyed) userOnDestroyed();
        }

        config.onDestroyStarted = function () {
            // Check whether the user is on the last step before destroying.
            // getActiveIndex() returns undefined/null after destroy, so capture it now.
            try {
                var idx = driverObj.getActiveIndex();
                _lastTourReachedEnd = (typeof idx === 'number' && idx >= steps.length - 1);
            } catch (e) {
                _lastTourReachedEnd = false;
            }

            if (!completed) {
                completed = true;
                markCompletedLocally(tourKey);
                serverMarkCompleted(tourKey);
            }
            driverObj.destroy();
            // Fire immediately — don't wait for the overlay animation to finish.
            _fireDestroyed();
        };

        var driverObj = driverFn(config);
        return driverObj;
    }

    // ── Public API ─────────────────────────────────────────────────────

    /**
     * Register a tour.
     * @param {string}   tourKey  Unique key, e.g. 'user-new-request'
     * @param {Array}    steps    Driver.js step array
     * @param {Object}   options  Optional Driver.js config overrides
     */
    function register(tourKey, steps, options) {
        REGISTRY[tourKey] = { steps: steps, options: options || {} };
    }

    /**
     * Force-start a tour (used by the "?" help button). Ignores completion state.
     * @param {string} tourKey
     */
    function startTour(tourKey) {
        var driverObj = buildDriver(tourKey, true);
        if (driverObj) driverObj.drive();
    }

    /**
     * Auto-start a tour if the user hasn't completed it yet.
     * Checks localStorage first (fast path), then the server (for new devices).
     * @param {string} tourKey
     */
    function autoStart(tourKey) {
        if (isCompletedLocally(tourKey)) {
            console.log('[YakultTour] Tour "' + tourKey + '" already completed (localStorage). Skipping auto-start. Call YakultTour.resetCompletion("' + tourKey + '") to reset.');
            return;
        }

        serverHasCompleted(tourKey).then(function (done) {
            console.log('[YakultTour] Server says tour "' + tourKey + '" completed:', done);
            if (!done) startTour(tourKey);
        });
    }

    /**
     * Reset completion state so the tour can be relaunched automatically.
     * Useful during development / testing.
     * @param {string} tourKey
     */
    function resetCompletion(tourKey) {
        clearLocalCompletion(tourKey);
    }

    // Expose the page-specific "start this page's tour" slot so the
    // help button can call it without knowing the tour key.
    // Each page script sets:  YakultTour.startTourForPage = function() { ... };
    var startTourForPage = null;

    // ── Modal helpers (reusable for any modal-based tour step) ────────────
    //
    // Pattern for spotlighting content inside a Bootstrap modal:
    //   1. One step spotlights the trigger element (a row/button) — explains the action.
    //   2. The NEXT step's onHighlightStarted calls openModalForTour() — opens the modal
    //      instantly (no animation) so Driver.js can measure its elements immediately.
    //   3. Subsequent steps use element selectors inside the modal with side: 'left'
    //      and popoverClass: 'yakult-tour-aside' — tooltip lands in the left margin,
    //      never on top of the modal content.
    //   4. The final modal step's onDeselected calls closeModalForTour() to dismiss.

    function openModalForTour(modalId) {
        var el = document.getElementById(modalId);
        if (!el) return;
        // animation: false ensures the modal is immediately visible when Driver.js
        // measures element positions in the same tick as onHighlightStarted.
        bootstrap.Modal.getOrCreateInstance(el, { animation: false }).show();
    }

    function closeModalForTour(modalId) {
        var el = document.getElementById(modalId);
        if (!el) return;
        var m = bootstrap.Modal.getInstance(el);
        if (m) m.hide();
    }

    // ── Chain navigation bar ──────────────────────────────────────────────
    //
    // Injected as a child of the Driver.js popover, below the footer buttons,
    // with a thin gray border-top as a separator. A RAF loop re-injects it
    // whenever Driver.js rebuilds the popover between steps.

    var _chainNavActive   = false;
    var _chainNavRaf      = null;
    var _chainNavSkipLabel = '';
    var _chainNavOnSkip   = null;

    function _injectChainNav(popover) {
        var chain = null;
        try {
            var raw = sessionStorage.getItem(CHAIN_KEY);
            if (raw) chain = JSON.parse(raw);
        } catch (ex) {}
        var showBack = chain && chain.index > 0;

        var sm = window.innerWidth < 480;
        var pad     = sm ? '5px 10px 3px' : '8px 14px 4px';
        var gap     = sm ? '4px' : '6px';
        var fs      = sm ? '.72rem' : '.78rem';
        var btnPad  = sm ? '4px 8px'  : '5px 10px';

        var nav = document.createElement('div');
        nav.id = 'yakultChainNav';
        nav.style.cssText =
            'border-top:1px solid #e2e8f0;margin-top:6px;padding:' + pad + ';' +
            'display:flex;flex-direction:column;gap:' + gap + ';';

        // Row 1 — label
        var lbl = document.createElement('span');
        lbl.style.cssText =
            'color:#94a3b8;font-weight:600;font-size:.72rem;letter-spacing:.05em;';
        lbl.textContent = 'COMPLETE TOUR';
        nav.appendChild(lbl);

        // Row 2 — buttons (wrap on very small screens so long skip label never clips)
        var row = document.createElement('div');
        row.style.cssText =
            'display:flex;align-items:center;gap:' + gap + ';' +
            (sm ? 'flex-wrap:wrap;' : '');

        if (showBack) {
            var backBtn = document.createElement('button');
            backBtn.type = 'button';
            backBtn.style.cssText =
                'flex-shrink:0;background:none;border:1px solid #e2e8f0;border-radius:6px;' +
                'color:#475569;padding:' + btnPad + ';font-size:' + fs + ';cursor:pointer;white-space:nowrap;';
            backBtn.innerHTML = '← Back';
            backBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                _hideChainNav();
                _skipToPrevInChain();
            });
            row.appendChild(backBtn);
        }

        if (_chainNavSkipLabel) {
            var skipBtn = document.createElement('button');
            skipBtn.type = 'button';
            skipBtn.style.cssText =
                'flex:1;background:#d50032;color:#fff;border:none;border-radius:6px;' +
                'padding:' + btnPad + ';font-weight:700;font-size:' + fs + ';cursor:pointer;' +
                'white-space:nowrap;min-width:0;overflow:hidden;text-overflow:ellipsis;';
            skipBtn.textContent = _chainNavSkipLabel + ' →';
            skipBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                _hideChainNav();
                if (typeof _chainNavOnSkip === 'function') _chainNavOnSkip();
            });
            row.appendChild(skipBtn);
        }

        var exitBtn = document.createElement('button');
        exitBtn.type = 'button';
        exitBtn.style.cssText =
            'flex-shrink:0;background:none;border:1px solid #e2e8f0;border-radius:6px;' +
            'color:#94a3b8;padding:' + btnPad + ';font-size:' + fs + ';cursor:pointer;white-space:nowrap;';
        exitBtn.textContent = 'Exit';
        exitBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            _hideChainNav();
            try { sessionStorage.removeItem(CHAIN_KEY); } catch (ex) {}
        });
        row.appendChild(exitBtn);

        nav.appendChild(row);
        popover.appendChild(nav);
    }

    function _chainNavLoop() {
        if (!_chainNavActive) return;
        var popover = document.querySelector('.driver-popover');
        if (popover && !popover.querySelector('#yakultChainNav')) {
            _injectChainNav(popover);
        }
        _chainNavRaf = requestAnimationFrame(_chainNavLoop);
    }

    function _showChainNav(skipLabel, onSkip) {
        _chainNavSkipLabel = skipLabel;
        _chainNavOnSkip    = onSkip;
        _chainNavActive    = true;
        if (_chainNavRaf) cancelAnimationFrame(_chainNavRaf);
        _chainNavRaf = requestAnimationFrame(_chainNavLoop);
    }

    function _hideChainNav() {
        _chainNavActive = false;
        if (_chainNavRaf) { cancelAnimationFrame(_chainNavRaf); _chainNavRaf = null; }
        var existing = document.getElementById('yakultChainNav');
        if (existing) existing.remove();
    }

    // Force-navigate to the previous tour in the chain (Back button).
    function _skipToPrevInChain() {
        var raw;
        try { raw = sessionStorage.getItem(CHAIN_KEY); } catch (e) { return; }
        if (!raw) return;
        var chain;
        try { chain = JSON.parse(raw); } catch (e) { return; }
        var prev = chain.index - 1;
        if (prev < 0) return;
        chain.index = prev;
        try { sessionStorage.setItem(CHAIN_KEY, JSON.stringify(chain)); } catch (e) {}
        var prevUrl = CHAIN_URLS[chain.keys[prev]];
        if (prevUrl) window.location.href = prevUrl;
    }

    // Force-advance the chain (used by the Skip button — bypasses _lastTourReachedEnd).
    function _skipToNextInChain() {
        var raw;
        try { raw = sessionStorage.getItem(CHAIN_KEY); } catch (e) { return; }
        if (!raw) return;
        var chain;
        try { chain = JSON.parse(raw); } catch (e) { return; }
        var next = chain.index + 1;
        if (next >= chain.keys.length) {
            try { sessionStorage.removeItem(CHAIN_KEY); } catch (e) {}
            return;
        }
        chain.index = next;
        try { sessionStorage.setItem(CHAIN_KEY, JSON.stringify(chain)); } catch (e) {}
        var nextUrl = CHAIN_URLS[chain.keys[next]];
        if (nextUrl) window.location.href = nextUrl;
    }

    // ── Tour chain (Complete Tour — cross-page sequencing) ────────────────
    //
    // startChain(['approver-queue', 'approver-submit-request', 'approver-history'])
    //   stores the key list in sessionStorage and navigates to the first page.
    // Each page's tour script calls _advanceChain() in its onDestroyStarted hook
    //   when sessionStorage shows an active chain.

    var CHAIN_KEY = 'yakult_tour_chain';

    // Map tour keys to the URL that hosts them.
    var CHAIN_URLS = {
        'approver-homepage':        '/Authorization/Landing',
        'approver-queue':           '/Authorization/SupervisorQueue?tour=approver-queue',
        'approver-submit-request':  '/Request/Index?tour=approver-submit-request',
        'approver-history':         '/Authorization/History?tour=approver-history'
    };

    function startChain(tourKeys) {
        if (!tourKeys || !tourKeys.length) return;
        try {
            sessionStorage.setItem(CHAIN_KEY, JSON.stringify({ keys: tourKeys, index: 0 }));
        } catch (e) { /* storage unavailable */ }
        var firstUrl = CHAIN_URLS[tourKeys[0]];
        if (firstUrl) window.location.href = firstUrl;
    }

    function _advanceChain() {
        // Only advance if the user explicitly completed the last step.
        // If they clicked X / clicked away, _lastTourReachedEnd is false and
        // we stay on the current page — the chain remains in sessionStorage so
        // they can continue later if they wish.
        if (!_lastTourReachedEnd) return;

        var raw;
        try { raw = sessionStorage.getItem(CHAIN_KEY); } catch (e) { return; }
        if (!raw) return;
        var chain;
        try { chain = JSON.parse(raw); } catch (e) { return; }
        var next = chain.index + 1;
        if (next >= chain.keys.length) {
            try { sessionStorage.removeItem(CHAIN_KEY); } catch (e) {}
            return;
        }
        chain.index = next;
        try { sessionStorage.setItem(CHAIN_KEY, JSON.stringify(chain)); } catch (e) {}
        var nextUrl = CHAIN_URLS[chain.keys[next]];
        if (nextUrl) window.location.href = nextUrl;
    }

    function _isInChain(tourKey) {
        var raw;
        try { raw = sessionStorage.getItem(CHAIN_KEY); } catch (e) { return false; }
        if (!raw) return false;
        try {
            var chain = JSON.parse(raw);
            return chain.keys && chain.keys[chain.index] === tourKey;
        } catch (e) { return false; }
    }

    function _clearChain() {
        try { sessionStorage.removeItem(CHAIN_KEY); } catch (e) {}
    }

    global.YakultTour = {
        register: register,
        startTour: startTour,
        autoStart: autoStart,
        resetCompletion: resetCompletion,
        openModalForTour: openModalForTour,
        closeModalForTour: closeModalForTour,
        startChain: startChain,
        _advanceChain: _advanceChain,
        _skipToNextInChain: _skipToNextInChain,
        _skipToPrevInChain: _skipToPrevInChain,
        _isInChain: _isInChain,
        _clearChain: _clearChain,
        _showChainNav: _showChainNav,
        _hideChainNav: _hideChainNav,
        // Overwritten by each page's tour script:
        startTourForPage: function () {
            if (startTourForPage) startTourForPage();
        },
        _setPageTourFn: function (fn) { startTourForPage = fn; }
    };

}(window));
