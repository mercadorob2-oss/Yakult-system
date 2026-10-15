# Local AI Chat (Ask AI) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An "Ask AI" button in the workspace header opens an inquiry-only chat dialog answered by the office Ollama server (qwen2.5:1.5b).

**Architecture:** WinForms chat dialog posts conversation history to `POST {OllamaBaseUrl}/api/chat` (non-streaming) and renders the reply. Server address and model come from `App.config` so any PC can point at the AI host without rebuilding. No database access, no actions, no API keys (LAN Ollama needs none).

**Tech Stack:** .NET Framework 4.8 WinForms, `HttpClient`, Newtonsoft.Json (already referenced).

**Spec:** The chat history in this conversation (local Ollama on the user's Ultra 7, qwen2.5:1.5b, inquiry-only, configurable address). `App.config` already carries `OllamaBaseUrl` (default `http://localhost:11434`) and `OllamaModel` (default `qwen2.5:1.5b`) — uncommitted, Task 1 adopts them.

## Global Constraints

- `Yakult.Inventory.App\Yakult.Inventory.App.csproj` is non-SDK-style: EVERY new `.cs` file MUST get an explicit `<Compile Include>` entry.
- No test runner exists in the repo: verification steps are MSBuild (exit 0, zero `error` lines) plus the stated manual runtime check, not pytest.
- Inquiry-only: the chat client sends nothing but the user's typed messages plus a fixed system prompt. No session data, no inventory data, no DB queries.
- Never freeze the UI on network calls: all HTTP off the UI thread, send button disabled while awaiting, dialog stays closable mid-request.
- After editing `App.config`, validate it parses: `try { [xml](Get-Content "Yakult.Inventory.App\App.config" -Raw) | Out-Null; Write-Host "VALID XML" } catch { Write-Host "INVALID" }`.

## Review Focus

- Ollama down/unreachable: user gets "Couldn't reach the AI server at {url}. Check that Ollama is running and the address is correct." in-chat, not a crash or hang — manual check by stopping Ollama and sending a message.
- Slow first reply (model load on the host): 180s timeout; "thinking…" indicator shown; dialog closable while waiting — manual check by asking while host is cold.
- Very long answer: history box scrolls, dialog remains responsive — manual check with "explain(Sql joins in detail".
- Empty send: Enter/click with blank input does nothing (no empty POST) — manual check.
- Special characters/quotes in questions must not break the JSON payload — manual check with quotes and newlines in one message.

---
### Task 1: Config + Ollama chat service

**Files:**
- Modify: `Yakult.Inventory.App\Core\AppConfig.cs` (add cached `OllamaBaseUrl`, `OllamaModel` properties following the `ItcmServerUrl` pattern)
- Modify: `Yakult.Inventory.App\App.config` (adopt the already-present `OllamaBaseUrl`/`OllamaModel` keys; verify values)
- Create: `Yakult.Inventory.App\Services\OllamaChatService.cs`
- Modify: `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (add `<Compile Include="Services\OllamaChatService.cs" />` in the Services block)

**Interfaces:**
- Consumes: `AppConfig.OllamaBaseUrl`, `AppConfig.OllamaModel`.
- Produces (used by Task 2):
  - `OllamaChatService.AskAsync(List<ChatMessage> history, string question, CancellationToken ct) -> Task<string>` — appends nothing itself; POSTs `{model, messages: [system + history + question], stream:false}` to `/api/chat`, returns `message.content` trimmed; throws `HttpRequestException`/`TaskCanceledException` on failure (caller renders friendly text)
  - `ChatMessage { string Role ("system"/"user"/"assistant"), string Content }`
  - `OllamaChatService.SystemPrompt` const = `"You are Yakult's internal IT assistant inside the Yakult Inventory desktop app. Answer basic inquiries concisely."`

- [ ] **Step 1: Add `OllamaBaseUrl`/`OllamaModel` cached properties to `AppConfig.cs`** mirroring `ItcmServerUrl` (lazy cache, `?? "http://localhost:11434"` / `?? "qwen2.5:1.5b"` fallbacks).
- [ ] **Step 2: Confirm `App.config` keys exist** with those defaults; run the XML validation command. Expected: `VALID XML`.
- [ ] **Step 3: Implement `OllamaChatService`** with the exact interface above; single static `HttpClient` with `Timeout = 180s`; Newtonsoft.Json for payload/parse.
- [ ] **Step 4: Register the csproj entry**, validate csproj XML, build: `MSBuild Yakult.Inventory.App.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU /p:OutputPath=<temp> /m /v:minimal /nologo`. Expected: exit 0, zero `error` lines.
- [ ] **Step 5: Commit** `git add` only these files.

---
### Task 2: Chat dialog

**Files:**
- Create: `Yakult.Inventory.App\Dialogs\AskAiDialog.cs` (`<SubType>Form</SubType>`)
- Modify: `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (add the Compile Include)

**Interfaces:**
- Consumes: Task 1's `OllamaChatService.AskAsync`, `ChatMessage`, `AppConfig.OllamaBaseUrl` (shown in the title/status so the user knows which server answers).
- Produces (used by Task 3): `AskAiDialog.ShowDialog(IWin32Window owner)` — static helper `public static void Show(IWin32Window owner)` that opens the form modeless (non-blocking; user keeps working while it answers).

**Dialog contract (my calls):** 760×560, title `"Ask AI — {model} @ {host}"`; read-only history `RichTextBox` (user lines bold dark, AI lines plain, errors red italic); multiline input with Enter=send / Shift+Enter=newline; Send button; "thinking…" appended while awaiting; in-memory `List<ChatMessage>` history per open dialog (lost on close — inquiry-only, no persistence); close/cancel mid-request via CancellationTokenSource.

- [ ] **Step 1: Implement `AskAiDialog`** per the contract above; empty input never POSTs; all network on background (`await Task.Run` or async HttpClient, never blocking the UI thread).
- [ ] **Step 2: Register the csproj entry**, validate XML, build (same MSBuild command). Expected: exit 0, zero errors.
- [ ] **Step 3: Manual check** — with Ollama running locally, open the dialog (temporary `Main()`-style invocation is fine, or wait for Task 3), send `"Reply with exactly: hello"`, confirm the reply renders; stop Ollama, send again, confirm the friendly unreachable message (Review Focus #1).
- [ ] **Step 4: Commit** `git add` only these files.

---
### Task 3: Header entry point

**Files:**
- Modify: `Yakult.Inventory.App\Wpf\Portal\WpfMainDashboardShell.cs` (`BuildHeader`, ~lines 157-175; add `chat` case to `MakeIcon`)

**Interfaces:**
- Consumes: Task 2's `AskAiDialog.Show(owner)` — owner is the WinForms-side handle; use a modeless `Show()` with no owner (dialog is top-level, like the account dialog pattern in `OpenAccountDialog`).
- Produces: nothing downstream.

**Contract (my calls):** new header action `"Ask AI"` placed immediately after `"Help"`, same `HeaderActionStyle`/foreground as its siblings, `ToolTip` = `"Ask the office AI assistant"`; `MakeIcon` gains `case "chat"` with bubble path `M4,4 L20,4 20,14 14,14 10,20 10,14 4,14 Z` (stroke style matching siblings, no new icon files).

- [ ] **Step 1: Add the header button + icon case** per the contract.
- [ ] **Step 2: Build** (same MSBuild command). Expected: exit 0, zero errors.
- [ ] **Step 3: Manual check** — open the workspace, click Ask AI, run the Task 2 conversation checks end-to-end from the real entry point.
- [ ] **Step 4: Commit** `git add` only this file.
