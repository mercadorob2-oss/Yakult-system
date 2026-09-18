using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public sealed class ItemLifecycleDecisionRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Executes a terminal lifecycle decision for an Item: Sold or Disposed.
        /// Writes:
        /// - dbo.ItemInspectionLog (recommendation snapshot)
        /// - dbo.ItemLifecycleDecision (DecisionStatus='Executed')
        /// - dbo.Inventory (EntryType='Negative')
        /// - dbo.ArchiveStatus (EntityType='Item', IsArchived=1)
        /// And updates dbo.Item (Active=0, StockOnHand decremented).
        /// </summary>
        public async Task<int> ExecuteSellOrDisposeAsync(
            int itemId,
            string action,
            int decidedByUserId,
            string decidedByDisplayName,
            int quantity,
            string recipientName,
            decimal? saleAmount,
            string remarks)
        {
            if (itemId <= 0) throw new ArgumentOutOfRangeException(nameof(itemId));

            action = (action ?? string.Empty).Trim();
            if (!string.Equals(action, "Disposed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(action, "Sold", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid action '{action}'. Must be 'Disposed' or 'Sold'.", nameof(action));
            }

            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be > 0.");

            var decisionTypeName = string.Equals(action, "Disposed", StringComparison.OrdinalIgnoreCase) ? "DISPOSE" : "SELL";
            var recommendation = decisionTypeName; // 'DISPOSE' | 'SELL'
            var archivedBy = string.IsNullOrWhiteSpace(decidedByDisplayName) ? decidedByUserId.ToString() : decidedByDisplayName.Trim();

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
SET NOCOUNT ON;

DECLARE @StockOnHand INT = 0;
DECLARE @ConditionId INT = NULL;
DECLARE @SerialNumber NVARCHAR(100) = NULL;

SELECT
    @StockOnHand = ISNULL(i.StockOnHand, 0),
    @ConditionId = i.ConditionId,
    @SerialNumber = i.SerialNumber
FROM dbo.Item i WITH (UPDLOCK, HOLDLOCK)
WHERE i.ItemId = @ItemId;

IF @@ROWCOUNT = 0
    THROW 51010, 'Item not found.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.ArchiveStatus a
    WHERE a.EntityType = 'Item'
      AND a.EntityId = @ItemId
      AND a.IsArchived = 1
)
    THROW 51011, 'Item is already archived.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.ItemLifecycleDecision d
    WHERE d.ItemId = @ItemId
      AND d.DecisionStatus = 'Executed'
)
    THROW 51012, 'Item already has an Executed lifecycle decision.', 1;

IF (@StockOnHand > 0 AND @Quantity > @StockOnHand)
    THROW 51013, 'Quantity exceeds StockOnHand.', 1;

IF (@StockOnHand = 0 AND @Quantity <> 1)
    THROW 51014, 'When StockOnHand is 0, only Quantity=1 is allowed.', 1;

INSERT INTO dbo.ItemInspectionLog
    (ItemId, ConditionId, InspectedAt, InspectedBy, Recommendation, Notes)
VALUES
    (@ItemId, @ConditionId, SYSUTCDATETIME(), @DecidedByUserId, @Recommendation, @InspectionNotes);

DECLARE @DecisionTypeId INT =
(
    SELECT TOP (1) DecisionTypeId
    FROM dbo.ItemDecisionType
    WHERE DecisionTypeName = @DecisionTypeName
);

IF @DecisionTypeId IS NULL
    THROW 51015, 'Decision type not configured (dbo.ItemDecisionType).', 1;

INSERT INTO dbo.ItemLifecycleDecision
    (ItemId, DecisionTypeId, ConditionId, Quantity,
     DecisionStatus, DecidedAt, DecidedBy, RecipientName, SaleAmount, Remarks)
VALUES
    (@ItemId, @DecisionTypeId, @ConditionId, @Quantity,
     'Executed', SYSUTCDATETIME(), @DecidedByUserId, @RecipientName, @SaleAmount, @Remarks);

DECLARE @DecisionId INT = CAST(SCOPE_IDENTITY() AS INT);

INSERT INTO dbo.Inventory
    (ItemId, EntryType, Quantity, DatePosted, PostedBy, ReqId, Description, ConditionID, Active)
