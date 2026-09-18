/**
 * Tour: approver-history
 * Page: Authorization/History  (Authorization History — Approver view)
 * Audience: approvers only.
 *
 * The approver history page has two Bootstrap tabs:
 *   #myHistoryPane   — requests the approver submitted for themselves
 *   #signedPane      — authorizations the approver signed for others
 *
 * If both tables are empty, YakultTourDemo injects sample rows so every step
 * is reachable. The tour programmatically switches to the Signed tab mid-tour.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'approver-history';

    function isMobile() { return window.innerWidth < 768; }

    function needsHistoryDemo() {
        var myRows     = document.querySelectorAll('#myHistoryTbody tr');
        var signedRows = document.querySelectorAll('#signedTbody tr');
        return (myRows.length === 0 || signedRows.length === 0)
            && typeof window.YakultTourDemo !== 'undefined';
    }

    function switchToTab(tabId) {
        var tabEl = document.getElementById(tabId);
        if (tabEl && typeof bootstrap !== 'undefined') {
            bootstrap.Tab.getOrCreateInstance(tabEl).show();
        }
    }

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'Authorization History',
                description: 'This page shows a complete record of all your authorization activity — requests you submitted yourself and authorizations you signed for others. This tour walks through both views.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. Navigation menu ──────────────────────────────────────────
        {
            element: 'button[data-bs-target="#navSidebar"]',
            popover: {
                title: 'Navigation Menu',
                description: 'Tap the menu icon to open the sidebar. From here you can return to your <strong>Home</strong> dashboard, view your <strong>Authorization Queue</strong>, or switch between modules at any time.',
                side: isMobile() ? 'over' : 'right',
                align: isMobile() ? 'center' : 'start'
            }
        },

        // ── 3. Notification bell ────────────────────────────────────────
        {
            element: '#notifBellBtn',
            popover: {
                title: 'Notification Bell',
                description: 'You\'ll receive notifications here when new requests are waiting for your approval, and when requests you submitted move through the workflow.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'end'
            }
        },

        // ── 4. History tabs ─────────────────────────────────────────────
        {
            element: '#historyTabs',
            popover: {
                title: 'Two History Views',
                description: 'The <strong>My Authorization History</strong> tab shows cartridge requests you submitted for yourself. The <strong>Approvals I\'ve Signed</strong> tab shows requests from others that you authorized.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'start'
            }
        },

        // ── 5. My History tab ───────────────────────────────────────────
        {
            element: '#my-history-tab',
            popover: {
                title: 'My Authorization History',
                description: 'This tab lists every cartridge request you have ever submitted, along with its current status and the approver who signed it (or &ldquo;&mdash;&rdquo; if still pending).',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'start'
            }
        },

        // ── 6. My History table overview ────────────────────────────────
        {
            element: '#myHistoryPane',
            popover: {
                title: 'History Table',
                description: 'Each row is one request. Columns show the authorization ID, status badge, source (Web Portal or Desktop App), department, cartridge models, who signed it, date signed, and when it was originally requested.',
                side: isMobile() ? 'over' : 'top',
                align: isMobile() ? 'center' : 'start'
            }
        },

        // ── 7. First row ─────────────────────────────────────────────────
        {
            element: '#myHistoryTbody tr:first-child',
            dynamic: true,
            popover: {
                title: 'Authorization Record',
                description: 'Click any row to open a detail modal showing the full authorization — cartridge list, quantities, returned empties, and the signed document if the request was approved.',
                side: isMobile() ? 'over' : 'top',
                align: isMobile() ? 'center' : 'start'
            }
        },

        // ── 8. Status badge in row ───────────────────────────────────────
        {
            element: '#myHistoryTbody tr:first-child .badge',
            dynamic: true,
            popover: {
                title: 'Status Badges',
                description: '<span class="badge bg-warning text-dark">Pending</span> awaiting sign-off<br><span class="badge bg-success">Approved</span> signed and authorized<br><span class="badge bg-danger">Rejected</span> declined',
                side: isMobile() ? 'over' : 'right',
                align: isMobile() ? 'center' : 'start'
            }
        },

        // ── 9. Open detail modal ─────────────────────────────────────────
        // No element — floating centre popup so Driver.js doesn't spotlight
        // inside the Bootstrap modal (which causes its overlay to dim the modal content).
        // The modal opens while this step's popover is shown; steps 8 & 9 then
        // spotlight specific sections using the yakult-tour-aside class.
        {
            popover: {
                title: 'Authorization Detail Modal',
                description: 'Clicking a row opens this modal showing the complete authorization record — the requester, department, cartridge list, and the signed document with the approver\'s signature.',
                side: 'over',
                align: 'center'
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    var body = document.getElementById('authDetailBody');
                    if (body) {
                        body.innerHTML = window.YakultTourDemo
                            ? window.YakultTourDemo.getApproverHistoryDetailHtml()
                            : '<p class="p-3 text-muted">Loading&hellip;</p>';
                    }
                    window.YakultTour.openModalForTour('authDetailModal');
                    setTimeout(resolve, 180);
                });
            },
            onDeselected: function () {
                // Close the modal when navigating back to step 8 (Status Badges).
                // Steps 10+ re-open it via their own onHighlightStarted guard.
                window.YakultTour.closeModalForTour('authDetailModal');
            }
        },

        // ── 10. Info section ─────────────────────────────────────────────
        {
            element: '#authDetailInfoSection',
            dynamic: true,
            popover: {
                title: 'Authorization Info',
                description: 'This section shows the authorization ID, current status, department, request source, the date it was submitted, and who signed it.',
                side: 'left',
                align: 'start',
                popoverClass: 'yakult-tour-aside'
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    var modalEl = document.getElementById('authDetailModal');
                    var isOpen  = modalEl && modalEl.classList.contains('show');
                    if (!isOpen) {
                        var body = document.getElementById('authDetailBody');
                        if (body) {
                            body.innerHTML = window.YakultTourDemo
                                ? window.YakultTourDemo.getApproverHistoryDetailHtml()
                                : '<p class="p-3 text-muted">Loading&hellip;</p>';
                        }
                        switchToTab('my-history-tab');
                        window.YakultTour.openModalForTour('authDetailModal');
                    }
                    setTimeout(resolve, 180);
                });
            }
        },

        // ── 11. Cartridge section ────────────────────────────────────────
        {
            element: '#authDetailCartridgeSection',
            dynamic: true,
            popover: {
                title: 'Cartridge Details',
                description: 'The cartridge table lists every model that was requested, the quantities, and the returned empties. This is the same information the approver reviewed before signing.',
                side: 'left',
                align: 'start',
                popoverClass: 'yakult-tour-aside'
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    var modalEl = document.getElementById('authDetailModal');
                    var isOpen  = modalEl && modalEl.classList.contains('show');
                    if (!isOpen) {
                        var body = document.getElementById('authDetailBody');
                        if (body) {
                            body.innerHTML = window.YakultTourDemo
                                ? window.YakultTourDemo.getApproverHistoryDetailHtml()
                                : '<p class="p-3 text-muted">Loading&hellip;</p>';
                        }
                        switchToTab('my-history-tab');
                        window.YakultTour.openModalForTour('authDetailModal');
                    }
                    setTimeout(resolve, 180);
                });
            },
            onDeselected: function () {
                window.YakultTour.closeModalForTour('authDetailModal');
            }
        },

        // ── 12. Switch to Signed tab ─────────────────────────────────────
        {
            element: '#signed-tab',
            popover: {
                title: 'Approvals I\'ve Signed',
                description: 'This tab shows requests from employees in your department that <strong>you</strong> personally signed and authorized. Switch here to see your signing history.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'start'
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    switchToTab('signed-tab');
                    setTimeout(resolve, 250);
                });
            }
        },

        // ── 13. Signed pane overview ─────────────────────────────────────
        {
            element: '#signedPane',
            dynamic: true,
            popover: {
                title: 'Your Signed Authorizations',
                description: 'Each row here is a request you approved. Columns include the employee who requested, the department, cartridge models, the date submitted, and the date you signed it.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 14. First signed row ─────────────────────────────────────────
        {
            element: '#signedTbody tr:first-child',
            dynamic: true,
            popover: {
                title: 'Signed Record',
                description: 'Click any row here to navigate to the full authorization detail page, where you can see the signed document with your embedded signature.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 15. Help button ──────────────────────────────────────────────
        {
            element: 'button[title="Start Guided Tour"]',
            popover: {
                title: "That's It — You're All Set!",
                description: 'You can replay this History guide at any time using the <strong><i class="bi bi-question-circle"></i></strong> icon in the top-right corner of this page.',
                side: window.innerWidth < 768 ? 'over' : 'bottom',
                align: window.innerWidth < 768 ? 'center' : 'center'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        var inChain = window.YakultTour._isInChain(TOUR_KEY);

        // In chain mode: drop the standalone "That's It" step (last in steps[])
        // so "Complete Tour Finished!" is the single closing step — no double-ending.
        var finalSteps = inChain ? steps.slice(0, -1) : steps.slice();
        if (inChain) {
            finalSteps.push({
                popover: {
                    title: 'Complete Tour Finished!',
                    description: 'You\'ve completed the full Approver walkthrough — Authorization Queue, Submit Request, and Authorization History. You\'re ready to get started. Click <strong>Finish</strong> to close.',
                    side: 'over',
                    align: 'center'
                }
            });
            // doneBtnText stays as default "Finish" — this is the last tour in the chain.
        }

        window.YakultTour.register(TOUR_KEY, finalSteps, {
            beforeStart: function () {
                switchToTab('my-history-tab');
                if (needsHistoryDemo()) {
                    window.YakultTourDemo.injectApproverHistory();
                }
                // Re-check at start time so a stale sessionStorage entry from a
                // previously abandoned chain never injects the chain nav into a
                // standalone "?" tour.
                if (window.YakultTour._isInChain(TOUR_KEY)) {
                    // Last stop — no skip-forward destination, so pass empty label.
                    // _injectChainNav will show only the Back button.
                    window.YakultTour._showChainNav('', null);
                }
            },
            onDestroyed: function () {
                window.YakultTour._hideChainNav();
                window.YakultTour.closeModalForTour('authDetailModal');
                if (typeof window.YakultTourDemo !== 'undefined') {
                    window.YakultTourDemo.cleanupApproverHistory();
                }
                switchToTab('my-history-tab');
                // _advanceChain() clears the chain if this is the last entry.
                // It's a no-op if the user skipped rather than finishing.
                window.YakultTour._advanceChain();
            }
        });

        // Wire the "?" help button to this tour.
        // Clear any stale chain state first so the standalone guide never
        // shows the Complete Tour nav bar.
        window.YakultTour._setPageTourFn(function () {
            window.YakultTour._clearChain();
            window.YakultTour.startTour(TOUR_KEY);
        });

        // Auto-start if triggered via ?tour= param or chain
        var params = new URLSearchParams(window.location.search);
        if (params.get('tour') === TOUR_KEY || inChain) {
            window.YakultTour.startTour(TOUR_KEY);
        } else {
            window.YakultTour.autoStart(TOUR_KEY);
        }
    });
}());
