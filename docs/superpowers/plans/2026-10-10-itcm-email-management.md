# Email Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give admins a dashboard UI to edit email templates and to inspect + resend per-ticket email deliveries on the ITCM Server (`http://192.168.100.186:7017/`).

**Architecture:** Three repo methods (`GetAllEmailTemplatesAsync`, `SaveEmailTemplateAsync` as UPDATE-else-INSERT upsert per `UQ_CallEmailTemplate_Type`, `GetEmailLogForTicketAsync`) + four admin endpoints in `Program.cs` reusing `ValidateCsrfAsync`/audit patterns + two Monitoring-tab cards (Templates editor, Ticket Email Log) wired with `getJson`/`postJson` in `wwwroot/js/dashboard.js`. Resend v1 re-sends a fresh StatusUpdate notification to the ticket's current recipients (the `CallEmailLog` table stores subject but not body, so byte-identical replay is impossible — documented in the UI helper text).

**Tech Stack:** .NET 8 Minimal APIs, Razor Pages, vanilla JS, ADO.NET, `CallEmailTemplate` / `CallEmailLog` tables via `SchemaGate` guards.

---

### Task 1: Repo — template list + save + per-ticket log

**Files:**
- Modify: `Yakult.ITCM.Server/Data/IItcmRepository.cs` (add 3 signatures after `GetEmailTemplateByTypeAsync`)
- Modify: `Yakult.ITCM.Server/Data/ItcmRepository.cs` (add 3 methods after `GetEmailTemplateByTypeAsync`, before the Ticket Notification Data section)
- Test: `dotnet build` (no test project exists in this repo)

- [ ] **Step 1: Add interface signatures**

```csharp
Task<List<CallEmailTemplateItem>> GetAllEmailTemplatesAsync();
Task SaveEmailTemplateAsync(CallEmailTemplateItem template);
Task<List<CallEmailLogItem>> GetEmailLogForTicketAsync(int ticketId, int maxRows = 50);
```

- [ ] **Step 2: Add implementations**

```csharp
public async Task<List<CallEmailTemplateItem>> GetAllEmailTemplatesAsync()
{
    var items = new List<CallEmailTemplateItem>();
    await using var conn = CreateConnection();
    await conn.OpenAsync();
    if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailTemplate"))
        return items;

    // One row per type enforced by UQ_CallEmailTemplate_Type: plain select.
    const string sql = @"
SELECT TemplateId, TemplateType, Subject, Body, IsActive, UpdatedAt, UpdatedByUserId
FROM dbo.CallEmailTemplate
ORDER BY TemplateType;";
    await using var cmd = new SqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new CallEmailTemplateItem
        {
            TemplateId = reader.GetInt32(0),
            TemplateType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Subject = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Body = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            UpdatedAt = reader.GetDateTime(5),
            UpdatedByUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6)
        });
    }
    return items;
}

public async Task SaveEmailTemplateAsync(CallEmailTemplateItem template)
{
    ArgumentNullException.ThrowIfNull(template);
    await using var conn = CreateConnection();
    await conn.OpenAsync();
    if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailTemplate"))
        throw new InvalidOperationException("CallEmailTemplate table is not installed in this database yet.");

    // UQ_CallEmailTemplate_Type enforces one row per type: UPDATE if present,
    // else INSERT. (Unlike notification rules, templates are NOT append-only.)
    const string updateSql = @"
UPDATE dbo.CallEmailTemplate
SET Subject = @Subject, Body = @Body, IsActive = @IsActive, UpdatedByUserId = @UpdatedByUserId
WHERE TemplateType = @TemplateType;";
    await using (var updateCmd = new SqlCommand(updateSql, conn))
    {
        updateCmd.Parameters.AddWithValue("@TemplateType", template.TemplateType.Trim());
        updateCmd.Parameters.AddWithValue("@Subject", (object?)template.Subject ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@Body", (object?)template.Body ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@IsActive", template.IsActive);
        updateCmd.Parameters.AddWithValue("@UpdatedByUserId", (object?)template.UpdatedByUserId ?? DBNull.Value);
        if (await updateCmd.ExecuteNonQueryAsync() > 0)
            return;
    }
    const string insertSql = @"
INSERT dbo.CallEmailTemplate (TemplateType, Subject, Body, IsActive, UpdatedByUserId)
VALUES (@TemplateType, @Subject, @Body, @IsActive, @UpdatedByUserId);";
    await using var insertCmd = new SqlCommand(insertSql, conn);
    insertCmd.Parameters.AddWithValue("@TemplateType", template.TemplateType.Trim());
    insertCmd.Parameters.AddWithValue("@Subject", (object?)template.Subject ?? DBNull.Value);
    insertCmd.Parameters.AddWithValue("@Body", (object?)template.Body ?? DBNull.Value);
    insertCmd.Parameters.AddWithValue("@IsActive", template.IsActive);
    insertCmd.Parameters.AddWithValue("@UpdatedByUserId", (object?)template.UpdatedByUserId ?? DBNull.Value);
    await insertCmd.ExecuteNonQueryAsync();
}

public async Task<List<CallEmailLogItem>> GetEmailLogForTicketAsync(int ticketId, int maxRows = 50)
{
    var items = new List<CallEmailLogItem>();
    maxRows = Math.Clamp(maxRows, 1, 200);
    await using var conn = CreateConnection();
    await conn.OpenAsync();
    if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailLog"))
        return items;

    const string sql = @"
SELECT TOP (@MaxRows)
    EmailLogId, TicketId, EmailType, Recipient, Subject, Status, ErrorMessage, DateSent, CreatedByUserId
FROM dbo.CallEmailLog
WHERE TicketId = @TicketId
ORDER BY DateSent DESC, EmailLogId DESC;";
    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@MaxRows", maxRows);
    cmd.Parameters.AddWithValue("@TicketId", ticketId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new CallEmailLogItem
        {
            EmailLogId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
            TicketId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            EmailType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Recipient = reader.IsDBNull(3) ? null : reader.GetString(3),
            Subject = reader.IsDBNull(4) ? null : reader.GetString(4),
            Status = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
            DateSent = reader.GetDateTime(7),
            CreatedByUserId = reader.IsDBNull(8) ? null : reader.GetInt32(8)
        });
    }
    return items;
}
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build "Yakult.ITCM.Server\Yakult.ITCM.Server.csproj" -c Release --nologo -v q
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Yakult.ITCM.Server/Data/IItcmRepository.cs Yakult.ITCM.Server/Data/ItcmRepository.cs
git commit -m "feat(itcm): add email template + per-ticket log repo methods"
```

