using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using QRCoder;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using Yakult.Inventory.App.Pages; // Assuming these DTOs are defined elsewhere

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Helper class for generating QR codes and dispatch PDFs for Sets
    /// </summary>
    public static class SetQRGenerator
    {
        /// <summary>
        /// Generates the QR data JSON string for a Set (PUBLIC METHOD)
        /// This is the JSON that will be encoded in the QR code and saved to the database
        /// </summary>
        /// <param name="setDto">The Set information</param>
        /// <param name="requests">List of requests in the set</param>
        /// <param name="employeeDetail">Employee details including Company, Department, Branch</param>
        /// <returns>JSON string containing all set data</returns>
        public static string GenerateQRDataString(SetDto setDto, List<SetDetailRequestDto> requests, EmployeeDetailDto employeeDetail)
        {
            return BuildQRDataString(setDto, requests, employeeDetail);
        }

        /// <summary>
        /// Generates a QR code image for a Set
        /// </summary>
        /// <param name="setDto">The Set information</param>
        /// <param name="requests">List of requests in the set</param>
        /// <param name="employeeDetail">Employee details including Company, Department, Branch</param>
        /// <returns>Bitmap image of the QR code</returns>
        public static Bitmap GenerateQRCodeImage(SetDto setDto, List<SetDetailRequestDto> requests, EmployeeDetailDto employeeDetail)
        {
            // Build QR data string as JSON
            string qrData = setDto != null && setDto.QRToken != Guid.Empty
                ? $"yakult:set:v1:{setDto.QRToken:D}"
                : BuildQRDataString(setDto, requests, employeeDetail);

            // Generate QR code using QRCoder library
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);

            // Generate bitmap with 5 pixels per module
            Bitmap qrCodeImage = qrCode.GetGraphic(5);

            return qrCodeImage;
        }

        /// <summary>
        /// Encodes the QR code bitmap as PNG bytes for storage in the database
        /// </summary>
        /// <param name="qrImage">The QR code bitmap</param>
        /// <returns>PNG-encoded image bytes</returns>
        public static byte[] GetQRCodeImageBytes(Bitmap qrImage)
        {
            using (var ms = new MemoryStream())
            {
                qrImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds the QR data string with Set information as JSON
        /// </summary>
        private static string BuildQRDataString(SetDto setDto, List<SetDetailRequestDto> requests, EmployeeDetailDto employeeDetail)
        {
            // Map the C# DTOs to our JSON-friendly QrDispatchSet and QrDispatchItem objects
            var qrItems = new List<QrDispatchItem>();
            int count = 1;
            foreach (var request in requests)
            {
                qrItems.Add(new QrDispatchItem
                {
                    Id = count++,
                    Type = request.ItemName,
                    Category = request.Category ?? "N/A",
                    Quantity = request.Quantity,
                    SerialNumber = request.SerialNumber ?? "N/A",
                    ModelNumber = request.ModelNumber ?? "N/A",
                    Description = request.Description ?? "N/A", // or " "
                    ComputerName = null,
                    IPAddress = null,
                    Status = request.Status ?? "N/A"
                });
            }

            string hardwareComputer = string.Equals(setDto?.SetType, "Hardware", StringComparison.OrdinalIgnoreCase)
                ? setDto?.ComputerName
                : null;
            string hardwareIp = string.Equals(setDto?.SetType, "Hardware", StringComparison.OrdinalIgnoreCase)
                ? setDto?.IPAddress
                : null;

            var qrSet = new QrDispatchSet
            {
                SetCode = setDto.SetCode,
                Employee = employeeDetail?.EmployeeName ?? "Unknown",
                EmployeeNumber = employeeDetail?.EmployeeNumber,
                Position = employeeDetail?.Position ?? "N/A",
                Department = employeeDetail?.DepartmentName ?? "N/A",
                Branch = employeeDetail?.BranchName ?? "N/A",
                Status = setDto?.SetStatus ?? "Pending",
                CreatedBy = setDto.CreatedByName ?? "N/A", // Assuming CreatedByName from setDto
                Company = employeeDetail?.CompanyName ?? "N/A",
                Logistics = "Internal Logistics", // Placeholder, adjust if you have this in employeeDetail
                DispatchDate = setDto.DispatchDate.HasValue ? setDto.DispatchDate.Value.ToString("yyyy-MM-dd") : "Not Set",
                CreatedDate = setDto.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                HardwareComputerName = hardwareComputer,
                HardwareIPAddress = hardwareIp,
                Items = qrItems,
                ImageCount = setDto?.ImageCount ?? 0
            };

            // Serialize the object to a JSON string
            return JsonConvert.SerializeObject(qrSet);
        }

        /// <summary>
        /// Generates a dispatch PDF with QR code
        /// </summary>
        /// <param name="setDto">Set information</param>
        /// <param name="requests">List of requests in the set</param>
        /// <param name="employeeDetail">Employee details including Company, Department, Branch</param>
        /// <param name="qrImageBytes">PNG bytes of the QR code image</param>
        /// <returns>Path to generated PDF</returns>
        public static string GenerateDispatchPDF(SetDto setDto, List<SetDetailRequestDto> requests,
            EmployeeDetailDto employeeDetail, byte[] qrImageBytes)
        {
            // Create PDF document
            PdfDocument document = new PdfDocument();
            document.Info.Title = $"Dispatch Receipt - {setDto.SetCode}";
            document.Info.Author = "Yakult Inventory System";
            document.Info.Subject = "Dispatch Receipt";

            // Add a page
            PdfPage page = document.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);

            // Define fonts
            XFont fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
            XFont fontHeader = new XFont("Arial", 12, XFontStyle.Bold);
            XFont fontNormal = new XFont("Arial", 10, XFontStyle.Regular);
            XFont fontSmall = new XFont("Arial", 8, XFontStyle.Regular);


            // Starting positions
            double x = 50;
            double y = 50;
            double lineHeight = 15;

            // TITLE
            gfx.DrawString("YAKULT INVENTORY DISPATCH RECEIPT", fontTitle, XBrushes.Black, x, y);
            y += 30;

            // Horizontal line
            gfx.DrawLine(XPens.Gray, x, y, page.Width.Point - x, y);
            y += 20;

            // SET INFORMATION
            gfx.DrawString("SET INFORMATION", fontHeader, XBrushes.DarkBlue, x, y);
            y += lineHeight + 5;

            gfx.DrawString($"Set Code: {setDto.SetCode}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Employee: {employeeDetail?.EmployeeName ?? "Unknown"}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            if (!string.IsNullOrWhiteSpace(employeeDetail?.EmployeeNumber))
            {
                gfx.DrawString($"Employee Number: {employeeDetail.EmployeeNumber}", fontNormal, XBrushes.Black, x, y);
                y += lineHeight;
            }

            if (!string.IsNullOrWhiteSpace(employeeDetail?.Position))
            {
                gfx.DrawString($"Position: {employeeDetail.Position}", fontNormal, XBrushes.Black, x, y);
                y += lineHeight;
            }

            gfx.DrawString($"Company: {employeeDetail?.CompanyName ?? "N/A"}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Department: {employeeDetail?.DepartmentName ?? "N/A"}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Branch: {employeeDetail?.BranchName ?? "N/A"}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Dispatch Date: {(setDto.DispatchDate.HasValue ? setDto.DispatchDate.Value.ToString("yyyy-MM-dd") : "Not Set")}",
                fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Status: {setDto.Status ?? "Pending"}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Created: {setDto.CreatedAt:yyyy-MM-dd HH:mm}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight;

            gfx.DrawString($"Created By: {setDto.CreatedByName}", fontNormal, XBrushes.Black, x, y);
            y += lineHeight + 10;

            if (!string.IsNullOrWhiteSpace(setDto.Remarks))
            {
                gfx.DrawString($"Remarks: {setDto.Remarks}", fontNormal, XBrushes.DarkGray, x, y);
                y += lineHeight + 10;
            }

            // ITEMS TABLE
            y += 10;
            gfx.DrawString("ITEMS IN THIS SET", fontHeader, XBrushes.DarkBlue, x, y);
            y += lineHeight + 5;

            double tableWidth = page.Width.Point - (x * 2);
            double itemColWidth = 185;
            double categoryColWidth = 95;
            double qtyColWidth = 35;
            double serialColWidth = 115;
            double modelColWidth = tableWidth - itemColWidth - categoryColWidth - qtyColWidth - serialColWidth;
            double rowPadding = 4;
            double itemLineHeight = gfx.MeasureString("Ag", fontNormal).Height + 1;
            double headerHeight = 20;

            Action drawItemHeader = () =>
            {
                gfx.DrawRectangle(XBrushes.WhiteSmoke, x, y, tableWidth, headerHeight);
                gfx.DrawRectangle(XPens.Gray, x, y, tableWidth, headerHeight);

                double headerX = x;
                DrawTableHeaderCell(gfx, "ITEM DETAILS", fontHeader, headerX, y, itemColWidth, headerHeight, rowPadding);
                headerX += itemColWidth;
                DrawTableHeaderCell(gfx, "CATEGORY", fontHeader, headerX, y, categoryColWidth, headerHeight, rowPadding);
                headerX += categoryColWidth;
                DrawTableHeaderCell(gfx, "QTY", fontHeader, headerX, y, qtyColWidth, headerHeight, rowPadding);
                headerX += qtyColWidth;
                DrawTableHeaderCell(gfx, "SERIAL", fontHeader, headerX, y, serialColWidth, headerHeight, rowPadding);
                headerX += serialColWidth;
                DrawTableHeaderCell(gfx, "MODEL", fontHeader, headerX, y, modelColWidth, headerHeight, rowPadding);

                y += headerHeight;
            };

            drawItemHeader();

            // Table rows
            foreach (var request in requests)
            {
                string itemDetails = BuildItemDetailsText(request);
                var itemLines = WrapText(gfx, itemDetails, fontNormal, itemColWidth - rowPadding * 2);
                var categoryLines = WrapText(gfx, request.Category ?? "N/A", fontNormal, categoryColWidth - rowPadding * 2);
                var qtyLines = WrapText(gfx, request.Quantity.ToString(), fontNormal, qtyColWidth - rowPadding * 2);
                var serialLines = WrapText(gfx, request.SerialNumber ?? "N/A", fontNormal, serialColWidth - rowPadding * 2);
                var modelLines = WrapText(gfx, request.ModelNumber ?? "N/A", fontNormal, modelColWidth - rowPadding * 2);

                int maxLineCount = Math.Max(itemLines.Count,
                    Math.Max(categoryLines.Count,
                    Math.Max(qtyLines.Count, Math.Max(serialLines.Count, modelLines.Count))));
                double rowHeight = Math.Max(lineHeight + rowPadding * 2, maxLineCount * itemLineHeight + rowPadding * 2);

                if (y + rowHeight > page.Height.Point - 150)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = 50;
                    drawItemHeader();
                }

                double cellX = x;
                gfx.DrawRectangle(XPens.LightGray, cellX, y, itemColWidth, rowHeight);
                DrawWrappedLines(gfx, itemLines, fontNormal, XBrushes.Black, cellX + rowPadding, y + rowPadding, itemLineHeight);
                cellX += itemColWidth;

                gfx.DrawRectangle(XPens.LightGray, cellX, y, categoryColWidth, rowHeight);
                DrawWrappedLines(gfx, categoryLines, fontNormal, XBrushes.Black, cellX + rowPadding, y + rowPadding, itemLineHeight);
                cellX += categoryColWidth;

                gfx.DrawRectangle(XPens.LightGray, cellX, y, qtyColWidth, rowHeight);
                DrawWrappedLines(gfx, qtyLines, fontNormal, XBrushes.Black, cellX + rowPadding, y + rowPadding, itemLineHeight);
                cellX += qtyColWidth;

                gfx.DrawRectangle(XPens.LightGray, cellX, y, serialColWidth, rowHeight);
                DrawWrappedLines(gfx, serialLines, fontNormal, XBrushes.Black, cellX + rowPadding, y + rowPadding, itemLineHeight);
                cellX += serialColWidth;

                gfx.DrawRectangle(XPens.LightGray, cellX, y, modelColWidth, rowHeight);
                DrawWrappedLines(gfx, modelLines, fontNormal, XBrushes.Black, cellX + rowPadding, y + rowPadding, itemLineHeight);

                y += rowHeight;
            }

            y += 20;

            if (y + 190 > page.Height.Point - 50)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = 50;
            }

            // QR CODE
            if (qrImageBytes != null && qrImageBytes.Length > 0)
            {
                using (var qrStream = new MemoryStream(qrImageBytes))
                {
                    XImage qrImage = XImage.FromStream(qrStream);
                    gfx.DrawImage(qrImage, x, y, 120, 120);
                    gfx.DrawString("Scan to verify dispatch", fontSmall, XBrushes.Gray, x + 10, y + 130);
                    y += 150;
                }
            }

            // SIGNATURES
            y += 20;
            if (y + 115 > page.Height.Point - 50)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = 50;
            }

            gfx.DrawString("SIGNATURES", fontHeader, XBrushes.DarkBlue, x, y);
            y += lineHeight + 10;

            gfx.DrawString("Employee Signature:", fontNormal, XBrushes.Black, x, y);
            gfx.DrawLine(XPens.Black, x + 150, y, x + 350, y);
            y += lineHeight + 20;

            gfx.DrawString("Date Received:", fontNormal, XBrushes.Black, x, y);
            gfx.DrawLine(XPens.Black, x + 150, y, x + 350, y);
            y += lineHeight + 30;

            gfx.DrawString("Dispatcher Signature:", fontNormal, XBrushes.Black, x, y);
            gfx.DrawLine(XPens.Black, x + 150, y, x + 350, y);

            // FOOTER
            y = page.Height.Point - 30;
            gfx.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", fontSmall, XBrushes.Gray, x, y);
            gfx.DrawString("Yakult Inventory Management System", fontSmall, XBrushes.Gray,
                page.Width.Point - 200, y);

            // Save PDF
            string pdfFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DispatchPDFs");

            if (!Directory.Exists(pdfFolder))
            {
                Directory.CreateDirectory(pdfFolder);
            }

            string filename = $"Dispatch_{setDto.SetCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string pdfPath = Path.Combine(pdfFolder, filename);

            document.Save(pdfPath);

            return pdfPath;
        }

        /// <summary>
        /// Helper to truncate long strings
        /// </summary>
        private static string TruncateString(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private static void DrawTableHeaderCell(XGraphics gfx, string text, XFont font, double x, double y, double width, double height, double padding)
        {
            gfx.DrawRectangle(XPens.Gray, x, y, width, height);
            gfx.DrawString(text, font, XBrushes.DarkRed,
                new XRect(x + padding, y + 3, width - padding * 2, height - 6),
                XStringFormats.TopLeft);
        }

        private static string BuildItemDetailsText(SetDetailRequestDto request)
        {
            var details = new List<string>();
            details.Add(string.IsNullOrWhiteSpace(request?.ItemName) ? "N/A" : request.ItemName);

            if (!string.IsNullOrWhiteSpace(request?.Description))
                details.Add("Description: " + request.Description);

            if (!string.IsNullOrWhiteSpace(request?.Status))
                details.Add("Status: " + request.Status);

            return string.Join(Environment.NewLine, details);
        }

        private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                lines.Add("N/A");
                return lines;
            }

            var paragraphs = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var paragraph in paragraphs)
            {
                var words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string current = string.Empty;
                foreach (var word in words)
                {
                    var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                    if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                    {
                        current = candidate;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(current))
                    {
                        lines.Add(current);
                        current = string.Empty;
                    }

                    if (gfx.MeasureString(word, font).Width <= maxWidth)
                    {
                        current = word;
                        continue;
                    }

                    foreach (var part in BreakLongWord(gfx, word, font, maxWidth))
                        lines.Add(part);
                }

                if (!string.IsNullOrEmpty(current))
                    lines.Add(current);
            }

            return lines.Count == 0 ? new List<string> { "N/A" } : lines;
        }

        private static IEnumerable<string> BreakLongWord(XGraphics gfx, string word, XFont font, double maxWidth)
        {
            var current = new StringBuilder();
            foreach (char ch in word)
            {
                var candidate = current.ToString() + ch;
                if (current.Length > 0 && gfx.MeasureString(candidate, font).Width > maxWidth)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                yield return current.ToString();
        }

        private static void DrawWrappedLines(XGraphics gfx, IReadOnlyList<string> lines, XFont font, XBrush brush, double x, double y, double lineHeight)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                gfx.DrawString(lines[i], font, brush,
                    new XRect(x, y + i * lineHeight, 1000, lineHeight),
                    XStringFormats.TopLeft);
            }
        }
        // --- NEW: C# Data Transfer Objects (DTOs) for JSON serialization ---
        // These mirror the JSON structure expected by the Android app.
        private class QrDispatchItem
        {
            [JsonProperty("item_id")]
            public int Id { get; set; }

            [JsonProperty("item_type")]
            public string Type { get; set; }

            [JsonProperty("item_category")]
            public string Category { get; set; }

            [JsonProperty("quantity")]
            public int Quantity { get; set; }

            [JsonProperty("serial_number")]
            public string SerialNumber { get; set; }

            [JsonProperty("model_number")]
            public string ModelNumber { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("item_status")]
            public string Status { get; set; }

            [JsonProperty("computer_name")]
            public string ComputerName { get; set; }

            [JsonProperty("ip_address")]
            public string IPAddress { get; set; }
        }

        private class QrDispatchSet
        {
            [JsonProperty("set_code")]
            public string SetCode { get; set; }

            [JsonProperty("employee_name")]
            public string Employee { get; set; }

            [JsonProperty("employee_number")]
            public string EmployeeNumber { get; set; }

            [JsonProperty("employee_position")]
            public string Position { get; set; }

            [JsonProperty("department")]
            public string Department { get; set; }

            [JsonProperty("branch")]
            public string Branch { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("created_by")]
            public string CreatedBy { get; set; }

            [JsonProperty("company")]
            public string Company { get; set; }

            [JsonProperty("logistics_provider")]
            public string Logistics { get; set; }

            [JsonProperty("dispatch_date")]
            public string DispatchDate { get; set; }

            [JsonProperty("created_date")]
            public string CreatedDate { get; set; }

            [JsonProperty("hardware_computer_name")]
            public string HardwareComputerName { get; set; }

            [JsonProperty("hardware_ip_address")]
            public string HardwareIPAddress { get; set; }

            [JsonProperty("items")]
            public List<QrDispatchItem> Items { get; set; } = new List<QrDispatchItem>(); // Initialize to avoid null reference

            [JsonProperty("image_count")]
            public int ImageCount { get; set; }
        }
    }

    
}



