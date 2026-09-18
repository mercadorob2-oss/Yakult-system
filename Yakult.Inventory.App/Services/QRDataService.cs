using System;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Collections.Generic;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    public class QRDataService
    {
        private readonly string _connectionString;

        public QRDataService()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Regenerates QRData for all Sets that don't have it
        /// </summary>
        public async Task<int> RegenerateQRDataForExistingSetsAsync()
        {
            const string getSetsWithoutQRData = @"
                SELECT SetId 
                FROM dbo.[Set] 
                WHERE QRData IS NULL";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(getSetsWithoutQRData, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var setIds = new List<int>();
                    while (await reader.ReadAsync())
                    {
                        setIds.Add(reader.GetInt32(0));
                    }
                    reader.Close();

                    // Generate QRData for each Set
                    foreach (var setId in setIds)
                    {
                        await GenerateAndUpdateQRDataAsync(setId);
                    }

                    return setIds.Count;
                }
            }
        }

        /// <summary>
        /// Generates and updates QRData for a specific Set
        /// </summary>
        public async Task GenerateAndUpdateQRDataAsync(int setId)
        {
            // Declare variables at method level so they're in scope for JSON building
            string setCode = null;
            string dispatchDate = "Not Set";
            string setType = "Pending";
            string setRemarks = null;
            string setComputerName = null;
            string setIpAddress = null;
            string employeeName = "Unknown";
            string employeeNumber = null;
            string companyName = "N/A";
            string departmentName = "N/A";
            string branchName = "N/A";
            var items = new List<object>();

            // Query to get Set and Employee info
            const string sql = @"
                SELECT
                    s.SetCode,
                    s.DispatchDate,
                    s.SetType,
                    s.Remarks AS SetRemarks,
                    s.ComputerName,
                    s.IPAddress,
                    e.Name AS EmployeeName,
                    e.EmployeeNumber,
                    c.Name AS CompanyName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName
                FROM dbo.[Set] s
                LEFT JOIN dbo.Request r ON s.SetId = r.SetId
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                WHERE s.SetId = @SetId";

            // Query to get all Items in this Set
            const string itemsSql = @"
                SELECT 
                    i.Name AS ItemName,
                    i.Category,
                    r.Quantity,
                    i.ModelNumber,
                    i.SerialNumber,
                    r.Description AS RequestDescription,
                    r.Status
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                WHERE r.SetId = @SetId";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                // Get Set info
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {

                    if (await reader.ReadAsync())
                    {
                        setCode = reader.GetString(reader.GetOrdinal("SetCode"));

                        if (!reader.IsDBNull(reader.GetOrdinal("DispatchDate")))
                            dispatchDate = reader.GetDateTime(reader.GetOrdinal("DispatchDate")).ToString("yyyy-MM-dd");

                        if (!reader.IsDBNull(reader.GetOrdinal("SetType")))
                            setType = reader.GetString(reader.GetOrdinal("SetType"));

                        if (!reader.IsDBNull(reader.GetOrdinal("SetRemarks")))
                            setRemarks = reader.GetString(reader.GetOrdinal("SetRemarks"));

                        if (!reader.IsDBNull(reader.GetOrdinal("ComputerName")))
                            setComputerName = reader.GetString(reader.GetOrdinal("ComputerName"));

                        if (!reader.IsDBNull(reader.GetOrdinal("IPAddress")))
                            setIpAddress = reader.GetString(reader.GetOrdinal("IPAddress"));

                        if (!reader.IsDBNull(reader.GetOrdinal("EmployeeName")))
                            employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"));

                        if (!reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")))
                            employeeNumber = reader.GetString(reader.GetOrdinal("EmployeeNumber"));

                        if (!reader.IsDBNull(reader.GetOrdinal("CompanyName")))
                            companyName = reader.GetString(reader.GetOrdinal("CompanyName"));

                        if (!reader.IsDBNull(reader.GetOrdinal("DepartmentName")))
                            departmentName = reader.GetString(reader.GetOrdinal("DepartmentName"));

                        if (!reader.IsDBNull(reader.GetOrdinal("BranchName")))
                            branchName = reader.GetString(reader.GetOrdinal("BranchName"));
                    }
                    } // Close reader using
                } // Close cmd using

                // Get Items
                using (var itemsCmd = new SqlCommand(itemsSql, con))
                {
                    itemsCmd.Parameters.AddWithValue("@SetId", setId);
                    
                    using (var itemsReader = await itemsCmd.ExecuteReaderAsync())
                    {
                        while (await itemsReader.ReadAsync())
                        {
                            var category = itemsReader.IsDBNull(itemsReader.GetOrdinal("Category"))
                                ? "N/A"
                                : itemsReader.GetString(itemsReader.GetOrdinal("Category"));

                            items.Add(new
                            {
                                ItemName = itemsReader.GetString(itemsReader.GetOrdinal("ItemName")),
                                Category = category,
                                Quantity = itemsReader.GetInt32(itemsReader.GetOrdinal("Quantity")),
                                ModelNumber = itemsReader.IsDBNull(itemsReader.GetOrdinal("ModelNumber"))
                                    ? "N/A"
                                    : itemsReader.GetString(itemsReader.GetOrdinal("ModelNumber")),
                                SerialNumber = itemsReader.IsDBNull(itemsReader.GetOrdinal("SerialNumber"))
                                    ? "N/A"
                                    : itemsReader.GetString(itemsReader.GetOrdinal("SerialNumber")),
                                Status = itemsReader.IsDBNull(itemsReader.GetOrdinal("Status"))
                                    ? "N/A"
                                    : itemsReader.GetString(itemsReader.GetOrdinal("Status"))
                            });
                        }
                    }
                }

                // Build JSON (now all variables are in scope)
                var qrData = new
                {
                    SetCode = setCode,
                    DispatchDate = dispatchDate,
                    SetType = setType,
                    Remarks = setRemarks,
                    Employee = new
                    {
                        Name = employeeName,
                        EmployeeNumber = employeeNumber,
                        Company = companyName,
                        Department = departmentName,
                        Branch = branchName
                    },
                    HardwareComputerName = string.Equals(setType, "Hardware", StringComparison.OrdinalIgnoreCase)
                        ? setComputerName
                        : null,
                    HardwareIPAddress = string.Equals(setType, "Hardware", StringComparison.OrdinalIgnoreCase)
                        ? setIpAddress
                        : null,
                    Items = items
                };

                var json = JsonSerializer.Serialize(qrData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Update QRData in database
                const string updateSql = @"
                    UPDATE dbo.[Set] 
                    SET QRData = @QRData 
                    WHERE SetId = @SetId";

                using (var updateCmd = new SqlCommand(updateSql, con))
                {
                    updateCmd.Parameters.AddWithValue("@QRData", json);
                    updateCmd.Parameters.AddWithValue("@SetId", setId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            } // Close connection using
        }
    }
}
