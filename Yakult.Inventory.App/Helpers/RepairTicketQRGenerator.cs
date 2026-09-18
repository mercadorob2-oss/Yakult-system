using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using QRCoder;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>QR generation for Repair Tickets, mirroring SetQRGenerator's pattern. The QR pixels
    /// only ever encode the short token URI ("yakult:repair:v1:{guid}") — the descriptive JSON built
    /// by BuildQRDataString is a separate audit payload stored in dbo.RepairTicket.QRData, not what
    /// gets scanned.</summary>
    public static class RepairTicketQRGenerator
    {
        public static Bitmap GenerateQRCodeImage(Guid qrToken)
        {
            string qrData = $"yakult:repair:v1:{qrToken:D}";

            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCode(qrCodeData);

            return qrCode.GetGraphic(5);
        }

        public static byte[] GetQRCodeImageBytes(Bitmap qrImage)
        {
            using (var ms = new MemoryStream())
            {
                qrImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>Builds the descriptive JSON stored alongside the QR image — content similar to
        /// the printed Repair Report (asset info, requester org, conclusion, item disposition).</summary>
        public static string BuildQRDataString(RepairTicketDetail detail, RepairReportData reportData)
        {
            var qr = new QrRepairTicket
            {
                TicketCode = detail?.TicketCode,
                ItemName = reportData?.ItemName ?? detail?.ItemNameSnapshot,
                SerialNumber = reportData?.SerialNumber ?? detail?.ItemSerialSnapshot,
                ModelNumber = reportData?.ModelNumber ?? detail?.ModelNumber,
                Category = reportData?.Category ?? detail?.Category,
                RequesterName = detail?.RequestedByEmpName ?? detail?.RequestedByDeptName,
                Department = detail?.RequestedByDeptName ?? detail?.DeptName,
                Branch = detail?.RequestedByBranchName ?? detail?.BranchName,
                Company = detail?.RequestedByComName ?? detail?.CompanyName,
                Status = detail?.Status,
                DateReceived = detail?.DateReceived.ToString("yyyy-MM-dd HH:mm"),
                DateCompleted = detail?.CompletedAt.HasValue == true ? detail.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm") : null,
                TechnicianName = detail?.AssignedTechName,
                RootCause = reportData?.RootCause,
                WorkPerformed = reportData?.WorkPerformed,
                Recommendations = reportData?.Recommendations,
                ItemDisposition = reportData?.ItemDispositionText
            };

            return JsonConvert.SerializeObject(qr);
        }

        private class QrRepairTicket
        {
            [JsonProperty("ticket_code")] public string TicketCode { get; set; }
            [JsonProperty("item_name")] public string ItemName { get; set; }
            [JsonProperty("serial_number")] public string SerialNumber { get; set; }
            [JsonProperty("model_number")] public string ModelNumber { get; set; }
            [JsonProperty("category")] public string Category { get; set; }
            [JsonProperty("requester_name")] public string RequesterName { get; set; }
            [JsonProperty("department")] public string Department { get; set; }
            [JsonProperty("branch")] public string Branch { get; set; }
            [JsonProperty("company")] public string Company { get; set; }
            [JsonProperty("status")] public string Status { get; set; }
            [JsonProperty("date_received")] public string DateReceived { get; set; }
            [JsonProperty("date_completed")] public string DateCompleted { get; set; }
            [JsonProperty("technician_name")] public string TechnicianName { get; set; }
            [JsonProperty("root_cause")] public string RootCause { get; set; }
            [JsonProperty("work_performed")] public string WorkPerformed { get; set; }
            [JsonProperty("recommendations")] public string Recommendations { get; set; }
            [JsonProperty("item_disposition")] public string ItemDisposition { get; set; }
        }
    }
}
