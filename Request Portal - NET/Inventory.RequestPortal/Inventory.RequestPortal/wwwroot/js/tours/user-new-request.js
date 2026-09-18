/**
 * Tour: user-new-request
 * Page: Request/Index  (New Request form)
 * Audience: regular (non-approver) users only — the Razor view gates rendering.
 *
 * To add or reorder steps, edit the `steps` array below.
 * Element selectors map to IDs/classes defined in Request/Index.cshtml.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'user-new-request';

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'Welcome to the Request Portal',
                description: 'This short tour walks you through everything you need to submit and track cartridge requests. Use the buttons below to move through each step, or close the tour at any time.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. Hamburger / sidebar ──────────────────────────────────────
        {
            element: 'button[data-bs-target="#navSidebar"]',
            popover: {
                title: 'Navigation Menu',
                description: 'Tap the menu icon to open the sidebar. From there you can jump to New Request, My Submitted Requests, Authorization, and Notification Settings at any time.',
                side: 'right',
                align: 'start'
            }
        },

        // ── 3. Notification bell ────────────────────────────────────────
        {
            element: '#notifBellBtn',
            popover: {
                title: 'Notification Bell',
                description: 'You\'ll receive real-time alerts here when your request is approved, rejected, or fulfilled. A red badge shows how many unread notifications you have.',
                side: 'bottom',
                align: 'end'
            }
        },

        // ── 4. Tab bar ──────────────────────────────────────────────────
        {
            element: '.nav-tabs',
            popover: {
                title: 'Page Navigation',
                description: 'These tabs are your main navigation: <strong>New Request</strong> to submit, <strong>My Submitted Requests</strong> to track your history, and <strong>Authorization</strong> to view your approval status.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 5. Cartridge table ──────────────────────────────────────────
        {
            element: '.cartridge-table',
            popover: {
                title: 'Cartridge Request Table',
                description: 'Each row is one cartridge model you want to request. You can include several different models in a single submission.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 6. Cartridge model text input ───────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-row:first-child input[name$=".CartridgeModel"]',
            popover: {
                title: 'Cartridge Model Number',
                description: 'Type the model number here — for example <em>HP 83A</em> or <em>Canon 051H</em>. This is the primary field; whatever you type is what gets recorded.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 7. Available models reference dropdown ──────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-row:first-child select.model-ref-select',
            popover: {
                title: 'Available Models Reference',
                description: 'Use this dropdown as a guide to find known models. Selecting one auto-fills the model number above. It\'s optional — you can always type directly instead.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 8. Quantity ─────────────────────────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-row:first-child input.qty-number-input',
            popover: {
                title: 'Quantity Requested',
                description: 'Enter how many cartridges you need for this model. The maximum is <strong>3 per model</strong> per submission. A helper hint appears when stock information is available.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 9. Returned empty cartridges ────────────────────────────────
        {
            element: '#cartridgeRowsBody .cartridge-return-row:first-child',
            popover: {
                title: 'Returned Empty Cartridges',
                description: 'When collecting new cartridges, you return your old empties. Enter how many are in <strong>Good</strong> condition and how many are <strong>Damaged</strong>. The combined total cannot exceed your requested quantity.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 10. Add another model ───────────────────────────────────────
        {
            element: '#addRowBtn',
            popover: {
                title: 'Request Multiple Models',
                description: 'Need more than one cartridge type? Click here to add another row. Each row is independent — you can request up to 3 of each model in a single submission.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 11. Employee in Session — dept account banner (only rendered for dept accounts) ──
        {
            element: '#deptEmpPickerWrapper',
            popover: {
                title: 'Employee in Session',
                description: 'This account is shared by your entire department. Before submitting, you must identify <strong>which employee</strong> is making this specific request. The info banner above the field shows the department and branch this account belongs to.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 12. Employee search field ────────────────────────────────────
        {
            element: '#deptEmpInput',
            popover: {
                title: 'Search for Employee',
                description: 'Start typing a name to search. A dropdown will appear with matching employees — select the correct person before filling in the rest of the form. <strong>This field is required</strong> and the form will not submit without a selection.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 13. Distribution method ──────────────────────────────────────
        {
            element: '#distributionMethodRow',
            popover: {
                title: 'Distribution Method',
                description: 'Choose how you\'ll receive your cartridges:<br><strong>PICKUP</strong> — you or a representative collects them at the IT office.<br><strong>DELIVERY</strong> — IT delivers to your branch.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 14. Received by (PICKUP only) ───────────────────────────────
        {
            element: '#receivedByRow',
            popover: {
                title: 'To Be Received By',
                description: 'When PICKUP is selected, specify who will collect the cartridges at the IT office.<br><br>Use the <strong>Company</strong>, <strong>Branch</strong>, and <strong>Department</strong> dropdowns to narrow the list, then search by name in the field below. Click <strong>×</strong> to clear all filters at once.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 15. Distribution preview ────────────────────────────────────
        {
            element: '#distributionPreview',
            popover: {
                title: 'Distribution Summary',
                description: 'This preview confirms your selected distribution method and who will receive the cartridges — useful for double-checking before you submit.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 16. Additional remarks ──────────────────────────────────────
        {
            element: 'textarea[name="Request.AdditionalRemarks"]',
            popover: {
                title: 'Additional Remarks',
                description: 'Optionally add any special instructions or context — for example: <em>"Urgent, needed for client meeting Friday."</em>',
                side: 'top',
                align: 'start'
            }
        },

        // ── 17. Submit button ───────────────────────────────────────────
        {
            element: 'button.btn-submit',
            popover: {
                title: 'Submit Your Request',
                description: 'Once everything is filled in, click <strong>Submit Request</strong>. You\'ll be taken to the My Submitted Requests page where you can track the approval status.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 18. My Requests tab ─────────────────────────────────────────
        {
            element: '.nav-tabs .nav-item:nth-child(2) .nav-link',
            popover: {
                title: 'Track Your Submissions',
                description: 'After submitting, click the <strong>My Submitted Requests</strong> tab to view your history and track approval status. The tour there will explain what each column means.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 19. Help button — relaunch tour ─────────────────────────────
        {
            element: 'button[title="Start Guided Tour"]',
            popover: {
                title: "That's It — You're Ready!",
                description: 'You can replay this guide at any time by clicking the <strong><i class="bi bi-question-circle"></i></strong> button here in the top-right corner of the page.',
                side: 'bottom',
                align: 'end'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        window.YakultTour.register(TOUR_KEY, steps);

        // Wire the "?" help button to this tour
        window.YakultTour._setPageTourFn(function () {
            window.YakultTour.startTour(TOUR_KEY);
        });

        // Auto-launch on first visit
        window.YakultTour.autoStart(TOUR_KEY);
    });
}());
