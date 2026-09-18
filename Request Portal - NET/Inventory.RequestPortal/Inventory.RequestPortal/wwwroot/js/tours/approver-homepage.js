/**
 * Tour: approver-homepage
 * Page: Authorization/Landing  (Approver dashboard)
 * Audience: approvers only.
 *
 * This tour is NOT auto-started and NOT triggered by the "?" button directly.
 * Instead, the "?" button opens the User Guide Center modal (defined in Landing.cshtml).
 * Clicking "Homepage Overview" in that modal calls YakultTour.startTour('approver-homepage').
 */
(function () {
    'use strict';

    var TOUR_KEY = 'approver-homepage';

    function isMobile() { return window.innerWidth < 768; }

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'Welcome to the Approver Portal',
                description: 'This tour walks you through the Approver homepage. You\'ll learn how to navigate, access your modules, and get help at any time. Use <strong>Next</strong> and <strong>Back</strong> to move through each step.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. Portal brand ─────────────────────────────────────────────
        {
            element: '.dashboard-header .brand',
            popover: {
                title: 'Yakult Request Portal',
                description: 'You are using the <strong>Approver view</strong> of the Request Portal. This is your main hub for reviewing and signing cartridge requests from your department.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 3. Notification bell ────────────────────────────────────────
        {
            element: '#notifBellBtn',
            popover: {
                title: 'Notification Bell',
                description: 'You\'ll be alerted here whenever a new cartridge request from your department is waiting for your approval. A red badge shows how many unread notifications you have.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'end'
            }
        },

        // ── 4. Hamburger menu ───────────────────────────────────────────
        {
            element: '#hamburgerBtn',
            popover: {
                title: 'Account Menu',
                description: 'Click here to access your <strong>Account</strong> settings, <strong>Notification Settings</strong>, and <strong>Logout</strong>. Your name is displayed inside.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'end'
            }
        },

        // ── 5. Module grid heading ──────────────────────────────────────
        {
            element: '.dashboard-body h6',
            popover: {
                title: 'Your Available Modules',
                description: 'The cards below are the three main areas of your portal. Each one takes you to a different workflow.',
                side: 'bottom',
                align: 'center'
            }
        },

        // ── 6. Authorization Queue card ─────────────────────────────────
        {
            element: '.module-grid .module-card:nth-child(1)',
            popover: {
                title: 'Authorize Cartridge Requests',
                description: 'This is your primary module. When employees in your department submit cartridge requests, they appear here for you to <strong>review, sign, and approve</strong> — or reject. The red badge shows how many are currently waiting.',
                side: 'right',
                align: 'start'
            }
        },

        // ── 7. Submit Request card ──────────────────────────────────────
        {
            element: '.module-grid .module-card:nth-child(2)',
            popover: {
                title: 'Submit a Request',
                description: 'As an approver, you can also submit cartridge requests for yourself. Because you have approval authority, your request will appear in your own Authorization Queue — you can sign and authorize it immediately.',
                side: 'left',
                align: 'start'
            }
        },

        // ── 8. Authorization History card ───────────────────────────────
        {
            element: '.module-grid .module-card:nth-child(3)',
            popover: {
                title: 'Authorization History',
                description: 'View a complete record of all your past authorizations — both requests you submitted yourself and requests you signed for others in your department.',
                side: 'right',
                align: 'start'
            }
        },

        // ── 9. Help button ──────────────────────────────────────────────
        {
            element: '#approverHelpBtn',
            popover: {
                title: "That's It — You're All Set!",
                description: 'You can reopen this <strong>User Guide Center</strong> at any time by clicking the <i class="bi bi-question-circle"></i> help icon here. Each module also has its own contextual guide accessible from within that page.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'end'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        window.YakultTour.register(TOUR_KEY, steps);

        // The "?" help button on Landing opens the User Guide Center modal, not this tour.
        // This tour is launched from within the modal via startTour('approver-homepage').
        // We still wire startTourForPage so the "?" button works if the modal is somehow unavailable.
        window.YakultTour._setPageTourFn(function () {
            var modal = document.getElementById('userGuideCenterModal');
            if (modal && typeof bootstrap !== 'undefined') {
                bootstrap.Modal.getOrCreateInstance(modal).show();
            } else {
                window.YakultTour.startTour(TOUR_KEY);
            }
        });

        // Check for ?tour= query param (e.g. navigated here from a chain, unlikely for homepage)
        var params = new URLSearchParams(window.location.search);
        if (params.get('tour') === TOUR_KEY) {
            window.YakultTour.startTour(TOUR_KEY);
        }
    });
}());
