# Dashboard Ticket Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Resolve/Reopen and bulk-assign ticket actions to the ITCM Server dashboard (`http://192.168.100.186:7017/`).

**Architecture:** Two new admin-only Minimal API endpoints in `Program.cs` mirroring the existing assign/escalate endpoints (CSRF + audit + `ResolveTicketIdAsync`), plus triage-table checkboxes and a Resolve/Reopen card in `Pages/Index.cshtml` with `postJson` helpers in `wwwroot/js/dashboard.js`. No repo-interface changes needed: both endpoints reuse `SetTicketStatusAsync`, `AssignTicketEmployeeAsync`, `GetTicketNotificationDataAsync`, `IsItEmployeeAsync`, `GetTicketIdByCodeAsync`, and `EmailService.NotifyStatusChangeAsync` / `NotifyAssignmentAsync`.

**Tech Stack:** .NET 8 Minimal APIs, Razor Pages, vanilla JS (`postJson` with `X-CSRF-TOKEN`), ADO.NET via `IItcmRepository`, `sp_Call_SetTicketStatus` / `sp_Call_AssignTicket`.

---

### Task 1: POST resolve/reopen endpoint

**Files:**
- Modify: `Yakult.ITCM.Server/Program.cs` (insert after the escalate endpoint block, before the `// ── Run Now` section)
- Test: manual probe via `Invoke-RestMethod` (no test project exists in this repo)

- [ ] **Step 1: Insert the endpoint + request record**

Insert after the escalate endpoint's closing `}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);` line:

```csharp
// ── Ticket Resolve / Reopen (manual close-the-loop from the dashboard) ──
app.MapPost("/api/itcm/tickets/{ticketId}/resolve", async (
    string ticketId,
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailService emailService,
    TicketResolveRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "TicketResolve"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    var id = await ResolveTicketIdAsync(repo, ticketId);
    if (!id.HasValue)
    {
        audit.Record(context, "TicketResolve", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    var ticket = await repo.GetTicketNotificationDataAsync(id.Value);
    if (ticket is null)
    {
        audit.Record(context, "TicketResolve", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    var newStatus = (req.NewStatus ?? string.Empty).Trim();
    var allowed = new[] { "Solved", "Resolved (Temporary)", "Closed", "Reopened" };
    if (!allowed.Any(a => string.Equals(a, newStatus, StringComparison.OrdinalIgnoreCase)))
    {
        audit.Record(context, "TicketResolve", false, "validation-error");
        return Results.BadRequest(new { Message = "NewStatus must be Solved, Resolved (Temporary), Closed, or Reopened." });
    }
    var note = (req.Note ?? string.Empty).Trim();
    var current = ticket.Status ?? string.Empty;
    var targetIsFinal = !string.Equals(newStatus, "Reopened", StringComparison.OrdinalIgnoreCase);
    if (targetIsFinal)
    {
        if (IsFinalTicketStatus(current))
        {
            audit.Record(context, "TicketResolve", false, "already-final");
            return Results.Conflict(new { Message = "Ticket is already in a final state." });
        }
        if (string.IsNullOrWhiteSpace(note))
        {
            audit.Record(context, "TicketResolve", false, "validation-error");
            return Results.BadRequest(new { Message = "A resolution note is required (desktop Mark-As parity)." });
        }
    }
    else
    {
        if (!IsFinalTicketStatus(current))
        {
            audit.Record(context, "TicketResolve", false, "nothing-to-reopen");
            return Results.Conflict(new { Message = "Only a Solved / Resolved (Temporary) / Closed ticket can be reopened." });
        }
    }

    var userId = GetUserId(context.User);
    await repo.SetTicketStatusAsync(id.Value, newStatus, userId, string.IsNullOrWhiteSpace(note) ? null : note);
    await emailService.NotifyStatusChangeAsync(id.Value, current, newStatus, note, userId);
    audit.Record(context, "TicketResolve", true, $"status={newStatus}");
    return Results.Ok(new { success = true, message = $"Ticket marked as {newStatus}." });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);
```

And add the record next to the existing `TicketEscalateRequest` record at the bottom of `Program.cs`:

```csharp
record TicketResolveRequest(string? NewStatus, string? Note);
```

