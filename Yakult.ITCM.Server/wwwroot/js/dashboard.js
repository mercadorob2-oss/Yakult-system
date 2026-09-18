/* ═══════════════════════════════════════════════════════════════
   Yakult ITCM Server — dashboard.js
   ═══════════════════════════════════════════════════════════════ */

const statusUrl  = '/api/itcm/scheduler/status';
const historyUrl = '/api/itcm/heartbeat/history';

let currentHistoryPage = 1;
const historyPageSize  = 20;
let _historyData = [];
let _statusFilter = 'all';
let _searchQuery  = '';

// ── Formatting ───────────────────────────────────────────────────────────────
function fmt(value) {
  if (!value) return '-';
  const d = new Date(value);
  return isNaN(d) ? String(value) : d.toLocaleString();
}
function setText(id, value) {
  const el = document.getElementById(id);
  if (el) el.textContent = value ?? '-';
}
function setPill(id, text, tone = 'muted') {
  const el = document.getElementById(id);
  if (!el) return;
  el.className = `pill ${tone}`;
  el.textContent = text;
}
function setTabBadge(id, text, attention = false) {
  const el = document.getElementById(id);
  if (!el) return;
  if (text === null || text === undefined || text === '') {
    el.hidden = true;
    return;
  }
  el.hidden = false;
  el.textContent = text;
  el.classList.toggle('attention', !!attention);
}
function fmtCountdown(value) {  if (!value) return '-';
  const d = new Date(value);
  if (isNaN(d)) return '-';
  const diffS = Math.round((d.getTime() - Date.now()) / 1000);
  if (diffS < -60) return 'overdue';
  if (diffS < 60) return 'starting…';
  const m = Math.floor(diffS / 60);
  if (m < 60) return `in ${m} min`;
  const h = Math.floor(m / 60);
  if (h < 48) return `in ${h}h`;
  const days = Math.floor(h / 24);
  return days === 1 ? 'in 1 day' : `in ${days} days`;
}
function fmtDuration(totalSeconds) {
  if (totalSeconds === null || totalSeconds === undefined) return '-';
  const s = Math.max(0, Math.round(totalSeconds));
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  if (m < 60) {
    const r = s % 60;
    return r ? `${m}m ${r}s` : `${m}m`;
  }
  const h = Math.floor(m / 60);
  const rm = m % 60;
  return rm ? `${h}h ${rm}m` : `${h}h`;
}

async function getJson(url) {
  const res = await fetch(url, { cache: 'no-store', credentials: 'same-origin' });
  if (res.status === 401) {
    window.location.href = `/Account/Login?ReturnUrl=${encodeURIComponent(window.location.pathname)}`;
    throw new Error('Authentication required');
  }
  if (!res.ok) throw new Error(`Request failed (${res.status})`);
  return await res.json();
}

// ── Theme ────────────────────────────────────────────────────────────────────
function applyTheme(dark) {
  document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  const icon  = document.getElementById('themeIcon');
  const label = document.getElementById('themeLabel');
  const mob   = document.getElementById('themeToggleMobile');
  if (icon)  icon.textContent  = dark ? '☀️' : '🌙';
  if (label) label.textContent = dark ? 'Light mode' : 'Dark mode';
  if (mob)   mob.textContent   = dark ? '☀️' : '🌙';
}
function toggleTheme() {
  const dark = document.documentElement.getAttribute('data-theme') !== 'dark';
  localStorage.setItem('itcm-theme', dark ? 'dark' : 'light');
  applyTheme(dark);
}
// Apply on load (also handled inline in _Layout for flash prevention)
(function () {
  const t = localStorage.getItem('itcm-theme');
  const dark = t === 'dark' || (!t && window.matchMedia('(prefers-color-scheme: dark)').matches);
  applyTheme(dark);
})();

// ── Sidebar ──────────────────────────────────────────────────────────────────
function toggleSidebar() {
  const sidebar  = document.getElementById('sidebar');
  const content  = document.getElementById('pageContent');
  const overlay  = document.getElementById('sidebarOverlay');
  const isMobile = window.innerWidth <= 768;

  if (isMobile) {
    sidebar.classList.toggle('mobile-open');
    overlay.classList.toggle('show', sidebar.classList.contains('mobile-open'));
  } else {
    sidebar.classList.toggle('collapsed');
    content.classList.toggle('expanded', sidebar.classList.contains('collapsed'));
    localStorage.setItem('itcm-sidebar', sidebar.classList.contains('collapsed') ? '1' : '0');
  }
}
function closeSidebar() {
  document.getElementById('sidebar')?.classList.remove('mobile-open');
  document.getElementById('sidebarOverlay')?.classList.remove('show');
}
// Restore sidebar state on load
(function () {
  if (window.innerWidth > 768 && localStorage.getItem('itcm-sidebar') === '1') {
    document.getElementById('sidebar')?.classList.add('collapsed');
    document.getElementById('pageContent')?.classList.add('expanded');
  }
})();

// ── Toast notifications ──────────────────────────────────────────────────────
function showToast(message, type = 'info', ms = 3500) {
  const container = document.getElementById('toastContainer');
  if (!container) return;
  const icons = { success: '✅', error: '❌', info: 'ℹ️', warn: '⚠️' };
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.setAttribute('role', 'status');
  el.innerHTML = `<span aria-hidden="true">${icons[type] ?? 'ℹ️'}</span><span>${message}</span>`;
  container.appendChild(el);
  setTimeout(() => {
    el.classList.add('fade-out');
    el.addEventListener('animationend', () => el.remove(), { once: true });
  }, ms);
}

// ── Connection banner ────────────────────────────────────────────────────────
function showConnBanner(show) {
  document.getElementById('connBanner')?.classList.toggle('show', !!show);
}

// ── document.title status ───────────────────────────────────────────────────
function updateTitle(state) {
  const base = 'Yakult ITCM Server';
  if (!state)            { document.title = base; return; }
  if (state.isRunning)   document.title = `[Running] ${base}`;
  else if (state.isPaused) document.title = `[Paused] ${base}`;
  else                   document.title = `[Idle] ${base}`;
}

