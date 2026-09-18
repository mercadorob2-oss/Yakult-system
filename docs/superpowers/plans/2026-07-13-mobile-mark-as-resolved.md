# Mobile "Mark As Resolved" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a full "Mark As Resolved" replacement dialog to the YakultScanner mobile app matching the desktop ITCM implementation, with Service Only and Replacement (inventory-impacting) resolution types.

**Architecture:** Three tiers: (1) Two new API `.ashx` handlers + one extended handler for the resolution endpoint, (2) Mobile Kotlin API models + repository + ViewModel, (3) New Compose screen for the resolution wizard UI.

**Tech Stack:** C# (.NET Framework 4.7.2, ADO.NET), Kotlin (Retrofit, Jetpack Compose, Hilt ViewModel)

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Yakult.Inventory.Api2_remote\call-items-lookup.ashx` | **Create** | GET: returns out-hardware (`?type=out`) or stock-hardware (`?type=stock`) items |
| `Yakult.Inventory.Api2_remote\call-conditions.ashx` | **Create** | GET: returns item conditions list |
| `Yakult.Inventory.Api2_remote\call-ticket-action.ashx` | **Modify** | Adds `resolution` action (replacement/service-only with inventory transactions) |
| `Latest_sys/YakultScanner/.../api/ApiClient.kt` | **Modify** | Add endpoints + DTOs for resolution, item lookup, conditions |
| `Latest_sys/YakultScanner/.../data/repository/CallMonitoringRepository.kt` | **Modify** | Add `applyResolution`, `getCallItemsLookup`, `getCallConditions` |
| `Latest_sys/YakultScanner/.../viewmodels/CallMonitoringViewModel.kt` | **Modify** | Add resolution state + `applyResolution`, `loadItemsForResolution` |
| `Latest_sys/YakultScanner/.../ui/screens/CallMarkAsResolvedScreen.kt` | **Create** | Full wizard: type → old item → condition → action → new item → preview → confirm |
| `Latest_sys/YakultScanner/.../ui/screens/TicketDetailScreen.kt` | **Modify** | Replace simple dialog with navigation to `CallMarkAsResolvedScreen` |
| `Latest_sys/YakultScanner/.../navigation/AppNavigator.kt` | **Modify** | Add route `call_ticket_resolve/{ticketId}` |

---

### Task 1: API — Create `call-items-lookup.ashx`

**Files:**
- Create: `Yakult.Inventory.Api2_remote\call-items-lookup.ashx`

- [ ] **Step 1: Write the handler**

```csharp
<%@ WebHandler Language="C#" Class="CallItemsLookupHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

public class CallItemsLookupHandler : IHttpHandler
{
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }

    public void ProcessRequest(HttpContext c)
    {
        c.Response.AddHeader("Access-Control-Allow-Origin", "*");
        c.Response.ContentType = "application/json";

        var type = (c.Request["type"] ?? "").Trim().ToLowerInvariant();

        string stockCondition;
        if (type == "stock")
            stockCondition = "AND ISNULL(i.StockOnHand, 0) > 0";
        else if (type == "out")
            stockCondition = "AND ISNULL(i.StockOnHand, 0) = 0";
        else
        {
            c.Response.StatusCode = 400;
            c.Response.Write(JsonConvert.SerializeObject(new { success = false, items = new object[0] }));
            return;
        }

        try
        {
            using (var con = new SqlConnection(Cs()))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
SELECT i.ItemId, i.Name, i.ModelNumber, i.CategoryId, ISNULL(c.Name, '') AS CategoryName, ISNULL(i.StockOnHand, 0) AS StockOnHand
FROM dbo.Item i
LEFT JOIN dbo.Category c ON c.CategoryId = i.CategoryId
WHERE i.ItemType = 'Hardware'
  AND i.Active = 1
  " + stockCondition + @"
ORDER BY i.Name, i.ModelNumber", con))
                using (var r = cmd.ExecuteReader())
                {
                    var items = new List<object>();
                    while (r.Read())
                    {
                        items.Add(new
                        {
                            itemId = Convert.ToInt32(r["ItemId"]),
                            displayText = r["Name"].ToString() + " (" + r["ModelNumber"].ToString() + ")",
                            category = r["CategoryName"].ToString(),
                            stockOnHand = Convert.ToInt32(r["StockOnHand"])
                        });
                    }
                    c.Response.Write(JsonConvert.SerializeObject(new { success = true, items }));
                }
            }
        }
        catch (Exception ex)
        {
            c.Response.StatusCode = 500;
            c.Response.Write(JsonConvert.SerializeObject(new { success = false, items = new object[0], message = ex.Message }));
        }
    }

    public bool IsReusable { get { return false; } }
}
```

---

### Task 2: API — Create `call-conditions.ashx`

**Files:**
- Create: `Yakult.Inventory.Api2_remote\call-conditions.ashx`

- [ ] **Step 1: Write the handler**

```csharp
<%@ WebHandler Language="C#" Class="CallConditionsHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json;