- [ ] **Step 2: Build the server project**

Run:

```powershell
dotnet build "Yakult.ITCM.Server\Yakult.ITCM.Server.csproj" -c Release --nologo -v q
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Yakult.ITCM.Server/Program.cs
git commit -m "feat(itcm): add dashboard resolve/reopen ticket endpoint"
```

---

### Task 2: POST bulk-assign endpoint

**Files:**
- Modify: `Yakult.ITCM.Server/Program.cs` (insert after the single-assign endpoint block)
- Test: manual probe (no test project exists in this repo)

- [ ] **Step 1: Insert the endpoint + request record**

Insert after the single-assign endpoint's closing `}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);` line:

```csharp
// ── Ticket Bulk Assign (triage queue clearing) ───────────────────────────
app.MapPost("/api/itcm/tickets/bulk-assign", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailService emailService,
    TicketBulkAssignRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "TicketBulkAssign"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    var ids = (req.TicketIds ?? new List<int>()).Where(i => i > 0).Distinct().Take(100).ToList();
    if (ids.Count == 0)
    {
        audit.Record(context, "TicketBulkAssign", false, "validation-error");
        return Results.BadRequest(new { Message = "TicketIds must contain at least one positive ticket id (max 100)." });
    }
    if (req.AssignedToEmpId.HasValue)
    {
        if (req.AssignedToEmpId.Value <= 0)
        {
            audit.Record(context, "TicketBulkAssign", false, "validation-error");
            return Results.BadRequest(new { Message = "AssignedToEmpId must be a positive employee id, or omitted to unassign." });
        }
        if (!await repo.IsItEmployeeAsync(req.AssignedToEmpId.Value))
        {
            audit.Record(context, "TicketBulkAssign", false, "validation-error");
            return Results.BadRequest(new { Message = "AssignedToEmpId must reference an active IT employee." });
        }
    }

    var userId = GetUserId(context.User);
    var results = new List<object>();
    var assigned = 0;
    var skipped = 0;
    foreach (var ticketId in ids)
    {
        var ticket = await repo.GetTicketNotificationDataAsync(ticketId);
        if (ticket is null) { skipped++; results.Add(new { ticketId, ok = false, message = "Ticket not found." }); continue; }
        if (IsFinalTicketStatus(ticket.Status)) { skipped++; results.Add(new { ticketId, ok = false, message = "Final ticket; reopen first." }); continue; }
        if (ticket.AssignedToEmpId == req.AssignedToEmpId) { results.Add(new { ticketId, ok = true, message = "Already assigned." }); continue; }
        var prev = ticket.AssignedToEmpId;
        var prevName = ticket.AssignedTo;
        await repo.AssignTicketEmployeeAsync(ticketId, req.AssignedToEmpId, userId);
        if (req.AssignedToEmpId.HasValue)
            await emailService.NotifyAssignmentAsync(ticketId, req.AssignedToEmpId.Value, userId, prev, prevName);
        assigned++;
        results.Add(new { ticketId, ok = true, message = req.AssignedToEmpId.HasValue ? "Assigned." : "Unassigned." });
    }
    audit.Record(context, "TicketBulkAssign", true, $"assignee={req.AssignedToEmpId?.ToString() ?? "unassigned"};assigned={assigned};skipped={skipped};total={ids.Count}");
    return Results.Ok(new { success = true, assigned, skipped, total = ids.Count, results });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);
```

Record next to `TicketAssignRequest`:

```csharp
record TicketBulkAssignRequest(List<int>? TicketIds, int? AssignedToEmpId);
```

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build "Yakult.ITCM.Server\Yakult.ITCM.Server.csproj" -c Release --nologo -v q
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Yakult.ITCM.Server/Program.cs
git commit -m "feat(itcm): add bulk-assign tickets endpoint"
```

---

### Task 3: Dashboard Resolve/Reopen card (Razor + JS)

**Files:**
- Modify: `Yakult.ITCM.Server/Pages/Index.cshtml` (insert after the Manual Escalation card closing `</div>`, still inside `panel-monitoring`)
- Modify: `Yakult.ITCM.Server/wwwroot/js/dashboard.js` (append next to `escalateTicket`)
- Test: browse `http://localhost:50330/`, open Ticket Monitoring tab

