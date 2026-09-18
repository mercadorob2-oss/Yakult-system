using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Writes "Activity" notifications for Inventory System events (currently just "an item was
    /// added") into dbo.Notification, owned by the InventorySystem portal and fanned out to every
    /// user with Inventory System access via NotificationRepository.CreateForPortalAudience.
    ///
    /// Item-added calls are COALESCED per actor over a short window so a batch add / CSV import
    /// (which calls ItemRepository.AddItem once per row) produces a single "added N items"
    /// notification instead of one per item. A lone add still yields "added &lt;item&gt;".
    ///
    /// Every method is fully best-effort: any failure is swallowed and logged. A notification
    /// problem must never break the operation that triggered it.
    /// </summary>
    public static class InventoryActivityNotifier
    {
        // Time to wait for more AddItem calls from the same actor before flushing a summary.
        // A batch loop finishes well inside this; a lone add just costs this much latency
        // before the (non-blocking) feed row appears.
        private const int CoalesceWindowMs = 2000;

        // Cap on how many items get itemised in the expandable list of a batch row.
        // Beyond this the message still says the true count; the list shows the first N.
        private const int MaxItemisedChildren = 100;

        /// <summary>Payload stored in dbo.Notification.DetailsJson for an ITEM_ADDED row.
        /// <c>source</c> = the flow the add came from ("Request Set", "Invoice", …); null if unknown.
        /// <c>items</c> = the per-item list for a batch row; null for a single add.
        /// Property names are the JSON keys — keep them short and stable (the WPF row parses this).</summary>
        public sealed class ActivityDetails
        {
            public string source { get; set; }
            public List<ActivityItemDetail> items { get; set; }
        }

        public sealed class ActivityItemDetail
        {
            public int    id     { get; set; }
            public string name   { get; set; }
            public string type   { get; set; }
            public string serial { get; set; }   // shown instead of name in the expandable list when present
            public string note   { get; set; }   // extra context, e.g. a renewal's new end date
            public int    qty    { get; set; }   // StockOnHand / line quantity; 0 or 1 => not shown
        }

        // What kind of thing was created — drives the notification type, wording and navigation.
        private enum ActivityKind { Item, Request, Renewal }

        private sealed class PendingBatch
        {
            public ActivityKind Kind;
            public int    ActorUserId;
            public int    Count;
            public string Source;
            public readonly List<ActivityItemDetail> Items = new List<ActivityItemDetail>();
        }

        private static readonly object _lock = new object();
        // Keyed by "actorUserId|kind" so an item batch and a request batch by the same user
        // in the same window stay separate.
        private static readonly Dictionary<string, PendingBatch> _pending = new Dictionary<string, PendingBatch>();
        private static Timer _flushTimer;

        private static string PendingKey(int actorUserId, ActivityKind kind) => $"{actorUserId}|{(int)kind}";

        // Ambient "where did this add come from" label. A calling flow wraps its item-creation
        // loop in `using (InventoryActivityNotifier.Source("Request Set")) { ... }`; the AddItem
        // hook reads it here. AsyncLocal so it survives await points inside that loop.
        private static readonly AsyncLocal<string> _ambientSource = new AsyncLocal<string>();

        /// <summary>Tag every item-added recorded inside the returned scope with <paramref name="source"/>
        /// (e.g. "Add Item", "Batch Add", "Request Set", "Invoice", "Repair Ticket").</summary>
        public static IDisposable Source(string source)
        {
            var previous = _ambientSource.Value;
            _ambientSource.Value = source;
            return new SourceScope(previous);
        }

        private sealed class SourceScope : IDisposable
        {
            private readonly string _previous;
            private bool _disposed;
            public SourceScope(string previous) { _previous = previous; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _ambientSource.Value = _previous;
            }
        }

        // When set, the coalesced Notify* calls (item added / request created / renewal created)
        // are no-ops. Used by flows that emit their own higher-level row (e.g.
        // RequestSetBuilderDialog -> NotifySetCreated) so the things they create inside don't
        // also produce individual rows.
        private static readonly AsyncLocal<bool> _suppressItemAdded = new AsyncLocal<bool>();

        /// <summary>Within the returned scope, the coalesced Notify* calls do nothing.</summary>
        public static IDisposable SuppressItemAdded()
        {
            var previous = _suppressItemAdded.Value;
            _suppressItemAdded.Value = true;
            return new SuppressScope(previous);
        }

        private sealed class SuppressScope : IDisposable
        {
            private readonly bool _previous;
            private bool _disposed;
            public SuppressScope(bool previous) { _previous = previous; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _suppressItemAdded.Value = _previous;
            }
        }

        /// <summary>Raised (on a threadpool thread) after a buffered batch/single has been written,
        /// so an open Activity panel can refresh even for the actor's own — deliberately silent — rows.</summary>
        public static event Action Emitted;

        /// <summary>
        /// Record that <paramref name="actorUserId"/> added an item. Buffered and flushed as one
        /// notification (detailed if it was the only item in the window, a count summary otherwise).
        /// </summary>
        public static void NotifyItemAdded(int itemId, string itemName, string itemType, int actorUserId, string source = null, int quantity = 1, string serialNumber = null)
        {
            Enqueue(ActivityKind.Item, actorUserId, source, "NotifyItemAdded", new ActivityItemDetail
            {
                id = itemId, name = itemName, type = itemType,
                serial = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
                qty = quantity > 1 ? quantity : 0,
            });
        }

        /// <summary>Record that <paramref name="actorUserId"/> created an item request. Coalesced
        /// per actor like item adds — a Batch Add Requests run yields one "N requests created" row.</summary>
        public static void NotifyRequestCreated(int requestId, string itemName, int quantity, int actorUserId, string serialNumber = null, string source = null)
        {
            Enqueue(ActivityKind.Request, actorUserId, source, "NotifyRequestCreated", new ActivityItemDetail
            {
                id = requestId, name = itemName,
                serial = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
                qty = quantity > 1 ? quantity : 0,
            });
        }

        /// <summary>Record that <paramref name="actorUserId"/> renewed the given item. Coalesced per
        /// actor — a "Renew All" run yields one "N renewals created" row. ReferenceId is the ItemId
        /// so the row opens the Renewals page filtered to that item.</summary>
        public static void NotifyRenewalCreated(int itemId, DateTime? newEndDate, int actorUserId, string source = null)
        {
            var name = ResolveItemName(itemId) ?? (itemId > 0 ? $"Item #{itemId}" : "an item");
            Enqueue(ActivityKind.Renewal, actorUserId, source, "NotifyRenewalCreated", new ActivityItemDetail
            {
                id = itemId, name = name,
                note = newEndDate.HasValue ? $"new end date {newEndDate.Value:yyyy-MM-dd}" : null,
            });
        }

        private static void Enqueue(ActivityKind kind, int actorUserId, string source, string caller, ActivityItemDetail detail)
        {
            try
            {
                if (_suppressItemAdded.Value) return;

                var src = source ?? _ambientSource.Value;
                var key = PendingKey(actorUserId, kind);

                lock (_lock)
                {
                    if (!_pending.TryGetValue(key, out var batch))
                    {
                        batch = new PendingBatch { Kind = kind, ActorUserId = actorUserId, Source = src };
                        _pending[key] = batch;
                    }
                    else if (!string.Equals(batch.Source, src, StringComparison.Ordinal))
                    {
                        batch.Source = null; // mixed sources in one coalesce window — don't guess
                    }
                    batch.Count++;
                    if (batch.Items.Count < MaxItemisedChildren)
                        batch.Items.Add(detail);

                    _flushTimer?.Dispose();
                    _flushTimer = new Timer(_ => Flush(), null, CoalesceWindowMs, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError($"[InventoryActivityNotifier] {caller} failed", ex);
            }
        }

        /// <summary>Flush any buffered item-added notifications immediately (call on app shutdown).</summary>
        public static void FlushPending() => Flush();

        /// <summary>
        /// One Activity row for a Set / Invoice that was just created (Build Request Set,
        /// Build / Import Invoice). Written immediately — no coalescing. <paramref name="source"/>
        /// ("Request Set" or "Invoice") becomes the pill and picks the detail page the row opens.
        /// Wrap the item-creation loop of the calling flow in <see cref="SuppressItemAdded"/> so
        /// those items don't also produce "added X" rows.
        /// </summary>
        public static void NotifySetCreated(int setId, int itemCount, int actorUserId, string source,
                                            IReadOnlyList<ActivityItemDetail> items = null)
        {
            try
            {
                var label     = string.IsNullOrWhiteSpace(source) ? "Set" : source.Trim();
                var actorName = ResolveUserName(actorUserId) ?? AppSession.CurrentUserName ?? "Someone";
                var article   = (label.Length > 0 && "aeiouAEIOU".IndexOf(label[0]) >= 0) ? "an" : "a";

                var list  = (items != null && items.Count > 0)
                                ? items.Take(MaxItemisedChildren).ToList()
                                : null;
                var count = itemCount > 0 ? itemCount : (list?.Count ?? 0);
                var countText = count > 0 ? $" with {count} item(s)" : string.Empty;

                string detailsJson = null;
                try { detailsJson = JsonSerializer.Serialize(new ActivityDetails { source = label, items = list }); }
                catch (Exception ex) { Debug.WriteLine($"[InventoryActivityNotifier] set DetailsJson serialize failed: {ex.Message}"); }

                var dto = new NotificationCreateDto
                {
                    Title            = $"{label} created",
                    Message          = $"{actorName} created {article} {label}{countText}.",
                    NotificationType = NotificationType.SetCreated,
                    ReferenceId      = setId > 0 ? setId : (int?)null,
                    ActorUserId      = actorUserId > 0 ? actorUserId : (int?)null,
                    DetailsJson      = detailsJson,
                    PortalKey        = NotificationType.InventorySystemKey,
                };

                Core.Logger.LogInfo($"[InventoryActivityNotifier] emitting set-created — actor={actorUserId}, source={label}, setId={setId}, items={itemCount}");
                new NotificationRepository().CreateForPortalAudience(NotificationType.InventorySystemKey, dto);

                try { Emitted?.Invoke(); }
                catch (Exception ex) { Core.Logger.LogError("[InventoryActivityNotifier] Emitted handler failed", ex); }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("[InventoryActivityNotifier] NotifySetCreated failed", ex);
            }
        }

        private static void Flush()
        {
            List<PendingBatch> toEmit;
            lock (_lock)
            {
                if (_pending.Count == 0) return;
                toEmit = _pending.Values.ToList();
                _pending.Clear();
                _flushTimer?.Dispose();
                _flushTimer = null;
            }

            bool any = false;
            foreach (var batch in toEmit)
            {
                try { Emit(batch); any = true; }
                catch (Exception ex) { Core.Logger.LogError("[InventoryActivityNotifier] Emit failed", ex); }
            }

            if (any)
            {
                try { Emitted?.Invoke(); }
                catch (Exception ex) { Core.Logger.LogError("[InventoryActivityNotifier] Emitted handler failed", ex); }
            }
        }

        private static void Emit(PendingBatch batch)
        {
            var actorName = ResolveUserName(batch.ActorUserId) ?? AppSession.CurrentUserName ?? "Someone";

            var first   = batch.Items.FirstOrDefault();
            var isBatch = batch.Count > 1;

            var name = string.IsNullOrWhiteSpace(first?.name) ? null : $"\"{first.name.Trim()}\"";
            var type = string.IsNullOrWhiteSpace(first?.type) ? null : $" ({first.type.Trim()})";
            var sn   = string.IsNullOrWhiteSpace(first?.serial) ? null : $" (SN {first.serial.Trim()})";
            var qty  = (first?.qty ?? 0) > 1 ? $" ×{first.qty}" : null;
            var note = string.IsNullOrWhiteSpace(first?.note) ? null : $" — {first.note.Trim()}";

            string title, message, notifType;

            switch (batch.Kind)
            {
                case ActivityKind.Request:
                    notifType = NotificationType.RequestCreated;
                    title     = isBatch ? "Requests created" : "Request created";
                    message   = isBatch
                        ? $"{actorName} created {batch.Count} requests."
                        : $"{actorName} created a request for {name ?? "an item"}{qty}.";
                    break;

                case ActivityKind.Renewal:
                    notifType = NotificationType.RenewalCreated;
                    title     = isBatch ? "Renewals created" : "Renewal created";
                    message   = isBatch
                        ? $"{actorName} created {batch.Count} renewals."
                        : $"{actorName} renewed {name ?? "an item"}{note}.";
                    break;

                default: // Item
                    notifType = NotificationType.ItemAdded;
                    title     = isBatch ? "Items added" : "New item added";
                    message   = isBatch
                        ? $"{actorName} added {batch.Count} items to inventory."
                        : $"{actorName} added {name ?? "an item"}{type}{sn}{qty} to inventory.";
                    break;
            }

            int? referenceId = (!isBatch && (first?.id ?? 0) > 0) ? first.id : (int?)null;

            // DetailsJson carries the source label (any row) and the per-item list (batch only).
            string detailsJson = null;
            var source = string.IsNullOrWhiteSpace(batch.Source) ? null : batch.Source.Trim();
            if (source != null || isBatch)
            {
                var details = new ActivityDetails { source = source, items = isBatch ? batch.Items : null };
                try { detailsJson = JsonSerializer.Serialize(details); }
                catch (Exception ex) { Debug.WriteLine($"[InventoryActivityNotifier] DetailsJson serialize failed: {ex.Message}"); }
            }

            var dto = new NotificationCreateDto
            {
                Title            = title,
                Message          = message,
                NotificationType = notifType,
                ReferenceId      = referenceId,
                ActorUserId      = batch.ActorUserId > 0 ? batch.ActorUserId : (int?)null,
                DetailsJson      = detailsJson,
                PortalKey        = NotificationType.InventorySystemKey,
            };

            Core.Logger.LogInfo($"[InventoryActivityNotifier] emitting {batch.Kind} {(isBatch ? $"batch x{batch.Count}" : "single")} — actor={batch.ActorUserId}, source={source ?? "(none)"}, ref={referenceId}");
            new NotificationRepository().CreateForPortalAudience(NotificationType.InventorySystemKey, dto);
        }

        private static string ResolveUserName(int userId) => ResolveScalar(
            "SELECT Name FROM dbo.[User] WHERE UserId = @Id", userId, "ResolveUserName");

        private static string ResolveItemName(int itemId) => ResolveScalar(
            "SELECT Name FROM dbo.Item WHERE ItemId = @Id", itemId, "ResolveItemName");

        private static string ResolveScalar(string sql, int id, string caller)
        {
            if (id <= 0) return null;
            try
            {
                DatabaseConfig.EnsureConfigured();
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    con.Open();
                    return cmd.ExecuteScalar() as string;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InventoryActivityNotifier] {caller} failed: {ex.Message}");
                return null;
            }
        }
    }
}