public class CallConditionsHandler : IHttpHandler
{
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }

    public void ProcessRequest(HttpContext c)
    {
        c.Response.AddHeader("Access-Control-Allow-Origin", "*");
        c.Response.ContentType = "application/json";

        try
        {
            using (var con = new SqlConnection(Cs()))
            {
                con.Open();
                // Try ItemCondition table first, fall back to Condition
                string sql;
                if (TableExists(con, "dbo.ItemCondition"))
                    sql = "SELECT ConditionId, ConditionName FROM dbo.ItemCondition WHERE Active = 1 OR Active IS NULL ORDER BY ConditionName";
                else if (TableExists(con, "dbo.Condition"))
                    sql = "SELECT ConditionId, ConditionName FROM dbo.Condition WHERE Active = 1 OR Active IS NULL ORDER BY ConditionName";
                else
                {
                    c.Response.Write(JsonConvert.SerializeObject(new { success = false, conditions = new object[0], message = "No condition table found" }));
                    return;
                }

                using (var cmd = new SqlCommand(sql, con))
                using (var r = cmd.ExecuteReader())
                {
                    var conditions = new List<object>();
                    while (r.Read())
                    {
                        conditions.Add(new
                        {
                            conditionId = Convert.ToInt32(r["ConditionId"]),
                            conditionName = r["ConditionName"].ToString()
                        });
                    }
                    c.Response.Write(JsonConvert.SerializeObject(new { success = true, conditions }));
                }
            }
        }
        catch (Exception ex)
        {
            c.Response.StatusCode = 500;
            c.Response.Write(JsonConvert.SerializeObject(new { success = false, conditions = new object[0], message = ex.Message }));
        }
    }

    private static bool TableExists(SqlConnection con, string name)
    {
        using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@N,'U') IS NOT NULL THEN 1 ELSE 0 END", con))
        { cmd.Parameters.Add("@N", SqlDbType.NVarChar).Value = name; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
    }

    public bool IsReusable { get { return false; } }
}
```

---

### Task 3: API — Add `resolution` action to `call-ticket-action.ashx`

**Files:**
- Modify: `Yakult.Inventory.Api2_remote\call-ticket-action.ashx` (before the `Err(c,400,"Unknown action")` line)

- [ ] **Step 1: Add the `resolution` action handler before the unknown-action fallback**

Insert this block in `call-ticket-action.ashx` right before line 90 (`Err(c,400,"Unknown action. Supported: status, priority, note");`):

```csharp
                if (action == "resolution")
                {
                    string resolutionType = (body.ContainsKey("resolutionType") ? body["resolutionType"].ToString() : "").Trim();
                    if (string.IsNullOrWhiteSpace(resolutionType)) { Err(c, 400, "resolutionType is required"); return; }

                    string remarks = body.ContainsKey("remarks") ? body["remarks"].ToString() : "";
                    string oldStatus = "";

                    using (var qCmd = new SqlCommand("SELECT TOP 1 Status FROM dbo.CallTicket WHERE TicketId=@Id", con))
                    {
                        qCmd.Parameters.Add("@Id", SqlDbType.Int).Value = ticketId;
                        object sv = qCmd.ExecuteScalar();
                        if (sv != null && sv != DBNull.Value) oldStatus = sv.ToString();
                    }

                    if (resolutionType.Equals("Service Only", StringComparison.OrdinalIgnoreCase))
                    {
                        // Log resolution history
                        using (var cmd = new SqlCommand(@"
INSERT INTO dbo.CallTicketHistory(TicketId, FieldName, NewValue, Note, ChangedByUserId, ChangedAt)
VALUES(@TicketId, 'ResolutionType', @ResolutionType, @Remarks, @UserId, SYSUTCDATETIME())", con))
                        {
                            cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                            cmd.Parameters.Add("@ResolutionType", SqlDbType.NVarChar).Value = resolutionType;
                            cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar).Value = (object)remarks ?? DBNull.Value;
                            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId == null ? (object)DBNull.Value : (object)userId.Value;
                            cmd.ExecuteNonQuery();
                        }

                        // Add resolution note
                        if (!string.IsNullOrWhiteSpace(remarks))
                        {
                            using (var cmd = new SqlCommand("dbo.sp_Call_AddTicketNote", con))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                                cmd.Parameters.Add("@NoteType", SqlDbType.NVarChar).Value = "Resolution";
                                cmd.Parameters.Add("@NoteText", SqlDbType.NVarChar).Value = "Service Only: " + remarks;
                                cmd.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = userId == null ? (object)DBNull.Value : (object)userId.Value;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Set status to Solved
                        using (var cmd = new SqlCommand("dbo.sp_Call_SetTicketStatus", con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                            cmd.Parameters.Add("@NewStatus", SqlDbType.NVarChar).Value = "Solved";
                            cmd.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = userId == null ? (object)DBNull.Value : (object)userId.Value;
                            if (HasParam(con, "dbo.sp_Call_SetTicketStatus", "@Note"))
                                cmd.Parameters.Add("@Note", SqlDbType.NVarChar).Value = "Marked as: Service Only" + (string.IsNullOrWhiteSpace(remarks) ? "" : " | " + remarks);
                            cmd.ExecuteNonQuery();
                        }

                        Ok(c, new { success = true, message = "Ticket resolved as Service Only." });
                        ItcmEmailHelper.TrySendStatusUpdateEmailAsync(Cs(), ticketId, oldStatus, "Solved", remarks, userId);
                        return;
                    }

                    if (resolutionType.Equals("Replacement", StringComparison.OrdinalIgnoreCase))
                    {
                        int newItemId = body.ContainsKey("newItemId") ? Convert.ToInt32(body["newItemId"]) : 0;
                        int quantity = body.ContainsKey("quantity") ? Convert.ToInt32(body["quantity"]) : 1;
                        int oldItemConditionId = body.ContainsKey("oldItemConditionId") ? Convert.ToInt32(body["oldItemConditionId"]) : 0;
                        string oldItemConditionRemarks = body.ContainsKey("oldItemConditionRemarks") ? body["oldItemConditionRemarks"].ToString() : "";
                        string oldItemRepairAction = body.ContainsKey("oldItemRepairAction") ? body["oldItemRepairAction"].ToString() : "Repaired";
                        bool isTemporary = body.ContainsKey("isTemporary") && Convert.ToBoolean(body["isTemporary"]);
                        bool useUnlisted = body.ContainsKey("useUnlistedOldItem") && Convert.ToBoolean(body["useUnlistedOldItem"]);

                        if (newItemId <= 0) { Err(c, 400, "newItemId is required for replacement"); return; }
                        if (oldItemConditionId <= 0) { Err(c, 400, "oldItemConditionId is required for replacement"); return; }
                        if (quantity < 1) { Err(c, 400, "quantity must be at least 1"); return; }

                        int oldItemId;
                        if (useUnlisted)
                        {
                            string unlistedName = body.ContainsKey("unlistedOldItemName") ? body["unlistedOldItemName"].ToString() : "";
                            string unlistedModel = body.ContainsKey("unlistedOldItemModelNumber") ? body["unlistedOldItemModelNumber"].ToString() : "";
                            string unlistedSerial = body.ContainsKey("unlistedOldItemSerialNumber") ? body["unlistedOldItemSerialNumber"].ToString() : "";
                            string unlistedUnit = body.ContainsKey("unlistedOldItemUnitOfMeasure") ? body["unlistedOldItemUnitOfMeasure"].ToString() : "";
                            string unlistedDesc = body.ContainsKey("unlistedOldItemDescription") ? body["unlistedOldItemDescription"].ToString() : "";
                            int unlistedCatId = body.ContainsKey("unlistedOldItemCategoryId") ? Convert.ToInt32(body["unlistedOldItemCategoryId"]) : 0;
                            string unlistedCatName = body.ContainsKey("unlistedOldItemCategoryName") ? body["unlistedOldItemCategoryName"].ToString() : "";

                            if (string.IsNullOrWhiteSpace(unlistedName)) { Err(c, 400, "unlistedOldItemName is required"); return; }
                            if (string.IsNullOrWhiteSpace(unlistedModel)) { Err(c, 400, "unlistedOldItemModelNumber is required"); return; }
                            if (string.IsNullOrWhiteSpace(unlistedUnit)) { Err(c, 400, "unlistedOldItemUnitOfMeasure is required"); return; }
                            if (unlistedCatId <= 0) { Err(c, 400, "unlistedOldItemCategoryId is required"); return; }

                            using (var cmd = new SqlCommand(@"
INSERT INTO dbo.Item (Name, Description, ModelNumber, SerialNumber, UnitOfMeasure, CreatedByUserId, CreatedByName, Active, DateCreated, ItemType, CategoryId, Category, StockOnHand, ConditionID, Remarks, AffectsInventory, IsTrackedAsset)
VALUES (@Name, @Desc, @Model, @Serial, @Unit, @UserId, '', 1, SYSUTCDATETIME(), 'Hardware', @CatId, @CatName, 0, @ConditionId, @CondRemarks, 1, 0);
SELECT CAST(SCOPE_IDENTITY() AS INT);", con))
                            {
                                cmd.Parameters.Add("@Name", SqlDbType.NVarChar).Value = unlistedName;
                                cmd.Parameters.Add("@Desc", SqlDbType.NVarChar).Value = (object)unlistedDesc ?? DBNull.Value;
                                cmd.Parameters.Add("@Model", SqlDbType.NVarChar).Value = unlistedModel;
                                cmd.Parameters.Add("@Serial", SqlDbType.NVarChar).Value = (object)unlistedSerial ?? DBNull.Value;
                                cmd.Parameters.Add("@Unit", SqlDbType.NVarChar).Value = unlistedUnit;
                                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId == null ? (object)DBNull.Value : (object)userId.Value;
                                cmd.Parameters.Add("@CatId", SqlDbType.Int).Value = unlistedCatId;
                                cmd.Parameters.Add("@CatName", SqlDbType.NVarChar).Value = (object)unlistedCatName ?? DBNull.Value;
                                cmd.Parameters.Add("@ConditionId", SqlDbType.Int).Value = oldItemConditionId;
                                cmd.Parameters.Add("@CondRemarks", SqlDbType.NVarChar).Value = (object)oldItemConditionRemarks ?? DBNull.Value;
                                oldItemId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                        }
                        else
                        {
                            oldItemId = body.ContainsKey("oldItemId") ? Convert.ToInt32(body["oldItemId"]) : 0;
                            if (oldItemId <= 0) { Err(c, 400, "oldItemId is required when useUnlistedOldItem is false"); return; }
                            if (oldItemId == newItemId) { Err(c, 400, "Old and new items must be different"); return; }
                        }

                        var safeRepairAction = string.IsNullOrWhiteSpace(oldItemRepairAction) ? "Repaired" : oldItemRepairAction.Trim();
                        bool returnOldToStock = !safeRepairAction.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase);

                        using (var tx = con.BeginTransaction())
                        {
                            try
                            {
                                // Verify both items exist and affect inventory
                                var oldAffects = Convert.ToBoolean(new SqlCommand("SELECT AffectsInventory FROM dbo.Item WHERE ItemId=@Id", con) { Transaction = tx, Parameters = { new SqlParameter("@Id", SqlDbType.Int) { Value = oldItemId } } }.ExecuteScalar());
                                var newAffects = Convert.ToBoolean(new SqlCommand("SELECT AffectsInventory FROM dbo.Item WHERE ItemId=@Id", con) { Transaction = tx, Parameters = { new SqlParameter("@Id", SqlDbType.Int) { Value = newItemId } } }.ExecuteScalar());
                                if (!oldAffects || !newAffects) { tx.Rollback(); Err(c, 400, "Replacement items must affect inventory stock."); return; }

                                var nowUtc = DateTime.UtcNow;

                                // Old unit pullout (return to stock)
                                if (returnOldToStock)
                                {
                                    new SqlCommand(@"
INSERT INTO dbo.Inventory (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId)
VALUES (@Desc, 'Return', @Qty, @Now, @UserId, NULL, @OldId)", con)
                                    { Transaction = tx, Parameters = { new SqlParameter("@Desc", SqlDbType.NVarChar) { Value = "Call ticket replacement pullout (Ticket " + ticketId + ")" }, new SqlParameter("@Qty", SqlDbType.Int) { Value = quantity }, new SqlParameter("@Now", SqlDbType.DateTime2) { Value = nowUtc }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? 0 }, new SqlParameter("@OldId", SqlDbType.Int) { Value = oldItemId } } }.ExecuteNonQuery();

                                    new SqlCommand("UPDATE dbo.Item SET StockOnHand = ISNULL(StockOnHand,0) + @Qty, DateModified = @Now, ModifiedBy = @UserId WHERE ItemId = @OldId", con)
                                    { Transaction = tx, Parameters = { new SqlParameter("@Qty", SqlDbType.Int) { Value = quantity }, new SqlParameter("@Now", SqlDbType.DateTime2) { Value = nowUtc }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? 0 }, new SqlParameter("@OldId", SqlDbType.Int) { Value = oldItemId } } }.ExecuteNonQuery();
                                }
                                else
                                {
                                    // Force stock to 0 for unrepaired items
                                    new SqlCommand("UPDATE dbo.Item SET StockOnHand = 0, DateModified = @Now, ModifiedBy = @UserId WHERE ItemId = @OldId", con)
                                    { Transaction = tx, Parameters = { new SqlParameter("@Now", SqlDbType.DateTime2) { Value = nowUtc }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? 0 }, new SqlParameter("@OldId", SqlDbType.Int) { Value = oldItemId } } }.ExecuteNonQuery();
                                }

                                // New item allocation (concurrency-safe)
                                new SqlCommand(@"
INSERT INTO dbo.Inventory (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId)
VALUES (@Desc, 'Allocation', @Qty, @Now, @UserId, NULL, @NewId)", con)
                                { Transaction = tx, Parameters = { new SqlParameter("@Desc", SqlDbType.NVarChar) { Value = "Call ticket replacement allocation (Ticket " + ticketId + ")" }, new SqlParameter("@Qty", SqlDbType.Int) { Value = -quantity }, new SqlParameter("@Now", SqlDbType.DateTime2) { Value = nowUtc }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? 0 }, new SqlParameter("@NewId", SqlDbType.Int) { Value = newItemId } } }.ExecuteNonQuery();

                                int affected = new SqlCommand("UPDATE dbo.Item SET StockOnHand = ISNULL(StockOnHand,0) - @Qty, DateModified = @Now, ModifiedBy = @UserId WHERE ItemId = @NewId AND ISNULL(StockOnHand,0) >= @Qty", con)
                                { Transaction = tx, Parameters = { new SqlParameter("@Qty", SqlDbType.Int) { Value = quantity }, new SqlParameter("@Now", SqlDbType.DateTime2) { Value = nowUtc }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? 0 }, new SqlParameter("@NewId", SqlDbType.Int) { Value = newItemId } } }.ExecuteNonQuery();

                                if (affected <= 0)
                                {
                                    tx.Rollback();
                                    Err(c, 400, "Not enough stock on hand for the selected replacement item.");
                                    return;
                                }

                                // Update old item condition
                                new SqlCommand(@"
UPDATE dbo.Item SET ConditionID = @CondId, Remarks = CASE WHEN @CondRemarks IS NULL OR LTRIM(RTRIM(@CondRemarks)) = '' THEN Remarks ELSE @CondRemarks END, DateModified = @Now, ModifiedBy = @UserId WHERE ItemId = @OldId", con)
                                { Transaction = tx, Parameters = { new SqlParameter("@CondId", SqlDbType.Int) { Value = oldItemConditionId }, new SqlParameter("@CondRemarks", SqlDbType.NVarChar) { Value = (object)oldItemConditionRemarks ?? DBNull.Value }, new SqlParameter("@Now", SqlDbType.DateTime2) { Value = nowUtc }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId ?? 0 }, new SqlParameter("@OldId", SqlDbType.Int) { Value = oldItemId } } }.ExecuteNonQuery();

                                // Insert resolution history
                                new SqlCommand("INSERT INTO dbo.CallTicketHistory (TicketId, FieldName, NewValue, Note, ChangedByUserId, ChangedAt) VALUES (@TicketId, 'ResolutionType', @ResType, @Remarks, @UserId, SYSUTCDATETIME())", con)
                                { Transaction = tx, Parameters = { new SqlParameter("@TicketId", SqlDbType.Int) { Value = ticketId }, new SqlParameter("@ResType", SqlDbType.NVarChar) { Value = "Replacement" }, new SqlParameter("@Remarks", SqlDbType.NVarChar) { Value = (object)remarks ?? DBNull.Value }, new SqlParameter("@UserId", SqlDbType.Int) { Value = userId == null ? (object)DBNull.Value : (object)userId.Value } } }.ExecuteNonQuery();

                                // Add resolution note
                                if (!string.IsNullOrWhiteSpace(remarks))
                                {
                                    using (var noteCmd = new SqlCommand("dbo.sp_Call_AddTicketNote", con))
                                    {
                                        noteCmd.CommandType = CommandType.StoredProcedure;
                                        noteCmd.Transaction = tx;
                                        noteCmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                                        noteCmd.Parameters.Add("@NoteType", SqlDbType.NVarChar).Value = "Resolution";
                                        noteCmd.Parameters.Add("@NoteText", SqlDbType.NVarChar).Value = "Replacement: " + remarks;
                                        noteCmd.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = userId == null ? (object)DBNull.Value : (object)userId.Value;
                                        noteCmd.ExecuteNonQuery();
                                    }
                                }

                                tx.Commit();
                            }
                            catch
                            {
                                try { tx.Rollback(); } catch { }
                                throw;
                            }
                        }

                        var markAsStatus = isTemporary ? "Resolved (Temporary)" : "Solved";
                        using (var cmd = new SqlCommand("dbo.sp_Call_SetTicketStatus", con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                            cmd.Parameters.Add("@NewStatus", SqlDbType.NVarChar).Value = markAsStatus;
                            cmd.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = userId == null ? (object)DBNull.Value : (object)userId.Value;
                            if (HasParam(con, "dbo.sp_Call_SetTicketStatus", "@Note"))
                                cmd.Parameters.Add("@Note", SqlDbType.NVarChar).Value = "Marked as: Replacement" + (string.IsNullOrWhiteSpace(remarks) ? "" : " | " + remarks);
                            cmd.ExecuteNonQuery();
                        }

                        Ok(c, new { success = true, message = "Ticket resolved as Replacement." });
                        ItcmEmailHelper.TrySendStatusUpdateEmailAsync(Cs(), ticketId, oldStatus, markAsStatus, remarks, userId);
                        return;
                    }

                    Err(c, 400, "Unknown resolutionType. Supported: Service Only, Replacement");
                    return;
                }
```

---

### Task 4: Mobile API — Add new endpoints and DTOs to `ApiClient.kt`

**Files:**
- Modify: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/api/ApiClient.kt`

- [ ] **Step 1: Add new endpoints to the `YakultApiService` interface**

Add these AFTER the existing `call-escalation-settings.ashx` endpoint (after line 213):

```kotlin
    @POST("call-ticket-action.ashx")
    suspend fun callTicketResolution(@Body request: ResolutionRequest): Response<CallTicketActionResponse>

    @GET("call-items-lookup.ashx")
    suspend fun getCallItemsLookup(
        @Query("type") type: String
    ): Response<CallItemLookupResponse>

    @GET("call-conditions.ashx")
    suspend fun getCallConditions(): Response<CallConditionResponse>
```

- [ ] **Step 2: Add new DTOs at the end of the file**

Add BEFORE the last closing line:

```kotlin
data class ResolutionRequest(
    @SerializedName("action") val action: String = "resolution",
    @SerializedName("ticketId") val ticketId: Int,
    @SerializedName("resolutionType") val resolutionType: String,
    @SerializedName("remarks") val remarks: String? = null,
    @SerializedName("userId") val userId: Int? = null,
    @SerializedName("isTemporary") val isTemporary: Boolean? = null,
    @SerializedName("useUnlistedOldItem") val useUnlistedOldItem: Boolean? = null,
    @SerializedName("oldItemId") val oldItemId: Int? = null,
    @SerializedName("newItemId") val newItemId: Int? = null,
    @SerializedName("quantity") val quantity: Int? = null,
    @SerializedName("oldItemConditionId") val oldItemConditionId: Int? = null,
    @SerializedName("oldItemConditionRemarks") val oldItemConditionRemarks: String? = null,
    @SerializedName("oldItemRepairAction") val oldItemRepairAction: String? = null,
    @SerializedName("unlistedOldItemName") val unlistedOldItemName: String? = null,
    @SerializedName("unlistedOldItemDescription") val unlistedOldItemDescription: String? = null,
    @SerializedName("unlistedOldItemCategoryId") val unlistedOldItemCategoryId: Int? = null,
    @SerializedName("unlistedOldItemCategoryName") val unlistedOldItemCategoryName: String? = null,
    @SerializedName("unlistedOldItemSerialNumber") val unlistedOldItemSerialNumber: String? = null,
    @SerializedName("unlistedOldItemModelNumber") val unlistedOldItemModelNumber: String? = null,
    @SerializedName("unlistedOldItemUnitOfMeasure") val unlistedOldItemUnitOfMeasure: String? = null
)

data class CallItemLookupDto(
    @SerializedName("itemId") val itemId: Int,
    @SerializedName("displayText") val displayText: String,
    @SerializedName("category") val category: String?,
    @SerializedName("stockOnHand") val stockOnHand: Int?
)

data class CallItemLookupResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("items") val items: List<CallItemLookupDto> = emptyList()
)

data class CallConditionDto(
    @SerializedName("conditionId") val conditionId: Int,
    @SerializedName("conditionName") val conditionName: String
)

data class CallConditionResponse(
    @SerializedName("success") val success: Boolean,
    @SerializedName("conditions") val conditions: List<CallConditionDto> = emptyList()
)
```

---

### Task 5: Mobile Repository — Add new methods

**Files:**
- Modify: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/data/repository/CallMonitoringRepository.kt`

- [ ] **Step 1: Add repository methods at the end of the file (before the closing `}`)**

```kotlin
    suspend fun applyResolution(request: ResolutionRequest): Response<CallTicketActionResponse> {
        return withContext(Dispatchers.IO) {
            apiService.callTicketResolution(request)
        }
    }

    suspend fun getCallItemsLookup(type: String): Response<CallItemLookupResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallItemsLookup(type)
        }
    }

    suspend fun getCallConditions(): Response<CallConditionResponse> {
        return withContext(Dispatchers.IO) {
            apiService.getCallConditions()
        }
    }

    suspend fun getItemCategories(): Response<List<ItemCategoryDto>> {
        return withContext(Dispatchers.IO) {
            apiService.getItemCategories()
        }
    }
