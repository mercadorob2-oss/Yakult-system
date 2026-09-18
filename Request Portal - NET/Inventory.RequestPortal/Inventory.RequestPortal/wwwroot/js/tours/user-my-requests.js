/**
 * Tour: user-my-requests
 * Page: Request/MyRequests  (Request History)
 * Audience: regular (non-approver) users only — the Razor view gates rendering.
 *
 * Modal sub-tour pattern:
 *   Steps that spotlight elements INSIDE #requestDetailModal use:
 *     side: 'left'  — tooltip appears in the left margin, outside the modal
 *     popoverClass: 'yakult-tour-aside'  — narrows the popover to fit
 *   The modal is opened without animation in onHighlightStarted so Driver.js
 *   can measure element positions immediately in the same tick.
 *   See YakultTour.openModalForTour / closeModalForTour in tour-system.js.
 */
(function () {
    'use strict';

    var TOUR_KEY    = 'user-my-requests';
    var MODAL_ID    = 'requestDetailModal';
    var ASIDE_CLASS = 'yakult-tour-aside';

    // ── Shared helper: populate the detail modal from the first usable row ──
    // Called by step 7's onHighlightStarted AND by steps 8–11's onHighlightStarted
    // when backward navigation has left the modal closed.
    function _populateAndOpenModal() {
        var rows = Array.from(document.querySelectorAll('#historyTable tbody .history-row'));
        var targetRow = rows.find(function (r) {
            try {
                var d = JSON.parse(r.dataset.detail);
                return d.setCode && d.setCode !== '—' && d.setCode.trim() !== '';
            } catch (e) { return false; }
        }) || rows[0];

        if (!targetRow) { window.YakultTour.openModalForTour(MODAL_ID); return; }

        var d = JSON.parse(targetRow.dataset.detail);

        document.getElementById('mdlSetCode').textContent     = d.setCode      || '—';
        document.getElementById('mdlDate').textContent        = d.date         || '—';
        document.getElementById('mdlEmployee').textContent    = d.employee     || '—';
        document.getElementById('mdlBranch').textContent      = d.branch       || '—';
        document.getElementById('mdlDepartment').textContent  = d.department   || '—';
        document.getElementById('mdlCompany').textContent     = d.company      || '—';
        document.getElementById('mdlFulfillment').textContent = d.fulfillment  || '—';

        var statusEl = document.getElementById('mdlStatus');
        if (statusEl) {
            statusEl.textContent = d.status || '—';
            statusEl.className   = 'fw-semibold fs-6 ' + (d.statusCss || '');
        }

        var tbody = document.getElementById('mdlItemsBody');
        if (tbody) {
            tbody.innerHTML = '';
            (d.items || []).forEach(function (item) {
                var tr = document.createElement('tr');
                tr.innerHTML = '<td><strong>' + (item.cartridgeModel || '—') + '</strong></td>' +
                    '<td class="text-center">' + (item.qty       || 0) + '</td>' +
                    '<td class="text-center">' + (item.goodQty    || 0) + '</td>' +
                    '<td class="text-center">' + (item.damagedQty || 0) + '</td>';
                tbody.appendChild(tr);
            });
        }

        var remarksBlock = document.getElementById('mdlRemarksBlock');
        if (remarksBlock) {
            if (d.remarks) {
                document.getElementById('mdlRemarks').textContent = d.remarks;
                remarksBlock.classList.remove('d-none');
            } else {
                remarksBlock.classList.add('d-none');
            }
        }

        rows.forEach(function (r) { r.classList.remove('active-row'); });
        targetRow.classList.add('active-row');

        window.YakultTour.openModalForTour(MODAL_ID);
    }

    // Reopens the modal only if it isn't already visible (used by back-navigation).
    function _ensureModalOpen() {
        return new Promise(function (resolve) {
            var modalEl = document.getElementById(MODAL_ID);
            if (!modalEl || !modalEl.classList.contains('show')) {
                _populateAndOpenModal();
            }
            setTimeout(resolve, 180);
        });
    }

    var steps = [

        // ── 1. Welcome ───────────────────────────────────────────────────
        {
            popover: {
                title: 'My Submitted Requests — Your History',
                description: 'This page shows every cartridge request you\'ve submitted and its current approval status. Let\'s walk through each part.',
                side: 'over',
                align: 'center'
            }
        },

        // ── 2. History card ──────────────────────────────────────────────
        {
            element: '#requestHistoryCard',
            popover: {
                title: 'Request History Table',
                description: 'Your submissions appear here, most recent first. Each row is one batch — a set code groups all the models you requested together.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 3. Column headers ────────────────────────────────────────────
        {
            element: '#requestHistoryHead',
            popover: {
                title: 'Understanding the Columns',
                description: '<strong>Set Code</strong> — unique batch ID.<br><strong>Date</strong> — when submitted.<br><strong>Cartridge Model(s)</strong> — what was requested.<br><strong>Qty</strong> — total units.<br><strong>Empties</strong> — returned cartridges.<br><strong>Fulfillment</strong> — PICKUP or DELIVERY.<br><strong>Branch</strong> — your assigned branch.',
                side: 'bottom',
                align: 'start'
            }
        },

        // ── 4. Status column ─────────────────────────────────────────────
        {
            element: '#requestHistoryStatus',
            popover: {
                title: 'Request Status',
                description: 'Track where each request stands:<br><span style="color:#e08a00">●</span> <strong>Pending</strong> — awaiting approval.<br><span style="color:#0d6efd">●</span> <strong>Processing</strong> — approved, being prepared.<br><span style="color:#198754">●</span> <strong>Fulfilled</strong> — ready or delivered.<br><span style="color:#dc3545">●</span> <strong>Unfulfilled</strong> — rejected or cancelled.',
                side: 'left',
                align: 'start'
            }
        },

        // ── 5. Refresh button ─────────────────────────────────────────────
        {
            element: '#requestHistoryRefresh',
            popover: {
                title: 'Refresh',
                description: 'Approvals happen in real time. Click <strong>Refresh</strong> to reload and see the latest status on all your requests.',
                side: 'left',
                align: 'start'
            }
        },

        // ── 6. Row — explain double-click (modal not yet open) ───────────
        {
            element: '#historyTable tbody .history-row',
            popover: {
                title: 'View Full Request Details',
                description: '<strong>Double-click any row</strong> to open a detailed breakdown of that request — models, quantities, fulfillment info, and more. Go ahead and try it on any row.',
                side: 'top',
                align: 'start'
            }
        },

        // ── 7. Modal header — opens modal, introduces the panel ──────────
        {
            element: '#' + MODAL_ID + ' .modal-header',
            popover: {
                title: 'Request Details Panel',
                description: 'This panel gives you a complete view of a single request. The header shows the Set Code that identifies the batch.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onHighlightStarted: function () {
                return new Promise(function (resolve) {
                    _populateAndOpenModal();
                    setTimeout(resolve, 180);
                });
            }
        },

        // ── 8. Status section ─────────────────────────────────────────────
        {
            element: '#mdlStatusSection',
            popover: {
                title: 'Status & Date',
                description: 'At a glance you can see the current <strong>status</strong> of the request and exactly <strong>when</strong> it was submitted.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onHighlightStarted: _ensureModalOpen
        },

        // ── 9. Requester info section ─────────────────────────────────────
        {
            element: '#mdlInfoSection',
            popover: {
                title: 'Request Information',
                description: 'This section contains the full context of the request: <strong>employee</strong>, <strong>branch</strong>, <strong>department</strong>, <strong>company</strong>, and <strong>fulfillment method</strong> (Pickup or Delivery).',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onHighlightStarted: _ensureModalOpen
        },

        // ── 10. Cartridge models section ──────────────────────────────────
        {
            element: '#mdlCartridgeSection',
            popover: {
                title: 'Cartridge Details',
                description: 'The table lists every model in this batch, showing <strong>quantity requested</strong> and how many empties were returned — both good and damaged.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onHighlightStarted: _ensureModalOpen
        },

        // ── 11. Close button — last modal step, closes modal on deselect ──
        {
            element: '#' + MODAL_ID + ' .btn-close',
            popover: {
                title: 'Close the Panel',
                description: 'Click the <strong>✕</strong> here — or press <kbd>Esc</kbd> — to dismiss the detail panel and return to your request list.',
                side: 'left',
                align: 'start',
                popoverClass: ASIDE_CLASS
            },
            onHighlightStarted: _ensureModalOpen,
            onDeselected: function () {
                window.YakultTour.closeModalForTour(MODAL_ID);
            }
        },

        // ── 12. Help button — relaunch tour ──────────────────────────────
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
            var rows = document.querySelectorAll('#historyTable tbody .history-row');
            return rows.length === 0 && typeof window.YakultTourDemo !== 'undefined';
        }

        function buildOptions() {
            var opts = {
                // Always close the modal when the tour ends — covers both the normal
                // finish path (step 11 onDeselected fires) and early dismiss (click
                // outside the popover), where onDeselected never fires.
                onDestroyed: function () {
                    window.YakultTour.closeModalForTour(MODAL_ID);
                    if (typeof window.YakultTourDemo !== 'undefined') {
                        window.YakultTourDemo.cleanupMyRequests();
                    }
                }
            };
            if (needsDemo()) {
                opts.beforeStart = function () { window.YakultTourDemo.injectMyRequests(); };
            }
            return opts;
        }

        window.YakultTour.register(TOUR_KEY, steps, buildOptions());

        window.YakultTour._setPageTourFn(function () {
            window.YakultTour.register(TOUR_KEY, steps, buildOptions());
            window.YakultTour.startTour(TOUR_KEY);
        });

        window.YakultTour.autoStart(TOUR_KEY);
    });
}());
