/**
 * Tour: approver-queue
 * Page: Authorization/SupervisorQueue  (Authorization Queue)
 * Audience: approvers only.
 *
 * If the queue is empty, YakultTourDemo injects 3 demo queue items and a
 * fully-rendered demo detail panel so every step is reachable.
 * All demo content is removed the moment the tour ends.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'approver-queue';

    function needsQueueDemo() {
        var real = document.querySelectorAll('.queue-item[data-id]:not([data-tour-demo])');
        return real.length === 0 && typeof window.YakultTourDemo !== 'undefined';
    }

    /* ── Mobile split-pane helpers ──────────────────────────────────────────
       On phones the left list and right detail are mutually exclusive.
       These helpers switch between the two views so tour steps always land
       on a visible, highlightable element.                                  */
    function isMobile() { return window.innerWidth < 768; }

    function mobileOpenDetail() {
        if (!isMobile()) return;
        var pane = document.querySelector('.split-pane');
        if (pane) pane.classList.add('detail-open');
    }

    function mobileCloseDetail() {
        var pane = document.querySelector('.split-pane');
        if (pane) pane.classList.remove('detail-open');
    }

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'Authorization Queue',
                description: 'This is where cartridge requests from your department arrive for your review. This tour walks you through how to find, review, and approve or reject requests. Use <strong>Next</strong> and <strong>Back</strong> to navigate.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. Navigation menu ──────────────────────────────────────────
        {
            element: 'button[data-bs-target="#navSidebar"]',
            popover: {
                title: 'Navigation Menu',
                description: 'Tap the menu icon to open the sidebar. From here you can return to your <strong>Home</strong> dashboard, view your <strong>Authorization History</strong>, or switch between modules at any time.',
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

        // ── 4. Filter bar overview ──────────────────────────────────────
        {
            element: '.queue-filter-bar',
            popover: {
                title: 'Search & Filter',
                description: 'Use these controls to quickly find a specific request. You can filter by employee name, cartridge model, date range, company, branch, or department.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 5. Search input ─────────────────────────────────────────────
        {
            element: '#filterSearch',
            popover: {
                title: 'Quick Search',
                description: 'Type any part of an employee name, department, or cartridge model number to instantly narrow the queue.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 6. Date From ────────────────────────────────────────────────
        {
            element: '#filterDateFrom',
            popover: {
                title: 'Date Range — From',
                description: 'Set the <strong>start date</strong> of the range. Only requests submitted on or after this date will be shown.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 7. Date To ──────────────────────────────────────────────────
        {
            element: '#filterDateTo',
            popover: {
                title: 'Date Range — To',
                description: 'Set the <strong>end date</strong> of the range. Use both fields together to isolate requests from a specific period — for example, the past week or a specific month.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 8. Company dropdown ─────────────────────────────────────────
        {
            element: '#filterCompany',
            popover: {
                title: 'Company Filter',
                description: 'Filter by company entity. Useful when your approval authority spans multiple companies.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 9. Branch dropdown ──────────────────────────────────────────
        {
            element: '#filterBranch',
            popover: {
                title: 'Branch Filter',
                description: 'Narrow the list to a specific branch or office location.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 10. Department dropdown ─────────────────────────────────────
        {
            element: '#filterDept',
            popover: {
                title: 'Department Filter',
                description: 'Filter by department. Combine this with Company and Branch to zero in on a specific team\'s requests.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 11. Left panel header + count ───────────────────────────────
        {
            element: '.split-left',
            popover: {
                title: 'Pending Requests',
                description: 'This panel lists all cartridge requests currently waiting for your authorization. The red badge shows the total count. Click any request to load its details on the right.',
                side: 'right',
                align: 'start'
            },
            onHighlightStarted: function () { mobileCloseDetail(); }
        },

        // ── 12. Status dot ──────────────────────────────────────────────
        {
            element: '.queue-item:first-child',
            popover: {
                title: 'Request Card & Status Indicator',
                description: 'Each card shows the employee name, department, requested models, and submission date. The coloured dot on the right indicates the approval stage:<br>' +
                    '<span style="color:#f9a825;">&#9679;</span> <strong>Yellow</strong> — Awaiting your sign-off (PendingSupervisor)<br>' +
                    '<span style="color:#1976d2;">&#9679;</span> <strong>Blue</strong> — Awaiting a manager (PendingManager)<br>' +
                    '<span style="color:#9e9e9e;">&#9679;</span> <strong>Grey</strong> — Awaiting coordinator',
                side: 'right',
                align: 'start'
            },
            onHighlightStarted: function () { mobileCloseDetail(); }
        },

        // ── 13. Detail panel overview — injected before this step ───────
        {
            element: '#detailsPanel',
            dynamic: true,
            popover: {
                title: 'Request Details Panel',
                description: 'When you click a request, the full details appear here — the employee info, cartridge table, your authorization preview, and the signature area. Scroll down within this panel to see and complete the form.',
                side: isMobile() ? 'over' : 'left',
                align: 'center'
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    mobileOpenDetail();
                    if (typeof window.YakultTourDemo !== 'undefined') {
                        window.YakultTourDemo.injectApproverDetailPanel();
                    }
                    setTimeout(resolve, 180);
                });
            }
        },

        // ── 14. Employee info card ──────────────────────────────────────
        {
            element: '#aqDemoInfoCard',
            dynamic: true,
            popover: {
                title: 'Requester Information',
                description: 'This card shows the employee who submitted the request, their department, the date and time it was submitted, and the authorization reference number.',
                side: isMobile() ? 'bottom' : 'left',
                align: 'start',
                popoverClass: 'yakult-tour-aside'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoInfoCard');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 15. Cartridge details table ─────────────────────────────────
        {
            element: '#aqDemoCartridgeTable',
            dynamic: true,
            popover: {
                title: 'Cartridge Details',
                description: 'Review the cartridge models and quantities being requested, along with the count of returned empty cartridges (good and damaged). Verify these details before signing.',
                side: isMobile() ? 'bottom' : 'left',
                align: 'start',
                popoverClass: 'yakult-tour-aside'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoCartridgeTable');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 16. Authorization statement preview ─────────────────────────
        {
            element: '#aqDemoPreview',
            dynamic: true,
            popover: {
                title: 'Authorization Statement Preview',
                description: 'This is a preview of the official authorization document that will be generated when you sign. It includes your name, position, department, and all cartridge details. Review it carefully.',
                side: isMobile() ? 'bottom' : 'left',
                align: 'start',
                popoverClass: 'yakult-tour-aside'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoPreview');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 17. Signature — Draw tab ────────────────────────────────────
        {
            element: '#aqDemoBtnTabDraw',
            dynamic: true,
            popover: {
                title: 'Draw Your Signature',
                description: 'The <strong>Draw</strong> tab lets you sign directly with your mouse or finger on the canvas below. This is the default method.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoBtnTabDraw');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 18. Signature — Upload tab ──────────────────────────────────
        {
            element: '#aqDemoBtnTabUpload',
            dynamic: true,
            popover: {
                title: 'Upload Your Signature',
                description: 'Prefer a pre-prepared signature? Switch to the <strong>Upload</strong> tab to attach an image file or a PDF containing your signature. It is embedded into the authorization document just like a drawn signature.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoBtnTabUpload');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 19. Clear button ────────────────────────────────────────────
        {
            element: '#aqDemoClearBtn',
            dynamic: true,
            popover: {
                title: 'Clear and Redo',
                description: 'Made a mistake? Click <strong>Clear</strong> to erase the canvas and start your signature over. The Clear button only appears on the Draw tab.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoClearBtn');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 20. Sign & Approve button ───────────────────────────────────
        {
            element: '#aqDemoApproveBtn',
            dynamic: true,
            popover: {
                title: 'Sign & Approve',
                description: 'Once you have reviewed all the details and drawn or uploaded your signature, click <strong>Sign &amp; Approve</strong> to finalize the authorization. The signed document is generated immediately and the request moves forward for processing.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoApproveBtn');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 21. Reject button ────────────────────────────────────────────
        {
            element: '#aqDemoRejectBtn',
            dynamic: true,
            popover: {
                title: 'Reject Request',
                description: 'If the request cannot be approved, click <strong>Reject Request</strong>. You will be prompted to provide a reason. <strong>This action cannot be undone</strong>, so be sure to review all details before rejecting.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                mobileOpenDetail();
                var el = document.getElementById('aqDemoRejectBtn');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 22. New items banner ────────────────────────────────────────
        {
            element: '#newItemsBanner',
            dynamic: true,
            popover: {
                title: 'Real-Time Updates',
                description: 'The queue polls the server every 10 seconds. If new requests arrive while you\'re on this page, a yellow banner appears at the top of the list — click it to refresh without losing your current view.',
                side: 'bottom',
                align: 'start'
            },
            onHighlightStarted: function () {
                mobileCloseDetail();
                var banner = document.getElementById('newItemsBanner');
                if (banner) banner.style.display = 'block';
            },
            onDeselected: function () {
                var banner = document.getElementById('newItemsBanner');
                if (banner) banner.style.display = 'none';
            }
        },

        // ── 23. You're all set — help button ────────────────────────────
        {
            element: '#approverQueueHelpBtn',
            popover: {
                title: "That's It — You're All Set!",
                description: 'You now know how the Authorization Queue works. Click the <i class="bi bi-question-circle"></i> icon here any time to reopen this guide and revisit any step.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'end'
            },
            onHighlightStarted: function () { mobileCloseDetail(); }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        var inChain = window.YakultTour._isInChain(TOUR_KEY);

        // When running as part of the Complete Tour, append an explicit transition
        // step so the user consciously clicks "Continue" rather than having the
        // next page load automatically on tour close/skip.
        // In chain mode: drop the standalone "That's It" step (last in steps[])
        // so "Tour Complete!" is the single closing step — no double-ending.
        var finalSteps = inChain ? steps.slice(0, -1) : steps.slice();
        var extraConfig = {};
        if (inChain) {
            finalSteps.push({
                popover: {
                    title: 'Authorization Queue — Tour Complete!',
                    description: 'You now know how to find, review, and sign authorization requests. Click <strong>Continue</strong> to move on to the <strong>Submit a Request</strong> walkthrough.',
                    side: 'over',
                    align: 'center'
                }
            });
            extraConfig.doneBtnText = 'Continue to Submit Request →';
        }

        window.YakultTour.register(TOUR_KEY, finalSteps, Object.assign(extraConfig, {
            beforeStart: function () {
                if (needsQueueDemo()) {
                    window.YakultTourDemo.injectApproverQueue();
                }
                // Re-check at start time so a stale sessionStorage entry from a
                // previously abandoned chain never injects the chain nav into a
                // standalone "?" tour.
                if (window.YakultTour._isInChain(TOUR_KEY)) {
                    window.YakultTour._showChainNav('Skip to Submit Request', function () {
                        if (typeof window.YakultTourDemo !== 'undefined') {
                            window.YakultTourDemo.cleanupApproverQueue();
                        }
                        window.YakultTour._skipToNextInChain();
                    });
                }
            },
            onDestroyed: function () {
                window.YakultTour._hideChainNav();
                mobileCloseDetail();
                if (typeof window.YakultTourDemo !== 'undefined') {
                    window.YakultTourDemo.cleanupApproverQueue();
                }
                // _advanceChain() is a no-op unless the user reached the last step.
                window.YakultTour._advanceChain();
            }
        }));

        // Wire the "?" help button to this tour.
        // Clear any stale chain state first so the standalone guide never
        // shows the Complete Tour nav bar.
        window.YakultTour._setPageTourFn(function () {
            window.YakultTour._clearChain();
            window.YakultTour.startTour(TOUR_KEY);
        });

        // Auto-start if triggered via ?tour= param (from UGC modal or chain)
        var params = new URLSearchParams(window.location.search);
        if (params.get('tour') === TOUR_KEY || inChain) {
            window.YakultTour.startTour(TOUR_KEY);
        } else {
            window.YakultTour.autoStart(TOUR_KEY);
        }
    });
}());