```

- [ ] **Step 2: Add imports at the top of the file**

```kotlin
import com.example.yakultscanner.api.CallItemLookupResponse
import com.example.yakultscanner.api.CallConditionResponse
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.ResolutionRequest
```

---

### Task 6: Mobile ViewModel — Add resolution logic

**Files:**
- Modify: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/viewmodels/CallMonitoringViewModel.kt`

- [ ] **Step 1: Add new sealed class for resolution state**

Add AFTER `ActionUiState` (after line 39):

```kotlin
sealed class ResolutionUiState {
    object Idle : ResolutionUiState()
    object Loading : ResolutionUiState()
    data class Success(val message: String) : ResolutionUiState()
    data class Error(val message: String) : ResolutionUiState()
}
```

- [ ] **Step 2: Add state flows for resolution and lookup data**

Add inside `CallMonitoringViewModel` class after the existing state flows:

```kotlin
    private val _resolutionState = MutableStateFlow<ResolutionUiState>(ResolutionUiState.Idle)
    val resolutionState: StateFlow<ResolutionUiState> = _resolutionState.asStateFlow()

    private val _resolutionOutItems = MutableStateFlow<List<CallItemLookupDto>>(emptyList())
    val resolutionOutItems: StateFlow<List<CallItemLookupDto>> = _resolutionOutItems.asStateFlow()

    private val _resolutionStockItems = MutableStateFlow<List<CallItemLookupDto>>(emptyList())
    val resolutionStockItems: StateFlow<List<CallItemLookupDto>> = _resolutionStockItems.asStateFlow()

    private val _resolutionConditions = MutableStateFlow<List<CallConditionDto>>(emptyList())
    val resolutionConditions: StateFlow<List<CallConditionDto>> = _resolutionConditions.asStateFlow()

    private val _resolutionCategories = MutableStateFlow<List<ItemCategoryDto>>(emptyList())
    val resolutionCategories: StateFlow<List<ItemCategoryDto>> = _resolutionCategories.asStateFlow()

    private val _resolutionLoadingLookups = MutableStateFlow(false)
    val resolutionLoadingLookups: StateFlow<Boolean> = _resolutionLoadingLookups.asStateFlow()
```

