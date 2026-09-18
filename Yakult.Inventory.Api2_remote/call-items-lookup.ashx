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
    void Ok(HttpContext c, object d) { c.Response.StatusCode = 200; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode = s; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(new { success = false, message = m })); }

    public void ProcessRequest(HttpContext c)
    {
        c.Response.AddHeader("Access-Control-Allow-Origin", "*");
        c.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods", "GET,OPTIONS");
        if (c.Request.HttpMethod == "OPTIONS") { c.Response.StatusCode = 200; c.Response.End(); return; }
        if(c.Request.HttpMethod!="GET"){Err(c,405,"Method not allowed");return;}

        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        var type = (c.Request.QueryString["type"] ?? "").ToLowerInvariant();
        if (type != "out" && type != "stock") { Err(c, 400, "type must be 'out' or 'stock'"); return; }

        try
        {
            using (var con = new SqlConnection(Cs()))
            {
                con.Open();
                string sql = type == "out"
                    ? @"SELECT i.ItemId, i.Name, i.ModelNumber,
                              ISNULL(NULLIF(LTRIM(RTRIM(i.Category)), ''), cat.Name) AS CategoryName,
                              CAST(ISNULL(i.StockOnHand,0) AS int) AS StockOnHand
                       FROM dbo.Item i
                       LEFT JOIN dbo.ItemCategory cat ON cat.CategoryId = i.CategoryId
                       LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
                       WHERE i.Active = 1
                         AND i.ItemType = 'Hardware'
                         AND (
                               ISNULL(i.StockOnHand,0) = 0
                            OR i.DurationStartDate IS NOT NULL
                            OR EXISTS (
                                 SELECT 1
                                 FROM dbo.SetItem si
                                 INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
                                 LEFT JOIN dbo.ArchiveStatus asSet ON asSet.EntityType = 'Set' AND asSet.EntityId = s.SetId AND asSet.IsArchived = 1
                                 WHERE si.ItemId = i.ItemId
                                   AND ISNULL(s.Active,1) = 1
                                   AND asSet.EntityId IS NULL
                            )
                            OR EXISTS (
                                 SELECT 1
                                 FROM dbo.Request req
                                 INNER JOIN dbo.[Set] s2 ON s2.SetId = req.SetId
                                 LEFT JOIN dbo.ArchiveStatus asSet2 ON asSet2.EntityType = 'Set' AND asSet2.EntityId = s2.SetId AND asSet2.IsArchived = 1
                                 WHERE req.ItemId = i.ItemId
                                   AND req.SetId IS NOT NULL
                                   AND ISNULL(s2.Active,1) = 1
                                   AND asSet2.EntityId IS NULL
                            )
                         )
                         AND arch.EntityId IS NULL
                       ORDER BY i.Name"
                    : @"SELECT i.ItemId, i.Name, i.ModelNumber,
                              ISNULL(NULLIF(LTRIM(RTRIM(i.Category)), ''), cat.Name) AS CategoryName,
                              CAST(ISNULL(i.StockOnHand,0) AS int) AS StockOnHand
                       FROM dbo.Item i
                       LEFT JOIN dbo.ItemCategory cat ON cat.CategoryId = i.CategoryId
                       LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
                       WHERE i.Active = 1
                         AND i.ItemType = 'Hardware'
                         AND ISNULL(i.StockOnHand,0) > 0
                         AND i.DurationStartDate IS NULL
                         AND NOT EXISTS (
                              SELECT 1
                              FROM dbo.SetItem si
                              INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
                              LEFT JOIN dbo.ArchiveStatus asSet ON asSet.EntityType = 'Set' AND asSet.EntityId = s.SetId AND asSet.IsArchived = 1
                              WHERE si.ItemId = i.ItemId
                                AND ISNULL(s.Active,1) = 1
                                AND asSet.EntityId IS NULL
                         )
                         AND NOT EXISTS (
                              SELECT 1
                              FROM dbo.Request req
                              INNER JOIN dbo.[Set] s2 ON s2.SetId = req.SetId
                              LEFT JOIN dbo.ArchiveStatus asSet2 ON asSet2.EntityType = 'Set' AND asSet2.EntityId = s2.SetId AND asSet2.IsArchived = 1
                              WHERE req.ItemId = i.ItemId
                                AND req.SetId IS NOT NULL
                                AND ISNULL(s2.Active,1) = 1
                                AND asSet2.EntityId IS NULL
                         )
                         AND arch.EntityId IS NULL
                       ORDER BY i.Name";

                using (var cmd = new SqlCommand(sql, con))
                using (var r = cmd.ExecuteReader())
                {
                    var items = new List<object>();
                    while (r.Read())
                    {
                        var name = r["Name"] == DBNull.Value ? "" : r["Name"].ToString();
                        var model = r["ModelNumber"] == DBNull.Value ? null : r["ModelNumber"].ToString();
                        var displayText = string.IsNullOrEmpty(model) ? name : name + " (" + model + ")";
                        items.Add(new
                        {
                            itemId = Convert.ToInt32(r["ItemId"]),
                            displayText,
                            category = r["CategoryName"] == DBNull.Value ? null : r["CategoryName"].ToString(),
                            stockOnHand = Convert.ToInt32(r["StockOnHand"])
                        });
                    }
                    Ok(c, new { success = true, items });
                }
            }
        }
        catch (Exception ex) { Err(c, 500, "Lookup failed: " + ex.Message); }
    }

    public bool IsReusable { get { return false; } }
}
