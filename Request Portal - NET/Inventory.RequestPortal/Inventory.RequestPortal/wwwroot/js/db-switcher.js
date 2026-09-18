/**
 * db-switcher.js
 * Shared DB Switcher logic for Login and authenticated pages.
 *
 * Usage:
 *   initDbSwitcher({ modalId, triggerId, labelId, statusId,
 *     btnCustomId, btnPresetId, btnLocalId,
 *     customPanelId, presetPanelId, localPanelId,
 *     presetSelectId, localSelectId,
 *     serverInputId, dbInputId,
 *     authSqlId, authWinId, sqlFieldsId,
 *     usernameInputId, passwordInputId,
 *     testApplyBtnId, switchSourceFn });
 */
function initDbSwitcher(cfg) {
    var trigger       = document.getElementById(cfg.triggerId);
    var modalEl       = document.getElementById(cfg.modalId);
    var label         = document.getElementById(cfg.labelId);
    var status        = document.getElementById(cfg.statusId);
    var btnCustom     = document.getElementById(cfg.btnCustomId);
    var btnPreset     = document.getElementById(cfg.btnPresetId);
    var btnLocal      = document.getElementById(cfg.btnLocalId);
    var customPanel   = document.getElementById(cfg.customPanelId);
    var presetPanel   = document.getElementById(cfg.presetPanelId);
    var localPanel    = document.getElementById(cfg.localPanelId);
    var presetSelect  = document.getElementById(cfg.presetSelectId);
    var localSelect   = document.getElementById(cfg.localSelectId);
    var serverInput   = document.getElementById(cfg.serverInputId);
    var dbInput       = document.getElementById(cfg.dbInputId);
    var authSql       = document.getElementById(cfg.authSqlId);
    var authWin       = document.getElementById(cfg.authWinId);
    var sqlFields     = document.getElementById(cfg.sqlFieldsId);
    var usernameInput = document.getElementById(cfg.usernameInputId);
    var passwordInput = document.getElementById(cfg.passwordInputId);
    var testApplyBtn  = document.getElementById(cfg.testApplyBtnId);

    // ── Helpers ───────────────────────────────────────────────────────────────

    function setStatus(html, type) {
        // type: 'info' | 'success' | 'error' | 'warning' | 'loading'
        var colors = {
            info:    { bg: 'rgba(33,150,243,0.1)',  color: '#1976D2' },
            success: { bg: 'rgba(76,175,80,0.1)',   color: '#2E7D32' },
            error:   { bg: 'rgba(244,67,54,0.1)',   color: '#C62828' },
            warning: { bg: 'rgba(255,152,0,0.1)',   color: '#E65100' },
            loading: { bg: 'rgba(158,158,158,0.1)', color: '#666'    }
        };
        var c = colors[type] || colors.info;
        if (status) {
            status.innerHTML = html;
            status.style.background = c.bg;
            status.style.color = c.color;
        }
    }

    async function refreshLabel() {
        try {
            var res  = await fetch('/DevTools/GetCurrentDatabase');
            var data = await res.json();
            if (label) label.textContent = data.database || 'DB';
        } catch (e) {
            if (label) label.textContent = 'DB';
        }
    }

    // ── Panel switching ───────────────────────────────────────────────────────

    function switchSource(source) {
        customPanel.style.display = source === 'custom' ? '' : 'none';
        presetPanel.style.display = source === 'preset' ? '' : 'none';
        localPanel.style.display  = source === 'local'  ? '' : 'none';

        btnCustom.className = 'btn btn-sm flex-fill ' + (source === 'custom' ? 'btn-primary' : 'btn-outline-secondary');
        btnPreset.className = 'btn btn-sm flex-fill ' + (source === 'preset' ? 'btn-primary' : 'btn-outline-secondary');
        btnLocal.className  = 'btn btn-sm flex-fill ' + (source === 'local'  ? 'btn-primary' : 'btn-outline-secondary');
    }

    // Expose as a named global so onclick="..." attributes work
    if (cfg.switchSourceFn) window[cfg.switchSourceFn] = switchSource;

    // ── Auth mode toggle (SQL / Windows) ──────────────────────────────────────

    function updateAuthFields() {
        if (!authWin) return;
        sqlFields.style.display = authWin.checked ? 'none' : '';
    }

    if (authSql) authSql.addEventListener('change', updateAuthFields);
    if (authWin) authWin.addEventListener('change', updateAuthFields);

    // ── Pre-fill custom fields from current connection ────────────────────────

    async function loadConnectionDetails() {
        try {
            var res  = await fetch('/DevTools/GetConnectionDetails');
            var data = await res.json();
            if (serverInput   && data.server)   serverInput.value   = data.server;
            if (dbInput       && data.database)  dbInput.value       = data.database;
            if (usernameInput && data.username)  usernameInput.value = data.username;
            if (data.useWindowsAuth) {
                if (authWin) authWin.checked = true;
            } else {
                if (authSql) authSql.checked = true;
            }
            updateAuthFields();
        } catch (e) { /* ignore — fields stay blank */ }
    }

    // ── Load preset databases ─────────────────────────────────────────────────

    async function loadPresetDatabases(current) {
        setStatus('Loading preset databases...', 'loading');
        try {
            var res  = await fetch('/DevTools/GetAvailableDatabases');
            var data = await res.json();
            var active = current || data.current || '';
            presetSelect.innerHTML = '';
            data.databases.forEach(function (db) {
                var opt = document.createElement('option');
                opt.value = db; opt.textContent = db;
                if (db === active) opt.selected = true;
                presetSelect.appendChild(opt);
            });
            setStatus('<i class="bi bi-info-circle-fill"></i> Current: <strong>' + (active || data.databases[0] || '') + '</strong>', 'info');
        } catch (e) {
            presetSelect.innerHTML = '<option>Error loading databases</option>';
            setStatus('<i class="bi bi-exclamation-triangle-fill"></i> Failed to load preset databases', 'error');
        }
    }

    // ── Load local databases ──────────────────────────────────────────────────

    async function loadLocalDatabases(selected) {
        setStatus('Loading local databases (Windows Auth)...', 'loading');
        try {
            var res  = await fetch('/DevTools/GetLocalDatabases');
            var data = await res.json();
            localSelect.innerHTML = '';
            if (!data.success || !data.databases || data.databases.length === 0) {
                localSelect.innerHTML = '<option value="">No databases found on localhost</option>';
                setStatus('<i class="bi bi-exclamation-triangle-fill"></i> ' + (data.message || 'No local databases found'), 'warning');
                return;
            }
            var active = selected || data.current || '';
            data.databases.forEach(function (db) {
                var opt = document.createElement('option');
                opt.value = db; opt.textContent = db;
                if (db === active) opt.selected = true;
                localSelect.appendChild(opt);
            });
            setStatus(active
                ? '<i class="bi bi-pc-display"></i> Current: <strong>' + active + '</strong> (Windows Auth)'
                : '<i class="bi bi-info-circle-fill"></i> ' + data.databases.length + ' local databases found', 'info');
        } catch (e) {
            localSelect.innerHTML = '<option value="">Failed to connect to localhost</option>';
            setStatus('<i class="bi bi-exclamation-triangle-fill"></i> Cannot reach local SQL Server', 'error');
        }
    }

    // ── Initialize modal state ────────────────────────────────────────────────

    async function initModal() {
        setStatus('Loading...', 'loading');
        try {
            var res  = await fetch('/DevTools/GetCurrentDatabase');
            var data = await res.json();
            var current = data.database || '';

            if (current.startsWith('[LOCAL]')) {
                switchSource('local');
                await loadLocalDatabases(current.replace('[LOCAL] ', '').trim());
            } else if (current.includes(' / ')) {
                // Custom connection (server / database format)
                switchSource('custom');
                await loadConnectionDetails();
                setStatus('<i class="bi bi-plug-fill"></i> Current: <strong>' + current + '</strong>', 'success');
            } else {
                switchSource('custom');
                await loadConnectionDetails();
                setStatus('<i class="bi bi-info-circle-fill"></i> Current: <strong>' + current + '</strong>', 'info');
            }
        } catch (e) {
            setStatus('<i class="bi bi-exclamation-triangle-fill"></i> Failed to detect current database', 'error');
            switchSource('custom');
        }
    }

    // ── Test & Apply ──────────────────────────────────────────────────────────

    if (testApplyBtn) {
        testApplyBtn.addEventListener('click', async function () {
            var server   = serverInput   ? serverInput.value.trim()   : '';
            var database = dbInput       ? dbInput.value.trim()       : '';
            var useWin   = authWin       ? authWin.checked            : false;
            var username = usernameInput ? usernameInput.value.trim() : '';
            var password = passwordInput ? passwordInput.value        : '';

            if (!server)   { setStatus('<i class="bi bi-exclamation-triangle-fill"></i> Server address is required.', 'error'); return; }
            if (!database) { setStatus('<i class="bi bi-exclamation-triangle-fill"></i> Database name is required.', 'error'); return; }
            if (!useWin && !username) { setStatus('<i class="bi bi-exclamation-triangle-fill"></i> Username is required for SQL Server auth.', 'error'); return; }

            testApplyBtn.disabled = true;
            setStatus('<i class="bi bi-hourglass-split"></i> Testing connection...', 'loading');

            try {
                var res = await fetch('/DevTools/SetCustomConnection', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        server:         server,
                        database:       database,
                        useWindowsAuth: useWin,
                        username:       useWin ? null : username,
                        password:       useWin ? null : password
                    })
                });
                var result = await res.json();
                if (result.success) {
                    setStatus('<i class="bi bi-check-circle-fill"></i> <strong>Connected!</strong> ' + result.message, 'success');
                    refreshLabel();
                } else {
                    setStatus('<i class="bi bi-x-circle-fill"></i> <strong>Failed:</strong> ' + result.message, 'error');
                }
            } catch (e) {
                setStatus('<i class="bi bi-exclamation-triangle-fill"></i> <strong>Error:</strong> ' + e.message, 'error');
            } finally {
                testApplyBtn.disabled = false;
            }
        });
    }

    // ── Preset select change ──────────────────────────────────────────────────

    if (presetSelect) {
        presetSelect.addEventListener('change', async function () {
            var db = presetSelect.value;
            if (!db) return;
            setStatus('Switching to: ' + db + '...', 'info');
            try {
                var res = await fetch('/DevTools/SetDatabase', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ databaseName: db })
                });
                var result = await res.json();
                if (result.success) {
                    setStatus('<i class="bi bi-check-circle-fill"></i> <strong>Switched!</strong> Now using: <strong>' + db + '</strong>', 'success');
                    refreshLabel();
                } else {
                    setStatus('<i class="bi bi-x-circle-fill"></i> <strong>Failed:</strong> ' + result.message, 'error');
                }
            } catch (e) {
                setStatus('<i class="bi bi-exclamation-triangle-fill"></i> <strong>Error:</strong> ' + e.message, 'error');
            }
        });
    }

    // ── Local select change ───────────────────────────────────────────────────

    if (localSelect) {
        localSelect.addEventListener('change', async function () {
            var db = localSelect.value;
            if (!db) return;
            setStatus('Switching to local: ' + db + '...', 'info');
            try {
                var res = await fetch('/DevTools/SetLocalDatabase', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ databaseName: db })
                });
                var result = await res.json();
                if (result.success) {
                    setStatus('<i class="bi bi-check-circle-fill"></i> <strong>Switched!</strong> Now using local: <strong>' + db + '</strong> (Windows Auth)', 'success');
                    refreshLabel();
                } else {
                    setStatus('<i class="bi bi-x-circle-fill"></i> <strong>Failed:</strong> ' + result.message, 'error');
                }
            } catch (e) {
                setStatus('<i class="bi bi-exclamation-triangle-fill"></i> <strong>Error:</strong> ' + e.message, 'error');
            }
        });
    }

    // ── Tab button click handlers ─────────────────────────────────────────────

    if (btnCustom) {
        btnCustom.addEventListener('click', async function () {
            switchSource('custom');
            await loadConnectionDetails();
        });
    }

    if (btnPreset) {
        btnPreset.addEventListener('click', async function () {
            switchSource('preset');
            await loadPresetDatabases('');
        });
    }

    if (btnLocal) {
        btnLocal.addEventListener('click', async function () {
            switchSource('local');
            await loadLocalDatabases('');
        });
    }

    // ── Trigger button opens modal ────────────────────────────────────────────

    refreshLabel();

    if (trigger && modalEl) {
        trigger.addEventListener('click', function (e) {
            e.preventDefault();
            var bsModal = new bootstrap.Modal(modalEl);
            bsModal.show();
            initModal();
        });
    }
}