- [ ] **Step 3: Add imports at the top**

```kotlin
import com.example.yakultscanner.api.CallItemLookupDto
import com.example.yakultscanner.api.CallItemLookupResponse
import com.example.yakultscanner.api.CallConditionDto
import com.example.yakultscanner.api.CallConditionResponse
import com.example.yakultscanner.api.ItemCategoryDto
import com.example.yakultscanner.api.ResolutionRequest
```

- [ ] **Step 4: Add `loadResolutionLookups()` method**

```kotlin
    fun loadResolutionLookups() {
        viewModelScope.launch {
            _resolutionLoadingLookups.value = true
            try {
                val outResp = repository.getCallItemsLookup("out")
                if (outResp.isSuccessful) _resolutionOutItems.value = outResp.body()?.items ?: emptyList()

                val stockResp = repository.getCallItemsLookup("stock")
                if (stockResp.isSuccessful) _resolutionStockItems.value = stockResp.body()?.items ?: emptyList()

                val condResp = repository.getCallConditions()
                if (condResp.isSuccessful) _resolutionConditions.value = condResp.body()?.conditions ?: emptyList()

                val catResp = repository.getItemCategories()
                if (catResp.isSuccessful) _resolutionCategories.value = catResp.body() ?: emptyList()
            } catch (_: Exception) {}
            _resolutionLoadingLookups.value = false
        }
    }
```

