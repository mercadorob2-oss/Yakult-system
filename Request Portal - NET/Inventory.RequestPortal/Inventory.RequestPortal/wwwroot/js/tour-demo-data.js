/**
 * YakultTourDemo — supplies sample data when a guided-tour page has no real records.
 *
 * Activated only during a tour on empty-state pages via the beforeStart / onDestroyed
 * hooks registered alongside each tour's step definitions. All injected elements carry
 * a data-tour-demo attribute so cleanup is surgical and complete.
 *
 * Rules enforced here:
 *  - No data is ever saved to the database.
 *  - No real records are affected.
 *  - Demo content is removed the moment the tour ends.
 *  - Users with real data never see this code run.
 */
(function (global) {
    'use strict';

    var _active = false;
    var BANNER_ATTR = 'data-tour-demo-banner';

    // ── Banner ──────────────────────────────────────────────────────────────

    function showBanner(anchorEl) {
        if (document.querySelector('[' + BANNER_ATTR + ']')) return;

        var banner = document.createElement('div');
        banner.setAttribute(BANNER_ATTR, '1');
        banner.style.cssText =
            'background:#1d4ed8;color:#fff;padding:9px 16px;font-size:.83rem;' +
            'font-weight:600;text-align:center;border-radius:8px;margin-bottom:14px;' +
            'box-shadow:0 2px 8px rgba(0,0,0,.15);';
        banner.innerHTML =
            '<i class="bi bi-mortarboard-fill me-2"></i>' +
            'Guided Tour Mode &mdash; Sample data is being displayed for training purposes. ' +
            'No actions performed during this tour will affect real records.';

        if (anchorEl && anchorEl.parentNode) {
            anchorEl.parentNode.insertBefore(banner, anchorEl);
        } else {
            var cp = document.querySelector('.content-padded');
            if (cp) cp.insertBefore(banner, cp.firstChild);
        }
    }

    function hideBanner() {
        document.querySelectorAll('[' + BANNER_ATTR + ']').forEach(function (el) {
            el.remove();
        });
    }

    // ── Shared helper ────────────────────────────────────────────────────────

    function removeByAttr(attr, value) {
        var sel = value !== undefined
            ? '[' + attr + '="' + value + '"]'
            : '[' + attr + ']';
        document.querySelectorAll(sel).forEach(function (el) { el.remove(); });
    }

    // ── MyRequests — sample history rows ────────────────────────────────────

    var MR_ROWS = [
        {
            setCode:          'YKL-2026-0042',
            date:             'June 10, 2026  9:15 AM',
            status:           'Fulfilled',
            statusCss:        'text-success',
            employee:         'Sample User',
            branch:           'Manila Main Branch',
            department:       'IT Department',
            company:          'Yakult Philippines',
            fulfillment:      'PICKUP',
            remarks:          '',
            displayDate:      'Jun 10, 2026',
            cartridgeDisplay: 'CE285A, CC388A',
            totalQty:         6,
            returnInfo:       '6 good, 0 damaged',
            items: [
                { cartridgeModel: 'CE285A (HP 85A)', qty: 3, goodQty: 3, damagedQty: 0 },
                { cartridgeModel: 'CC388A (HP 88A)', qty: 3, goodQty: 3, damagedQty: 0 }
            ]
        },
        {
            setCode:          'YKL-2026-0056',
            date:             'June 15, 2026  2:30 PM',
            status:           'Pending',
            statusCss:        'status-pending',
            employee:         'Sample User',
            branch:           'Manila Main Branch',
            department:       'IT Department',
            company:          'Yakult Philippines',
            fulfillment:      'DELIVERY',
            remarks:          'Urgent — printers on 3rd floor are all out.',
            displayDate:      'Jun 15, 2026',
            cartridgeDisplay: 'Q2612A',
            totalQty:         4,
            returnInfo:       '—',
            items: [
                { cartridgeModel: 'Q2612A (HP 12A)', qty: 4, goodQty: 0, damagedQty: 0 }
            ]
        },
        {
            setCode:          'YKL-2026-0063',
            date:             'June 17, 2026  10:00 AM',
            status:           'Processing',
            statusCss:        'text-primary',
            employee:         'Sample User',
            branch:           'Manila Main Branch',
            department:       'IT Department',
            company:          'Yakult Philippines',
            fulfillment:      'PICKUP',
            remarks:          '',
            displayDate:      'Jun 17, 2026',
            cartridgeDisplay: 'CF226A',
            totalQty:         2,
            returnInfo:       '—',
            items: [
                { cartridgeModel: 'CF226A (HP 26A)', qty: 2, goodQty: 0, damagedQty: 0 }
            ]
        }
    ];

    function _mrRowHtml(row) {
        var detail = JSON.stringify({
            setCode:    row.setCode,
            date:       row.date,
            status:     row.status,
            statusCss:  row.statusCss,
            employee:   row.employee,
            branch:     row.branch,
            department: row.department,
            company:    row.company,
            fulfillment:row.fulfillment,
            remarks:    row.remarks,
            items:      row.items
        }).replace(/&/g, '&amp;').replace(/"/g, '&quot;');

        return '<tr class="history-row" style="cursor:pointer;"' +
            ' data-detail="' + detail + '" data-tour-demo="mr-row">' +
            '<td class="px-3 py-3 fw-semibold align-middle">' + row.setCode + '</td>' +
            '<td class="px-3 py-3 align-middle">' + row.displayDate + '</td>' +
            '<td class="px-3 py-3 align-middle">' + row.cartridgeDisplay + '</td>' +
            '<td class="px-3 py-3 text-center fw-bold align-middle">' + row.totalQty + '</td>' +
            '<td class="px-3 py-3 text-center align-middle" style="color:#64748b;font-size:.88rem;">' + row.returnInfo + '</td>' +
            '<td class="px-3 py-3 text-center align-middle" style="font-size:.88rem;">' + row.fulfillment + '</td>' +
            '<td class="px-3 py-3 align-middle">' + row.branch + '</td>' +
            '<td class="px-3 py-3 text-center align-middle">' +
                '<span class="' + row.statusCss + ' fw-semibold" style="font-size:.88rem;">' + row.status + '</span>' +
            '</td>' +
        '</tr>';
    }

    function injectMyRequests() {
        var card = document.getElementById('requestHistoryCard');
        if (!card) return false;
        if (document.querySelector('[data-tour-demo="mr-table"]')) return true;

        // Hide the empty-state card body
        var emptyBody = card.querySelector('.card-body.text-center');
        if (emptyBody) {
            emptyBody.setAttribute('data-tour-demo-hidden', 'mr');
            emptyBody.style.display = 'none';
        }

        // Update count badge to show sample count
        var badge = card.querySelector('.badge.rounded-pill');
        if (badge) {
            badge.setAttribute('data-tour-demo-orig-count', badge.textContent);
            badge.textContent = MR_ROWS.length;
            badge.setAttribute('data-tour-demo', 'mr-badge');
        }

        // Build the full table with sample rows
        var tbodyHtml = MR_ROWS.map(_mrRowHtml).join('');
        var tableHtml =
            '<div class="table-responsive" data-tour-demo="mr-table">' +
                '<table class="table table-hover mb-0" id="historyTable" style="font-size:.92rem;">' +
                    '<thead style="background:#f1f5f9;">' +
                        '<tr id="requestHistoryHead">' +
                            '<th class="px-3 py-3" style="color:#64748b;font-weight:600;width:110px;">Set Code</th>' +
                            '<th class="px-3 py-3" style="color:#64748b;font-weight:600;width:110px;">Date</th>' +
                            '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Cartridge Model(s)</th>' +
                            '<th class="px-3 py-3 text-center" style="color:#64748b;font-weight:600;width:60px;">Qty</th>' +
                            '<th class="px-3 py-3 text-center" style="color:#64748b;font-weight:600;width:100px;">Empties</th>' +
                            '<th class="px-3 py-3 text-center" style="color:#64748b;font-weight:600;width:100px;">Fulfillment</th>' +
                            '<th class="px-3 py-3" style="color:#64748b;font-weight:600;width:180px;">Branch</th>' +
                            '<th id="requestHistoryStatus" class="px-3 py-3 text-center" style="color:#64748b;font-weight:600;width:150px;">Status</th>' +
                        '</tr>' +
                    '</thead>' +
                    '<tbody>' + tbodyHtml + '</tbody>' +
                '</table>' +
            '</div>';

        card.insertAdjacentHTML('beforeend', tableHtml);
        showBanner(card);
        _active = true;
        return true;
    }

    function cleanupMyRequests() {
        removeByAttr('data-tour-demo', 'mr-table');

        // Restore badge
        var badge = document.querySelector('[data-tour-demo="mr-badge"]');
        if (badge) {
            badge.textContent = badge.getAttribute('data-tour-demo-orig-count') || '0';
            badge.removeAttribute('data-tour-demo');
            badge.removeAttribute('data-tour-demo-orig-count');
        }

        // Restore empty-state body
        document.querySelectorAll('[data-tour-demo-hidden="mr"]').forEach(function (el) {
            el.style.display = '';
            el.removeAttribute('data-tour-demo-hidden');
        });

        hideBanner();
        _active = false;
    }

    // ── AuthStatus — sample status rows + modal body ─────────────────────────

    var AS_ROWS = [
        {
            authId:    'S001', status: 'Pending',
            badgeCls:  'bg-warning text-dark',
            source:    'web',
            dept:      'IT Department',
            models:    'CE285A (HP 85A) (3)',
            signedBy:  '&mdash;',
            dateSigned:'&mdash;',
            reqOn:     'Jun 15, 2026  9:00 AM'
        },
        {
            authId:    'S002', status: 'Approved',
            badgeCls:  'bg-success',
            source:    'web',
            dept:      'IT Department',
            models:    'CC388A (HP 88A) (2)',
            signedBy:  'J. Santos',
            dateSigned:'Jun 10, 2026  3:15 PM',
            reqOn:     'Jun 10, 2026  9:00 AM'
        },
        {
            authId:    'S003', status: 'Rejected',
            badgeCls:  'bg-danger',
            source:    'desktop',
            dept:      'IT Department',
            models:    'Q2612A (HP 12A) (4)',
            signedBy:  'M. Cruz',
            dateSigned:'Jun 5, 2026  2:00 PM',
            reqOn:     'Jun 5, 2026  8:30 AM'
        }
    ];

    function _asSourceBadge(source) {
        return source === 'web'
            ? '<span class="badge rounded-pill" style="background:#e8f4fd;color:#1565c0;font-weight:600;font-size:0.72rem;"><i class="bi bi-globe me-1"></i>Web Portal</span>'
            : '<span class="badge rounded-pill" style="background:#f3f0ff;color:#5e35b1;font-weight:600;font-size:0.72rem;"><i class="bi bi-display me-1"></i>Desktop App</span>';
    }

    function _asRowHtml(row) {
        return '<tr data-auth-id="' + row.authId + '" data-status="' + row.status +
            '" data-tour-demo="as-row" style="cursor:pointer;">' +
            '<td class="px-3 py-2 text-muted">#' + row.authId + '</td>' +
            '<td class="px-3 py-2"><span class="badge ' + row.badgeCls + '">' + row.status + '</span></td>' +
            '<td class="px-3 py-2">' + _asSourceBadge(row.source) + '</td>' +
            '<td class="px-3 py-2">' + row.dept + '</td>' +
            '<td class="px-3 py-2"><div class="small">' + row.models + '</div></td>' +
            '<td class="px-3 py-2">' + row.signedBy + '</td>' +
            '<td class="px-3 py-2 text-muted">' + row.dateSigned + '</td>' +
            '<td class="px-3 py-2 text-muted">' + row.reqOn + '</td>' +
            '</tr>';
    }

    function injectAuthStatus() {
        var content = document.getElementById('empStatusContent');
        if (!content) return false;
        if (document.querySelector('[data-tour-demo="as-wrapper"]')) return true;

        // Hide the server-rendered empty state
        content.setAttribute('data-tour-demo-hidden', 'as');
        content.style.display = 'none';

        var tbodyHtml = AS_ROWS.map(_asRowHtml).join('');

        var wrapper = document.createElement('div');
        wrapper.setAttribute('data-tour-demo', 'as-wrapper');
        wrapper.innerHTML =
            '<div id="empStatusBanner" class="alert alert-warning mb-4" role="alert">' +
                '<i class="bi bi-hourglass-split me-2"></i>' +
                '<strong>Pending</strong> &mdash; Your latest request is awaiting supervisor authorization.' +
            '</div>' +
            '<div class="card shadow-sm" id="empStatusCard">' +
                '<div class="table-responsive">' +
                    '<table class="table table-hover mb-0" style="font-size:0.9rem;">' +
                        '<thead style="background:#f1f5f9;">' +
                            '<tr id="empStatusHead">' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">ID</th>' +
                                '<th id="empStatusStatusTh" class="px-3 py-3" style="color:#64748b;font-weight:600;">Status</th>' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Source</th>' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Department</th>' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Cartridge Models</th>' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Signed By</th>' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Date Signed</th>' +
                                '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Requested On</th>' +
                            '</tr>' +
                        '</thead>' +
                        '<tbody id="empStatusTbody">' + tbodyHtml + '</tbody>' +
                    '</table>' +
                '</div>' +
                '<div id="empStatusPagination"></div>' +
            '</div>';

        content.parentNode.insertBefore(wrapper, content.nextSibling);
        showBanner(wrapper);
        _active = true;
        return true;
    }

    function cleanupAuthStatus() {
        removeByAttr('data-tour-demo', 'as-wrapper');

        document.querySelectorAll('[data-tour-demo-hidden="as"]').forEach(function (el) {
            el.style.display = '';
            el.removeAttribute('data-tour-demo-hidden');
        });

        hideBanner();
        _active = false;
    }

    // Returns sample HTML for the auth detail modal body (#authDetailBody).
    // Used by user-auth-status.js when the highlighted row is a demo row.
    function getAuthDetailHtml() {
        return '' +
            '<div id="authDetailInfoSection" class="row g-3 mb-3 px-3 pt-3">' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Authorization ID</div>' +
                    '<div class="fw-semibold">#S001</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Status</div>' +
                    '<div><span class="badge bg-warning text-dark">Pending</span></div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Department</div>' +
                    '<div class="fw-semibold">IT Department</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Source</div>' +
                    '<div class="fw-semibold">Web Portal</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Requested On</div>' +
                    '<div class="fw-semibold">Jun 15, 2026  9:00 AM</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Signed By</div>' +
                    '<div class="fw-semibold">&mdash;</div>' +
                '</div>' +
            '</div>' +
            '<hr class="my-2">' +
            '<div id="authDetailCartridgeSection" class="px-3 pb-3">' +
                '<div class="text-muted small mb-2 fw-semibold text-uppercase" style="letter-spacing:.05em;">Cartridge Models</div>' +
                '<div class="table-responsive">' +
                    '<table class="table table-bordered table-sm mb-0" style="font-size:.88rem;">' +
                        '<thead class="table-light">' +
                            '<tr>' +
                                '<th>Model</th>' +
                                '<th class="text-center" style="width:70px;">Qty</th>' +
                                '<th class="text-center">Returned Cartridges (Good)</th>' +
                                '<th class="text-center">Returned Cartridges (Damaged)</th>' +
                            '</tr>' +
                        '</thead>' +
                        '<tbody>' +
                            '<tr>' +
                                '<td><strong>CE285A (HP 85A)</strong></td>' +
                                '<td class="text-center">3</td>' +
                                '<td class="text-center">&mdash;</td>' +
                                '<td class="text-center">&mdash;</td>' +
                            '</tr>' +
                        '</tbody>' +
                    '</table>' +
                '</div>' +
            '</div>';
    }

    // ── AuthHistory — single sample row ──────────────────────────────────────

    function injectAuthHistory() {
        var tbody = document.getElementById('nonApproverTbody');
        if (!tbody) return false;
        if (tbody.querySelector('[data-tour-demo="ah-row"]')) return true;

        var rowHtml =
            '<tr data-tour-demo="ah-row" style="cursor:pointer;">' +
                '<td class="px-3 py-3">1001</td>' +
                '<td class="px-3 py-3"><span class="badge bg-warning text-dark">Pending</span></td>' +
                '<td class="px-3 py-3">' + _asSourceBadge('web') + '</td>' +
                '<td class="px-3 py-3">IT Department</td>' +
                '<td class="px-3 py-3"><div class="small">CE285A (HP 85A) (3)</div></td>' +
                '<td class="px-3 py-3">&mdash;</td>' +
                '<td class="px-3 py-3 text-muted">&mdash;</td>' +
                '<td class="px-3 py-3 text-muted">Jun 15, 2026  9:00 AM</td>' +
            '</tr>';

        tbody.insertAdjacentHTML('afterbegin', rowHtml);
        showBanner(document.getElementById('authHistoryCard'));
        _active = true;
        return true;
    }

    function cleanupAuthHistory() {
        removeByAttr('data-tour-demo', 'ah-row');
        hideBanner();
        _active = false;
    }

    // ── Approver Queue — sample queue items + detail panel ──────────────────

    var AQ_ITEMS = [
        {
            id:         'DEMO-001',
            name:       'Maria Santos',
            dept:       'Sales Department',
            branch:     'Manila Main Branch',
            company:    'Yakult Philippines',
            models:     'CE285A (HP 85A)',
            date:       'Jun 16, 2026  9:15 AM',
            dateVal:    '2026-06-16',
            level:      'PendingSupervisor'
        },
        {
            id:         'DEMO-002',
            name:       'Roberto Cruz',
            dept:       'Finance Department',
            branch:     'Makati Branch',
            company:    'Yakult Philippines',
            models:     'CC388A (HP 88A), Q2612A (HP 12A)',
            date:       'Jun 17, 2026  10:30 AM',
            dateVal:    '2026-06-17',
            level:      'PendingSupervisor'
        },
        {
            id:         'DEMO-003',
            name:       'Ana Reyes',
            dept:       'Human Resources',
            branch:     'Quezon City Branch',
            company:    'Yakult Philippines',
            models:     'CF226A (HP 26A)',
            date:       'Jun 17, 2026  2:00 PM',
            dateVal:    '2026-06-17',
            level:      'PendingManager'
        }
    ];

    function _aqItemHtml(item) {
        return '<div class="queue-item" data-id="' + item.id + '"' +
            ' data-name="' + item.name.toLowerCase() + '"' +
            ' data-dept="' + item.dept + '"' +
            ' data-branch="' + item.branch + '"' +
            ' data-company="' + item.company + '"' +
            ' data-date="' + item.dateVal + '"' +
            ' data-models="' + item.models.toLowerCase() + '"' +
            ' data-tour-demo="aq-item"' +
            ' onclick="YakultTourDemo._selectDemoItem(this)">' +
            '<div class="d-flex justify-content-between align-items-start gap-2">' +
                '<div class="qi-name flex-grow-1">' + item.name + '</div>' +
                '<span class="status-dot" data-level="' + item.level + '" title="' + item.level + '"></span>' +
            '</div>' +
            '<div class="qi-sub">' + item.dept + '</div>' +
            '<div class="qi-sub mt-1">' + item.models + '</div>' +
            '<div class="qi-sub">' + item.date + '</div>' +
        '</div>';
    }

    function getApproverDetailHtml() {
        return '' +
            '<div class="card shadow-sm mb-3" id="aqDemoInfoCard" data-tour-demo="aq-detail-part">' +
                '<div class="card-body py-3">' +
                    '<div class="d-flex justify-content-between align-items-start">' +
                        '<div>' +
                            '<h5 class="mb-1 fw-bold">Maria Santos</h5>' +
                            '<span class="text-muted small">Sales Department</span>' +
                        '</div>' +
                        '<span class="badge fs-6 bg-warning text-dark">Pending</span>' +
                    '</div>' +
                    '<div class="row g-2 mt-2">' +
                        '<div class="col-sm-6">' +
                            '<small class="text-muted d-block">Requested On</small>' +
                            '<span>June 16, 2026  9:15 AM</span>' +
                        '</div>' +
                        '<div class="col-sm-6">' +
                            '<small class="text-muted d-block">Authorization #</small>' +
                            '<span class="text-muted">#DEMO-001</span>' +
                        '</div>' +
                    '</div>' +
                '</div>' +
            '</div>' +
            '<div class="card shadow-sm mb-3" data-tour-demo="aq-detail-part">' +
                '<div class="card-header bg-white"><h6 class="mb-0"> Sign &amp; Authorize</h6></div>' +
                '<div class="card-body">' +
                    '<div class="mb-3" id="aqDemoCartridgeTable">' +
                        '<label class="form-label fw-bold mb-1">' +
                            '<span class="badge bg-danger me-1">1</span> Cartridge Details' +
                        '</label>' +
                        '<div class="table-responsive">' +
                            '<table class="table table-bordered table-sm mb-1">' +
                                '<thead class="table-light">' +
                                    '<tr>' +
                                        '<th>Model</th>' +
                                        '<th class="text-center">Qty</th>' +
                                        '<th class="text-center">Returned Cartridges (Good)</th>' +
                                        '<th class="text-center">Returned Cartridges (Damaged)</th>' +
                                    '</tr>' +
                                '</thead>' +
                                '<tbody>' +
                                    '<tr>' +
                                        '<td><strong>CE285A (HP 85A)</strong></td>' +
                                        '<td class="text-center">3</td>' +
                                        '<td class="text-center">2</td>' +
                                        '<td class="text-center">0</td>' +
                                    '</tr>' +
                                '</tbody>' +
                            '</table>' +
                        '</div>' +
                        '<small class="text-muted">Review before signing.</small>' +
                    '</div>' +
                    '<div class="mb-3" id="aqDemoPreview" style="background:#f9f9fb;border:1px dashed #ccc;border-radius:10px;font-size:.92rem;padding:20px 24px 24px;">' +
                        '<p class="text-muted small mb-1 fw-bold text-uppercase" style="letter-spacing:.4px;">' +
                            '<i class="bi bi-file-text"></i> Cartridge Request Preview' +
                        '</p>' +
                        '<p class="mb-1" style="font-style:italic;color:#333;line-height:1.7;">' +
                            'I, <strong>[Your Name]</strong>, <strong>[Your Position]</strong> of ' +
                            '<strong>Yakult Philippines &ndash; Sales Department</strong>, based at the ' +
                            '<strong>Manila Main Branch</strong>, hereby authorize the consumable/s ' +
                            'request(s) submitted and facilitated by <strong>Maria Santos</strong>.' +
                        '</p>' +
                        '<div class="table-responsive mb-1">' +
                            '<table class="table table-bordered table-sm mb-0" style="background:#fff;font-size:.88rem;">' +
                                '<thead class="table-light">' +
                                    '<tr><th>Model</th><th class="text-center">Qty</th>' +
                                    '<th class="text-center">Returned (Good)</th><th class="text-center">Returned (Damaged)</th></tr>' +
                                '</thead>' +
                                '<tbody>' +
                                    '<tr><td><strong>CE285A (HP 85A)</strong></td><td class="text-center">3</td>' +
                                    '<td class="text-center">2</td><td class="text-center">0</td></tr>' +
                                '</tbody>' +
                            '</table>' +
                        '</div>' +
                        '<p class="mb-1 mt-2" style="font-style:italic;color:#333;line-height:1.7;">Mode of Distribution: <strong>Pickup</strong> &mdash; To Be Received by: <strong>Maria Santos</strong></p>' +
                        '<div class="mt-4 pt-3" style="border-top:1px solid #e0e0e0;">' +
                            '<p style="font-size:.75rem;color:#999;margin-bottom:8px;">Noted by:</p>' +
                            '<div style="min-height:60px;max-width:320px;color:#ccc;font-style:italic;font-size:.85rem;padding:4px 2px;">(signature will appear here)</div>' +
                            '<strong style="font-size:.92rem;color:#111;display:block;line-height:1.5;margin-top:8px;">[Your Name]</strong>' +
                            '<span style="font-size:.8rem;color:#555;font-style:italic;display:block;line-height:1.5;">[Your Position]</span>' +
                            '<small class="text-muted d-block mt-2">Date Signed: June 17, 2026</small>' +
                        '</div>' +
                    '</div>' +
                    '<div class="mb-3" id="aqDemoSignArea">' +
                        '<label class="form-label fw-bold mb-1">' +
                            '<span class="badge bg-danger me-1">2</span> Your Signature' +
                        '</label>' +
                        '<div class="btn-group mb-2 d-flex" style="max-width:280px;" role="group">' +
                            '<button type="button" id="aqDemoBtnTabDraw" class="btn btn-danger btn-sm flex-fill">' +
                                '<i class="bi bi-pencil-square"></i> Draw' +
                            '</button>' +
                            '<button type="button" id="aqDemoBtnTabUpload" class="btn btn-outline-secondary btn-sm flex-fill">' +
                                '<i class="bi bi-upload"></i> Upload' +
                            '</button>' +
                        '</div>' +
                        '<div id="aqDemoPanelDraw" style="max-width:520px;">' +
                            '<div style="border:1.5px solid #d0d0d0;border-radius:8px;overflow:hidden;background:#fafafa;position:relative;">' +
                                '<div style="height:100px;display:flex;align-items:center;justify-content:center;color:#ccc;font-style:italic;font-size:.9rem;">' +
                                    'Draw your signature here' +
                                '</div>' +
                                '<div style="position:absolute;bottom:22px;left:16px;right:16px;border-bottom:1.5px solid #c0c0c0;pointer-events:none;"></div>' +
                                '<div style="position:absolute;bottom:6px;left:16px;font-size:.7rem;color:#bbb;pointer-events:none;letter-spacing:.5px;">SIGN ABOVE</div>' +
                            '</div>' +
                            '<div class="mt-1 d-flex gap-2 align-items-center">' +
                                '<button type="button" id="aqDemoClearBtn" class="btn btn-sm btn-outline-secondary" disabled><i class="bi bi-trash"></i> Clear</button>' +
                                '<small class="text-muted">Draw with mouse or finger</small>' +
                            '</div>' +
                        '</div>' +
                        '<div id="aqDemoPanelUpload" style="display:none;max-width:520px;">' +
                            '<input type="file" class="form-control form-control-sm mb-2" accept="image/*,.pdf" disabled />' +
                            '<small class="text-muted">Accepted: image files or PDF</small>' +
                        '</div>' +
                    '</div>' +
                    '<div class="d-flex gap-2 align-items-center flex-wrap">' +
                        '<button type="button" class="btn btn-success px-4" id="aqDemoApproveBtn" style="border-radius:8px;font-weight:700;" disabled>' +
                            '<i class="bi bi-check-circle-fill"></i> Sign &amp; Approve' +
                        '</button>' +
                        '<small class="text-muted">This action cannot be undone.</small>' +
                    '</div>' +
                    '<hr class="my-3">' +
                    '<button type="button" class="btn btn-sm btn-outline-danger" id="aqDemoRejectBtn" disabled>' +
                        '<i class="bi bi-x-circle"></i> Reject Request' +
                    '</button>' +
                '</div>' +
            '</div>';
    }

    function injectApproverQueue() {
        if (document.querySelector('[data-tour-demo="aq-item"]')) return true;

        var contentPadded = document.querySelector('.content-padded');
        if (!contentPadded) return false;

        // If real queue items already exist, nothing to inject
        var realItems = document.querySelectorAll('.queue-item[data-id]:not([data-tour-demo])');
        if (realItems.length > 0) { _active = true; return true; }

        // Hide the empty-state card if present
        var emptyCard = document.querySelector('.content-padded .card.shadow-sm .card-body.text-center');
        if (emptyCard && emptyCard.closest('.card')) {
            emptyCard.closest('.card').setAttribute('data-tour-demo-hidden', 'aq');
            emptyCard.closest('.card').style.display = 'none';
        }

        // Inject a demo filter bar when the real one is absent (only rendered when queue has items)
        if (!document.querySelector('.queue-filter-bar')) {
            var demoFilterBar = document.createElement('div');
            demoFilterBar.className = 'queue-filter-bar';
            demoFilterBar.setAttribute('data-tour-demo', 'aq-filter-bar');
            demoFilterBar.innerHTML =
                '<div class="row g-2 align-items-center">' +
                    '<div class="col-12 col-md-4">' +
                        '<div class="input-group input-group-sm">' +
                            '<span class="input-group-text bg-white border-end-0"><i class="bi bi-search text-muted"></i></span>' +
                            '<input type="text" id="filterSearch" class="form-control border-start-0" placeholder="Search name, department, model..." disabled>' +
                        '</div>' +
                    '</div>' +
                    '<div class="col-6 col-md-2">' +
                        '<input type="date" id="filterDateFrom" class="form-control form-control-sm" title="From date" disabled>' +
                    '</div>' +
                    '<div class="col-6 col-md-2">' +
                        '<input type="date" id="filterDateTo" class="form-control form-control-sm" title="To date" disabled>' +
                    '</div>' +
                    '<div class="col-sm-4 col-md-auto flex-md-grow-1">' +
                        '<input type="text" id="filterCompany" class="form-control form-control-sm" placeholder="Company" disabled>' +
                    '</div>' +
                    '<div class="col-sm-4 col-md-auto flex-md-grow-1">' +
                        '<input type="text" id="filterBranch" class="form-control form-control-sm" placeholder="Branch" disabled>' +
                    '</div>' +
                    '<div class="col-sm-4 col-md-auto flex-md-grow-1">' +
                        '<input type="text" id="filterDept" class="form-control form-control-sm" placeholder="Department" disabled>' +
                    '</div>' +
                '</div>';
            contentPadded.insertBefore(demoFilterBar, contentPadded.firstChild);
        }

        // Build the split pane if it doesn't exist yet (empty state shows a flat card instead)
        var splitPane = document.querySelector('.split-pane');
        if (!splitPane) {
            var filterBar = document.querySelector('.queue-filter-bar');

            splitPane = document.createElement('div');
            splitPane.className = 'split-pane shadow-sm';
            splitPane.setAttribute('data-tour-demo', 'aq-pane');

            var newLeft = document.createElement('div');
            newLeft.className = 'split-left';

            var leftHeader = document.createElement('div');
            leftHeader.className = 'px-3 py-2 border-bottom d-flex align-items-center justify-content-between';
            leftHeader.style.background = '#fff';
            leftHeader.innerHTML =
                '<div><span class="text-muted fw-bold" style="font-size:.78rem;">' +
                '<i class="bi bi-shield-exclamation text-danger"></i> Pending &mdash; My Department</span>' +
                '<span class="badge bg-danger ms-1" id="queueCountBadge" style="font-size:.7rem;">3</span></div>';
            newLeft.appendChild(leftHeader);

            var newRight = document.createElement('div');
            newRight.className = 'split-right';
            newRight.id = 'detailsPanel';
            newRight.innerHTML =
                '<div class="split-placeholder">' +
                '<i class="bi bi-arrow-left-circle" style="font-size:2.5rem;"></i>' +
                '<span style="font-size:.95rem;">Select a request to view details</span></div>';

            splitPane.appendChild(newLeft);
            splitPane.appendChild(newRight);

            if (filterBar) {
                filterBar.insertAdjacentElement('afterend', splitPane);
            } else {
                contentPadded.appendChild(splitPane);
            }
        }

        // Inject demo queue items into the left panel
        var leftPanel = document.querySelector('.split-left');
        if (leftPanel) {
            AQ_ITEMS.forEach(function (item) {
                leftPanel.insertAdjacentHTML('beforeend', _aqItemHtml(item));
            });
        }

        // Banner must go BEFORE the split pane (not inside it as a flex child)
        showBanner(splitPane);

        _active = true;
        return true;
    }

    function injectApproverDetailPanel() {
        var panel = document.getElementById('detailsPanel');
        if (!panel) return;
        if (panel.querySelector('[data-tour-demo="aq-detail-part"]')) return;

        panel.innerHTML = getApproverDetailHtml();

        // Highlight first queue item as active
        var firstItem = document.querySelector('.queue-item[data-tour-demo="aq-item"]')
                     || document.querySelector('.queue-item');
        if (firstItem) {
            document.querySelectorAll('.queue-item').forEach(function (i) { i.classList.remove('active'); });
            firstItem.classList.add('active');
        }
    }

    function cleanupApproverQueue() {
        removeByAttr('data-tour-demo', 'aq-item');
        removeByAttr('data-tour-demo', 'aq-pane');
        removeByAttr('data-tour-demo', 'aq-filter-bar');

        var detailsPanel = document.getElementById('detailsPanel');
        if (detailsPanel && detailsPanel.querySelector('[data-tour-demo="aq-detail-part"]')) {
            detailsPanel.innerHTML = '<div class="split-placeholder"><i class="bi bi-arrow-left-circle" style="font-size:2.5rem;"></i><span style="font-size:.95rem;">Select a request to view details</span></div>';
        }

        document.querySelectorAll('[data-tour-demo-hidden="aq"]').forEach(function (el) {
            el.style.display = '';
            el.removeAttribute('data-tour-demo-hidden');
        });

        document.querySelectorAll('.queue-item').forEach(function (i) { i.classList.remove('active'); });

        hideBanner();
        _active = false;
    }

    // Called when a demo queue item is clicked — simulates loadPanel without AJAX.
    function _selectDemoItem(el) {
        document.querySelectorAll('.queue-item').forEach(function (i) { i.classList.remove('active'); });
        el.classList.add('active');
        injectApproverDetailPanel();
    }

    // ── Approver History — sample rows for both tabs ─────────────────────────

    var AH_MY_ROWS = [
        {
            authId: '2001', status: 'Approved', badgeCls: 'bg-success',
            source: 'web', dept: 'Sales Department',
            models: '<div class="small">CE285A (HP 85A) (3)</div>',
            signedBy: 'J. Santos', dateSigned: 'Jun 10, 2026  3:00 PM',
            reqOn: 'Jun 10, 2026  9:00 AM'
        },
        {
            authId: '2002', status: 'Pending', badgeCls: 'bg-warning text-dark',
            source: 'web', dept: 'Sales Department',
            models: '<div class="small">CC388A (HP 88A) (2)</div>',
            signedBy: '&mdash;', dateSigned: '&mdash;',
            reqOn: 'Jun 16, 2026  10:30 AM'
        },
        {
            authId: '2003', status: 'Rejected', badgeCls: 'bg-danger',
            source: 'web', dept: 'Sales Department',
            models: '<div class="small">Q2612A (HP 12A) (4)</div>',
            signedBy: 'M. Cruz', dateSigned: 'Jun 5, 2026  2:00 PM',
            reqOn: 'Jun 5, 2026  8:30 AM'
        }
    ];

    var AH_SIGNED_ROWS = [
        {
            authId: '1085', status: 'Approved', badgeCls: 'bg-success',
            source: 'web', employee: 'Maria Santos', dept: 'Sales Department',
            models: '<div class="small">CE285A (HP 85A) (3)</div>',
            submitted: '06/10/2026', approvedOn: '06/10/2026'
        },
        {
            authId: '1091', status: 'Approved', badgeCls: 'bg-success',
            source: 'desktop', employee: 'Roberto Cruz', dept: 'Finance Department',
            models: '<div class="small">CF226A (HP 26A) (2)</div>',
            submitted: '06/12/2026', approvedOn: '06/12/2026'
        }
    ];

    function _ahMyRowHtml(row) {
        return '<tr data-auth-id="' + row.authId + '" data-tour-demo="ah-my-row" style="cursor:pointer;">' +
            '<td class="px-3 py-2 text-muted">#' + row.authId + '</td>' +
            '<td class="px-3 py-2"><span class="badge ' + row.badgeCls + '">' + row.status + '</span></td>' +
            '<td class="px-3 py-2">' + _asSourceBadge(row.source) + '</td>' +
            '<td class="px-3 py-2">' + row.dept + '</td>' +
            '<td class="px-3 py-2">' + row.models + '</td>' +
            '<td class="px-3 py-2">' + row.signedBy + '</td>' +
            '<td class="px-3 py-2 text-muted">' + row.dateSigned + '</td>' +
            '<td class="px-3 py-2 text-muted">' + row.reqOn + '</td>' +
        '</tr>';
    }

    function _ahSignedRowHtml(row) {
        return '<tr data-auth-id="' + row.authId + '" data-tour-demo="ah-signed-row" style="cursor:pointer;">' +
            '<td class="px-3 py-2 text-muted">' + row.authId + '</td>' +
            '<td class="px-3 py-2"><span class="badge ' + row.badgeCls + '">' + row.status + '</span></td>' +
            '<td class="px-3 py-2">' + _asSourceBadge(row.source) + '</td>' +
            '<td class="px-3 py-2 fw-semibold">' + row.employee + '</td>' +
            '<td class="px-3 py-2">' + row.dept + '</td>' +
            '<td class="px-3 py-2">' + row.models + '</td>' +
            '<td class="px-3 py-2 text-muted small">' + row.submitted + '</td>' +
            '<td class="px-3 py-2 text-muted small">' + row.approvedOn + '</td>' +
        '</tr>';
    }

    function getApproverHistoryDetailHtml() {
        return '' +
            '<div id="authDetailInfoSection" class="row g-3 mb-3 px-3 pt-3">' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Authorization ID</div>' +
                    '<div class="fw-semibold">#2001</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Status</div>' +
                    '<div><span class="badge bg-success">Approved</span></div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Department</div>' +
                    '<div class="fw-semibold">Sales Department</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Source</div>' +
                    '<div class="fw-semibold">Web Portal</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Requested On</div>' +
                    '<div class="fw-semibold">Jun 10, 2026  9:00 AM</div>' +
                '</div>' +
                '<div class="col-sm-6">' +
                    '<div class="text-muted small mb-1">Signed By</div>' +
                    '<div class="fw-semibold">J. Santos</div>' +
                '</div>' +
            '</div>' +
            '<hr class="my-2">' +
            '<div id="authDetailCartridgeSection" class="px-3 pb-3">' +
                '<div class="text-muted small mb-2 fw-semibold text-uppercase" style="letter-spacing:.05em;">Cartridge Models</div>' +
                '<div class="table-responsive">' +
                    '<table class="table table-bordered table-sm mb-0" style="font-size:.88rem;">' +
                        '<thead class="table-light">' +
                            '<tr>' +
                                '<th>Model</th>' +
                                '<th class="text-center" style="width:70px;">Qty</th>' +
                                '<th class="text-center">Returned Cartridges (Good)</th>' +
                                '<th class="text-center">Returned Cartridges (Damaged)</th>' +
                            '</tr>' +
                        '</thead>' +
                        '<tbody>' +
                            '<tr>' +
                                '<td><strong>CE285A (HP 85A)</strong></td>' +
                                '<td class="text-center">3</td>' +
                                '<td class="text-center">2</td>' +
                                '<td class="text-center">0</td>' +
                            '</tr>' +
                        '</tbody>' +
                    '</table>' +
                '</div>' +
            '</div>';
    }

    function injectApproverHistory() {
        if (document.querySelector('[data-tour-demo="ah-my-row"]')) return true;

        var myTbody     = document.getElementById('myHistoryTbody');
        var signedTbody = document.getElementById('signedTbody');

        // If myHistoryTbody doesn't exist, the empty-state card is showing — create the table
        if (!myTbody) {
            var myPane = document.getElementById('myHistoryPane');
            if (myPane) {
                var myEmptyCard = myPane.querySelector('.card');
                if (myEmptyCard) {
                    myEmptyCard.setAttribute('data-tour-demo-hidden', 'ah-my');
                    myEmptyCard.style.display = 'none';
                }
                var myTableWrap = document.createElement('div');
                myTableWrap.setAttribute('data-tour-demo', 'ah-my-table');
                myTableWrap.className = 'card shadow-sm';
                myTableWrap.innerHTML =
                    '<div class="table-responsive">' +
                        '<table class="table table-hover mb-0" style="font-size:0.9rem;">' +
                            '<thead style="background:#f1f5f9;">' +
                                '<tr>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">ID</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Status</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Source</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Department</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Cartridge Models</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Signed By</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Date Signed</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Requested On</th>' +
                                '</tr>' +
                            '</thead>' +
                            '<tbody id="myHistoryTbody"></tbody>' +
                        '</table>' +
                    '</div>';
                myPane.appendChild(myTableWrap);
                myTbody = document.getElementById('myHistoryTbody');
            }
        }

        // If signedTbody doesn't exist, the empty-state card is showing — create the table
        if (!signedTbody) {
            var signedPane = document.getElementById('signedPane');
            if (signedPane) {
                var signedEmptyCard = signedPane.querySelector('.card');
                if (signedEmptyCard) {
                    signedEmptyCard.setAttribute('data-tour-demo-hidden', 'ah-signed');
                    signedEmptyCard.style.display = 'none';
                }
                var signedTableWrap = document.createElement('div');
                signedTableWrap.setAttribute('data-tour-demo', 'ah-signed-table');
                signedTableWrap.className = 'card shadow-sm';
                signedTableWrap.innerHTML =
                    '<div class="table-responsive">' +
                        '<table class="table table-hover mb-0" style="font-size:0.9rem;">' +
                            '<thead style="background:#f1f5f9;">' +
                                '<tr>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">#</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Status</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Source</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Employee</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Department</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Model/s Requested</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Submitted</th>' +
                                    '<th class="px-3 py-3" style="color:#64748b;font-weight:600;">Approved On</th>' +
                                '</tr>' +
                            '</thead>' +
                            '<tbody id="signedTbody"></tbody>' +
                        '</table>' +
                    '</div>';
                signedPane.appendChild(signedTableWrap);
                signedTbody = document.getElementById('signedTbody');
            }
        }

        if (!myTbody && !signedTbody) return false;

        var injectedMyRows = false;
        if (myTbody && myTbody.querySelectorAll('tr:not([data-tour-demo])').length === 0) {
            myTbody.innerHTML = AH_MY_ROWS.map(_ahMyRowHtml).join('');
            injectedMyRows = true;
            showBanner(myTbody.closest('.card') || myTbody);
        }

        if (signedTbody && signedTbody.querySelectorAll('tr:not([data-tour-demo])').length === 0) {
            signedTbody.innerHTML = AH_SIGNED_ROWS.map(_ahSignedRowHtml).join('');
        }

        // Update My History tab badge when we injected demo rows
        var myBadge = document.querySelector('#my-history-tab .badge');
        if (myBadge && injectedMyRows) {
            myBadge.setAttribute('data-tour-demo-orig-count', myBadge.textContent);
            myBadge.textContent = AH_MY_ROWS.length;
            myBadge.setAttribute('data-tour-demo', 'ah-badge');
        }

        _active = true;
        return true;
    }

    function cleanupApproverHistory() {
        removeByAttr('data-tour-demo', 'ah-my-row');
        removeByAttr('data-tour-demo', 'ah-signed-row');
        removeByAttr('data-tour-demo', 'ah-my-table');
        removeByAttr('data-tour-demo', 'ah-signed-table');

        // Restore empty-state cards that were hidden to make room for demo tables
        document.querySelectorAll('[data-tour-demo-hidden="ah-my"]').forEach(function (el) {
            el.style.display = '';
            el.removeAttribute('data-tour-demo-hidden');
        });
        document.querySelectorAll('[data-tour-demo-hidden="ah-signed"]').forEach(function (el) {
            el.style.display = '';
            el.removeAttribute('data-tour-demo-hidden');
        });

        var badge = document.querySelector('[data-tour-demo="ah-badge"]');
        if (badge) {
            badge.textContent = badge.getAttribute('data-tour-demo-orig-count') || '0';
            badge.removeAttribute('data-tour-demo');
            badge.removeAttribute('data-tour-demo-orig-count');
        }

        hideBanner();
        _active = false;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    global.YakultTourDemo = {
        isActive:                  function () { return _active; },
        injectMyRequests:          injectMyRequests,
        cleanupMyRequests:         cleanupMyRequests,
        injectAuthStatus:          injectAuthStatus,
        cleanupAuthStatus:         cleanupAuthStatus,
        getAuthDetailHtml:         getAuthDetailHtml,
        injectAuthHistory:         injectAuthHistory,
        cleanupAuthHistory:        cleanupAuthHistory,
        injectApproverQueue:       injectApproverQueue,
        injectApproverDetailPanel: injectApproverDetailPanel,
        cleanupApproverQueue:      cleanupApproverQueue,
        getApproverDetailHtml:     getApproverDetailHtml,
        injectApproverHistory:     injectApproverHistory,
        cleanupApproverHistory:    cleanupApproverHistory,
        getApproverHistoryDetailHtml: getApproverHistoryDetailHtml,
        _selectDemoItem:           _selectDemoItem
    };

}(window));
