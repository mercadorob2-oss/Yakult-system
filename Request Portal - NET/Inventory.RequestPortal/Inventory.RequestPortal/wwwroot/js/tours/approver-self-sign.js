/**
 * Tour: approver-self-sign
 * Page: Authorization/TourDetails  (demo) or Authorization/Details/{id}
 * Audience: approvers only.
 *
 * Demonstrates the sign & authorize panel that loads inside the Authorization
 * Queue when an approver selects their own pending request. Walks through
 * reviewing the cartridge details, drawing/uploading a signature, and signing.
 */
(function () {
    'use strict';

    var TOUR_KEY = 'approver-self-sign';

    var steps = [
        // ── 1. Welcome ──────────────────────────────────────────────────
        {
            popover: {
                title: 'Sign & Authorize — How It Works',
                description: 'After submitting your own request, you are taken to your <strong>Authorization Queue</strong> where your request is pre-selected. This panel is what opens on the right side. This tour walks you through reviewing the cartridge details and signing the authorization.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. Authorization header + Back to Queue ──────────────────────
        {
            element: '.d-flex.justify-content-between.align-items-center.mb-3',
            popover: {
                title: 'Authorization Reference',
                description: 'This is the unique authorization number for your request. Use the <strong>Back to Queue</strong> button if you want to return to the queue without signing yet — your request will remain pending.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 3. Employee info card ────────────────────────────────────────
        {
            element: '.card.shadow-sm.mb-4',
            popover: {
                title: 'Request Summary',
                description: 'This card confirms the employee who submitted the request (you), the department, when it was submitted, and its current status. Verify these details before proceeding to sign.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 4. Self-sign alert ───────────────────────────────────────────
        {
            element: '.alert.alert-success',
            popover: {
                title: 'Self-Authorization Notice',
                description: 'This green banner confirms your request was submitted successfully and is ready for your signature. Sign the authorization below to complete the process.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 5. Cartridge details section ─────────────────────────────────
        {
            element: '#approveForm .mb-4:first-child',
            popover: {
                title: 'Cartridge Details Being Authorized',
                description: 'Review the cartridge models, quantities, and returned empty counts you submitted. This is the record that will be locked into the signed authorization document.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 6. Authorization preview ─────────────────────────────────────
        {
            element: '#previewSigBox',
            popover: {
                title: 'Authorization Preview',
                description: 'This section shows a preview of the official authorization document — your full name, position, company, and a cartridge summary. Your drawn or uploaded signature will appear in the box above your name once you sign.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('previewSigBox');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 7. Draw tab ──────────────────────────────────────────────────
        {
            element: '#btnTabDraw',
            popover: {
                title: 'Draw Your Signature',
                description: 'The <strong>Draw</strong> tab (active by default) lets you sign directly on the canvas using your mouse or finger.',
                side: 'bottom',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('btnTabDraw');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 8. Upload tab ────────────────────────────────────────────────
        {
            element: '#btnTabUpload',
            popover: {
                title: 'Upload Your Signature',
                description: 'Alternatively, switch to the <strong>Upload</strong> tab to attach a pre-prepared image file or PDF of your signature. Both methods produce the same embedded signature on the authorization document.',
                side: 'bottom',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('btnTabUpload');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 9. Signature canvas / draw panel ────────────────────────────
        {
            element: '#panelDraw',
            popover: {
                title: 'Signature Canvas',
                description: 'Draw your signature here. Use the <strong>Clear</strong> button below the canvas to erase and start over. Your signature will appear in the authorization preview above in real time as you draw.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.getElementById('panelDraw');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 10. Sign & Approve button ────────────────────────────────────
        {
            element: '#approveForm button[type="submit"]',
            popover: {
                title: 'Sign & Approve',
                description: 'Once your signature is ready, click <strong>Sign &amp; Approve</strong> to finalize the authorization. The signed document is generated immediately and your request status changes to <strong>Approved</strong>. <em>This action cannot be undone.</em>',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.querySelector('#approveForm button[type="submit"]');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        // ── 11. Reject card ──────────────────────────────────────────────
        {
            element: '.card.shadow-sm.border-danger-subtle',
            popover: {
                title: 'Reject Instead',
                description: 'Changed your mind? Use the <strong>Reject</strong> button here to decline the request. You will be asked to confirm before the rejection is submitted. Rejected requests cannot be recovered.',
                side: 'top',
                align: 'start'
            },
            onHighlightStarted: function () {
                var el = document.querySelector('.card.shadow-sm.border-danger-subtle');
                if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    ];

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.YakultTour === 'undefined') return;

        var inChain = window.YakultTour._isInChain(TOUR_KEY);

        var finalSteps = steps.slice();
        var extraConfig = {};
        if (inChain) {
            finalSteps.push({
                popover: {
                    title: 'Authorization Details — Tour Complete!',
                    description: 'You know how to review and self-sign a request you submitted as an approver. Click <strong>Continue</strong> to finish with the <strong>Authorization History</strong> walkthrough.',
                    side: 'over',
                    align: 'center'
                }
            });
            extraConfig.doneBtnText = 'Continue to History →';
        }

        window.YakultTour.register(TOUR_KEY, finalSteps, Object.assign(extraConfig, {
            beforeStart: function () {
                if (inChain) {
                    window.YakultTour._showChainNav('Skip to History', function () {
                        window.YakultTour._skipToNextInChain();
                    });
                }
            },
            onDestroyed: function () {
                window.YakultTour._hideChainNav();
                window.YakultTour._advanceChain();
            }
        }));

        // Wire the "?" help button if present
        window.YakultTour._setPageTourFn(function () {
            window.YakultTour.startTour(TOUR_KEY);
        });

        // Start when: redirected after real submission (selfSign=True),
        // navigated from chain/UGC (?tour=), or currently active in chain.
        var params = new URLSearchParams(window.location.search);
        var shouldStart = params.get('selfSign') === 'True'
            || params.get('selfSign') === 'true'
            || params.get('tour') === TOUR_KEY
            || inChain;

        if (shouldStart) {
            window.YakultTour.startTour(TOUR_KEY);
        }
    });
}());