- [ ] **Step 5: Add `applyResolution()` method**

```kotlin
    fun applyResolution(request: ResolutionRequest) {
        viewModelScope.launch {
            _resolutionState.value = ResolutionUiState.Loading
            try {
                val response = repository.applyResolution(request)
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body != null && body.success) {
                        _resolutionState.value = ResolutionUiState.Success(body.message ?: "Ticket resolved")
                    } else {
                        _resolutionState.value = ResolutionUiState.Error(body?.message ?: "Resolution failed")
                    }
                } else {
                    _resolutionState.value = ResolutionUiState.Error("HTTP ${response.code()}: ${response.errorBody()?.string() ?: "Unknown error"}")
                }
            } catch (e: Exception) {
                _resolutionState.value = ResolutionUiState.Error("Network error: ${e.message}")
            }
        }
    }
```

- [ ] **Step 6: Add `resetResolutionState()` method**

```kotlin
    fun resetResolutionState() {
        _resolutionState.value = ResolutionUiState.Idle
    }
```

---

### Task 7: Mobile UI — Create `CallMarkAsResolvedScreen.kt`

**Files:**
- Create: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/ui/screens/CallMarkAsResolvedScreen.kt`

This is a large screen. It implements the full wizard with replacement details and preview.

- [ ] **Step 1: Create the screen file**

```kotlin
package com.example.yakultscanner.ui.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.lifecycle.viewmodel.compose.hiltViewModel
import androidx.navigation.NavController
import com.example.yakultscanner.UserSession
import com.example.yakultscanner.api.CallConditionDto
import com.example.yakultscanner.api.CallItemLookupDto
import com.example.yakultscanner.api.ResolutionRequest
import com.example.yakultscanner.viewmodels.CallMonitoringViewModel
import com.example.yakultscanner.viewmodels.ResolutionUiState
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CallMarkAsResolvedScreen(
    navController: NavController,
    ticketId: Int,
    viewModel: CallMonitoringViewModel = hiltViewModel()
) {
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }

    val resolutionState by viewModel.resolutionState.collectAsState()
    val outItems by viewModel.resolutionOutItems.collectAsState()
    val stockItems by viewModel.resolutionStockItems.collectAsState()
    val conditions by viewModel.resolutionConditions.collectAsState()
    val isLoadingLookups by viewModel.resolutionLoadingLookups.collectAsState()

    // Wizard state
    var isReplacement by remember { mutableStateOf(false) }
    var wizardStep by remember { mutableStateOf(0) } // 0=type, 1=replacement details, 2=preview

    // Old item state
    var useUnlisted by remember { mutableStateOf(false) }
    var oldCategoryFilter by remember { mutableStateOf("All Categories") }
    var selectedOldItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    val resolutionCategories by viewModel.resolutionCategories.collectAsStateWithLifecycle()
    var unlistedName by remember { mutableStateOf("") }
    var unlistedModel by remember { mutableStateOf("") }
    var unlistedSerial by remember { mutableStateOf("") }
    var unlistedUnit by remember { mutableStateOf("Unit") }
    var unlistedDesc by remember { mutableStateOf("") }
    var unlistedCategory by remember { mutableStateOf<ItemCategoryDto?>(null) }

    // Condition
    var selectedCondition by remember { mutableStateOf<CallConditionDto?>(null) }
    var conditionRemarks by remember { mutableStateOf("") }

    // Old unit action
    var repairAction by remember { mutableStateOf("Repaired") }

    // New item
    var newCategoryFilter by remember { mutableStateOf("All Categories") }
    var selectedNewItem by remember { mutableStateOf<CallItemLookupDto?>(null) }
    var quantity by remember { mutableStateOf("1") }
    var isTemporary by remember { mutableStateOf(false) }

    // Remarks
    var remarks by remember { mutableStateOf("") }

    // Derived data
    val outCategories = remember(outItems) {
        listOf("All Categories") + outItems.map { it.category ?: "" }.filter { it.isNotBlank() }.distinct().sorted()
    }
    val stockCategories = remember(stockItems) {
        listOf("All Categories") + stockItems.map { it.category ?: "" }.filter { it.isNotBlank() }.distinct().sorted()
    }
    val filteredOldItems = remember(outItems, oldCategoryFilter) {
        if (oldCategoryFilter == "All Categories") outItems
        else outItems.filter { it.category == oldCategoryFilter }
    }
    val filteredNewItems = remember(stockItems, newCategoryFilter) {
        if (newCategoryFilter == "All Categories") stockItems
        else stockItems.filter { it.category == newCategoryFilter }
    }
    val selectedNewItemStock = selectedNewItem?.stockOnHand ?: 0
    val qty = quantity.toIntOrNull() ?: 1

    val canConfirm = when {
        !isReplacement -> remarks.isNotBlank()
        useUnlisted -> unlistedName.isNotBlank() && unlistedModel.isNotBlank() && unlistedCategory != null && selectedCondition != null && selectedNewItem != null && qty > 0 && qty <= selectedNewItemStock && remarks.isNotBlank()
        else -> selectedOldItem != null && selectedCondition != null && selectedNewItem != null && qty > 0 && qty <= selectedNewItemStock && remarks.isNotBlank()
    }

    val isProcessing = resolutionState is ResolutionUiState.Loading

    // Load lookups on entry
    LaunchedEffect(ticketId) {
        viewModel.loadResolutionLookups()
    }

    // Observe resolution result
    LaunchedEffect(resolutionState) {
        when (val state = resolutionState) {
            is ResolutionUiState.Success -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetResolutionState()
                viewModel.loadTicketDetail(ticketId)
                navController.popBackStack()
            }
            is ResolutionUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetResolutionState()
            }
            else -> {}
        }
    }

    // Build categories out of outItems/stockItems

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Mark As Resolved") },
                navigationIcon = {
                    IconButton(onClick = { if (wizardStep > 0) wizardStep-- else navController.popBackStack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                    titleContentColor = MaterialTheme.colorScheme.onPrimaryContainer
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        bottomBar = {
            if (wizardStep < 2) {
                Button(
                    onClick = { wizardStep = if (wizardStep == 0) (if (isReplacement) 1 else 2) else 2 },
                    modifier = Modifier.fillMaxWidth().padding(16.dp).height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    enabled = when (wizardStep) {
                        0 -> true
                        1 -> isReplacement
                        else -> false
                    }
                ) {
                    Text(if (wizardStep == 0 && !isReplacement) "Continue" else "Next")
                }
            } else {
                Button(
                    onClick = {
                        val request = buildResolutionRequest(ticketId, isReplacement, useUnlisted, selectedOldItem,
                            unlistedName, unlistedModel, unlistedSerial, unlistedUnit, unlistedDesc, unlistedCategory,
                            selectedCondition, conditionRemarks, repairAction, selectedNewItem, qty, isTemporary, remarks)
                        viewModel.applyResolution(request)
                    },
                    modifier = Modifier.fillMaxWidth().padding(16.dp).height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    enabled = canConfirm && !isProcessing
                ) {
                    if (isProcessing) CircularProgressIndicator(modifier = Modifier.size(20.dp), color = MaterialTheme.colorScheme.onPrimary)
                    else Text("Confirm Resolution")
                }
            }
        }
    ) { padding ->
        if (isLoadingLookups) {
            Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
                    .padding(horizontal = 16.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Spacer(Modifier.height(4.dp))

                // Step indicator
                Text(
                    text = when {
                        wizardStep == 0 -> "Step 1: Resolution Type"
                        wizardStep == 1 -> "Step 2: Replacement Details"
                        else -> if (isReplacement) "Step 3: Review & Confirm" else "Step 2: Review & Confirm"
                    },
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )

                // Step 0: Resolution type
                if (wizardStep == 0) {
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("Resolution Type", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { isReplacement = false }) {
                                RadioButton(selected = !isReplacement, onClick = { isReplacement = false })
                                Text("Service Only (No Parts)", modifier = Modifier.padding(start = 4.dp))
                            }
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { isReplacement = true }) {
                                RadioButton(selected = isReplacement, onClick = { isReplacement = true })
                                Text("Replacement (Parts Used)", modifier = Modifier.padding(start = 4.dp))
                            }
                            if (!isReplacement) {
                                Spacer(Modifier.height(4.dp))
                                Text("Enter service notes about the resolution.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            } else {
                                Spacer(Modifier.height(4.dp))
                                Text("Record the replaced item and the new item issued from stock.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }

                    // Service notes (always shown in step 0)
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text(if (isReplacement) "Replacement remarks" else "Service notes", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            OutlinedTextField(
                                value = remarks,
                                onValueChange = { remarks = it },
                                placeholder = { Text("Describe the resolution...") },
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(10.dp),
                                maxLines = 4
                            )
                        }
                    }
                }

                // Step 1: Replacement details
                if (wizardStep == 1 && isReplacement) {
                    // Old Item Section
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text("Item Being Replaced (Old Unit)", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { useUnlisted = false }) {
                                RadioButton(selected = !useUnlisted, onClick = { useUnlisted = false })
                                Text("Listed in Inventory", modifier = Modifier.padding(start = 4.dp))
                            }
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { useUnlisted = true }) {
                                RadioButton(selected = useUnlisted, onClick = { useUnlisted = true })
                                Text("Not Listed", modifier = Modifier.padding(start = 4.dp))
                            }

                            if (useUnlisted) {
                                OutlinedTextField(value = unlistedName, onValueChange = { unlistedName = it }, label = { Text("Name*") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), singleLine = true)
                                OutlinedTextField(value = unlistedModel, onValueChange = { unlistedModel = it }, label = { Text("Model*") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), singleLine = true)
                                OutlinedTextField(value = unlistedSerial, onValueChange = { unlistedSerial = it }, label = { Text("Serial") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), singleLine = true)
                                UnitDropdown(value = unlistedUnit, onValueChange = { unlistedUnit = it })
                                OutlinedTextField(value = unlistedDesc, onValueChange = { unlistedDesc = it }, label = { Text("Description") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), maxLines = 2)
                                ItemCategoryDropdown(
                                    categories = resolutionCategories,
                                    selected = unlistedCategory,
                                    onSelected = { unlistedCategory = it }
                                )
                            } else {
                                CategoryDropdown(label = "Filter by Category", categories = outCategories, selected = oldCategoryFilter, onSelected = { oldCategoryFilter = it })
                                ItemDropdown(label = "Select Old Item", items = filteredOldItems, selected = selectedOldItem, onSelected = { selectedOldItem = it })
                            }
                        }
                    }

                    // Condition
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text("Old Item Condition", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            ConditionDropdown(conditions = conditions, selected = selectedCondition, onSelected = { selectedCondition = it })
                            OutlinedTextField(value = conditionRemarks, onValueChange = { conditionRemarks = it }, label = { Text("Remarks") }, modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(10.dp), maxLines = 2)
                        }
                    }

                    // Old Unit Action
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                            Text("Old Unit Action (After Pullout)", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            listOf("Repaired", "Unrepaired", "Repaired - Spare inventory").forEach { action ->
                                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.clickable { repairAction = action }) {
                                    RadioButton(selected = repairAction == action, onClick = { repairAction = action })
                                    Text(action, modifier = Modifier.padding(start = 4.dp))
                                }
                            }
                        }
                    }

                    // New Item
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            Text("Replacement Item (New Unit)", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            CategoryDropdown(label = "Filter by Category", categories = stockCategories, selected = newCategoryFilter, onSelected = { newCategoryFilter = it })
                            ItemDropdown(label = "Select Replacement Item", items = filteredNewItems, selected = selectedNewItem, onSelected = { selectedNewItem = it })

                            if (selectedNewItem != null) {
                                Text("Available stock: ${selectedNewItem.stockOnHand}", style = MaterialTheme.typography.bodySmall, color = if ((selectedNewItem.stockOnHand ?: 0) > 0) Color(0xFF2E7D32) else Color(0xFFC62828))
                            }

                            OutlinedTextField(
                                value = quantity,
                                onValueChange = { q -> if (q.all { it.isDigit() }) quantity = q },
                                label = { Text("Quantity") },
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(10.dp),
                                singleLine = true,
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                            )

                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Checkbox(checked = isTemporary, onCheckedChange = { isTemporary = it })
                                Text("Temporary replacement (marks as Resolved (Temporary))", style = MaterialTheme.typography.bodySmall)
                            }
                        }
                    }

                    // Remarks (shown here too for replacement)
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp)) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("Replacement remarks", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.Bold)
                            OutlinedTextField(
                                value = remarks,
                                onValueChange = { remarks = it },
                                placeholder = { Text("Describe the resolution...") },
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(10.dp),
                                maxLines = 4
                            )
                        }
                    }
                }

                // Step 2: Preview & Confirm
                if (wizardStep == 2) {
                    Card(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f))) {
                        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                                Text("Preview", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                                StatusBadge(
                                    text = if (isReplacement && isTemporary) "Resolved (Temporary)" else "Solved",
                                    color = if (isReplacement && isTemporary) Color(0xFF00838F) else Color(0xFF2E7D32)
                                )
                            }
                            HorizontalDivider()

                            if (isReplacement) {
                                val oldText = if (useUnlisted) "$unlistedName ($unlistedModel)" else (selectedOldItem?.displayText ?: "(Unknown)")
                                val newText = selectedNewItem?.displayText ?: "(Unknown)"
                                val condText = selectedCondition?.conditionName ?: "(Unknown)"
                                val actionText = repairAction.ifBlank { "Repaired" }
                                val stockAfter = selectedNewItemStock - qty
                                val oldImpact = if (repairAction == "Unrepaired") "0 (kept out of stock)" else "+$qty"

                                Text("Resolution: ${if (isTemporary) "Temporary Replacement" else "Permanent Replacement"}", style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.SemiBold)

                                GroupLabel("Old item:")
                                DetailLine(oldText)
                                DetailLine("Condition: $condText")
                                DetailLine("Action: $actionText")
                                DetailLine("Stock impact: $oldImpact")
                                Spacer(Modifier.height(4.dp))

                                GroupLabel("New item:")
                                DetailLine(newText)
                                DetailLine("Current stock: $selectedNewItemStock")
                                DetailLine("Stock impact: -$qty")
                                DetailLine("Stock after save: $stockAfter")
                                Spacer(Modifier.height(4.dp))

                                GroupLabel("Follow-up:")
                                DetailLine(if (isTemporary) "This ticket remains Resolved (Temporary) until the replacement item is returned." else "No temporary return is required.")
                            } else {
                                Text("Resolution: Service Only", style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.SemiBold)
                                DetailLine("Inventory impact: None")
                            }

                            if (remarks.isNotBlank()) {
                                Spacer(Modifier.height(4.dp))
                                GroupLabel("Remarks:")
                                DetailLine(remarks)
                            }
                        }
                    }

                    Spacer(Modifier.height(80.dp)) // space for bottom button
                }
            }
        }
    }
}