// ── Hero status banner ───────────────────────────────────────────────────────
function updateStatusHero(s) {
  const hero = document.getElementById('statusHero');
  const icon = document.getElementById('statusHeroIcon');
  const meta = document.getElementById('statusHeroMeta');
  if (!hero) return;

  hero.className = 'status-hero';
  if (s.isRunning) {
    hero.classList.add('state-running');
    if (icon) icon.textContent = '▶';
    setText('schedulerState', 'Running');
    setPill('schedulerStatusPill', 'Running', 'ok');
  } else if (s.isPaused) {
    hero.classList.add('state-paused');
    if (icon) icon.textContent = '⏸';
    setText('schedulerState', 'Paused');
    setPill('schedulerStatusPill', 'Paused', 'paused');
  } else if (s.lastRunSucceeded === false) {
    hero.classList.add('state-error');
    if (icon) icon.textContent = '!';
    setText('schedulerState', 'Attention needed');
    setPill('schedulerStatusPill', 'Failed', 'fail');
  } else {
    hero.classList.add('state-idle');
    if (icon) icon.textContent = '○';
    setText('schedulerState', 'Idle');
    setPill('schedulerStatusPill', 'Ready', 'idle');
  }

  const lastLine = document.getElementById('lastRunLine');
  const nextLine = document.getElementById('nextRunLine');
  if (lastLine) lastLine.textContent = s.lastCompletedAt ? `Last run ${fmt(s.lastCompletedAt)}` : 'No runs recorded yet';
  if (nextLine) nextLine.textContent = s.nextRunAt ? `Next run ${fmt(s.nextRunAt)}` : '';
  syncPauseResumeButton(s);
  updateLiveSummary(s);
}

// ── Pause/Resume toggle (single hero button) ─────────────────────────────
function syncPauseResumeButton(s) {
  const btn = document.getElementById('pauseResumeBtn');
  if (!btn) return;
  if (s.isPaused) {
    btn.dataset.mode = 'resume';
    btn.className = 'success';
    btn.innerHTML = '▶ <span>Resume</span>';
  } else {
    btn.dataset.mode = 'pause';
    btn.className = 'warn';
    btn.innerHTML = '⏸ <span>Pause</span>';
  }
}

function togglePauseResume() {
  const btn = document.getElementById('pauseResumeBtn');
  if (btn && btn.dataset.mode === 'resume') resumeScheduler();
  else pauseScheduler();
}

// ── Live strip summary ───────────────────────────────────────────────────
function updateLiveSummary(s) {
  const el = document.getElementById('liveSummary');
  if (!el || !s) return;
  const state = s.isRunning ? 'Running' : s.isPaused ? 'Paused' : 'Idle';
  el.textContent = s.nextRunAt ? `${state} · Next run ${fmt(s.nextRunAt)}` : state;
}

// ── Status ───────────────────────────────────────────────────────────────────
async function loadStatus() {
  try {
    const s = await getJson(statusUrl);

    updateStatusHero(s);
    setText('totalRuns',          s.totalRuns);
    setText('successfulRuns',     s.successfulRuns);
    setText('failedRuns',         s.failedRuns);
    setText('nextRunAt',          fmt(s.nextRunAt));
    const _total = Number(s.totalRuns) || 0;
    const _ok = Number(s.successfulRuns) || 0;
    const _failed = Number(s.failedRuns) || 0;
    setText('totalRunsSub', _total > 0 ? `${_ok} succeeded` : '-');
    setText('successRateSub', _total > 0 ? `${Math.round((_ok / _total) * 100)}% success rate` : '-');
    setText('failedRunsSub', _total > 0 ? (_failed > 0 ? 'Needs attention' : 'All clear') : '-');
    setText('nextRunRel', fmtCountdown(s.nextRunAt));
    setTabBadge('historyBadge', _failed > 0 ? String(_failed) : '', _failed > 0);
    const _hb = document.getElementById('historyBadge');
    if (_hb) _hb.title = _failed > 0 ? `${_failed} failed run(s)` : '';
    setText('lastStartedAt',      fmt(s.lastStartedAt));
    setText('lastCompletedAt',    fmt(s.lastCompletedAt));
    setText('lastRunSucceeded',   s.lastRunSucceeded === null ? '-' : (s.lastRunSucceeded ? '✓ Success' : '✗ Failed'));
    setText('lastError',          s.lastError || '-');
    setText('reminderCandidates', s.lastRunDetails?.reminderCandidates ?? '-');
    setText('remindersSent',      s.lastRunDetails?.remindersSent      ?? '-');
    setText('escalationCandidates', s.lastRunDetails?.escalationCandidates ?? '-');
    setText('escalationsApplied',   s.lastRunDetails?.escalationsApplied   ?? '-');
    setText('lockAcquired',       s.lastRunDetails?.lockAcquired === null ? '-' : (s.lastRunDetails.lockAcquired ? 'Yes' : 'No'));
    setText('rawStatus',          JSON.stringify(s, null, 2));
    showConnBanner(false);
    updateTitle(s);
  } catch (e) {
    console.error('loadStatus failed:', e);
    showConnBanner(true);
    updateTitle(null);
  }
}

// ── Settings ─────────────────────────────────────────────────────────────────
async function loadSettings() {
  try {
    const d = await getJson('/api/itcm/scheduler/settings');
    setText('settingsEnabled',    d.enabled ? 'Yes' : 'No');
    setText('settingsInterval',   d.intervalMinutes);
    setText('settingsMaxTickets', d.maxTicketsPerRun);
  } catch {}
}

// ── Heartbeat ─────────────────────────────────────────────────────────────────
async function loadHeartbeat() {
  try {
    const h = await getJson('/api/itcm/heartbeat');
    setText('hbLastStarted',  fmt(h.lastStartedUtc));
    setText('hbLastFinished', fmt(h.lastFinishedUtc));
    setText('hbLastSuccess',  fmt(h.lastSuccessUtc));
    setText('hbMachine',      h.machineName || '-');
    setText('hbVersion',      h.version     || '-');
  } catch {}
}

// ── Client presence ────────────────────────────────────────────────────────
let _presenceReport = null;
let _presenceMinutes = 15;
let _deviceStatusFilter = 'all';
let _deviceSearchQuery = '';

function fmtRelative(value) {
  if (!value) return '';
  const d = new Date(value);
  if (isNaN(d)) return '';
  const s = Math.max(0, Math.round((Date.now() - d.getTime()) / 1000));
  if (s < 60) return `${s}s ago`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 48) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}
