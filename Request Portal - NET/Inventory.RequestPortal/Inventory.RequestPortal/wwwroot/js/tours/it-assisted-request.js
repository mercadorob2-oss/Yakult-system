/**
 * Tour: it-assisted-request
 * Page: Request/AssistedRequest
 * Audience: IT/Developer users only — the Razor view gates rendering.
 *
 * To add or reorder steps, edit the `steps` array below.
 * Element selectors map to IDs/classes defined in Request/AssistedRequest.cshtml.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'it-assisted-request';

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'IT Assisted Request',
                description: 'This tab lets IT staff create a cartridge request on behalf of any employee. The request is filed under the selected employee\'s name and submitted for supervisor authorization. Use the buttons below to walk through each section.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. IT Assisted Request warning banner ───────────────────────
        {
            element: '#itAssistedBanner',
            popover: {
                title: 'IT Assisted Request Mode',
                description: 'This banner is a reminder that you are acting on behalf of another employee — not for yourself. Always double-check the selected employee before proceeding.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 3. Employee filter dropdowns ────────────────────────────────
        {
            element: '#empFilterRow',
            popover: {
                title: 'Filter by Company, Branch & Department',
                description: 'Use these dropdowns to narrow the employee list before searching. Select a Company, Branch, or Department to limit the results — useful when there are many employees to choose from. Click <strong>×</strong> to clear all filters at once.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 4. Employee picker ──────────────────────────────────────────
        {
            element: '#empPickerWrapper',
            popover: {
                title: 'Select Employee',
                description: 'Search for the employee you are creating this request for. Type part of their name, employee number, or department to filter the list. Once selected, their position, company, branch, and department are shown below as confirmation.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 5. Cartridge Models table ───────────────────────────────────
        {
            element: '.cartridge-table',
            popover: {
                title: 'Cartridge Models',
                description: 'Each row is one cartridge model being requested. You can add multiple models in a single submission by clicking <strong>Add another model</strong>.<br><br>If the employee is returning empty cartridges, enter the count under <strong>Good</strong> (intact) and <strong>Damaged</strong> in the row below each model.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 6. Distribution method & received by ────────────────────────
        {
            element: '#receivedByRow',
            popover: {
                title: 'Distribution & Received By',
                description: 'Choose <strong>PICKUP</strong> if the employee will collect the cartridges at the IT office, or <strong>DELIVERY</strong> if they will be sent to the branch.<br><br>When PICKUP is selected, you must also specify who will receive the cartridges.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 7. Received By filter dropdowns ─────────────────────────────
        {
            element: '#rbFilterRow',
            popover: {
                title: 'Filter by Company, Branch & Department',
                description: 'Use these dropdowns to narrow down the list of people who can receive the cartridges. Filter by Company, Branch, or Department to quickly find the right person. Click <strong>×</strong> to clear all filters at once.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 9. Additional Remarks ───────────────────────────────────────
        {
            element: 'textarea[name="Request.AdditionalRemarks"]',
            popover: {
                title: 'Additional Remarks',
                description: 'Optionally add any context or special instructions for this request — for example: <em>"Urgent, needed for client meeting Friday."</em>',
                side: 'top',
                align: 'start'
            }
        },

        // ── 10. Manual Authorization ────────────────────────────────────
        {
            element: '#manualAuthToggle',
            popover: {
                title: 'Manual Authorization Toggle',
                description: 'Use this toggle to decide how authorization is handled.<br><br><strong>Toggle OFF (default)</strong> — the request is submitted as Pending Approval. The approver signs it digitally through the portal at their convenience.<br><br><strong>Toggle ON</strong> — use this when the approver cannot access the portal right now (no internet, broken PC, network outage, etc.). Select the approver, choose <em>Approved</em> or <em>Rejected</em> to record their verbal or offline decision, and add remarks if required.<br><br>You decide which flow is appropriate based on the current situation.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var toggle = document.getElementById('manualAuthToggle');
                var label  = document.getElementById('manualAuthToggleLabel');
                if (toggle && !toggle.checked) {
                    toggle.checked = true;
                    if (label) { label.textContent = 'On'; label.style.color = '#198754'; }
                    document.getElementById('manualAuthSection').style.display = '';
                }
            }
        },

        // ── 11. Authorized By ───────────────────────────────────────────
        {
            element: '#approverWrapper',
            popover: {
                title: 'Authorized By',
                description: 'Select the supervisor or manager who gave the authorization for this request.<br><br>Only approvers linked to the target employee\'s department appear in this list.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 12. Decision ────────────────────────────────────────────────
        {
            element: '#decisionRow',
            popover: {
                title: 'Decision',
                description: 'Choose the outcome of the authorization:<br><br>• <strong>Approved</strong> — the approver confirmed the request.<br>• <strong>Rejected</strong> — the approver declined it.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 13. Auth Remarks ────────────────────────────────────────────
        {
            element: '#itRemarksInput',
            popover: {
                title: 'Auth Remarks',
                description: 'Add any relevant notes about the authorization — for example the reason for rejection or additional context.<br><br>Remarks are optional when Approved but required when Rejected.',
                side: 'top',
                align: 'start'
            },
            onDeselected: function () {
                var toggle = document.getElementById('manualAuthToggle');
                var label  = document.getElementById('manualAuthToggleLabel');
                if (toggle && toggle.checked) {
                    toggle.checked = false;
                    if (label) { label.textContent = 'Off'; label.style.color = '#6c757d'; }
                    document.getElementById('manualAuthSection').style.display = 'none';
                }
            }
        },

        // ── 14. Submit button ──────────────────────────────────────────
        {
            element: 'button.btn-submit',
            popover: {
                title: 'Submit the Request',
                description: 'When all fields are filled in and the authorization is recorded, click <strong>Submit Assisted Request</strong> to file the request under the selected employee\'s name.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 15. Help button — relaunch tour ─────────────────────────────
        {
            element: 'button[title="Start Guided Tour"]',
            popover: {
                title: "That's It — You're All Set!",
                description: 'You can replay this guide at any time by clicking the <strong><i class="bi bi-question-circle"></i></strong> button in the top-right corner of the page.',
                side: 'bottom',
                align: 'end'
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        window.YakultTour.register(TOUR_KEY, steps);

        window.YakultTour._setPageTourFn(function () {
            window.YakultTour.startTour(TOUR_KEY);
        });

        window.YakultTour.autoStart(TOUR_KEY);
    });
}());
