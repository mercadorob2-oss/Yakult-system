/**
 * Tour: user-auth-status
 * Page: Authorization/EmployeeStatus  (non-approver / regular user view)
 * Audience: all users who land on this page (approvers don't reach it).
 *
 * Modal sub-tour pattern:
 *   The auth detail modal fetches its body content from the server (async).
 *   Steps that spotlight elements inside #authDetailModal use:
 *     dynamic: true    — bypasses the "element exists at tour-start" filter
 *     side: 'left'     — tooltip appears in the left margin, outside the modal
 *     popoverClass: 'yakult-tour-aside'  — narrows the popover to fit
 *   The modal-intro step's onHighlightStarted returns a Promise: it manually
 *   fetches the first row's detail, populates #authDetailBody, and opens the
 *   modal without animation so Driver.js can measure positions immediately.
 */
(function () {
    'use strict';

    var TOUR_KEY    = 'user-auth-status';
    var MODAL_ID    = 'authDetailModal';
    var ASIDE_CLASS = 'yakult-tour-aside';

    var steps = [

        // ── 1. Welcome ───────────────────────────────────────────────────
        {
            popover: {
                title: 'Authorization Status',
                description: 'This page shows the supervisor authorization status for your cartridge requests. Let\'s walk through what each part means.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. Status banner ─────────────────────────────────────────────
        {
            element: '#empStatusBanner',
            popover: {
                title: 'Your Current Status',
                description: 'This banner reflects the status of your most recent request.<br><strong>Approved</strong> — your supervisor has authorized your request.<br><strong>Pending</strong> — waiting for supervisor review.<br><strong>Rejected</strong> — declined; contact your supervisor.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 3. Authorization history card ────────────────────────────────
        {
            element: '#empStatusCard',
            popover: {
                title: 'Authorization History',
                description: 'Every authorization request linked to your submissions is listed here — most recent first. You can see all past statuses, not just the latest.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 4. Column headers ────────────────────────────────────────────
        {
            element: '#empStatusHead',
            popover: {
                title: 'Understanding the Columns',
                description: '<strong>ID</strong> — unique authorization number.<br><strong>Status</strong> — current approval state.<br><strong>Source</strong> — Web Portal or Desktop App.<br><strong>Department</strong> — your department at time of request.<br><strong>Cartridge Models</strong> — what was requested.<br><strong>Signed By</strong> — which supervisor reviewed it.<br><strong>Date Signed</strong> — when it was reviewed.<br><strong>Requested On</strong> — when you submitted.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 5. Status column ─────────────────────────────────────────────
        {
            element: '#empStatusStatusTh',
            popover: {
                title: 'Authorization Status',
                description: '<span class="badge bg-warning text-dark">Pending</span> — awaiting supervisor review.<br><span class="badge bg-success">Approved</span> — authorized by your supervisor.<br><span class="badge bg-danger">Rejected</span> — declined; contact your supervisor.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 6. Row — explain single-click (modal not yet open) ───────────
        {
            element: '#empStatusTbody tr',
            popover: {
                title: 'View Authorization Details',
                description: '<strong>Click any row</strong> to open a detailed view of that authorization — including the full approval chain, cartridge breakdown, and supervisor signature info.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 7. Modal header — fetch content, open modal, introduce panel ─
        //   onHighlightStarted returns a Promise:
        //     1. Fetches the first row's detail from the server
        //     2. Populates #authDetailBody with the response HTML
        //     3. Opens the modal instantly (no animation) for Driver.js measurement
        //   Tooltip appears to the LEFT of the modal header, in the margin.
        {
            element: '#' + MODAL_ID + ' .modal-header',
            popover: {
                title: 'Authorization Detail Panel',
                description: 'Clicking a row opens this panel with the full picture of that authorization — who signed it, when, and what was requested.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    var firstRow = document.querySelector('#empStatusTbody tr[data-auth-id]');
                    if (!firstRow) {
                        window.YakultTour.openModalForTour(MODAL_ID);
                        resolve();
                        return;
                    }

                    var authId = firstRow.dataset.authId;
                    var status = firstRow.dataset.status || '';

                    document.getElementById('authDetailTitle').textContent =
                        '#' + authId + (status ? ' — ' + status : '');

                    // Demo row: use static sample HTML — no server fetch needed.
                    if (firstRow.dataset.tourDemo && window.YakultTourDemo) {
                        document.getElementById('authDetailBody').innerHTML =
                            window.YakultTourDemo.getAuthDetailHtml();
                        window.YakultTour.openModalForTour(MODAL_ID);
                        resolve();
                        return;
                    }

                    // Real row: fetch from server as normal.
                    document.getElementById('authDetailBody').innerHTML =
                        '<div class="text-center py-4"><span class="spinner-border text-danger" role="status"></span></div>';

                    window.YakultTour.openModalForTour(MODAL_ID);

                    fetch('/Authorization/EmployeeView/' + authId, { credentials: 'same-origin' })
                        .then(function (r) { return r.ok ? r.text() : null; })
                        .then(function (html) {
                            if (html) document.getElementById('authDetailBody').innerHTML = html;
                            resolve();
                        })
                        .catch(function () { resolve(); });
                });
            }
        },

        // ── 8. Info section — ID, status, department, dates ──────────────
        {
            element: '#authDetailInfoSection',
            dynamic: true,
            popover: {
                title: 'Authorization Info',
                description: 'This section shows the key fields: <strong>Authorization ID</strong>, <strong>Status</strong>, <strong>Department</strong>, <strong>Source</strong>, <strong>Requested On</strong>, <strong>Signed By</strong>, and <strong>Date Signed</strong>.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            }
        },

        // ── 9. Cartridge models section ───────────────────────────────────
        {
            element: '#authDetailCartridgeSection',
            dynamic: true,
            popover: {
                title: 'Cartridge Models',
                description: 'The table lists every model in this authorization, showing <strong>quantity requested</strong> and how many empties were returned — both good and damaged.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            }
        },

        // ── 10. Close button — last modal step, closes modal on deselect ──
        {
            element: '#' + MODAL_ID + ' .btn-close',
            popover: {
                title: 'Close the Panel',
                description: 'Click <strong>✕</strong> — or press <kbd>Esc</kbd> — to dismiss the detail panel and return to your authorization list.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onDeselected: function () {
                window.YakultTour.closeModalForTour(MODAL_ID);
            }
        },

        // ── 11. Help button — relaunch tour ──────────────────────────────
        {
            element: 'button[title="Start Guided Tour"]',
            popover: {
                title: "You're All Set!",
                description: 'You can replay this guide at any time by clicking the <strong><i class="bi bi-question-circle"></i></strong> button here in the top-right corner of the page.',
                side: 'bottom',
                align: 'end'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        function needsDemo() {
            return !document.querySelector('#empStatusCard') && typeof window.YakultTourDemo !== 'undefined';
        }

        function buildOptions() {
            if (!needsDemo()) return {};
            return {
                beforeStart: function () { window.YakultTourDemo.injectAuthStatus(); },
                onDestroyed: function () { window.YakultTourDemo.cleanupAuthStatus(); }
            };
        }

        window.YakultTour._setPageTourFn(function () {
            window.YakultTour.register(TOUR_KEY, steps, buildOptions());
            window.YakultTour.startTour(TOUR_KEY);
        });

        // The table content loads via AJAX — wait for it before auto-starting
        // so tour elements (#empStatusBanner, #empStatusCard, etc.) are in the DOM.
        document.addEventListener('empStatusLoaded', function () {
            window.YakultTour.register(TOUR_KEY, steps, buildOptions());
            window.YakultTour.autoStart(TOUR_KEY);
        }, { once: true });
    });
}());