- [ ] **Step 1: Add the Razor card after the Manual Escalation card**

```html
  <!-- ── Resolve / Reopen (admin only) ── -->
  <div class="card row-gap">
    <div class="card-header">
      <div>
        <div class="card-title"><span class="dot"></span>Resolve / Reopen</div>
        <div class="card-helper">Mark Solved / Resolved (Temporary) / Closed (note required) or Reopen a final ticket.</div>
      </div>
    </div>
    @if (User.HasClaim("ItcmAdministrator", "true"))
    {
      <div class="escalate-row">
        <input type="text" class="filter-input" id="qaResolveTicket" placeholder="TCK-000123 or 123" aria-label="Ticket code or ID" />
        <select id="qaResolveStatus" class="filter-input device-select" aria-label="Target status">
          <option>Solved</option>
          <option>Resolved (Temporary)</option>
          <option>Closed</option>
          <option>Reopened</option>
        </select>
        <input type="text" class="filter-input escalate-note" id="qaResolveNote" placeholder="Resolution note (required except Reopen)" aria-label="Resolution note" />
        <button class="warn sm" onclick="resolveTicket()">Apply</button>
      </div>
    }
    else
    {
      <div class="quick-action-row">
        <label class="grow">Resolve / reopen by ticket ID</label>
        <span class="pill muted" title="Needs an administrator sign-in">Admin only</span>
      </div>
    }
  </div>
```

- [ ] **Step 2: Add the JS helper after `escalateTicket`**

```js
async function resolveTicket() {
  const idInput = document.getElementById('qaResolveTicket');
  const statusInput = document.getElementById('qaResolveStatus');
  const noteInput = document.getElementById('qaResolveNote');
  if (!idInput || !statusInput) return;
  const rawId = (idInput.value || '').trim();
  const newStatus = (statusInput.value || '').trim();
  const note = ((noteInput && noteInput.value) || '').trim();
  if (!rawId) { showToast('Enter a ticket code or ID first.', 'warn'); return; }
  if (!newStatus) { showToast('Pick a target status.', 'warn'); return; }
  if (newStatus !== 'Reopened' && !note) { showToast('A resolution note is required.', 'warn'); return; }
  const ticketId = /^\d+$/.test(rawId) ? parseInt(rawId, 10) : rawId;
  await postJson(`/api/itcm/tickets/${encodeURIComponent(ticketId)}/resolve`, { newStatus, note },
    `Ticket marked as ${newStatus}.`, 'Ticket cannot move to that status from its current state.');
  idInput.value = '';
  if (noteInput) noteInput.value = '';
}
```

- [ ] **Step 3: Verify in browser**

Run dev server, browse Ticket Monitoring tab, resolve a test ticket code, confirm toast + movement row appears.

- [ ] **Step 4: Commit**

```bash
git add Yakult.ITCM.Server/Pages/Index.cshtml Yakult.ITCM.Server/wwwroot/js/dashboard.js
git commit -m "feat(itcm): add dashboard resolve/reopen card"
```

---

### Task 4: Triage bulk-select + bulk bar (Razor + JS)

**Files:**
- Modify: `Yakult.ITCM.Server/Pages/Index.cshtml` (triage table head + bulk bar under the Incoming Portal Tickets card)
- Modify: `Yakult.ITCM.Server/wwwroot/js/dashboard.js` (checkbox rendering in `renderMonitoring` triage section + `bulkAssignTickets` helper)
- Test: browse dashboard, check 2 triage rows, bulk assign

- [ ] **Step 1: Razor — checkbox header + bulk bar**

Change the triage `<thead>` row to (admin-only leading checkbox column):

```html
<thead><tr>@if (User.HasClaim("ItcmAdministrator", "true")) {<th><input type="checkbox" id="triageSelectAll" aria-label="Select all triage tickets" /></th>}<th>Ticket</th><th>Caller / Issue</th><th>Priority</th><th>Created</th>@if (User.HasClaim("ItcmAdministrator", "true")) {<th>Action</th>}</tr></thead>
```

Insert directly after the Incoming Portal Tickets card's `</div>` table-wrap close (still inside the same card, before the card closes):

