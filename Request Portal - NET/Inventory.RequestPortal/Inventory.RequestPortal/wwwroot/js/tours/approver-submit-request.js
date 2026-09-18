/**
 * Tour: approver-submit-request
 * Page: Request/Index  (New Request form — Approver view)
 * Audience: approvers only — the Razor view gates rendering.
 *
 * Approvers can submit requests for themselves. Because they have approval
 * authority, after submitting their request will appear in their own
 * Authorization Queue where they can sign and authorize it immediately.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'approver-submit-request';

    function isMobile() { return window.innerWidth < 768; }

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'Submitting a Request as an Approver',
                description: 'As an approver, you can submit cartridge requests for yourself just like any other employee. The key difference: once submitted, you are taken <strong>directly to your Authorization Queue</strong> where your request is already selected and ready for you to review and sign — no need to search for it manually.',
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

        // ── 4. Cartridge table ──────────────────────────────────────────
        {
            element: '.cartridge-table',
            popover: {
                title: 'Cartridge Request Table',
                description: 'Each row in this table represents one cartridge model you want to request. You can include multiple different models in a single submission.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 5. Model number input ───────────────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-row:first-child input[name$=".CartridgeModel"]',
            popover: {
                title: 'Cartridge Model Number',
                description: 'Type the model number here — for example <em>HP 85A</em> or <em>Canon 051H</em>. This is the primary field; whatever you type is what gets recorded on the request.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 6. Available models dropdown ────────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-row:first-child select.model-ref-select',
            popover: {
                title: 'Available Models Reference',
                description: 'Use this dropdown as a guide to find known models. Selecting one auto-fills the model number field. It\'s optional — you can always type directly.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 7. Quantity ─────────────────────────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-row:first-child input.qty-number-input',
            popover: {
                title: 'Quantity Requested',
                description: 'Enter how many cartridges you need for this model. The maximum is <strong>3 per model</strong> per submission.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 8. Returned empty cartridges ────────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-return-row:first-child',
            popover: {
                title: 'Returned Empty Cartridges',
                description: 'When collecting new cartridges, you return your old empties. Enter how many are <strong>Good</strong> and how many are <strong>Damaged</strong>. The combined total cannot exceed your requested quantity.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 9. Add row ──────────────────────────────────────────────────
        {
            element: '#addRowBtn',
            popover: {
                title: 'Request Multiple Models',
                description: 'Need more than one cartridge type? Click here to add another row. Each row is independent — you can request up to 3 of each model in a single submission.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 10. Employee picker (dept accounts only) ─────────────────────
        {
            element: '#deptEmpPickerWrapper',
            popover: {
                title: 'Employee in Session',
                description: 'This account is shared by your entire department. Before submitting, you must identify <strong>which employee</strong> is making this specific request.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 11. Employee search input ────────────────────────────────────
        {
            element: '#deptEmpInput',
            popover: {
                title: 'Search for Employee',
                description: 'Start typing a name to search. Select the correct person from the dropdown. <strong>This field is required</strong> before the form can be submitted.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 12. Distribution method ──────────────────────────────────────
        {
            element: '#distributionMethodRow',
            popover: {
                title: 'Distribution Method',
                description: 'Choose how you\'ll receive your cartridges:<br><strong>PICKUP</strong> — you or a representative collects them at the IT office.<br><strong>DELIVERY</strong> — IT delivers directly to your branch.',
                side: 'bottom',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('distributionMethodRow');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 13. Received by ──────────────────────────────────────────────
        {
            element: '#receivedByRow',
            popover: {
                title: 'To Be Received By',
                description: 'When PICKUP is selected, specify who will collect the cartridges at the IT office.<br><br>Use the <strong>Company</strong>, <strong>Branch</strong>, and <strong>Department</strong> dropdowns to narrow the list, then search by name in the field below. Click <strong>×</strong> to clear all filters at once.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('receivedByRow');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 14. Distribution preview ─────────────────────────────────────
        {
            element: '#distributionPreview',
            popover: {
                title: 'Distribution Summary',
                description: 'This preview confirms your selected distribution method and the designated receiver. Double-check this before submitting.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('distributionPreview');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 15. Additional remarks ────────────────────────────────────────
        {
            element: 'textarea[name="Request.AdditionalRemarks"]',
            popover: {
                title: 'Additional Remarks',
                description: 'Optionally add any special instructions or context — for example: <em>"Urgent — needed for client meeting Friday."</em>',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.querySelector('textarea[name="Request.AdditionalRemarks"]');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 16. Submit + self-sign redirect ──────────────────────────────
        {
            element: 'button.btn-submit',
            popover: {
                title: 'Submit Your Request',
                description: 'Once everything is filled in, click <strong>Submit Request</strong>. Because you are an approver, you are taken directly to your <strong>Authorization Queue</strong> with your request already open in the right panel — ready for you to review the cartridge details and sign right away.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.querySelector('button.btn-submit');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 17. You land on the Authorization Queue ───────────────────────
        {
            popover: {
                title: 'You Land on Your Authorization Queue',
                description: 'After submitting, you are redirected to your <strong>Authorization Queue</strong> where your new request is automatically highlighted and loaded in the detail panel. You can sign it immediately or close the panel and return to it later.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 18. Help button ───────────────────────────────────────────────
        {
            element: 'button[title="Start Guided Tour"]',
            popover: {
                title: "That's It — You're Ready!",
                description: 'You can replay this guide at any time using the <strong><i class="bi bi-question-circle"></i></strong> icon in the top-right corner of this page.',
                side: isMobile() ? 'over' : 'bottom',
                align: isMobile() ? 'center' : 'end'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        var inChain = window.YakultTour._isInChain(TOUR_KEY);

        // In chain mode: drop the standalone "That's It" step (last in steps[])
        // so "Tour Complete!" is the single closing step — no double-ending.
        var finalSteps = inChain ? steps.slice(0, -1) : steps.slice();
        var extraConfig = {};
        if (inChain) {
            finalSteps.push({
                popover: {
                    title: 'Submit Request — Tour Complete!',
                    description: 'You know how to fill in and submit a cartridge request. Click <strong>Continue</strong> to move on to the <strong>Authorization History</strong> walkthrough.',
                    side: 'over',
                    align: 'center'
                }
            });
            extraConfig.doneBtnText = 'Continue to History →';
        }

        window.YakultTour.register(TOUR_KEY, finalSteps, Object.assign(extraConfig, {
            beforeStart: function () {
                // Re-check at start time so a stale sessionStorage entry from a
                // previously abandoned chain never injects the chain nav into a
                // standalone "?" tour.
                if (window.YakultTour._isInChain(TOUR_KEY)) {
                    window.YakultTour._showChainNav('Skip to History', function () {
                        window.YakultTour._skipToNextInChain();
                    });
                }
            },
            onDestroyed: function () {
                window.YakultTour._hideChainNav();
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

        // Auto-start if triggered via ?tour= param or chain
        var params = new URLSearchParams(window.location.search);
        if (params.get('tour') === TOUR_KEY || inChain) {
            window.YakultTour.startTour(TOUR_KEY);
        } else {
            window.YakultTour.autoStart(TOUR_KEY);
        }
    });
}());
