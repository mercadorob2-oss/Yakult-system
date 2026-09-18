/**
 * Tour: user-auth-history
 * Page: Authorization/History  (non-approver view)
 * Audience: regular (non-approver, non-developer) users only — the Razor view gates rendering.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'user-auth-history';

    var steps = [

        // ── 1. Welcome ───────────────────────────────────────────────────
        {
            popover: {
                title: 'Authorization History',
                description: 'This page tracks the supervisor authorization status of every cartridge request you\'ve submitted. Let\'s walk through what each part means.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. History table card ────────────────────────────────────────
        {
            element: '#authHistoryCard',
            popover: {
                title: 'Your Authorization Records',
                description: 'Each row is one authorization request linked to a cartridge submission you made. The most recent entries appear at the top.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 3. Column headers ────────────────────────────────────────────
        {
            element: '#authHistoryHead',
            popover: {
                title: 'Understanding the Columns',
                description: '<strong>ID</strong> — unique authorization number.<br><strong>Status</strong> — current approval state.<br><strong>Source</strong> — Web Portal or Desktop App.<br><strong>Department</strong> — your department at time of request.<br><strong>Cartridge Models</strong> — what was requested.<br><strong>Signed By</strong> — which supervisor reviewed it.<br><strong>Date Signed</strong> — when it was reviewed.<br><strong>Requested On</strong> — when you submitted.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 4. Status column ─────────────────────────────────────────────
        {
            element: '#authHistoryStatusTh',
            popover: {
                title: 'Authorization Status',
                description: '<span class="badge bg-warning text-dark">Pending</span> — awaiting supervisor review.<br><span class="badge bg-success">Approved</span> — authorized by your supervisor.<br><span class="badge bg-danger">Rejected</span> — declined; contact your supervisor.<br><span class="badge bg-secondary">Used</span> — the authorization has been consumed by a fulfilled request.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 5. Row click to view details ─────────────────────────────────
        {
            element: '#nonApproverTbody tr',
            popover: {
                title: 'View Authorization Details',
                description: '<strong>Click any row</strong> to open a detailed view of that authorization — including the full approval chain, cartridge breakdown, and any supervisor remarks.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 6. Finish ────────────────────────────────────────────────────
        {
            popover: {
                title: "You're All Set!",
                description: 'You now know how to track your authorization requests. If you need a refresher, click the <strong><i class="bi bi-question-circle"></i></strong> button in the top-right header to replay this tour.',
                side: 'over',
                align: 'center'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        function needsDemo() {
            return !document.querySelector('#nonApproverTbody tr') && typeof window.YakultTourDemo !== 'undefined';
        }

        function buildOptions() {
            if (!needsDemo()) return {};
            return {
                beforeStart: function () { window.YakultTourDemo.injectAuthHistory(); },
                onDestroyed: function () { window.YakultTourDemo.cleanupAuthHistory(); }
            };
        }

        window.YakultTour.register(TOUR_KEY, steps, buildOptions());

        window.YakultTour._setPageTourFn(function () {
            window.YakultTour.register(TOUR_KEY, steps, buildOptions());
            window.YakultTour.startTour(TOUR_KEY);
        });

        window.YakultTour.autoStart(TOUR_KEY);
    });
}());