@Composable
private fun CategoryDropdown(label: String, categories: List<String>, selected: String, onSelected: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = selected,
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            categories.forEach { cat ->
                DropdownMenuItem(text = { Text(cat) }, onClick = { onSelected(cat); expanded = false })
            }
        }
    }
}

@Composable
private fun ItemDropdown(label: String, items: List<CallItemLookupDto>, selected: CallItemLookupDto?, onSelected: (CallItemLookupDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val displayText = selected?.displayText ?: ""
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = displayText,
            onValueChange = {},
            readOnly = true,
            label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            items.forEach { item ->
                DropdownMenuItem(
                    text = {
                        Column {
                            Text(item.displayText, style = MaterialTheme.typography.bodyMedium)
                            if (item.stockOnHand != null) {
                                Text("Stock: ${item.stockOnHand}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    },
                    onClick = { onSelected(item); expanded = false }
                )
            }
        }
    }
}

@Composable
private fun ConditionDropdown(conditions: List<CallConditionDto>, selected: CallConditionDto?, onSelected: (CallConditionDto) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val displayText = selected?.conditionName ?: ""
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = displayText,
            onValueChange = {},
            readOnly = true,
            label = { Text("Condition") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            conditions.forEach { cond ->
                DropdownMenuItem(text = { Text(cond.conditionName) }, onClick = { onSelected(cond); expanded = false })
            }
        }
    }
}

@Composable
private fun UnitDropdown(value: String, onValueChange: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    val units = listOf("Unit", "Piece", "Set", "Box")
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = value,
            onValueChange = {},
            readOnly = true,
            label = { Text("Unit") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            units.forEach { unit ->
                DropdownMenuItem(text = { Text(unit) }, onClick = { onValueChange(unit); expanded = false })
            }
        }
    }
}

@Composable
private fun GroupLabel(text: String) {
    Text(text, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.65f))
}