---

### Task 2: Endpoints — templates list/save, per-ticket log, resend

**Files:**
- Modify: `Yakult.ITCM.Server/Program.cs` (insert after the `/api/itcm/email/rules/save` endpoint block)
- Test: manual probes (no test project exists in this repo)

- [ ] **Step 1: Insert the four endpoints + save record**

```csharp
// ── Email Templates (admin editor backing) ─────────────────────────────
app.MapGet("/api/itcm/email/templates", async (IItcmRepository repo) =>
{
    var items = await repo.GetAllEmailTemplatesAsync();
    return Results.Ok(items.Select(t => new
    {
        templateType = t.TemplateType,
        subject = t.Subject,
        body = t.Body,
        isActive = t.IsActive,
        updatedAt = t.UpdatedAt
    }));
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

app.MapPost("/api/itcm/email/templates/save", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailTemplateSaveRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "EmailTemplateSave"))
        return Results.BadRequest(new { success = false, message = "Invalid request token." });

    var allowed = new[] { "Reminder", "Escalation", "StatusUpdate", "Assignment", "Reassignment" };
    var type = (req.TemplateType ?? string.Empty).Trim();
    if (!allowed.Any(a => string.Equals(a, type, StringComparison.OrdinalIgnoreCase)))
    {
        audit.Record(context, "EmailTemplateSave", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "Unknown template type." });
    }
    try
    {
        await repo.SaveEmailTemplateAsync(new CallEmailTemplateItem
        {
            TemplateType = type,
            Subject = req.Subject ?? string.Empty,
            Body = req.Body ?? string.Empty,
            IsActive = req.IsActive,
            UpdatedByUserId = GetUserId(context.User)
        });
        audit.Record(context, "EmailTemplateSave", true, $"type={type}");
        return Results.Ok(new { success = true, message = $"{type} template saved." });
    }
    catch (Exception ex)
    {
        audit.Record(context, "EmailTemplateSave", false, "save-failed");
        return Results.Problem($"Could not save template: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

// ── Per-ticket email log + resend ──────────────────────────────────────
app.MapGet("/api/itcm/tickets/{ticketId}/emails", async (string ticketId, IItcmRepository repo, int maxRows = 50) =>
{
    var id = await ResolveTicketIdAsync(repo, ticketId);
    if (!id.HasValue)
        return Results.NotFound(new { Message = "Ticket not found." });
    var items = await repo.GetEmailLogForTicketAsync(id.Value, maxRows);
    return Results.Ok(items.Select(e => new
    {
        emailLogId = e.EmailLogId,
        emailType = e.EmailType,
        recipient = e.Recipient,
        subject = e.Subject,
        status = e.Status,
        errorMessage = e.ErrorMessage,
        dateSent = e.DateSent
    }));
}).RequireAuthorization();

app.MapPost("/api/itcm/tickets/{ticketId}/emails/{emailLogId}/resend", async (
    string ticketId,
    long emailLogId,
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailService emailService) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "EmailResend"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    var id = await ResolveTicketIdAsync(repo, ticketId);
    if (!id.HasValue)
    {
        audit.Record(context, "EmailResend", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    var ticket = await repo.GetTicketNotificationDataAsync(id.Value);
    if (ticket is null)
    {
        audit.Record(context, "EmailResend", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    // V1: the log stores subject but not body, so resend issues a fresh
    // StatusUpdate notification to the ticket's current recipients.
    var userId = GetUserId(context.User);
    var result = await emailService.NotifyStatusChangeAsync(
        id.Value, ticket.Status ?? string.Empty, ticket.Status ?? string.Empty,
        $"Resent from email log #{emailLogId} via dashboard.", userId);
    audit.Record(context, "EmailResend", result.SentSuccessfully, $"log={emailLogId};ticket={id.Value}");
    if (result.SentSuccessfully)
        return Results.Ok(new { success = true, message = "Notification re-sent to current recipients." });
    return Results.Ok(new { success = false, message = result.Message ?? "Resend did not produce an email (check rules/template)." });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);
```

