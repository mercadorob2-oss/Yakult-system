// using System;
// using System.Collections.Generic;
// using System.Data.SqlClient;
// using Dapper;
// using Yakult.Inventory.App.Core;
// using Yakult.Inventory.App.Models;
// 
// namespace Yakult.Inventory.App.Repositories
// {
//     public class ReportsRepository
//     {
//         private string GetConnectionString()
//         {
//             DatabaseConfig.EnsureConfigured();
//             return DatabaseConfig.ConnectionString;
//         }
// 
//         /// <summary>
//         /// Retrieves all user-submitted reports/problems from dbo.UserReports.
//         /// </summary>
//         public List<ReportListItem> GetAvailableReports()
//         {
//             try
//             {
//                 const string sql = @"
//                     SELECT
//                         ReportId,
//                         Title,
//                         Description,
//                         SubmittedBy,
//                         SubmittedDate,
//                         Status,
//                         Priority,
//                         Category
//                     FROM dbo.UserReports
//                     WHERE IsDeleted = 0
//                     ORDER BY SubmittedDate DESC";
// 
//                 using (var conn = new SqlConnection(GetConnectionString()))
//                 {
//                     return conn.Query<ReportListItem>(sql).AsList();
//                 }
//             }
//             catch (Exception ex)
//             {
//                 System.Diagnostics.Debug.WriteLine($"Error loading reports: {ex.Message}");
//                 return new List<ReportListItem>();
//             }
//         }
// 
//         /// <summary>
//         /// Gets a single report by ID (placeholder for future use).
//         /// </summary>
//         public ReportListItem GetReportById(int reportId)
//         {
//             try
//             {
//                 // TODO: Implement once database schema is finalized
//                 return null;
//             }
//             catch (Exception ex)
//             {
//                 System.Diagnostics.Debug.WriteLine($"Error loading report {reportId}: {ex.Message}");
//                 return null;
//             }
//         }
//     }
// }