VALUES
    (@ItemId, 'Negative', @Quantity, SYSUTCDATETIME(), @DecidedByUserId, NULL, @InventoryDescription, @ConditionId, 1);

UPDATE dbo.Item
SET
    StockOnHand = CASE
        WHEN (ISNULL(StockOnHand, 0) - @Quantity) < 0 THEN 0
        ELSE (ISNULL(StockOnHand, 0) - @Quantity)
    END,
    Active = 0,
    DateModified = SYSUTCDATETIME(),
    ModifiedBy = @DecidedByUserId
WHERE ItemId = @ItemId;

MERGE dbo.ArchiveStatus AS tgt
USING (SELECT 'Item' AS EntityType, @ItemId AS EntityId) AS src
    ON tgt.EntityType = src.EntityType AND tgt.EntityId = src.EntityId
WHEN MATCHED THEN
    UPDATE SET
        IsArchived = 1,
        ArchivedAt = SYSUTCDATETIME(),
        ArchivedBy = @ArchivedBy,
        ArchiveReason = @ArchiveReason,
        RestoredAt = NULL,
        RestoredBy = NULL
WHEN NOT MATCHED THEN
    INSERT (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
    VALUES ('Item', @ItemId, 1, SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason);

INSERT INTO dbo.ItemAuditTrail
    (ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
     DepartmentId, DepartmentName, BranchId, BranchName, Direction,
     Status, ReferenceType, ReferenceId, Notes, CreatedBy)
VALUES
    (@ItemId, @SerialNumber, @AuditAction, SYSDATETIME(), @DecidedByUserId, @DecidedByDisplayName,
     NULL, NULL, NULL, NULL, 'OUT', 'Completed', 'Item', @ItemId, @AuditNotes, @CreatedBy);

SELECT @DecisionId;";

                        var inventoryDescription = string.Equals(action, "Disposed", StringComparison.OrdinalIgnoreCase)
                            ? $"Item disposed (ItemId {itemId})"
                            : $"Item sold (ItemId {itemId})";

                        if (!string.IsNullOrWhiteSpace(recipientName))
                            inventoryDescription += $" to {recipientName.Trim()}";

                        if (!string.IsNullOrWhiteSpace(remarks))
                            inventoryDescription += $" • {remarks.Trim()}";

                        var inspectionNotes = BuildInspectionNotes(action, recipientName, saleAmount, remarks);

                        using (var cmd = new SqlCommand(sql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            cmd.Parameters.AddWithValue("@Quantity", quantity);
                            cmd.Parameters.AddWithValue("@DecidedByUserId", decidedByUserId);
                            cmd.Parameters.AddWithValue("@DecisionTypeName", decisionTypeName);
                            cmd.Parameters.AddWithValue("@Recommendation", recommendation);
                            cmd.Parameters.AddWithValue("@InspectionNotes", (object)inspectionNotes ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RecipientName", string.IsNullOrWhiteSpace(recipientName) ? (object)DBNull.Value : recipientName.Trim());
                            cmd.Parameters.AddWithValue("@SaleAmount", decisionTypeName == "SELL" ? (object)saleAmount ?? DBNull.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim());
                            cmd.Parameters.AddWithValue("@InventoryDescription", inventoryDescription);
                            cmd.Parameters.AddWithValue("@ArchivedBy", archivedBy);
                            cmd.Parameters.AddWithValue("@ArchiveReason", action);
                            cmd.Parameters.AddWithValue("@AuditAction", decisionTypeName == "SELL" ? "Item Sold" : "Item Disposed");
                            cmd.Parameters.AddWithValue("@AuditNotes", inventoryDescription);
                            cmd.Parameters.AddWithValue("@DecidedByDisplayName", (object)decidedByDisplayName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CreatedBy", AppSession.CurrentUserName ?? "System");

                            var decisionIdObj = await cmd.ExecuteScalarAsync();
                            tx.Commit();
                            return Convert.ToInt32(decisionIdObj);
                        }
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Reverses an Executed lifecycle decision for an Item — companion to ExecuteSellOrDisposeAsync,
        /// used by RepairTicketRepository.Disposition.cs when a technician changes their mind about an
        /// Unrepairable ticket's disposition (Discard vs Replace, or picks a different replacement item).
        /// Flips DecisionStatus 'Executed' -> 'Cancelled' (a value the CK_ItemLifecycleDecision_Status
        /// constraint and the UQ_ItemLifecycleDecision_Executed filtered unique index already support,
        /// so no schema change is needed), restores Item.Active/StockOnHand, clears ArchiveStatus, and
        /// writes compensating dbo.Inventory/dbo.ItemAuditTrail rows — additive, matching this app's
        /// append-only audit convention rather than deleting the original rows.
        /// No-ops (returns false) if the item has no Executed decision to reverse.
        /// </summary>
        public async Task<bool> ReverseDecisionAsync(int itemId, int reversedByUserId, string reversedByDisplayName)
        {
            if (itemId <= 0) throw new ArgumentOutOfRangeException(nameof(itemId));

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
SET NOCOUNT ON;

DECLARE @DecisionId INT = NULL;
DECLARE @Quantity INT = 0;
DECLARE @ConditionId INT = NULL;
DECLARE @SerialNumber NVARCHAR(100) = NULL;

SELECT TOP (1) @DecisionId = d.DecisionId, @Quantity = d.Quantity, @ConditionId = d.ConditionId
FROM dbo.ItemLifecycleDecision d WITH (UPDLOCK, HOLDLOCK)
WHERE d.ItemId = @ItemId AND d.DecisionStatus = 'Executed';

IF @DecisionId IS NULL
    RETURN;

SELECT @SerialNumber = i.SerialNumber FROM dbo.Item i WHERE i.ItemId = @ItemId;

UPDATE dbo.ItemLifecycleDecision SET DecisionStatus = 'Cancelled' WHERE DecisionId = @DecisionId;

UPDATE dbo.Item
SET
    StockOnHand = ISNULL(StockOnHand, 0) + @Quantity,
    Active = 1,
    DateModified = SYSUTCDATETIME(),
    ModifiedBy = @ReversedByUserId
WHERE ItemId = @ItemId;

UPDATE dbo.ArchiveStatus
SET IsArchived = 0, RestoredAt = SYSUTCDATETIME(), RestoredBy = @ReversedBy
WHERE EntityType = 'Item' AND EntityId = @ItemId;

INSERT INTO dbo.Inventory
    (ItemId, EntryType, Quantity, DatePosted, PostedBy, ReqId, Description, ConditionID, Active)
VALUES
    (@ItemId, 'Positive', @Quantity, SYSUTCDATETIME(), @ReversedByUserId, NULL, @InventoryDescription, @ConditionId, 1);

INSERT INTO dbo.ItemAuditTrail
    (ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
     DepartmentId, DepartmentName, BranchId, BranchName, Direction,
     Status, ReferenceType, ReferenceId, Notes, CreatedBy)
VALUES
    (@ItemId, @SerialNumber, 'Disposal Reversed', SYSDATETIME(), @ReversedByUserId, @ReversedByDisplayName,
     NULL, NULL, NULL, NULL, 'IN', 'Completed', 'Item', @ItemId, @InventoryDescription, @CreatedBy);

SELECT @DecisionId;";

                        var reversedBy = string.IsNullOrWhiteSpace(reversedByDisplayName) ? reversedByUserId.ToString() : reversedByDisplayName.Trim();

                        using (var cmd = new SqlCommand(sql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            cmd.Parameters.AddWithValue("@ReversedByUserId", reversedByUserId);
                            cmd.Parameters.AddWithValue("@ReversedBy", reversedBy);
                            cmd.Parameters.AddWithValue("@ReversedByDisplayName", (object)reversedByDisplayName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@InventoryDescription", $"Disposal reversed (ItemId {itemId})");
                            cmd.Parameters.AddWithValue("@CreatedBy", AppSession.CurrentUserName ?? "System");

                            var decisionIdObj = await cmd.ExecuteScalarAsync();
                            tx.Commit();
                            return decisionIdObj != null;
                        }
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static string BuildInspectionNotes(string action, string recipientName, decimal? saleAmount, string remarks)
        {
            var notes = $"{action}";

            if (!string.IsNullOrWhiteSpace(recipientName))
                notes += $" • Recipient: {recipientName.Trim()}";

            if (string.Equals(action, "Sold", StringComparison.OrdinalIgnoreCase) && saleAmount.HasValue)
                notes += $" • Amount: {saleAmount.Value:0.00}";

            if (!string.IsNullOrWhiteSpace(remarks))
                notes += $" • {remarks.Trim()}";

            return notes;
        }
    }
}