Record next to `NotificationRulesSaveRequest`:

```csharp
record EmailTemplateSaveRequest(string? TemplateType, string? Subject, string? Body, bool IsActive);
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
git commit -m "feat(itcm): add template + per-ticket email endpoints"
```

---

### Task 3: Dashboard Templates editor + Ticket Email Log cards

**Files:**
- Modify: `Yakult.ITCM.Server/Pages/Index.cshtml` (two cards after the Manual Escalation / Resolve cards in `panel-monitoring`)
- Modify: `Yakult.ITCM.Server/wwwroot/js/dashboard.js` (template load/save + email-log search/resend helpers)
- Test: browse dashboard as admin, edit a template, search a ticket's log, resend

- [ ] **Step 1: Razor — Templates editor card (admin only)**

```html
  <!-- ── Email templates (admin only) ── -->
  <div class="card row-gap">
    <div class="card-header">
      <div>
        <div class="card-title"><span class="dot"></span>Email Templates</div>
        <div class="card-helper">Placeholders like {{TicketCode}} {{Status}} {{Note}} render per ticket. One row per type.</div>
      </div>
    </div>
    @if (User.HasClaim("ItcmAdministrator", "true"))
    {
      <div class="escalate-row">
        <select id="tplType" class="filter-input device-select" aria-label="Template type">
          <option>Reminder</option>
          <option>Escalation</option>
          <option>StatusUpdate</option>
          <option>Assignment</option>
          <option>Reassignment</option>
        </select>
        <label class="check-toggle" title="Inactive templates cause sends to be skipped + logged"><input type="checkbox" id="tplActive" checked /> Active</label>
        <button class="ghost sm" onclick="loadTemplate()">Load</button>
      </div>
      <div class="escalate-row">
        <input type="text" class="filter-input" id="tplSubject" placeholder="Subject ({{TicketCode}} …)" aria-label="Template subject" style="flex:2" />
      </div>
      <div class="escalate-row">
        <textarea class="filter-input" id="tplBody" rows="6" placeholder="Body — {{CallerName}} {{Issue}} {{OldStatus}} → {{NewStatus}} {{Note}}" aria-label="Template body" style="flex:1"></textarea>
      </div>
      <div class="escalate-row">
        <button class="primary sm" onclick="saveTemplate()">Save template</button>
      </div>
    }
    else
    {
      <div class="quick-action-row">
        <label class="grow">Email template editing</label>
        <span class="pill muted" title="Needs an administrator sign-in">Admin only</span>
      </div>
    }
  </div>
```

- [ ] **Step 2: Razor — Ticket email log card (any signed-in viewer searches; resend admin-only via JS gate)**

```html
  <!-- ── Ticket email log ── -->
  <div class="card row-gap">
    <div class="card-header">
      <div>
        <div class="card-title"><span class="dot"></span>Ticket Email Log</div>
        <div class="card-helper">Search deliveries for one ticket. Resend issues a fresh status notification (original body is not stored).</div>
      </div>
    </div>
    <div class="escalate-row">
      <input type="text" class="filter-input" id="emailLogTicket" placeholder="TCK-000123 or 123" aria-label="Ticket code or ID" />
      <button class="ghost sm" onclick="searchEmailLog()">Search</button>
    </div>
    <div class="data-table-wrap">
      <table class="data-table">
        <thead><tr><th>When</th><th>Type</th><th>Status</th><th>Recipients</th><th>Subject</th><th>Details</th>@if (User.HasClaim("ItcmAdministrator", "true")) {<th></th>}</tr></thead>
        <tbody id="ticketEmailLogBody"></tbody>
      </table>
    </div>
  </div>
```

