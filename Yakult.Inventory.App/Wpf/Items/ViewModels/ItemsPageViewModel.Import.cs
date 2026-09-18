using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDataReader;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.Items.ViewModels
{
    // Excel/CSV import, template generation, and the 3-phase Get-from-Mobile claim/
    // confirm/cancel HTTP flow — ported verbatim from Pages.Item.ViewItemsPage.
    public partial class ItemsPageViewModel
    {
        // ── Get-from-Mobile HTTP claim flow ──────────────────────────────────

        internal async Task<(bool Success, string ClaimToken, List<MobileSerialItem> Items)> ClaimMobileSerialsAsync()
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                var response = await client.GetStringAsync(AppConfig.ApiClaimMobileSerialsUrl);
                var json = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response);

                bool success = json.success;
                string rawToken = json.claimToken != null ? (string)json.claimToken : null;
                var serialsToken = json.serials;

                if (!success || serialsToken == null || string.IsNullOrEmpty(rawToken))
                    return (false, null, new List<MobileSerialItem>());

                var mobileItems = new List<MobileSerialItem>();
                foreach (var entry in serialsToken)
                {
                    string serial = entry.serialNumber != null ? (string)entry.serialNumber : null;
                    if (string.IsNullOrWhiteSpace(serial)) continue;

                    mobileItems.Add(new MobileSerialItem
                    {
                        SerialNumber = serial.Trim(),
                        CellPhoneNumber = entry.cellPhoneNumber != null ? ((string)entry.cellPhoneNumber)?.Trim() : null,
                        IMEI1 = entry.imei1 != null ? ((string)entry.imei1)?.Trim() : null,
                        IMEI2 = entry.imei2 != null ? ((string)entry.imei2)?.Trim() : null
                    });
                }

                return (true, rawToken, mobileItems);
            }
        }

        public async Task ConfirmMobileClaimAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                    await client.GetStringAsync(AppConfig.ApiConfirmMobileSerialClaimUrl(token));
            }
            catch { /* best-effort – claim file will be cleaned up on next restart */ }
        }

        public async Task CancelMobileClaimAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                    await client.GetStringAsync(AppConfig.ApiCancelMobileSerialClaimUrl(token));
            }
            catch { /* best-effort restore */ }
        }

        // ── Import file parsing ──────────────────────────────────────────────

        public DataTable LoadCsvToDataTable(string filePath)
        {
            var dt = new DataTable();

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
                return dt;

            var headerCells = ParseCsvLine(lines[0]);
            foreach (var h in headerCells)
                dt.Columns.Add(h.Trim().Trim('"'));

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var cells = ParseCsvLine(lines[i]);
                var row = dt.NewRow();
                for (int c = 0; c < headerCells.Length && c < cells.Length; c++)
                    row[c] = cells[c];
                dt.Rows.Add(row);
            }

            return dt;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                        inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                    sb.Append(c);
            }

            result.Add(sb.ToString());
            return result.ToArray();
        }

        public DataTable LoadExcelToDataTable(string filePath)
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var conf = new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    };

                    var dataSet = reader.AsDataSet(conf);
                    if (dataSet.Tables.Count == 0)
                        return new DataTable();

                    var table = dataSet.Tables[0];

                    if (table.Columns.Contains("ItemId"))
                        table.Columns.Remove("ItemId");

                    return table;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading Excel file: {ex.Message}", ex);
            }
        }

        public Task<(int SuccessCount, List<string> Errors)> ImportItemsFromTableAsync(DataTable table)
        {
            return Task.Run(() =>
            {
                int successCount = 0;
                var errors = new List<string>();

                int colModelName = GetColumnIndex(table.Columns, "Model Name");
                int colDescription = GetColumnIndex(table.Columns, "Description");
                int colModelNumber = GetColumnIndex(table.Columns, "Model Number");
                int colSerialNumber = GetColumnIndex(table.Columns, "Serial Number");
                int colQuantity = GetColumnIndex(table.Columns, "Quantity");
                int colUnit = GetColumnIndex(table.Columns, "Unit Of Measure");
                int colCategory = GetColumnIndex(table.Columns, "Category");
                int colAmount = GetColumnIndex(table.Columns, "Amount");
                int colCondition = GetColumnIndex(table.Columns, "Condition");
                int colItemType = GetColumnIndex(table.Columns, "Item Type");
                int colVendor = GetColumnIndex(table.Columns, "Vendor");
                int colStartDate = GetColumnIndex(table.Columns, "Start Date");
                int colEndDate = GetColumnIndex(table.Columns, "End Date");
                int colRemarks = GetColumnIndex(table.Columns, "Remarks");
                int colWarrantyYears = GetColumnIndex(table.Columns, "Warranty Years");
                int colDurationYears = GetColumnIndex(table.Columns, "Duration Years");
                int colDurationStartDate = GetColumnIndex(table.Columns, "Duration Start Date");
                int colDurationEndDate = GetColumnIndex(table.Columns, "Duration End Date");
                int colDatePurchased = GetColumnIndex(table.Columns, "Date Purchased");
                int colLicenseNumber = GetColumnIndex(table.Columns, "License Number");
                int colWarrantyStartDate = GetColumnIndex(table.Columns, "Warranty Start Date");
                int colAcquisitionType = GetColumnIndex(table.Columns, "Acquisition Type");
                int colIsBorrowable = GetColumnIndex(table.Columns, "Is Borrowable");

                var categoryCache = new Dictionary<string, int>();
                var conditionCache = new Dictionary<string, Tuple<int, string>>();
                var vendorCache = new Dictionary<string, int?>();

                for (int i = 0; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    int excelRowNumber = i + 2;

                    try
                    {
                        string modelName = GetCellString(row, colModelName);
                        string modelNumber = GetCellString(row, colModelNumber);
                        string serialNumber = GetCellString(row, colSerialNumber);
                        string categoryName = GetCellString(row, colCategory);

                        bool isRowEmpty =
                            string.IsNullOrWhiteSpace(modelName) &&
                            string.IsNullOrWhiteSpace(modelNumber) &&
                            string.IsNullOrWhiteSpace(serialNumber) &&
                            string.IsNullOrWhiteSpace(categoryName);

                        if (isRowEmpty)
                            continue;

                        string qtyRaw = GetCellString(row, colQuantity);
                        int quantity = 1;
                        if (!string.IsNullOrWhiteSpace(qtyRaw))
                        {
                            if (!int.TryParse(qtyRaw, out quantity) || quantity < 0)
                            {
                                errors.Add($"Row {excelRowNumber}: Invalid Quantity '{qtyRaw}'. Skipped.");
                                continue;
                            }
                        }

                        bool hasSerial = !string.IsNullOrWhiteSpace(serialNumber);
                        bool hasQuantity = quantity >= 0;

                        if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(modelNumber))
                        {
                            errors.Add($"Row {excelRowNumber}: Missing required fields (Model Name, Model Number). Skipped.");
                            continue;
                        }

                        if (!hasSerial && !hasQuantity)
                        {
                            errors.Add($"Row {excelRowNumber}: Non-serialized items must have Quantity > 0. Skipped.");
                            continue;
                        }

                        if (hasSerial)
                            quantity = 1;

                        string unit = GetCellString(row, colUnit);
                        if (string.IsNullOrWhiteSpace(unit))
                            unit = "Unit";

                        if (string.IsNullOrWhiteSpace(categoryName))
                        {
                            errors.Add($"Row {excelRowNumber}: Category is required. Skipped.");
                            continue;
                        }

                        int categoryId = GetOrCreateCategoryId(categoryName, categoryCache);

                        string amountRaw = GetCellString(row, colAmount);
                        decimal amount = 0m;
                        if (!string.IsNullOrWhiteSpace(amountRaw))
                        {
                            if (!decimal.TryParse(amountRaw, out amount) || amount < 0)
                            {
                                errors.Add($"Row {excelRowNumber}: Invalid Amount '{amountRaw}'. Skipped.");
                                continue;
                            }
                        }

                        string conditionText = GetCellString(row, colCondition);
                        if (!TryGetCondition(conditionText, conditionCache, out int conditionId, out string conditionName))
                        {
                            errors.Add($"Row {excelRowNumber}: Condition '{conditionText}' not found. Skipped.");
                            continue;
                        }

                        string itemTypeRaw = GetCellString(row, colItemType);
                        string itemType;
                        if (string.IsNullOrWhiteSpace(itemTypeRaw))
                            itemType = ItemTypes.Hardware;
                        else if (itemTypeRaw.Equals(ItemTypes.Hardware, StringComparison.OrdinalIgnoreCase))
                            itemType = ItemTypes.Hardware;
                        else if (itemTypeRaw.Equals(ItemTypes.SoftwareLicense, StringComparison.OrdinalIgnoreCase))
                            itemType = ItemTypes.SoftwareLicense;
                        else if (itemTypeRaw.Equals("Service", StringComparison.OrdinalIgnoreCase) || itemTypeRaw.Equals(ItemTypes.Services, StringComparison.OrdinalIgnoreCase) || itemTypeRaw.Equals("Services", StringComparison.OrdinalIgnoreCase))
                            itemType = ItemTypes.Services;
                        else
                        {
                            errors.Add($"Row {excelRowNumber}: Invalid Item Type '{itemTypeRaw}'. Skipped.");
                            continue;
                        }

                        string vendorName = GetCellString(row, colVendor);
                        int? vendorId = null;
                        if (!string.IsNullOrWhiteSpace(vendorName))
                            vendorId = GetOrCreateVendorId(vendorName, vendorCache);

                        DateTime? startDate = null;
                        DateTime? endDate = null;
                        string startRaw = GetCellString(row, colStartDate);
                        string endRaw = GetCellString(row, colEndDate);

                        if (!string.IsNullOrWhiteSpace(startRaw))
                        {
                            if (!TryParseMultipleDateFormats(startRaw, out DateTime dt))
                            {
                                errors.Add($"Row {excelRowNumber}: Invalid Start Date '{startRaw}'. Supported formats: MM/dd/yyyy, dd/MM/yyyy, yyyy-MM-dd, etc. Skipped.");
                                continue;
                            }
                            startDate = dt;
                        }

                        if (!string.IsNullOrWhiteSpace(endRaw))
                        {
                            if (!TryParseMultipleDateFormats(endRaw, out DateTime dt))
                            {
                                errors.Add($"Row {excelRowNumber}: Invalid End Date '{endRaw}'. Supported formats: MM/dd/yyyy, dd/MM/yyyy, yyyy-MM-dd, etc. Skipped.");
                                continue;
                            }
                            endDate = dt;
                        }

                        string description = GetCellString(row, colDescription);
                        string remarks = GetCellString(row, colRemarks);

                        int warrantyYears = 0;
                        string warrantyYearsRaw = GetCellString(row, colWarrantyYears);
                        if (!string.IsNullOrWhiteSpace(warrantyYearsRaw))
                        {
                            if (!int.TryParse(warrantyYearsRaw, out warrantyYears) || warrantyYears < 0)
                                warrantyYears = 0;
                        }

                        int durationYears = 0;
                        string durationYearsRaw = GetCellString(row, colDurationYears);
                        if (!string.IsNullOrWhiteSpace(durationYearsRaw))
                        {
                            if (!int.TryParse(durationYearsRaw, out durationYears) || durationYears < 0)
                                durationYears = 0;
                        }

                        DateTime? durationStartDate = null;
                        string durationStartRaw = GetCellString(row, colDurationStartDate);
                        if (!string.IsNullOrWhiteSpace(durationStartRaw) && TryParseMultipleDateFormats(durationStartRaw, out DateTime dsd))
                            durationStartDate = dsd;

                        DateTime? durationEndDate = null;
                        string durationEndRaw = GetCellString(row, colDurationEndDate);
                        if (!string.IsNullOrWhiteSpace(durationEndRaw) && TryParseMultipleDateFormats(durationEndRaw, out DateTime ded))
                            durationEndDate = ded;

                        DateTime? datePurchased = null;
                        string datePurchasedRaw = GetCellString(row, colDatePurchased);
                        if (!string.IsNullOrWhiteSpace(datePurchasedRaw) && TryParseMultipleDateFormats(datePurchasedRaw, out DateTime dp))
                            datePurchased = dp;

                        string licenseNumber = GetCellString(row, colLicenseNumber);

                        DateTime? warrantyStartDate = null;
                        string warrantyStartRaw = GetCellString(row, colWarrantyStartDate);
                        if (!string.IsNullOrWhiteSpace(warrantyStartRaw) && TryParseMultipleDateFormats(warrantyStartRaw, out DateTime wsd))
                            warrantyStartDate = wsd;

                        string acquisitionType = GetCellString(row, colAcquisitionType);
                        if (!string.IsNullOrWhiteSpace(acquisitionType))
                        {
                            if (!acquisitionType.Equals("Request", StringComparison.OrdinalIgnoreCase) &&
                                !acquisitionType.Equals("Invoice", StringComparison.OrdinalIgnoreCase) &&
                                !acquisitionType.Equals("Both", StringComparison.OrdinalIgnoreCase))
                                acquisitionType = null;
                        }

                        string isBorrowableRaw = GetCellString(row, colIsBorrowable);
                        bool? isBorrowable = string.IsNullOrWhiteSpace(isBorrowableRaw)
                            ? (bool?)null
                            : (isBorrowableRaw.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                               isBorrowableRaw.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                               isBorrowableRaw.Equals("1", StringComparison.OrdinalIgnoreCase));

                        var repo = new ItemRepository();
                        if (hasSerial && repo.SerialNumberExists(serialNumber))
                        {
                            errors.Add($"Row {excelRowNumber}: Serial Number '{serialNumber}' already exists in the database. Skipped.");
                            continue;
                        }

                        if (!hasSerial && repo.ItemExistsByModelAndCategory(modelNumber, categoryId))
                        {
                            errors.Add($"Row {excelRowNumber}: Item with ModelNumber '{modelNumber}' in this category already exists. Skipped.");
                            continue;
                        }

                        int? resolvedCartridgeModelId = null;
                        if (string.Equals(categoryName, "Cartridge", StringComparison.OrdinalIgnoreCase))
                            resolvedCartridgeModelId = repo.GetOrCreateDefaultCartridgeModelId(AppSession.CurrentUserId);

                        var item = new ItemDto
                        {
                            Name = modelName,
                            Description = description,
                            ModelNumber = modelNumber,
                            SerialNumber = hasSerial ? serialNumber : null,
                            CartridgeModelId = resolvedCartridgeModelId,
                            StockOnHand = quantity,
                            UnitOfMeasure = unit,
                            Active = true,
                            Category = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName,
                            CategoryId = categoryId,
                            Amount = amount,
                            ItemType = itemType,
                            StartDate = startDate,
                            EndDate = endDate,
                            ConditionId = conditionId,
                            ConditionName = conditionName,
                            VendorId = vendorId,
                            VendorName = string.IsNullOrWhiteSpace(vendorName) ? null : vendorName,
                            DateCreated = DateTime.Now,
                            CreatedByUserId = AppSession.CurrentUserId,
                            CreatedByName = AppSession.CurrentUserName,
                            Remarks = remarks,
                            WarrantyYears = warrantyYears,
                            DurationYears = durationYears,
                            DurationStartDate = durationStartDate,
                            DurationEndDate = durationEndDate,
                            DatePurchased = datePurchased,
                            LicenseNumber = licenseNumber,
                            WarrantyStartDate = warrantyStartDate,
                            AcquisitionType = acquisitionType,
                            AffectsInventory = true,
                            IsTrackedAsset = false,
                            IsBorrowable = isBorrowable
                        };

                        repo.AddItem(item);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {excelRowNumber}: Unexpected error - {ex.Message}");
                    }
                }

                return (successCount, errors);
            });
        }

        private static int GetColumnIndex(DataColumnCollection columns, string headerName)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var name = columns[i].ColumnName;
                if (string.Equals(name?.Trim('"', ' '), headerName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string GetCellString(DataRow row, int index)
        {
            if (index < 0 || index >= row.Table.Columns.Count)
                return null;

            if (row.IsNull(index))
                return null;

            var value = row[index].ToString().Trim().Trim('"');

            if (string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
                return null;

            return value;
        }

        private static bool TryParseMultipleDateFormats(string dateString, out DateTime result)
        {
            result = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(dateString))
                return false;

            if (DateTime.TryParse(dateString, out result))
                return true;

            string[] formats =
            {
                "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yyyy", "d/M/yyyy",
                "yyyy-MM-dd", "yyyy/MM/dd", "MM-dd-yyyy", "dd-MM-yyyy",
                "M-d-yyyy", "d-M-yyyy", "MMM dd, yyyy", "dd MMM yyyy",
                "MMMM dd, yyyy", "dd MMMM yyyy", "MM/dd/yy", "dd/MM/yy", "yy-MM-dd"
            };

            foreach (string format in formats)
            {
                if (DateTime.TryParseExact(dateString, format,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out result))
                    return true;
            }

            return false;
        }

        private int GetOrCreateCategoryId(string categoryName, Dictionary<string, int> cache)
        {
            var key = categoryName.Trim().ToLowerInvariant();
            if (cache.TryGetValue(key, out int cached))
                return cached;

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();

                using (var cmd = new SqlCommand("SELECT CategoryId FROM dbo.ItemCategory WHERE Name = @Name AND Active = 1", con))
                {
                    cmd.Parameters.AddWithValue("@Name", categoryName.Trim());
                    var existing = cmd.ExecuteScalar();
                    if (existing != null && existing != DBNull.Value)
                    {
                        int id = Convert.ToInt32(existing);
                        cache[key] = id;
                        return id;
                    }
                }

                using (var cmd = new SqlCommand(@"INSERT INTO dbo.ItemCategory (Name, Active, DateCreated, CreatedBy)
                                                 OUTPUT INSERTED.CategoryId
                                                 VALUES (@Name, 1, @DateCreated, @CreatedBy)", con))
                {
                    cmd.Parameters.AddWithValue("@Name", categoryName.Trim());
                    cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                    cmd.Parameters.AddWithValue("@CreatedBy", AppSession.CurrentUserId);
                    int newId = (int)cmd.ExecuteScalar();
                    cache[key] = newId;
                    return newId;
                }
            }
        }

        private bool TryGetCondition(string conditionText, Dictionary<string, Tuple<int, string>> cache, out int conditionId, out string conditionName)
        {
            if (string.IsNullOrWhiteSpace(conditionText))
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    using (var cmdGood = new SqlCommand("SELECT TOP (1) ConditionID, ConditionName FROM dbo.[Condition] WHERE ConditionName = @Name", con))
                    {
                        cmdGood.Parameters.AddWithValue("@Name", "Good");
                        using (var reader = cmdGood.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                conditionId = reader.GetInt32(0);
                                conditionName = reader.GetString(1);
                                cache[conditionName.Trim().ToLowerInvariant()] = Tuple.Create(conditionId, conditionName);
                                return true;
                            }
                        }
                    }

                    using (var cmdAny = new SqlCommand("SELECT TOP (1) ConditionID, ConditionName FROM dbo.[Condition] ORDER BY ConditionID", con))
                    using (var reader = cmdAny.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            conditionId = reader.GetInt32(0);
                            conditionName = reader.GetString(1);
                            cache[conditionName.Trim().ToLowerInvariant()] = Tuple.Create(conditionId, conditionName);
                            return true;
                        }
                    }
                }

                conditionId = 0;
                conditionName = null;
                return false;
            }

            var key = conditionText.Trim().ToLowerInvariant();
            if (cache.TryGetValue(key, out Tuple<int, string> cached))
            {
                conditionId = cached.Item1;
                conditionName = cached.Item2;
                return true;
            }

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();

                using (var cmd = new SqlCommand("SELECT ConditionID, ConditionName FROM dbo.Condition WHERE ConditionName = @Name", con))
                {
                    cmd.Parameters.AddWithValue("@Name", conditionText.Trim());

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            conditionId = reader.GetInt32(0);
                            conditionName = reader.GetString(1);
                            cache[key] = Tuple.Create(conditionId, conditionName);
                            return true;
                        }
                    }
                }
            }

            conditionId = 0;
            conditionName = null;
            return false;
        }

        private int? GetOrCreateVendorId(string vendorName, Dictionary<string, int?> cache)
        {
            var key = vendorName.Trim().ToLowerInvariant();
            if (cache.TryGetValue(key, out int? cached))
                return cached;

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();

                using (var cmd = new SqlCommand("SELECT VendorID FROM dbo.Vendor WHERE VendorName = @Name AND IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@Name", vendorName.Trim());
                    var existing = cmd.ExecuteScalar();
                    if (existing != null && existing != DBNull.Value)
                    {
                        int id = Convert.ToInt32(existing);
                        cache[key] = id;
                        return id;
                    }
                }

                using (var cmd = new SqlCommand(@"INSERT INTO dbo.Vendor (VendorName, IsActive, CreatedDate)
                                                 OUTPUT INSERTED.VendorID
                                                 VALUES (@Name, 1, @CreatedDate)", con))
                {
                    cmd.Parameters.AddWithValue("@Name", vendorName.Trim());
                    cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    int newId = (int)cmd.ExecuteScalar();
                    cache[key] = newId;
                    return newId;
                }
            }
        }

        public string BuildTemplateCsvContent()
        {
            var headers = new[]
            {
                "Model Name", "Description", "Model Number", "Serial Number", "Quantity",
                "Unit Of Measure", "Category", "Amount", "Condition", "Item Type", "Vendor",
                "Start Date", "End Date", "Remarks", "Warranty Years", "Duration Years",
                "Duration Start Date", "Duration End Date", "Date Purchased", "License Number",
                "Warranty Start Date", "Acquisition Type", "Is Borrowable"
            };

            var sampleRow = new[]
            {
                "Dell Laptop Latitude 5420", "Business laptop with 14-inch display", "LAT5420",
                "DL123456789", "1", "Unit", "Computer Equipment", "35000.00", "Good", "Hardware",
                "Dell Technologies", "01/15/2024", "01/15/2029", "Sample item for reference", "3",
                "0", "", "", "2024-01-10", "", "01/10/2024", "Request", "No"
            };

            return string.Join(",", headers) + Environment.NewLine + string.Join(",", sampleRow);
        }
    }
}