function fmtAge(fromValue, toValue) {
  const a = new Date(fromValue), b = new Date(toValue);
  if (isNaN(a) || isNaN(b)) return '—';
  const s = Math.max(0, Math.round((b - a) / 1000));
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  if (h < 48) return `${h}h`;
  const d = Math.floor(h / 24);
  return d === 1 ? '1 day' : `${d} days`;
}
function lastSeenCell(value) {
  const wrap = document.createElement('span');
  wrap.textContent = fmt(value);
  const rel = fmtRelative(value);
  if (rel) {
    const r = document.createElement('span');
    r.className = 'rel-time';
    r.textContent = rel;
    wrap.appendChild(document.createTextNode(' '));
    wrap.appendChild(r);
  }
  return wrap;
}

async function loadPresence() {
  try {
    _presenceReport = await getJson(`/api/itcm/presence?onlineMinutes=${_presenceMinutes}`);
    renderPresenceSummary(_presenceReport);
    renderDevices(_presenceReport);
  } catch {}
}
function setPresenceThreshold(v) {
  const n = parseInt(v, 10);
  _presenceMinutes = (isNaN(n) || n < 1) ? 15 : Math.min(n, 1440);
  loadPresence();
}
function setDeviceFilter(status) {
  _deviceStatusFilter = status;
  document.querySelectorAll('[data-devstatus]').forEach(chip => {
    chip.classList.toggle('active', chip.dataset.devstatus === status);
  });
  renderDevices(_presenceReport);
}
function applyDeviceFilter() {
  _deviceSearchQuery = (document.getElementById('deviceSearch')?.value || '').toLowerCase();
  renderDevices(_presenceReport);
}
function clearDeviceFilter() {
  _deviceSearchQuery = '';
  _deviceStatusFilter = 'all';
  const search = document.getElementById('deviceSearch');
  if (search) search.value = '';
  const hide = document.getElementById('hideStaleDevices');
  if (hide) hide.checked = false;
  document.querySelectorAll('[data-devstatus]').forEach(chip => chip.classList.toggle('active', chip.dataset.devstatus === 'all'));
  renderDevices(_presenceReport);
}
function filterDeviceItems() {
  const items = _presenceReport?.clients || [];
  const hideStale = document.getElementById('hideStaleDevices')?.checked;
  return items.filter(c => {
    if (_deviceStatusFilter === 'online' && !c.online) return false;
    if (_deviceStatusFilter === 'stale' && c.online) return false;
    if (hideStale && !c.online) return false;
    if (_deviceSearchQuery) {
      const hay = `${c.machineName || ''} ${c.userName || ''} ${c.module || ''} ${c.clientVersion || ''}`.toLowerCase();
      if (!hay.includes(_deviceSearchQuery)) return false;
    }
    return true;
  });
}
function renderPresenceSummary(report) {
  const count = document.getElementById('clientPresenceCount');
  if (count) {
    const n = report?.onlineCount ?? 0;
    count.textContent = `${n} online`;
    count.className = `pill ${n > 0 ? 'ok' : 'muted'}`;
  }
  const tbody = document.getElementById('clientPresenceSummary');
  if (!tbody) return;
  tbody.innerHTML = '';
  const items = report?.clients || [];
  if (!items.length) {
    monitoringEmptyRow(tbody, 4, 'No desktop clients have called the server yet.');
    return;
  }
  items.filter(c => c.online).slice(0, 3).forEach(c => {
    const row = document.createElement('tr');
    appendCells(row, [
      createPill('Online', 'ok'),
      c.machineName || '—',
      c.userName || '—',
      lastSeenCell(c.lastSeenUtc)
    ]);
    tbody.appendChild(row);
  });
  const stale = items.length - items.filter(c => c.online).length;
  if (!items.some(c => c.online)) {
    monitoringEmptyRow(tbody, 4, stale > 0 ? `${stale} known device(s), none online in the last ${_presenceMinutes} min. See the Devices tab.` : 'No desktop clients have called the server yet.');
  } else if (items.filter(c => c.online).length > 3 || stale > 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 4;
    cell.style.textAlign = 'center';
    const link = document.createElement('span');
    link.className = 'filter-clear';
    link.textContent = `See all ${items.length} in the Devices tab ›`;
    link.onclick = () => activateDashboardTab('devices');
    cell.appendChild(link);
    row.appendChild(cell);
    tbody.appendChild(row);
  }
}
function renderDevices(report) {
  const n = report?.onlineCount ?? 0;
  const total = report?.clients?.length ?? 0;
  const count = document.getElementById('devicePresenceCount');
  if (count) {
    count.textContent = `${n} online / ${total} known`;
    count.className = `pill ${n > 0 ? 'ok' : 'muted'}`;
  }
  const badge = document.getElementById('devicePresenceBadge');
  if (badge) badge.textContent = n > 0 ? `${n} ONLINE` : 'NO DEVICES ONLINE';
  setTabBadge('devicesBadge', total > 0 ? `${n} online` : '', false);
  const tbody = document.getElementById('devicePresenceBody');
  if (!tbody) return;
  tbody.innerHTML = '';
  const items = filterDeviceItems();
  if (!items.length) {
    monitoringEmptyRow(tbody, 8, total === 0 ? 'No desktop clients have called the server yet.' : 'No devices match the current filter.');
    return;
  }
  items.forEach(c => {
    const row = document.createElement('tr');
    appendCells(row, [
      createPill(c.online ? 'Online' : 'Stale', c.online ? 'ok' : 'muted'),
      c.machineName || '—',
      c.userName || '—',
      c.module || '—',
      c.clientVersion || '—',
      fmt(c.firstSeenUtc),
      lastSeenCell(c.lastSeenUtc),
      fmtAge(c.firstSeenUtc, c.lastSeenUtc)
    ]);
    tbody.appendChild(row);
  });
}
function exportPresenceCSV() {
  const items = filterDeviceItems();
  if (!items.length) { showToast('No device data to export', 'warn'); return; }
  const headers = ['Status','Machine','User','Module','Version','FirstSeenUtc','LastSeenUtc'];
  const q = v => `"${String(v ?? '').replace(/"/g, '""')}"`;
  const rows = items.map(c => [
    c.online ? 'Online' : 'Stale',
    q(c.machineName), q(c.userName), q(c.module), q(c.clientVersion),
    c.firstSeenUtc ? new Date(c.firstSeenUtc).toISOString() : '',
    c.lastSeenUtc ? new Date(c.lastSeenUtc).toISOString() : ''
  ].join(','));
  const blob = new Blob([[headers.join(','), ...rows].join('\r\n')], { type: 'text/csv' });
  const a = Object.assign(document.createElement('a'), { href: URL.createObjectURL(blob), download: `itcm-devices-${new Date().toISOString().slice(0, 10)}.csv` });
  a.click();
  URL.revokeObjectURL(a.href);
  showToast('Devices exported as CSV', 'success');
}