@Composable
private fun DetailLine(text: String) {
    Text(text, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurface)
}

private fun buildResolutionRequest(
    ticketId: Int,
    isReplacement: Boolean,
    useUnlisted: Boolean,
    selectedOldItem: CallItemLookupDto?,
    unlistedName: String,
    unlistedModel: String,
    unlistedSerial: String,
    unlistedUnit: String,
    unlistedDesc: String,
    unlistedCategory: ItemCategoryDto?,
    selectedCondition: CallConditionDto?,
    conditionRemarks: String,
    repairAction: String,
    selectedNewItem: CallItemLookupDto?,
    quantity: Int,
    isTemporary: Boolean,
    remarks: String
): ResolutionRequest {
    if (!isReplacement) {
        return ResolutionRequest(
            ticketId = ticketId,
            resolutionType = "Service Only",
            remarks = remarks,
            userId = UserSession.currentUser?.userId
        )
    }

    return ResolutionRequest(
        ticketId = ticketId,
        resolutionType = "Replacement",
        remarks = remarks,
        userId = UserSession.currentUser?.userId,
        isTemporary = isTemporary,
        useUnlistedOldItem = useUnlisted,
        oldItemId = if (useUnlisted) null else selectedOldItem?.itemId,
        newItemId = selectedNewItem?.itemId,
        quantity = quantity,
        oldItemConditionId = selectedCondition?.conditionId,
        oldItemConditionRemarks = conditionRemarks.ifBlank { null },
        oldItemRepairAction = repairAction,
        unlistedOldItemName = unlistedName.ifBlank { null },
        unlistedOldItemDescription = unlistedDesc.ifBlank { null },
        unlistedOldItemCategoryId = unlistedCategory?.categoryId,
        unlistedOldItemCategoryName = unlistedCategory?.name,
        unlistedOldItemSerialNumber = unlistedSerial.ifBlank { null },
        unlistedOldItemModelNumber = unlistedModel.ifBlank { null },
        unlistedOldItemUnitOfMeasure = unlistedUnit.ifBlank { null }
    )
}
```

---

### Task 8: Mobile Navigation — Add route for resolution screen

**Files:**
- Modify: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/navigation/AppNavigator.kt`

