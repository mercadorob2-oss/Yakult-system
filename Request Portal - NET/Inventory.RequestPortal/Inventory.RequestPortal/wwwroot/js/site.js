// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// The nav-tabs-container tab strip (New Request / My Submitted Requests / IT Assisted
// Request / ...) scrolls horizontally with its scrollbar hidden, so on narrow screens
// the active tab you just clicked can land partially off-screen with no visual way to
// tell you need to swipe. Scroll it fully into view automatically instead.
function scrollActiveTabIntoView() {
    document.querySelectorAll('.nav-tabs-container .nav-tabs').forEach(function (tabs) {
        var active = tabs.querySelector('.nav-link.active');
        if (active) {
            active.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }
    });
}
document.addEventListener('DOMContentLoaded', scrollActiveTabIntoView);

// The ::after fade cue on .nav-tabs-container (site.css) hints that its tab row
// scrolls horizontally. Toggle it on only when the tabs actually overflow their
// container — otherwise (e.g. a page with a single "History" tab) it paints an
// unexplained white patch over the container's right edge with nothing to scroll to.
function updateNavTabsScrollableState() {
    document.querySelectorAll('.nav-tabs-container').forEach(function (container) {
        var tabs = container.querySelector('.nav-tabs');
        if (!tabs) return;
        container.classList.toggle('is-scrollable', tabs.scrollWidth > tabs.clientWidth + 1);
    });
}
document.addEventListener('DOMContentLoaded', updateNavTabsScrollableState);
window.addEventListener('resize', updateNavTabsScrollableState);

// ── AJAX tab-switching for the Request Portal (New Request / My Submitted Requests /
// IT Assisted Request) ──────────────────────────────────────────────────────────────
// These three tabs are all GET actions on RequestController and share the exact same
// page shape (.portal-header + .content-padded, from _Layout.cshtml), so switching
// between them can swap just .content-padded instead of a full page reload/flash.
// Other nav-tabs-container links (Authorization Queue, Authorization, etc.) point at a
// different controller/page shape and are deliberately left to navigate normally —
// isSwappableTabLink() below is what tells the two apart.
//
// Each of the three views' page-specific <script>/<style> content lives inside a
// #pageScripts wrapper (added specifically for this), because Razor's @section Scripts
// renders at the very end of <body> — outside .content-padded — so it needs its own
// marker to be found and swapped independently. Each page's main inline <script> is
// also wrapped in an IIFE rather than a document.addEventListener('DOMContentLoaded', ...)
// handler: DOMContentLoaded only fires once per document, so a handler registered by a
// script re-injected after that point would simply never run; an IIFE runs immediately
// (safe here since these scripts sit after all the markup they reference) both on the
// original full page load and every time this code re-injects the script afterward.
(function () {
    var TAB_PATH_RE = /^\/Request\/(Index|MyRequests|AssistedRequest)\/?$/i;

    function isSwappableTabLink(a) {
        if (!a || a.target === '_blank') return false;
        var url;
        try { url = new URL(a.href, window.location.href); } catch (e) { return false; }
        if (url.origin !== window.location.origin) return false;
        return TAB_PATH_RE.test(url.pathname);
    }

    // Cloning nodes (or setting innerHTML) never executes <script> elements — the DOM
    // spec only runs a script tag when it's inserted by the parser or freshly created via
    // document.createElement. Rebuild each one so the browser actually runs it.
    function reExecuteScripts(root) {
        Array.prototype.slice.call(root.querySelectorAll('script')).forEach(function (oldScript) {
            var newScript = document.createElement('script');
            // A script element created via createElement() defaults `async` to true
            // when it has a src — unlike one parsed from HTML, which defaults to false.
            // Force it off so external scripts with a src (jquery.validate, then
            // jquery.validate.unobtrusive, which depends on it) still execute in the
            // original document order instead of whichever happens to finish loading first.
            newScript.async = false;
            for (var i = 0; i < oldScript.attributes.length; i++) {
                var attr = oldScript.attributes[i];
                newScript.setAttribute(attr.name, attr.value);
            }
            newScript.textContent = oldScript.textContent;
            oldScript.parentNode.replaceChild(newScript, oldScript);
        });
    }

    function swapInDocument(html, url) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        var newContent = doc.querySelector('.content-padded');
        var newScripts = doc.getElementById('pageScripts');
        var curContent = document.querySelector('.content-padded');

        if (!newContent || !curContent) {
            // Response didn't have the shape we expected (login redirect, error page,
            // etc.) — fall back to a real navigation instead of showing something broken.
            window.location.href = url;
            return;
        }

        document.title = doc.title;
        curContent.innerHTML = newContent.innerHTML;

        var curScripts = document.getElementById('pageScripts');
        if (curScripts) curScripts.remove();
        if (newScripts) {
            var container = document.createElement('div');
            container.id = 'pageScripts';
            container.style.display = 'none';
            container.innerHTML = newScripts.innerHTML;
            document.body.appendChild(container);
            reExecuteScripts(container);
        }

        // Re-wire jQuery unobtrusive validation against whatever form just landed —
        // it only auto-parses the forms present when it first loaded, so a form injected
        // afterward needs to be parsed explicitly. Harmless no-op on tabs with no
        // validated form (My Submitted Requests).
        if (window.jQuery && jQuery.validator && jQuery.validator.unobtrusive) {
            jQuery(curContent).find('form').each(function () {
                jQuery.validator.unobtrusive.parse(this);
            });
        }

        scrollActiveTabIntoView();
        window.scrollTo(0, 0);
    }

    function loadTab(url, pushHistory) {
        fetch(url, { credentials: 'same-origin' })
            .then(function (res) {
                if (!res.ok) throw new Error('Tab request failed: ' + res.status);
                return res.text();
            })
            .then(function (html) {
                swapInDocument(html, url);
                if (pushHistory) history.pushState({ ajaxTab: true, url: url }, '', url);
            })
            .catch(function () {
                // Network error or unexpected response — just do a real navigation.
                window.location.href = url;
            });
    }

    document.addEventListener('click', function (e) {
        var a = e.target.closest('.nav-tabs-container .nav-link');
        if (!a || e.defaultPrevented) return;
        if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        if (a.classList.contains('active')) { e.preventDefault(); return; } // already here
        if (!isSwappableTabLink(a)) return; // different controller/page shape — navigate normally
        e.preventDefault();
        loadTab(a.href, true);
    });

    window.addEventListener('popstate', function (e) {
        if (e.state && e.state.ajaxTab) {
            loadTab(e.state.url, false);
        }
    });
})();