// ── Monitoring dashboard ─────────────────────────────────────────────────────
function appendCells(row, values) {
  values.forEach(value => {
    const cell = document.createElement('td');
    if (value instanceof Node) cell.appendChild(value);
    else cell.textContent = value ?? '—';
    row.appendChild(cell);
  });
}
function monitoringEmptyRow(tbody, columns, message, action) {
  const row = document.createElement('tr');
  const cell = document.createElement('td');
  cell.colSpan = columns;
  cell.textContent = message;
  cell.style.color = 'var(--muted)';
  cell.style.textAlign = 'center';
  cell.style.padding = '20px';
  if (action && action.label && typeof action.onClick === 'function') {
    cell.appendChild(document.createTextNode(' '));
    const btn = document.createElement('button');
    btn.className = 'ghost sm';
    btn.textContent = action.label;
    btn.addEventListener('click', action.onClick);
    cell.appendChild(btn);
  }
  row.appendChild(cell);
  tbody.appendChild(row);
}
function createPill(text, className) {
  const pill = document.createElement('span');
  pill.className = `pill ${className}`;
  pill.textContent = text;
  return pill;
}
function trunc(value, length = 72) {
  const text = value || '';
  return text.length > length ? `${text.slice(0, length - 1)}…` : text || '—';
}
// Expandable truncated cell: tap (or Enter) toggles between the short
// preview and the full text. Full text stays in the title for hover/long-press.
function expandableCell(fullText, length = 72) {
  const span = document.createElement('span');
  const full = fullText || '';
  if (!full || full.length <= length) {
    span.textContent = full || '—';
    return span;
  }
  span.className = 'expandable-cell';
  span.textContent = trunc(full, length);
  span.title = full;
  span.dataset.expanded = 'false';
  span.setAttribute('role', 'button');
  span.setAttribute('tabindex', '0');
  const toggle = () => {
    const expanded = span.dataset.expanded === 'true';
    span.dataset.expanded = String(!expanded);
    span.textContent = expanded ? trunc(full, length) : full;
  };
  span.addEventListener('click', toggle);
  span.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
  });
  return span;
}
function renderMonitoring(data) {
  setText('unassignedPortalCount', data.unassignedPortalTicketCount ?? 0);
  setText('recentAutoEscalationCount', data.recentAutoEscalationCount ?? 0);
  setText('reminderEmailsToday', data.reminderEmailsSentToday ?? 0);
  const _unassigned = Number(data.unassignedPortalTicketCount) || 0;
  setTabBadge('monitoringBadge', _unassigned > 0 ? String(_unassigned) : '', _unassigned > 0);
  const _mb = document.getElementById('monitoringBadge');
  if (_mb) _mb.title = _unassigned > 0 ? `${_unassigned} unassigned Portal ticket(s)` : '';

  const triageBody = document.getElementById('portalTriageBody');
  if (triageBody) {
    triageBody.innerHTML = '';
    const items = data.unassignedPortalTickets || [];
    const triageCols = canAssignTickets() ? 5 : 4;
    if (!items.length) monitoringEmptyRow(triageBody, triageCols, 'No unassigned Portal tickets — queue is clear ✓.');
    items.forEach(ticket => {
      const row = document.createElement('tr');
      const callerIssue = document.createElement('div');
      callerIssue.className = 'two-line-cell';
      const callerName = document.createElement('strong');
      callerName.textContent = ticket.callerName || 'Unknown';
      const issueText = document.createElement('span');
      issueText.textContent = trunc(ticket.issue, 90);
      issueText.title = ticket.issue || '';
      callerIssue.appendChild(callerName);
      callerIssue.appendChild(issueText);
      appendCells(row, [ticket.ticketCode || `#${ticket.ticketId}`, callerIssue, ticket.priority || '—', fmt(ticket.createdAt)]);
      if (canAssignTickets()) {
        const actionCell = document.createElement('td');
        const assignBtn = document.createElement('button');
        assignBtn.className = 'ghost sm';
        assignBtn.textContent = 'Assign';
        assignBtn.setAttribute('aria-label', `Assign ${ticket.ticketCode || `ticket ${ticket.ticketId}`}`);
        assignBtn.onclick = () => openAssignModal(ticket.ticketId, ticket.ticketCode || `#${ticket.ticketId}`);
        actionCell.appendChild(assignBtn);
        row.appendChild(actionCell);
      }
      triageBody.appendChild(row);
    });
  }

  const schedulerBody = document.getElementById('schedulerActivityBody');
  if (schedulerBody) {
    schedulerBody.innerHTML = '';
    const items = data.recentSchedulerActivity || [];
    if (!items.length) monitoringEmptyRow(schedulerBody, 4, 'No scheduler activity recorded yet.',
      canAssignTickets() ? { label: '▶ Run now', onClick: runNow } : null);
    items.forEach(activity => {
      const row = document.createElement('tr');
      const completed = activity.succeeded === null ? createPill('Unknown', 'muted') : createPill(activity.succeeded ? 'Success' : 'Failed', activity.succeeded ? 'ok' : 'fail');
      appendCells(row, [`#${activity.heartbeatId}`, completed, activity.remindersSent ?? 0, activity.escalationsApplied ?? 0]);
      schedulerBody.appendChild(row);
    });
  }

  const movementBody = document.getElementById('ticketMovementBody');
  if (movementBody) {
    movementBody.innerHTML = '';
    const items = data.recentTicketMovements || [];
    if (!items.length) monitoringEmptyRow(movementBody, 7, 'No ticket movements recorded yet.');
    items.forEach(movement => {
      const row = document.createElement('tr');
      const change = movement.isAutoEscalation ? createPill('Auto-escalation', 'fail') : movement.fieldName || 'Update';
      const actor = movement.changedByUserId ? `User #${movement.changedByUserId}` : movement.isAutoEscalation ? 'ITCM Server' : 'System';
      appendCells(row, [fmt(movement.changedAt), movement.ticketCode || `#${movement.ticketId}`, change, trunc(movement.oldValue, 30), trunc(movement.newValue, 30), actor, expandableCell(movement.note, 72)]);
      movementBody.appendChild(row);
    });
  }

  const notificationBody = document.getElementById('notificationActivityBody');
  if (notificationBody) {
    notificationBody.innerHTML = '';
    const items = data.recentNotifications || [];
    if (!items.length) monitoringEmptyRow(notificationBody, 6, 'No reminder or escalation deliveries yet.');
    items.forEach(notification => {
      const row = document.createElement('tr');
      const status = createPill(notification.status || 'Unknown', (notification.status || '').toLowerCase() === 'sent' ? 'ok' : (notification.status || '').toLowerCase() === 'failed' ? 'fail' : 'muted');
      appendCells(row, [fmt(notification.dateSent), notification.ticketCode || (notification.ticketId ? `#${notification.ticketId}` : '—'), notification.emailType || '—', status, expandableCell(notification.recipient, 60), expandableCell(notification.errorMessage, 70)]);
      notificationBody.appendChild(row);
    });
  }
}
function renderMonitoringUnavailable(message) {
  const tables = [
    ['portalTriageBody', canAssignTickets() ? 5 : 4],
    ['schedulerActivityBody', 4],
    ['ticketMovementBody', 7],
    ['notificationActivityBody', 6]
  ];
  tables.forEach(([id, columns]) => {
    const body = document.getElementById(id);
    if (!body) return;
    body.innerHTML = '';
    monitoringEmptyRow(body, columns, message);
  });
}
async function loadMonitoring() {
  try {
    renderMonitoring(await getJson('/api/itcm/monitoring?maxRows=12'));
  } catch (e) {
    console.error('loadMonitoring failed:', e);
    renderMonitoringUnavailable('Monitoring data is temporarily unavailable. Refresh after checking the Server connection.');
  }
}

