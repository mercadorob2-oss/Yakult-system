using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// One row of the reconciliation between a beginning-balance CSV export
    /// (Description, Remaining Balance, TYPE) and live dbo.ConsumableModel stock.
    /// </summary>
    public class ConsumableStockComparisonRow
    {
        public string ModelNumber { get; set; }
        public string Category { get; set; }
        public int CsvBalance { get; set; }
        public bool FoundInDatabase { get; set; }
        public int DbAvailableStock { get; set; }
        public int OutstandingRequestedQty { get; set; }

        /// <summary>DbAvailableStock net of quantity already committed to unissued Requests.</summary>
        public int NetDbStock => DbAvailableStock - OutstandingRequestedQty;

        public int Difference => NetDbStock - CsvBalance;

        public bool IsMatch => FoundInDatabase && Difference == 0;
    }

    /// <summary>
    /// Reconciles a beginning-balance CSV export against live dbo.ConsumableModel stock,
    /// net of quantity already committed to outstanding (not-yet-issued) Requests.
    /// </summary>
    public class ConsumableStockReconciliationService
    {
        private readonly ConsumableModelRepository _consumableModelRepo;

        public ConsumableStockReconciliationService()
        {
            _consumableModelRepo = new ConsumableModelRepository();
        }

        public async Task<List<ConsumableStockComparisonRow>> CompareAsync(string csvFilePath)
        {
            var csvRows = ParseCsv(csvFilePath);

            var models = await _consumableModelRepo.GetAllActiveModelsAsync();
            var outstandingByModelId = await _consumableModelRepo.GetOutstandingRequestedQtyByModelIdAsync();

            // ModelNumber is unique-enough per row (color variants get their own row, e.g.
            // "HP LASERJET 202X (MAGENTA)") — normalize away case/whitespace only, since the
            // CSV's TYPE column is sometimes mistyped and shouldn't gate the match.
            var modelsByNumber = models
                .GroupBy(m => NormalizeModelNumber(m.ModelNumber))
                .ToDictionary(g => g.Key, g => g.First());

            var results = new List<ConsumableStockComparisonRow>();

            foreach (var csvRow in csvRows)
            {
                var row = new ConsumableStockComparisonRow
                {
                    ModelNumber = csvRow.ModelNumber,
                    Category = csvRow.Category,
                    CsvBalance = csvRow.RemainingBalance
                };

                if (modelsByNumber.TryGetValue(NormalizeModelNumber(csvRow.ModelNumber), out var model))
                {
                    row.FoundInDatabase = true;
                    row.Category = model.Category;
                    row.DbAvailableStock = model.AvailableStock;
                    row.OutstandingRequestedQty = outstandingByModelId.TryGetValue(model.ConsumableModelId, out var qty)
                        ? qty
                        : 0;
                }

                results.Add(row);
            }

            return results;
        }

        private static string NormalizeModelNumber(string modelNumber)
        {
            if (string.IsNullOrWhiteSpace(modelNumber)) return "";
            return Regex.Replace(modelNumber.Trim().ToUpperInvariant(), @"\s+", " ");
        }

        private static List<(string ModelNumber, string Category, int RemainingBalance)> ParseCsv(string filePath)
        {
            var rows = new List<(string, string, int)>();

            foreach (var line in File.ReadAllLines(filePath))
            {
                var fields = SplitCsvLine(line);
                if (fields.Count < 3) continue;

                string description = fields[0].Trim();
                if (description.Length == 0 || description.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    continue; // title row / header row

                if (!int.TryParse(fields[1].Trim(), out int balance))
                    continue; // title row has no numeric balance in the 2nd column

                string type = fields[2].Trim();
                rows.Add((description, type, balance));
            }

            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); continue; }
                current.Append(c);
            }
            fields.Add(current.ToString());

            return fields;
        }
    }
}
