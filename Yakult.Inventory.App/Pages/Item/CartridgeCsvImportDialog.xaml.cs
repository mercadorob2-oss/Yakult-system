using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class CartridgeCsvImportDialog : Window
    {
        private List<CsvPreviewRow> _previewRows;
        private List<string> _unrecognizedTypes;

        public CartridgeCsvImportDialog()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Preview row model
        // ─────────────────────────────────────────────────────────────────────
        private enum TargetKind { Cartridge, Consumable }

        private class CsvPreviewRow
        {
            public string ModelName     { get; set; }
            public string TypeLabel     { get; set; }   // raw TYPE column text, shown as-is
            public TargetKind Kind      { get; set; }
            public string Category      { get; set; }   // ConsumableModel.Category (null for Cartridge)
            public int    CsvStock      { get; set; }
            public string ModelStatus   { get; set; }   // "Exists" | "NEW"
            public int    ModelId       { get; set; }
            public string ItemStatus    { get; set; }   // "Exists" | "NEW"
            public int    ItemId        { get; set; }
            public int?   CurrentStock  { get; set; }

            public string CurrentStockDisplay => CurrentStock.HasValue ? CurrentStock.Value.ToString() : "—";

            public Brush ModelStatusBrush => ModelStatus == "NEW"
                ? new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));

            public Brush ItemStatusBrush => ItemStatus == "NEW"
                ? new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Window chrome
        // ─────────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ─────────────────────────────────────────────────────────────────────
        // File picker
        // ─────────────────────────────────────────────────────────────────────
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WinForms.OpenFileDialog
            {
                Title  = "Select Cartridge/Consumable Stock CSV File",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtCsvPath.Text = dlg.FileName;
                BtnLoadPreview.IsEnabled = true;
                BtnImport.IsEnabled = false;
                PreviewGrid.Visibility = Visibility.Collapsed;
                LblEmptyState.Visibility = Visibility.Visible;
                BannerDupes.Visibility = Visibility.Collapsed;
                LblStatus.Text = string.Empty;
                _previewRows = null;
                _unrecognizedTypes = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Load preview
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnLoadPreview_Click(object sender, RoutedEventArgs e)
        {
            var path = TxtCsvPath.Text;
            if (!File.Exists(path))
            {
                MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnLoadPreview.IsEnabled = false;
            BtnImport.IsEnabled = false;
            LblStatus.Text = "Loading preview…";
            LblEmptyState.Visibility = Visibility.Visible;
            PreviewGrid.Visibility = Visibility.Collapsed;

            try
            {
                var parsed = ParseCsv(path, out var unrecognizedTypes);
                _unrecognizedTypes = unrecognizedTypes;

                var dupeModels = parsed
                    .GroupBy(r => (r.ModelName.ToUpperInvariant(), r.Kind, r.Category))
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key.Item1)
                    .Distinct()
                    .ToList();

                // Merge duplicates (same name + type) by summing stock
                var merged = parsed
                    .GroupBy(r => (Name: r.ModelName, r.Kind, r.Category, r.TypeLabel))
                    .Select(g => new
                    {
                        g.Key.Name,
                        g.Key.Kind,
                        g.Key.Category,
                        g.Key.TypeLabel,
                        CsvStock = g.Sum(r => r.CsvStock)
                    })
                    .OrderBy(r => r.Name)
                    .ToList();

                var rows = await Task.Run(() => BuildPreviewRows(merged.Select(m =>
                    (m.Name, m.Kind, m.Category, m.TypeLabel, m.CsvStock)).ToList()));
                _previewRows = rows;

                PreviewGrid.ItemsSource = rows;
                PreviewGrid.Visibility = Visibility.Visible;
                LblEmptyState.Visibility = Visibility.Collapsed;

                int newModels = rows.Count(r => r.ModelStatus == "NEW");
                int newItems  = rows.Count(r => r.ItemStatus  == "NEW");
                int updItems  = rows.Count(r => r.ItemStatus  == "Exists");
                LblStatus.Text = $"{rows.Count} rows — {newModels} new model(s), {newItems} new item(s), {updItems} to update";

                var bannerLines = new List<string>();
                if (dupeModels.Count > 0)
                    bannerLines.Add($"Duplicate model names found in CSV (stock summed): {string.Join(", ", dupeModels)}");
                if (_unrecognizedTypes != null && _unrecognizedTypes.Count > 0)
                    bannerLines.Add($"Skipped {_unrecognizedTypes.Count} row(s) with an unrecognized TYPE: {string.Join(", ", _unrecognizedTypes.Distinct())}");

                if (bannerLines.Count > 0)
                {
                    LblDupeNotice.Text = string.Join("  •  ", bannerLines);
                    BannerDupes.Visibility = Visibility.Visible;
                }
                else
                {
                    BannerDupes.Visibility = Visibility.Collapsed;
                }

                BtnImport.IsEnabled = rows.Count > 0;
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                BtnLoadPreview.IsEnabled = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CSV parsing
        // ─────────────────────────────────────────────────────────────────────
        // Expected layout: Description, Remaining Balance, TYPE (any leading title
        // row and the header row itself are skipped automatically — a row is only
        // treated as data once its stock column parses as an integer).
        private static List<CsvPreviewRow> ParseCsv(string path, out List<string> unrecognizedTypes)
        {
            var rows = new List<CsvPreviewRow>();
            unrecognizedTypes = new List<string>();
            var lines = File.ReadAllLines(path);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = SplitCsvLine(line);
                if (cols.Count < 3) continue;

                var modelName = cols[0].Trim();
                if (string.IsNullOrWhiteSpace(modelName)) continue;

                if (!int.TryParse(cols[1].Trim(), out int stock)) continue; // header/title rows fall through here

                var typeLabel = cols[2].Trim();
                if (!TryMapType(typeLabel, out var kind, out var category))
                {
                    unrecognizedTypes.Add(string.IsNullOrWhiteSpace(typeLabel) ? "(blank)" : typeLabel);
                    continue;
                }

                rows.Add(new CsvPreviewRow
                {
                    ModelName = modelName,
                    TypeLabel = typeLabel,
                    Kind      = kind,
                    Category  = category,
                    CsvStock  = stock
                });
            }

            return rows;
        }

        /// <summary>
        /// Maps a CSV TYPE value to either the CartridgeModel table or a ConsumableModel
        /// category. Whatever the sheet spells ("PRINT HEAD", "printer-head", "Toner Cart.",
        /// "ink"…), the category returned here is always the canonical bucket from
        /// Models.ConsumableCategories.Canonicalize — so no drift ever reaches
        /// dbo.ConsumableModel.Category or dbo.Item.Category on import.
        /// </summary>
        private static bool TryMapType(string typeLabel, out TargetKind kind, out string category)
        {
            kind = TargetKind.Cartridge;
            category = null;

            if (string.IsNullOrWhiteSpace(typeLabel)) return false;
            var normalized = typeLabel.Replace(" ", "").Replace("-", "").Replace("_", "").ToUpperInvariant();

            // Consumable families first — "TONER CARTRIDGE" must not fall into the plain
            // Cartridge bucket just because it contains the word "CARTRIDGE".
            if (normalized.Contains("TONER"))
            {
                kind = TargetKind.Consumable;
                category = ConsumableCategories.Canonicalize(typeLabel);   // -> "Toner"
                return true;
            }
            if (normalized.Contains("INK"))
            {
                kind = TargetKind.Consumable;
                category = ConsumableCategories.Canonicalize(typeLabel);   // -> "Ink"
                return true;
            }
            if (normalized.Contains("PRINT") && normalized.Contains("HEAD"))
            {
                kind = TargetKind.Consumable;
                category = ConsumableCategories.Canonicalize(typeLabel);   // -> "Printhead"
                return true;
            }
            if (normalized.Contains("CARTRIDGE"))
            {
                kind = TargetKind.Cartridge;
                return true;
            }

            return false;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')       { inQuotes = !inQuotes; }
                else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else                { current.Append(c); }
            }
            result.Add(current.ToString());
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Preview row building (runs on background thread)
        // ─────────────────────────────────────────────────────────────────────
        private List<CsvPreviewRow> BuildPreviewRows(List<(string ModelName, TargetKind Kind, string Category, string TypeLabel, int CsvStock)> entries)
        {
            var cartridgeRepo  = new CartridgeModelRepository();
            var consumableRepo = new ConsumableModelRepository();
            var rows = new List<CsvPreviewRow>();

            foreach (var (modelName, kind, category, typeLabel, csvStock) in entries)
            {
                var row = new CsvPreviewRow
                {
                    ModelName = modelName,
                    TypeLabel = typeLabel,
                    Kind      = kind,
                    Category  = category,
                    CsvStock  = csvStock
                };

                if (kind == TargetKind.Cartridge)
                {
                    var existing = cartridgeRepo.FindByModelNumberAsync(modelName).GetAwaiter().GetResult();
                    if (existing != null)
                    {
                        row.ModelStatus = "Exists";
                        row.ModelId     = existing.CartridgeModelId;

                        var itemInfo = FindItemByModelId("CartridgeModelId", existing.CartridgeModelId);
                        if (itemInfo.HasValue)
                        {
                            row.ItemStatus   = "Exists";
                            row.ItemId       = itemInfo.Value.ItemId;
                            row.CurrentStock = itemInfo.Value.StockOnHand;
                        }
                        else
                        {
                            row.ItemStatus = "NEW";
                        }
                    }
                    else
                    {
                        row.ModelStatus = "NEW";
                        row.ItemStatus  = "NEW";
                    }
                }
                else
                {
                    var existing = consumableRepo.FindByModelNumberAsync(modelName, category).GetAwaiter().GetResult();
                    if (existing != null)
                    {
                        row.ModelStatus = "Exists";
                        row.ModelId     = existing.ConsumableModelId;

                        var itemInfo = FindItemByModelId("ConsumableModelId", existing.ConsumableModelId);
                        if (itemInfo.HasValue)
                        {
                            row.ItemStatus   = "Exists";
                            row.ItemId       = itemInfo.Value.ItemId;
                            row.CurrentStock = itemInfo.Value.StockOnHand;
                        }
                        else
                        {
                            row.ItemStatus = "NEW";
                        }
                    }
                    else
                    {
                        row.ModelStatus = "NEW";
                        row.ItemStatus  = "NEW";
                    }
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Looks up the Item bound to a Cartridge/ConsumableModel by its FK alone — no
        /// Category text filter. Category text isn't a reliable match key: it varies by
        /// environment (e.g. dbo.Item.Category is "Toner Cartridge" in DEV vs "Toner" in
        /// PROD for the same consumable bucket), so filtering on it here would silently
        /// miss the real bound item and cause the importer to create a duplicate instead
        /// of updating it. The ModelId FK already scopes this uniquely.
        /// </summary>
        private static (int ItemId, int StockOnHand)? FindItemByModelId(string modelIdColumn, int modelId)
        {
            string sql = $@"
SELECT TOP 1 ItemId, StockOnHand
FROM dbo.Item
WHERE {modelIdColumn} = @ModelId
  AND Active = 1";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelId", modelId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            return (r.GetInt32(0), r.GetInt32(1));
                    }
                }
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Import
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (_previewRows == null || _previewRows.Count == 0) return;

            int newModels = _previewRows.Count(r => r.ModelStatus == "NEW");
            int newItems  = _previewRows.Count(r => r.ItemStatus  == "NEW");
            int updItems  = _previewRows.Count(r => r.ItemStatus  == "Exists");

            var confirm = MessageBox.Show(
                $"Import {_previewRows.Count} model(s)?\n\n" +
                $"• {newModels} new model record(s) will be created\n" +
                $"• {newItems} new item record(s) will be created\n" +
                $"• {updItems} existing item record(s) will have StockOnHand updated\n\n" +
                "Continue?",
                "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnImport.IsEnabled = false;
            BtnLoadPreview.IsEnabled = false;
            LblStatus.Text = "Importing…";

            try
            {
                var rows = _previewRows.ToList();
                var (succeeded, failed, errors) = await Task.Run(() => RunImport(rows));

                if (errors.Count > 0)
                {
                    MessageBox.Show(
                        $"Import completed with errors.\n\n" +
                        $"Succeeded: {succeeded}   Failed: {failed}\n\n" +
                        string.Join("\n", errors.Take(10)),
                        "Import Results", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"Import complete! {succeeded} model(s) processed.",
                        "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = succeeded > 0;
                Close();
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Import failed: {ex.Message}";
                BtnImport.IsEnabled = true;
                BtnLoadPreview.IsEnabled = true;
            }
        }

        private (int succeeded, int failed, List<string> errors) RunImport(List<CsvPreviewRow> rows)
        {
            var cartridgeRepo  = new CartridgeModelRepository();
            var consumableRepo = new ConsumableModelRepository();
            var itemRepo = new ItemRepository();
            int succeeded = 0, failed = 0;
            var errors = new List<string>();

            int conditionId = GetDefaultConditionId();

            foreach (var row in rows)
            {
                try
                {
                    // row.Category is already canonical (TryMapType), but re-run Canonicalize
                    // here too so any future caller / hand-edited preview row can't smuggle a
                    // drifted spelling into dbo.ConsumableModel or dbo.Item.
                    string canonicalCategory = row.Kind == TargetKind.Cartridge
                        ? "Cartridge"
                        : ConsumableCategories.Canonicalize(row.Category);

                    if (row.Kind == TargetKind.Cartridge)
                    {
                        int modelId = row.ModelId;
                        if (modelId == 0)
                        {
                            var dto = new CartridgeModelDto
                            {
                                ModelNumber   = row.ModelName,
                                IsRequestable = true,
                                IsRefillable  = true,
                                IsActive      = true,
                                CreatedBy     = AppSession.CurrentUserId,
                                VendorId      = null
                            };
                            modelId = cartridgeRepo.CreateAsync(dto).GetAwaiter().GetResult();
                        }

                        if (row.ItemId > 0)
                        {
                            UpdateItemStock(row.ItemId, row.CsvStock);
                        }
                        else
                        {
                            var (categoryId, categoryText) = ResolveItemCategory(canonicalCategory, allowFuzzy: false);
                            itemRepo.AddItem(BuildNewItem(row, modelId, null, categoryId, categoryText, conditionId));
                        }
                    }
                    else
                    {
                        int modelId = row.ModelId;
                        if (modelId == 0)
                        {
                            var dto = new ConsumableModelDto
                            {
                                ModelNumber   = row.ModelName,
                                Category      = canonicalCategory,
                                IsRequestable = true,
                                IsActive      = true,
                                CreatedBy     = AppSession.CurrentUserId
                            };
                            modelId = consumableRepo.CreateAsync(dto).GetAwaiter().GetResult();
                        }

                        if (row.ItemId > 0)
                        {
                            UpdateItemStock(row.ItemId, row.CsvStock);
                        }
                        else
                        {
                            var (categoryId, categoryText) = ResolveItemCategory(canonicalCategory, allowFuzzy: true);
                            itemRepo.AddItem(BuildNewItem(row, null, modelId, categoryId, categoryText, conditionId));
                        }
                    }

                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{row.ModelName}: {ex.Message}");
                }
            }

            return (succeeded, failed, errors);
        }

        private static ItemDto BuildNewItem(CsvPreviewRow row, int? cartridgeModelId, int? consumableModelId, int categoryId, string categoryText, int conditionId)
        {
            return new ItemDto
            {
                Name              = row.ModelName,
                Description       = null,
                ModelNumber       = row.ModelName,
                CartridgeModelId  = cartridgeModelId,
                ConsumableModelId = consumableModelId,
                CategoryId        = categoryId,
                Category          = categoryText,
                SerialNumber      = null,
                StockOnHand       = row.CsvStock,
                UnitOfMeasure     = "pcs",
                Amount            = 0,
                ItemType          = "Hardware",
                AffectsInventory  = true,
                AcquisitionType   = "Both",
                IsTrackedAsset    = false,
                RefillStatus      = null,
                ConditionId       = conditionId,
                ConditionName     = "Good",
                VendorId          = null,
                DateCreated       = DateTime.Now,
                CreatedByUserId   = AppSession.CurrentUserId,
                CreatedByName     = AppSession.CurrentUserName,
                Active            = true
            };
        }

        private static void UpdateItemStock(int itemId, int stock)
        {
            const string sql = @"
UPDATE dbo.Item
SET StockOnHand  = @StockOnHand,
    DateModified = GETDATE(),
    ModifiedBy   = @ModifiedBy
WHERE ItemId = @ItemId";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@StockOnHand", stock);
                    cmd.Parameters.AddWithValue("@ModifiedBy",  AppSession.CurrentUserId);
                    cmd.Parameters.AddWithValue("@ItemId",      itemId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Resolves both the ItemCategory.CategoryId AND the actual category text to store
        /// on a new dbo.Item row for a canonical bucket ("Cartridge"/"Ink"/"Toner"/"Print Head").
        ///
        /// The literal category text used by dbo.Item/dbo.ItemCategory is NOT the same across
        /// environments — e.g. the "Toner" bucket is stored as Item.Category = "Toner" in PROD
        /// but "Toner Cartridge" in DEV, and "Print Head" (PROD Item text) vs "Printhead" (both
        /// environments' ItemCategory.Name). Hardcoding the canonical bucket string as the literal
        /// Item.Category value breaks the FK to ItemCategory (CategoryId NOT NULL) the moment an
        /// environment's real name differs — this happened importing Toner into DEV. So this
        /// looks up whatever the environment's real name actually is and returns that instead.
        ///
        /// allowFuzzy enables a substring match (e.g. "toner" matches "Toner Cartridge") as a
        /// last resort — only safe for the Ink/Toner/Print Head buckets. It is NOT used for
        /// Cartridge, because "cartridge" as a substring would also match "Toner Cartridge".
        /// </summary>
        private static (int CategoryId, string CategoryText) ResolveItemCategory(string canonicalCategory, bool allowFuzzy)
        {
            string needle = canonicalCategory.Replace(" ", "").ToLowerInvariant();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();

                // 1. An existing Item already using this category (exact, spacing-insensitive)
                var match = QueryCategoryMatch(con,
                    "SELECT TOP 1 Category, CategoryId FROM dbo.Item WHERE CategoryId IS NOT NULL AND CategoryId > 0 AND REPLACE(LOWER(Category), ' ', '') = @Needle",
                    needle);
                if (match.HasValue) return match.Value;

                // 2. The ItemCategory table itself (exact, spacing-insensitive)
                match = QueryCategoryMatch(con,
                    "SELECT TOP 1 Name, CategoryId FROM dbo.ItemCategory WHERE Active = 1 AND REPLACE(LOWER(Name), ' ', '') = @Needle",
                    needle);
                if (match.HasValue) return match.Value;

                if (allowFuzzy)
                {
                    match = QueryCategoryMatch(con,
                        "SELECT TOP 1 Category, CategoryId FROM dbo.Item WHERE CategoryId IS NOT NULL AND CategoryId > 0 AND REPLACE(LOWER(Category), ' ', '') LIKE '%' + @Needle + '%'",
                        needle);
                    if (match.HasValue) return match.Value;

                    match = QueryCategoryMatch(con,
                        "SELECT TOP 1 Name, CategoryId FROM dbo.ItemCategory WHERE Active = 1 AND REPLACE(LOWER(Name), ' ', '') LIKE '%' + @Needle + '%'",
                        needle);
                    if (match.HasValue) return match.Value;
                }
            }

            throw new InvalidOperationException(
                $"No ItemCategory found matching '{canonicalCategory}' in this database. " +
                "Add an ItemCategory row for this type (or add at least one Item using it) before importing.");
        }

        private static (int CategoryId, string CategoryText)? QueryCategoryMatch(SqlConnection con, string sql, string needle)
        {
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Needle", needle);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                        return (r.GetInt32(1), r.GetString(0));
                }
            }
            return null;
        }

        private static int GetDefaultConditionId()
        {
            const string sql = "SELECT TOP 1 ConditionId FROM dbo.Condition WHERE ConditionName = 'Good'";
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    var val = cmd.ExecuteScalar();
                    return val != null && val != DBNull.Value ? Convert.ToInt32(val) : 1;
                }
            }
        }
    }
}