// ── Sparkline ─────────────────────────────────────────────────────────────────
function drawSparkline(items) {
  const svg = document.getElementById('sparkline');
  if (!svg || !items?.length) return;
  svg.innerHTML = '';
  const W = 400, H = 36, n = Math.min(items.length, 40);
  const recent = items.slice(0, n).reverse();
  const barW   = Math.max(Math.floor(W / n) - 2, 2);

  recent.forEach((item, i) => {
    const x     = Math.floor(i * (W / n));
    const ok    = item.succeeded;
    const color = ok === null ? 'var(--muted-2)' : ok ? 'var(--green)' : 'var(--red)';
    const h     = ok === null ? 8 : ok ? H : Math.floor(H * 0.55);
    const rect  = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    rect.setAttribute('x',       x);
    rect.setAttribute('y',       H - h);
    rect.setAttribute('width',  barW);
    rect.setAttribute('height', h);
    rect.setAttribute('fill',   color);
    rect.setAttribute('rx',     '2');
    rect.setAttribute('opacity','0.85');
    const title = document.createElementNS('http://www.w3.org/2000/svg', 'title');
    title.textContent = `${item.startedAtUtc ? new Date(item.startedAtUtc).toLocaleString() : '?'} — ${ok === null ? 'Unknown' : ok ? 'Success' : 'Failed'}`;
    rect.appendChild(title);
    svg.appendChild(rect);
  });
}

// ── History ───────────────────────────────────────────────────────────────────
async function loadHistory(page = 1) {
  currentHistoryPage = Math.max(1, page);
  try {
    const data = await getJson(`${historyUrl}?page=${currentHistoryPage}&pageSize=${historyPageSize}`);
    _historyData = data.items || [];
    drawSparkline(_historyData);
    renderTimeline(_historyData);
    renderHistoryRows();

    setText('pageInfo', `Page ${data.page} / ${data.totalPages || 1}`);
    const prev = document.getElementById('prevPage');
    const next = document.getElementById('nextPage');
    if (prev) prev.disabled = data.page <= 1;
    if (next) next.disabled = data.page >= data.totalPages;
  } catch (e) {
    console.error('loadHistory failed:', e);
  }
}

// ── History filtering ────────────────────────────────────────────────────────
function setStatusFilter(status) {
  _statusFilter = status;
  document.querySelectorAll('#panel-history .filter-chip').forEach(chip => {
    chip.classList.toggle('active', chip.dataset.status === status);
  });
  renderHistoryRows();
}
function applyHistoryFilter() {
  _searchQuery = (document.getElementById('historySearch')?.value || '').toLowerCase();
  renderHistoryRows();
}
function clearHistoryFilter() {
  _searchQuery = '';
  _statusFilter = 'all';
  const search = document.getElementById('historySearch');
  if (search) search.value = '';
  document.querySelectorAll('#panel-history .filter-chip').forEach(chip => chip.classList.toggle('active', chip.dataset.status === 'all'));
  renderHistoryRows();
}
function filterHistoryItems() {
  return _historyData.filter(item => {
    if (_statusFilter === 'success' && item.succeeded !== true)  return false;
    if (_statusFilter === 'failed'  && item.succeeded !== false) return false;
    if (_searchQuery) {
      const haystack = `${item.machineName || ''} ${item.errorMessage || ''} ${item.heartbeatId}`.toLowerCase();
      if (!haystack.includes(_searchQuery)) return false;
    }
    return true;
  });
}