- [ ] **Step 1: Add the import**

```kotlin
import com.example.yakultscanner.ui.screens.CallMarkAsResolvedScreen
```

- [ ] **Step 2: Add the composable route AFTER the existing `call_ticket_create` route (after line 217)**

```kotlin
            composable(
                route = "call_ticket_resolve/{ticketId}",
                arguments = listOf(navArgument("ticketId") { type = NavType.IntType })
            ) { backStackEntry ->
                val tid = backStackEntry.arguments?.getInt("ticketId") ?: 0
                CallMarkAsResolvedScreen(navController = navController, ticketId = tid)
            }
```

---

### Task 9: Mobile — Update `TicketDetailScreen.kt` to navigate to resolution screen

**Files:**
- Modify: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/ui/screens/TicketDetailScreen.kt`

- [ ] **Step 1: Replace the inline Mark As Resolved dialog (lines 320-388) with navigation to the new screen**

Replace the existing "Mark As Resolved" button section:

```kotlin
                    if (!isFinal) {
                        OutlinedButton(
                            onClick = {
                                navController.navigate("call_ticket_resolve/$ticketId")
                            },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp),
                            enabled = !isActionLoading
                        ) {
                            Text("Mark As Resolved")
                        }
                    }
```

And remove the `showMarkAsDialog` variable and its associated dialog block (lines 93, 335-388).

---

### Appendix: Composable helpers (add after `CallMarkAsResolvedScreen.kt`)

```kotlin
@Composable
private fun ItemCategoryDropdown(
    categories: List<ItemCategoryDto>,
    selected: ItemCategoryDto?,
    onSelected: (ItemCategoryDto) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val displayText = selected?.categoryName ?: ""
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = displayText,
            onValueChange = {},
            readOnly = true,
            label = { Text("Category") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
            shape = RoundedCornerShape(10.dp),
            singleLine = true
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            categories.forEach { cat ->
                DropdownMenuItem(text = { Text(cat.categoryName) }, onClick = { onSelected(cat); expanded = false })
            }
        }
    }
}
```