```html
      @if (User.HasClaim("ItcmAdministrator", "true"))
      {
        <div class="escalate-row" id="triageBulkBar">
          <select id="triageBulkSelect" class="filter-input device-select" style="max-width:220px" aria-label="Bulk assignee"></select>
          <button class="primary sm" onclick="bulkAssignTickets()">Assign selected</button>
          <span class="pill muted" id="triageSelectedCount">0 selected</span>
        </div>
      }
```

- [ ] **Step 2: JS — render checkboxes + bulk helpers**

In `renderMonitoring`, inside the `items.forEach(ticket => {` triage loop, prepend a checkbox cell when `canAssignTickets()` is true (before `appendCells`):

```js
if (canAssignTickets()) {
  const checkCell = document.createElement('td');
  const check = document.createElement('input');
  check.type = 'checkbox';
  check.className = 'triage-check';
  check.value = String(ticket.ticketId);
  check.setAttribute('aria-label', `Select ${ticket.ticketCode || `ticket ${ticket.ticketId}`}`);
  check.onchange = updateTriageSelectedCount;
  checkCell.appendChild(check);
  row.appendChild(checkCell);
}
```

Append these helpers next to `confirmAssign` (also wire select-all + staff options on each render):

```js
function updateTriageSelectedCount() {
  const n = document.querySelectorAll('.triage-check:checked').length;
  setText('triageSelectedCount', `${n} selected`);
}

async function bulkAssignTickets() {
  const checked = [...document.querySelectorAll('.triage-check:checked')].map(c => parseInt(c.value, 10)).filter(i => i > 0);
  if (!checked.length) { showToast('Select at least one ticket.', 'warn'); return; }
  const raw = document.getElementById('triageBulkSelect').value;
  const assignedToEmpId = raw === '' ? null : parseInt(raw, 10);
  await postJson('/api/itcm/tickets/bulk-assign', { ticketIds: checked, assignedToEmpId },
    'Bulk assignment applied.', 'Bulk assignment failed.');
}
```

And inside `renderMonitoring` after the triage loop, populate the bulk staff select + select-all wiring:

```js
const bulkSelect = document.getElementById('triageBulkSelect');
if (bulkSelect && canAssignTickets()) {
  bulkSelect.innerHTML = '';
  const none = document.createElement('option');
  none.value = '';
  none.textContent = 'Leave unassigned';
  bulkSelect.appendChild(none);
  try {
    const employees = await loadItEmployees();
    (employees || []).forEach(emp => {
      const opt = document.createElement('option');
      opt.value = emp.empId;
      opt.textContent = emp.name || `Employee #${emp.empId}`;
      bulkSelect.appendChild(opt);
    });
  } catch (e) { /* staff list failure already toasted by single-assign path */ }
}
const selectAll = document.getElementById('triageSelectAll');
if (selectAll) {
  selectAll.checked = false;
  selectAll.onchange = () => {
    document.querySelectorAll('.triage-check').forEach(c => { c.checked = selectAll.checked; });
    updateTriageSelectedCount();
  };
}
updateTriageSelectedCount();
```

Note: the `await loadItEmployees()` block above must sit in `renderMonitoring` after the loop, so change `function renderMonitoring(data)` to `async function renderMonitoring(data)` AND change the caller in `loadMonitoring` from `renderMonitoring(await getJson('/api/itcm/monitoring?maxRows=12'));` to `await renderMonitoring(await getJson('/api/itcm/monitoring?maxRows=12'));` so render errors still hit `loadMonitoring`'s try/catch → `renderMonitoringUnavailable`.

- [ ] **Step 3: Verify in browser + build**

```powershell
dotnet build "Yakult.ITCM.Server\Yakult.ITCM.Server.csproj" -c Release --nologo -v q
```

Expected: `Build succeeded.` Then browse, select 2 rows, bulk assign, confirm toasts + triage refresh.

- [ ] **Step 4: Commit**

```bash
git add Yakult.ITCM.Server/Pages/Index.cshtml Yakult.ITCM.Server/wwwroot/js/dashboard.js
git commit -m "feat(itcm): add triage bulk-select and assign"
```