function renderHistoryRows() {
  const tbody = document.getElementById('historyBody');
  if (!tbody) return;
  const filtered = filterHistoryItems();
  tbody.innerHTML = '';

  if (_historyData.length === 0) {
    tbody.innerHTML = `<tr><td colspan="10"><div class="empty-state">
      <span class="empty-icon" aria-hidden="true">📭</span>
      <span class="empty-title">No run history yet</span>
      <span class="empty-msg">Once the scheduler runs, results will appear here.</span>
      ${canAssignTickets() ? '<button class="primary sm" onclick="runNow()">▶ Run now</button>' : ''}
    </div></td></tr>`;
    return;
  }
  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="10"><div class="empty-state">
      <span class="empty-icon" aria-hidden="true">🔍</span>
      <span class="empty-title">No matching runs</span>
      <span class="empty-msg">Try a different search term or clear the filter.</span>
      <button class="ghost sm" onclick="clearHistoryFilter()">Clear filter</button>
    </div></td></tr>`;
    return;
  }

  for (const item of filtered) {
    const started  = new Date(item.startedAtUtc);
    const finished = item.finishedAtUtc ? new Date(item.finishedAtUtc) : null;
    const duration = finished ? Math.round((finished - started) / 1000) : null;
    const statusHtml = item.succeeded === null
      ? '<span class="pill muted">Unknown</span>'
      : item.succeeded
        ? '<span class="pill ok">Success</span>'
        : '<span class="pill fail">Failed</span>';
    const lockHtml = item.lockAcquired
      ? '<span class="pill ok">Yes</span>'
      : '<span class="pill muted">No</span>';
    const err = item.errorMessage || '';

    const tr = document.createElement('tr');
    if (item.succeeded === false) tr.classList.add('row-fail');
    tr.innerHTML = `
      <td style="color:var(--muted);font-size:12px">${item.heartbeatId}</td>
      <td>${fmt(started)}</td>
      <td>${finished ? fmt(finished) : '<span style="color:var(--muted-2)">—</span>'}</td>
      <td>${statusHtml}</td>
      <td>${duration !== null ? fmtDuration(duration) : '<span style="color:var(--muted-2)">—</span>'}</td>
      <td>${item.remindersSent ?? 0} / ${item.reminderCandidates ?? 0}</td>
      <td>${item.escalationsApplied ?? 0} / ${item.escalationCandidates ?? 0}</td>
      <td>${lockHtml}</td>
      <td style="font-size:12px">${item.machineName || '<span style="color:var(--muted-2)">—</span>'}</td>
      <td></td>`;
    const errCell = tr.querySelector('td:last-child');
    if (err) {
      const errNode = expandableCell(err, 50);
      errNode.style.color = 'var(--red)';
      errNode.style.fontSize = '12px';
      errCell.appendChild(errNode);
    } else {
      errCell.innerHTML = '<span style="color:var(--muted-2)">—</span>';
    }
    tbody.appendChild(tr);
  }
}

// ── Recent runs timeline ─────────────────────────────────────────────────────
function renderTimeline(items) {
  const strip = document.getElementById('timelineStrip');
  if (!strip) return;
  if (!items || items.length === 0) {
    strip.innerHTML = `<div class="empty-state" style="padding:16px">
      <span class="empty-icon" aria-hidden="true">🕐</span>
      <span class="empty-msg">No runs recorded yet — trigger the first run to verify the scheduler.</span>
      ${canAssignTickets() ? '<button class="ghost sm" onclick="runNow()">▶ Run now</button>' : ''}
    </div>`;
    return;
  }
  const recent = items.slice(0, 15).reverse();
  strip.innerHTML = '';
  recent.forEach((item, i) => {
    if (i > 0) {
      const c = document.createElement('div');
      c.className = 'timeline-connector';
      strip.appendChild(c);
    }
    const dot = document.createElement('div');
    const cls = item.succeeded === null ? 'unknown' : item.succeeded ? 'ok' : 'fail';
    dot.className = `timeline-dot ${cls}`;
    dot.textContent = item.succeeded === null ? '?' : item.succeeded ? '✓' : '✗';
    const outcome = item.succeeded === null ? 'Unknown' : item.succeeded ? 'Success' : 'Failed';
    dot.title = `${item.startedAtUtc ? new Date(item.startedAtUtc).toLocaleString() : '?'} — ${outcome} (open in Run History)`;
    dot.setAttribute('role', 'button');
    dot.setAttribute('tabindex', '0');
    dot.setAttribute('aria-label', `Run #${item.heartbeatId ?? '?'}: ${outcome}. Open in Run History.`);
    dot.addEventListener('click', () => gotoHistoryRun(item));
    dot.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); gotoHistoryRun(item); }
    });
    strip.appendChild(dot);
  });
}

// ── Timeline → History drill-down ─────────────────────────────────────────
function gotoHistoryRun(item) {
  activateDashboardTab('history');
  _statusFilter = 'all';
  document.querySelectorAll('#panel-history .filter-chip').forEach(chip => {
    chip.classList.toggle('active', chip.dataset.status === 'all');
  });
  const search = document.getElementById('historySearch');
  const id = item && item.heartbeatId != null ? String(item.heartbeatId) : '';
  if (search) search.value = id;
  _searchQuery = id.toLowerCase();
  renderHistoryRows();
}

// ── Live refresh countdown ────────────────────────────────────────────────────
const REFRESH_INTERVAL_S = 10;
let _refreshSecondsLeft = REFRESH_INTERVAL_S;
function tickRefreshCountdown() {
  const ring  = document.getElementById('refreshRingProgress');
  const label = document.getElementById('refreshCountdown');
  if (!ring || !label) return;
  _refreshSecondsLeft = Math.max(0, _refreshSecondsLeft - 1);
  const circumference = 50.27;
  const offset = circumference * (1 - _refreshSecondsLeft / REFRESH_INTERVAL_S);
  ring.style.strokeDashoffset = offset.toFixed(2);
  label.textContent = `${_refreshSecondsLeft}s`;
  if (_refreshSecondsLeft <= 0) _refreshSecondsLeft = REFRESH_INTERVAL_S;
}