- [ ] **Step 3: JS — template + log helpers (append after `escalateTicket`)**

```js
async function loadEmailTemplates() {
  try {
    const items = await getJson('/api/itcm/email/templates');
    const sel = document.getElementById('tplType');
    if (sel && items && items.length) {
      const first = items[0];
      sel.value = first.templateType || sel.value;
      await loadTemplate(items);
    }
  } catch (e) { /* admin-only; viewers get 403 — stay silent */ }
}

async function loadTemplate(cached) {
  const sel = document.getElementById('tplType');
  if (!sel) return;
  const items = cached || await getJson('/api/itcm/email/templates');
  const found = (items || []).find(t => t.templateType === sel.value);
  setInputValue('tplSubject', found ? found.subject || '' : '');
  setInputValue('tplBody', found ? found.body || '' : '');
  const active = document.getElementById('tplActive');
  if (active) active.checked = found ? !!found.isActive : true;
}

function setInputValue(id, value) {
  const el = document.getElementById(id);
  if (el) el.value = value;
}

async function saveTemplate() {
  const sel = document.getElementById('tplType');
  if (!sel) return;
  const body = {
    templateType: sel.value,
    subject: document.getElementById('tplSubject').value || '',
    body: document.getElementById('tplBody').value || '',
    isActive: !!document.getElementById('tplActive').checked
  };
  await postJson('/api/itcm/email/templates/save', body, `${sel.value} template saved.`, 'Template save failed.');
}

async function searchEmailLog() {
  const input = document.getElementById('emailLogTicket');
  const tbody = document.getElementById('ticketEmailLogBody');
  if (!input || !tbody) return;
  const raw = (input.value || '').trim();
  if (!raw) { showToast('Enter a ticket code or ID first.', 'warn'); return; }
  tbody.innerHTML = '';
  try {
    const items = await getJson(`/api/itcm/tickets/${encodeURIComponent(raw)}/emails?maxRows=50`);
    if (!items.length) { monitoringEmptyRow(tbody, 7, 'No email deliveries for this ticket yet.'); return; }
    const admin = canAssignTickets();
    items.forEach(e => {
      const row = document.createElement('tr');
      const status = createPill(e.status || 'Unknown', (e.status || '').toLowerCase() === 'sent' ? 'ok' : (e.status || '').toLowerCase() === 'failed' ? 'fail' : 'muted');
      appendCells(row, [fmt(e.dateSent), e.emailType || '—', status, expandableCell(e.recipient, 60), expandableCell(e.subject, 70), expandableCell(e.errorMessage, 70)]);
      if (admin) {
        const cell = document.createElement('td');
        const btn = document.createElement('button');
        btn.className = 'ghost sm';
        btn.textContent = 'Resend';
        btn.onclick = () => resendEmail(raw, e.emailLogId);
        cell.appendChild(btn);
        row.appendChild(cell);
      }
      tbody.appendChild(row);
    });
  } catch (err) {
    monitoringEmptyRow(tbody, 7, 'Ticket not found or log unavailable.');
  }
}

async function resendEmail(ticketId, emailLogId) {
  await postJson(`/api/itcm/tickets/${encodeURIComponent(ticketId)}/emails/${emailLogId}/resend`, {},
    'Notification re-sent.', 'Resend produced no email — check rules/template.');
  searchEmailLog();
}
```

Wire `loadEmailTemplates()` into `loadAll()` after `loadMonitoring()`:

```js
await loadMonitoring();
if (canAssignTickets()) await loadEmailTemplates();
```

- [ ] **Step 4: Verify in browser + build**

```powershell
dotnet build "Yakult.ITCM.Server\Yakult.ITCM.Server.csproj" -c Release --nologo -v q
```

Expected: `Build succeeded.` Then as admin: load a template, save with a test word, reload to confirm; search a known ticket's log; resend one row and confirm new log row appears on re-search.

- [ ] **Step 5: Commit**

```bash
git add Yakult.ITCM.Server/Pages/Index.cshtml Yakult.ITCM.Server/wwwroot/js/dashboard.js
git commit -m "feat(itcm): add template editor + ticket email log UI"
```