// ── Dashboard tabs ────────────────────────────────────────────────────────────
const DASHBOARD_TAB_STORAGE_KEY = 'itcm-dashboard-tab';
function activateDashboardTab(tabId, moveFocus = false) {
  const tabs = [...document.querySelectorAll('[data-dashboard-tab]')];
  const panels = [...document.querySelectorAll('[data-dashboard-panel]')];
  const selected = tabs.find(tab => tab.dataset.dashboardTab === tabId) || tabs[0];
  if (!selected) return;

  tabs.forEach(tab => {
    const active = tab === selected;
    tab.classList.toggle('is-active', active);
    tab.setAttribute('aria-selected', active ? 'true' : 'false');
    tab.tabIndex = active ? 0 : -1;
  });
  panels.forEach(panel => {
    const active = panel.dataset.dashboardPanel === selected.dataset.dashboardTab;
    panel.hidden = !active;
    panel.classList.toggle('is-active', active);
  });
  localStorage.setItem(DASHBOARD_TAB_STORAGE_KEY, selected.dataset.dashboardTab);
  if (moveFocus) selected.focus();
}
function initDashboardTabs() {
  const tabs = [...document.querySelectorAll('[data-dashboard-tab]')];
  if (!tabs.length) return;
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => activateDashboardTab(tab.dataset.dashboardTab));
    tab.addEventListener('keydown', (event) => {
      if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
      event.preventDefault();
      let nextIndex = index;
      if (event.key === 'ArrowRight') nextIndex = (index + 1) % tabs.length;
      if (event.key === 'ArrowLeft') nextIndex = (index - 1 + tabs.length) % tabs.length;
      if (event.key === 'Home') nextIndex = 0;
      if (event.key === 'End') nextIndex = tabs.length - 1;
      activateDashboardTab(tabs[nextIndex].dataset.dashboardTab, true);
    });
  });
  activateDashboardTab(localStorage.getItem(DASHBOARD_TAB_STORAGE_KEY) || 'overview');
}

// ── Keyboard shortcuts ────────────────────────────────────────────────────────
function showShortcuts() { document.getElementById('shortcutsModal')?.classList.add('show'); }
function hideShortcuts() { document.getElementById('shortcutsModal')?.classList.remove('show'); }
document.addEventListener('keydown', (e) => {
  const tag = (e.target.tagName || '').toLowerCase();
  const typing = tag === 'input' || tag === 'textarea' || tag === 'select';

  if (e.key === 'Escape') { hideShortcuts(); closeSidebar(); return; }
  if (typing) return;

  switch (e.key.toLowerCase()) {
    case 'r': runNow(); break;
    case 'p': pauseScheduler(); break;
    case 'u': resumeScheduler(); break;
    case 'g': loadAll(); showToast('Refreshed', 'info', 1500); break;
    case 'd': toggleTheme(); break;
    case 's': toggleSidebar(); break;
    case 'f': document.getElementById('historySearch')?.focus(); e.preventDefault(); break;
    case '?': showShortcuts(); break;
  }
});


// ── Export CSV ────────────────────────────────────────────────────────────────
function exportHistoryCSV() {
  if (!_historyData.length) { showToast('No history data to export', 'warn'); return; }
  const headers = ['ID','Started','Finished','Status','Duration(s)','RemindersSent','ReminderCandidates','EscalationsApplied','EscalationCandidates','LockAcquired','Machine','Error'];
  const rows = _historyData.map(item => {
    const s = item.startedAtUtc  ? new Date(item.startedAtUtc)  : null;
    const f = item.finishedAtUtc ? new Date(item.finishedAtUtc) : null;
    const dur = s && f ? Math.round((f - s) / 1000) : '';
    const status = item.succeeded === null ? 'Unknown' : item.succeeded ? 'Success' : 'Failed';
    const q = v => `"${String(v ?? '').replace(/"/g, '""')}"`;
    return [item.heartbeatId, s?.toISOString()||'', f?.toISOString()||'', status, dur,
            item.remindersSent??0, item.reminderCandidates??0,
            item.escalationsApplied??0, item.escalationCandidates??0,
            item.lockAcquired?'Yes':'No', item.machineName||'', q(item.errorMessage||'')].join(',');
  });
  const blob = new Blob([[headers.join(','), ...rows].join('\r\n')], { type: 'text/csv' });
  const a    = Object.assign(document.createElement('a'), { href: URL.createObjectURL(blob), download: `itcm-history-${new Date().toISOString().slice(0,10)}.csv` });
  a.click();
  URL.revokeObjectURL(a.href);
  showToast('History exported as CSV', 'success');
}

// ── Actions ───────────────────────────────────────────────────────────────────
function getCsrfToken() {
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
}

async function post(url, okMsg, conflictMsg) {
  const actionButtons = [...document.querySelectorAll('.status-hero-actions button, .quick-actions button')];
  actionButtons.forEach(button => button.disabled = true);
  try {
    const headers = {};
    const csrf = getCsrfToken();
    if (csrf) headers['X-CSRF-TOKEN'] = csrf;

    const res = await fetch(url, { method: 'POST', headers, credentials: 'same-origin' });
    if (res.status === 401) {
      window.location.href = `/Account/Login?ReturnUrl=${encodeURIComponent(window.location.pathname)}`;
      return;
    }
    if (res.ok || res.status === 202) showToast(okMsg, 'success');
    else if (res.status === 409) showToast(conflictMsg || 'Already in that state', 'warn');
    else showToast(`Request failed (${res.status})`, 'error');
  } catch (e) {
    showToast('Network error: ' + e.message, 'error');
  } finally {
    actionButtons.forEach(button => button.disabled = false);
  }
  await loadAll();
  await loadHeartbeat();
  await loadHistory(currentHistoryPage);
}

function runNow()           { post('/api/itcm/scheduler/run-now', 'Scheduler run triggered',  'A run is already in progress'); }
function pauseScheduler() {
  if (!window.confirm('Pause the scheduler? Reminder and escalation runs will stop until resumed.')) return;
  post('/api/itcm/scheduler/pause',   'Scheduler paused',          'Scheduler is already paused');
}
function resumeScheduler()  { post('/api/itcm/scheduler/resume',  'Scheduler resumed',         'Scheduler is already running'); }

// ── Ticket actions (triage assign + manual escalation) ────────────────────────
function canAssignTickets() {
  return document.querySelector('[data-can-assign]')?.dataset.canAssign === 'true';
}

async function postJson(url, body, okMsg, conflictMsg) {
  const actionButtons = [...document.querySelectorAll('.status-hero-actions button, .quick-actions button, #portalTriageBody button, #assignModal button')];
  actionButtons.forEach(button => button.disabled = true);
  try {
    const headers = { 'Content-Type': 'application/json' };
    const csrf = getCsrfToken();
    if (csrf) headers['X-CSRF-TOKEN'] = csrf;

    const res = await fetch(url, { method: 'POST', headers, credentials: 'same-origin', body: JSON.stringify(body ?? {}) });
    if (res.status === 401) {
      window.location.href = `/Account/Login?ReturnUrl=${encodeURIComponent(window.location.pathname)}`;
      return;
    }
    if (res.ok || res.status === 202) showToast(okMsg, 'success');
    else if (res.status === 409) showToast(conflictMsg || 'Already in that state', 'warn');
    else if (res.status === 400) {
      let detail = `Request failed (400)`;
      try { const payload = await res.json(); if (payload?.message) detail = payload.message; } catch (_) { /* keep default */ }
      showToast(detail, 'error');
    }
    else showToast(`Request failed (${res.status})`, 'error');
  } catch (e) {
    showToast('Network error: ' + e.message, 'error');
  } finally {
    actionButtons.forEach(button => button.disabled = false);
  }
  await loadAll();
  await loadHeartbeat();
  await loadHistory(currentHistoryPage);
}

let assignModalTicketId = null;
let itEmployeesCache = null;

async function loadItEmployees() {
  if (itEmployeesCache) return itEmployeesCache;
  itEmployeesCache = await getJson('/api/itcm/it-employees');
  return itEmployeesCache;
}

async function openAssignModal(ticketId, ticketCode) {
  assignModalTicketId = ticketId;
  document.getElementById('assignModalTicket').textContent = ticketCode || `#${ticketId}`;
  const select = document.getElementById('assignModalSelect');
  select.innerHTML = '';
  const placeholder = document.createElement('option');
  placeholder.value = '';
  placeholder.textContent = 'Leave unassigned';
  select.appendChild(placeholder);
  try {
    const employees = await loadItEmployees();
    (employees || []).forEach(emp => {
      const opt = document.createElement('option');
      opt.value = emp.empId;
      opt.textContent = emp.name || `Employee #${emp.empId}`;
      select.appendChild(opt);
    });
  } catch (e) {
    showToast('Could not load IT staff list', 'error');
  }
  document.getElementById('assignModal')?.classList.add('show');
}

function hideAssignModal() {
  document.getElementById('assignModal')?.classList.remove('show');
  assignModalTicketId = null;
}

async function confirmAssign() {
  if (assignModalTicketId == null) return;
  const raw = document.getElementById('assignModalSelect').value;
  const assignedToEmpId = raw === '' ? null : parseInt(raw, 10);
  const ticketId = assignModalTicketId;
  hideAssignModal();
  await postJson(`/api/itcm/tickets/${ticketId}/assign`, { assignedToEmpId },
    assignedToEmpId ? 'Ticket assigned.' : 'Ticket unassigned.', 'Assignment is no longer possible on this ticket.');
}

async function escalateTicket() {
  const idInput = document.getElementById('qaEscalateTicket');
  const noteInput = document.getElementById('qaEscalateNote');
  if (!idInput || !noteInput) return;
  const rawId = (idInput.value || '').trim();
  const note = (noteInput.value || '').trim();
  if (!rawId) { showToast('Enter a ticket code or ID first.', 'warn'); return; }
  if (!note) { showToast('An escalation note is required.', 'warn'); return; }
  const ticketId = /^\d+$/.test(rawId) ? parseInt(rawId, 10) : rawId;
  await postJson(`/api/itcm/tickets/${encodeURIComponent(ticketId)}/escalate`, { note },
    'Ticket escalated.', 'Ticket cannot be escalated from its current state.');
  idInput.value = '';
  noteInput.value = '';
}

// ── Pagination ────────────────────────────────────────────────────────────────
document.getElementById('prevPage')?.addEventListener('click', () => loadHistory(currentHistoryPage - 1));
document.getElementById('nextPage')?.addEventListener('click', () => loadHistory(currentHistoryPage + 1));

// ── Boot ──────────────────────────────────────────────────────────────────────
let _initialLoadPending = true;
function skeletonTableRows(tbodyId, columns) {
  const tbody = document.getElementById(tbodyId);
  if (!tbody || tbody.rows.length > 0) return;
  tbody.innerHTML = '';
  for (let i = 0; i < 3; i++) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = columns;
    cell.style.padding = '14px';
    cell.innerHTML = '<span class="skeleton" style="width:85%">&nbsp;</span>';
    row.appendChild(cell);
    tbody.appendChild(row);
  }
}
function showLoadingSkeletons() {
  ['totalRuns', 'successfulRuns', 'failedRuns', 'nextRunAt',
   'totalRunsSub', 'successRateSub', 'failedRunsSub', 'nextRunRel'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.innerHTML = '<span class="skeleton" style="width:64px">&nbsp;</span>';
  });
  skeletonTableRows('portalTriageBody', canAssignTickets() ? 5 : 4);
  skeletonTableRows('schedulerActivityBody', 4);
  skeletonTableRows('ticketMovementBody', 7);
  skeletonTableRows('notificationActivityBody', 6);
  skeletonTableRows('historyBody', 10);
  skeletonTableRows('devicePresenceBody', 8);
}
async function loadAll() {
  if (_initialLoadPending) {
    _initialLoadPending = false;
    showLoadingSkeletons();
  }
  await loadStatus();
  await loadSettings();
  await loadHistory(currentHistoryPage);
  await loadMonitoring();
  await loadPresence();
}

document.addEventListener('DOMContentLoaded', () => {
  initDashboardTabs();
  loadAll();
  loadHeartbeat();
  setInterval(loadAll,       10000);
  setInterval(loadHeartbeat, 30000);
  setInterval(tickRefreshCountdown, 1000);
});

// ── Mobile swipe-to-close sidebar ─────────────────────────────────────────────
(function () {
  let touchStartX = null;
  const sidebar = document.getElementById('sidebar');
  if (!sidebar) return;

  sidebar.addEventListener('touchstart', (e) => { touchStartX = e.touches[0].clientX; }, { passive: true });
  sidebar.addEventListener('touchend', (e) => {
    if (touchStartX === null) return;
    const dx = e.changedTouches[0].clientX - touchStartX;
    if (dx < -40) closeSidebar(); // swipe left closes
    touchStartX = null;
  }, { passive: true });
})();
