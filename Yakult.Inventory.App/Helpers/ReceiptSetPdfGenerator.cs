using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Helpers
{
    public static class ReceiptSetPdfGenerator
    {
        /// <summary>
        /// Optional lookup: (receiptSetId, docType) -> all images for that document type, in display
        /// order. When null, falls back to the legacy single SiImage/DrImage/PoImage columns so the
        /// generator keeps working even if the multi-image migration hasn't run yet.
        /// </summary>
        public static Func<int, string, IReadOnlyList<ReceiptSetImageDto>> ImageProvider { get; set; }

        private static List<ReceiptSetImageDto> ResolveImagesForType(ReceiptSetDto dto, string docType)
        {
            if (dto == null)
                return new List<ReceiptSetImageDto>();

            if (ImageProvider != null)
            {
                try
                {
                    var fromProvider = ImageProvider(dto.ReceiptSetId, docType);
                    if (fromProvider != null && fromProvider.Count > 0)
                        return fromProvider.ToList();
                }
                catch
                {
                    // fall through to legacy below
                }
            }

            // Legacy fallback: a single image per type, taken from ReceiptSetDto's own columns.
            byte[] bytes;
            string path;
            switch (docType)
            {
                case "SI": bytes = dto.SiImage; path = dto.SiImagePath; break;
                case "DR": bytes = dto.DrImage; path = dto.DrImagePath; break;
                case "PO": bytes = dto.PoImage; path = dto.PoImagePath; break;
                default: return new List<ReceiptSetImageDto>();
            }

            if (!HasImage(bytes, path))
                return new List<ReceiptSetImageDto>();

            return new List<ReceiptSetImageDto>
            {
                new ReceiptSetImageDto { ReceiptSetId = dto.ReceiptSetId, DocType = docType, ImageBytes = bytes, ImagePath = path, SortOrder = 0 }
            };
        }

        public static int GetPageCount(IReadOnlyList<ReceiptSetDto> receiptSets)
        {
            if (receiptSets == null || receiptSets.Count == 0)
                return 0;

            int pages = 0;
            foreach (var dto in receiptSets)
            {
                if (dto == null)
                    continue;

                int images = ResolveImagesForType(dto, "SI").Count
                           + ResolveImagesForType(dto, "DR").Count
                           + ResolveImagesForType(dto, "PO").Count;

                pages += Math.Max(1, images);
            }

            return pages;
        }

        public static void GenerateReceiptSetPdf(ReceiptSetDto receiptSet, string outputPath)
        {
            if (receiptSet == null) throw new ArgumentNullException(nameof(receiptSet));
            GenerateReceiptSetsPdf(new List<ReceiptSetDto> { receiptSet }, outputPath, title: "Receipt Set");
        }

        public static void GenerateReceiptSetsPdf(IReadOnlyList<ReceiptSetDto> receiptSets, string outputPath, string title = "Receipt Sets")
        {
            if (receiptSets == null) throw new ArgumentNullException(nameof(receiptSets));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safe = receiptSets.Where(s => s != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Inventory Receipt Sets Export",
                    Author = "Yakult.Inventory.App"
                }
            };

            foreach (var dto in safe)
            {
                AddPagesForReceipt(doc, dto, title);
            }

            doc.Save(outputPath);
        }

        private static void AddPagesForReceipt(PdfDocument doc, ReceiptSetDto dto, string title)
        {
            if (doc == null || dto == null)
                return;

            var siImages = ResolveImagesForType(dto, "SI");
            var drImages = ResolveImagesForType(dto, "DR");
            var poImages = ResolveImagesForType(dto, "PO");

            bool hasAny = siImages.Count > 0 || drImages.Count > 0 || poImages.Count > 0;

            if (!hasAny)
            {
                AddSingleImagePage(doc, dto, title, slotTitle: "No Images", slotValue: string.Empty, blobBytes: null, imagePath: null, emptyMessage: "No SI/DR/PO images attached.");
                return;
            }

            AddPagesForDocType(doc, dto, title, "SI", NormalizeText(dto.SiNumber), siImages, "No SI image attached.");
            AddPagesForDocType(doc, dto, title, "DR", NormalizeText(dto.DrNumber), drImages, "No DR image attached.");
            AddPagesForDocType(doc, dto, title, "PO", NormalizeText(dto.PoNumber), poImages, "No PO image attached.");
        }

        private static void AddPagesForDocType(
            PdfDocument doc,
            ReceiptSetDto dto,
            string title,
            string docType,
            string docNumber,
            List<ReceiptSetImageDto> images,
            string emptyMessage)
        {
            if (images == null || images.Count == 0)
                return;

            for (int i = 0; i < images.Count; i++)
            {
                var slotTitle = images.Count > 1
                    ? $"{docType} Image ({i + 1}/{images.Count})"
                    : $"{docType} Image";

                AddSingleImagePage(doc, dto, title, slotTitle: slotTitle, slotValue: docNumber,
                    blobBytes: images[i].ImageBytes, imagePath: images[i].ImagePath, emptyMessage: emptyMessage);
            }
        }

        private static void AddSingleImagePage(
            PdfDocument doc,
            ReceiptSetDto dto,
            string title,
            string slotTitle,
            string slotValue,
            byte[] blobBytes,
            string imagePath,
            string emptyMessage)
        {
            var page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Portrait;

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                DrawReceiptImagePage(gfx, page, dto, title, slotTitle, slotValue, blobBytes, imagePath, emptyMessage);
            }
        }

        private static void DrawReceiptImagePage(
            XGraphics gfx,
            PdfPage page,
            ReceiptSetDto dto,
            string title,
            string slotTitle,
            string slotValue,
            byte[] blobBytes,
            string imagePath,
            string emptyMessage)
        {
            const double margin = 22;
            var y = margin;

            var fontTitle = new XFont("Segoe UI", 14, XFontStyle.Bold);
            var fontMeta = new XFont("Segoe UI", 9, XFontStyle.Regular);
            var fontLabel = new XFont("Segoe UI", 9, XFontStyle.Bold);
            var fontValue = new XFont("Segoe UI", 9, XFontStyle.Regular);
            var fontSmall = new XFont("Segoe UI", 8, XFontStyle.Regular);
            var fontSlot = new XFont("Segoe UI", 10, XFontStyle.Bold);

            var penBorder = new XPen(XColor.FromArgb(220, 220, 220), 0.8);
            var brushHeader = new XSolidBrush(XColor.FromArgb(245, 247, 250));

            var contentWidth = page.Width - margin * 2;

            var status = GetReceiptStatus(dto);
            var header = $"{title}  •  #{dto.ReceiptSetId}  •  {status}";
            gfx.DrawString(header, fontTitle, XBrushes.Black, new XRect(margin, y, contentWidth, 22), XStringFormats.TopLeft);
            y += 24;

            var meta = $"Created: {dto.CreatedAt:yyyy-MM-dd HH:mm}";
            gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, contentWidth, 14), XStringFormats.TopLeft);
            y += 18;

            y = DrawKeyValue(gfx, margin, y, contentWidth, "Linked Sets", NormalizeText(dto.SetCode), fontLabel, fontValue);
            y = DrawKeyValue(gfx, margin, y, contentWidth, "Supplier", NormalizeText(dto.Supplier), fontLabel, fontValue);
            y = DrawKeyValue(gfx, margin, y, contentWidth, "SI #", NormalizeText(dto.SiNumber), fontLabel, fontValue);
            y = DrawKeyValue(gfx, margin, y, contentWidth, "DR #", NormalizeText(dto.DrNumber), fontLabel, fontValue);
            y = DrawKeyValue(gfx, margin, y, contentWidth, "PO #", NormalizeText(dto.PoNumber), fontLabel, fontValue);

            y += 12;

            var slotHeader = string.IsNullOrWhiteSpace(slotValue) ? slotTitle : $"{slotTitle}  •  {slotValue}";
            gfx.DrawString(slotHeader, fontSlot, XBrushes.Black, new XRect(margin, y, contentWidth, 18), XStringFormats.TopLeft);
            y += 22;

            var imgRect = new XRect(margin, y, contentWidth, Math.Max(240, page.Height - margin - y - 20));
            DrawSingleImage(gfx, imgRect, slotTitle, blobBytes, imagePath, emptyMessage, penBorder, brushHeader, fontLabel, fontSmall);

            // Footer
            var footerY = page.Height - margin - 12;
            gfx.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", fontSmall, XBrushes.Gray,
                new XRect(margin, footerY, contentWidth, 12), XStringFormats.TopLeft);
        }

        private static double DrawKeyValue(XGraphics gfx, double x, double y, double width, string label, string value, XFont fontLabel, XFont fontValue)
        {
            const double rowH = 16;
            const double labelW = 90;

            gfx.DrawString(label + ":", fontLabel, XBrushes.Black, new XRect(x, y, labelW, rowH), XStringFormats.TopLeft);
            gfx.DrawString(string.IsNullOrWhiteSpace(value) ? "—" : value, fontValue, XBrushes.Black, new XRect(x + labelW, y, width - labelW, rowH), XStringFormats.TopLeft);
            return y + rowH;
        }

        private static void DrawSingleImage(
            XGraphics gfx,
            XRect rect,
            string title,
            byte[] blobBytes,
            string imagePath,
            string emptyMessage,
            XPen borderPen,
            XBrush headerBrush,
            XFont fontHeader,
            XFont fontSmall)
        {
            // Outer
            gfx.DrawRectangle(borderPen, rect);

            // Header
            const double headerH = 20;
            gfx.DrawRectangle(headerBrush, rect.X, rect.Y, rect.Width, headerH);
            gfx.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, headerH);
            gfx.DrawString(title, fontHeader, XBrushes.Black, new XRect(rect.X + 6, rect.Y + 3, rect.Width - 12, headerH - 6), XStringFormats.TopLeft);

            var content = new XRect(rect.X + 6, rect.Y + headerH + 6, rect.Width - 12, rect.Height - headerH - 12);

            byte[] raw = GetImageBytesRaw(blobBytes, imagePath);
            if (raw == null || raw.Length == 0)
            {
                gfx.DrawString(string.IsNullOrWhiteSpace(emptyMessage) ? "No image" : emptyMessage, fontSmall, XBrushes.Gray, content, XStringFormats.Center);
                return;
            }

            byte[] jpeg = NormalizeToJpeg(raw, maxEdge: 2200, quality: 85L);
            if (jpeg == null || jpeg.Length == 0)
            {
                gfx.DrawString("Invalid image", fontSmall, XBrushes.Gray, content, XStringFormats.Center);
                return;
            }

            string tmp = null;
            try
            {
                tmp = WriteTempJpeg(jpeg);
                using (var img = XImage.FromFile(tmp))
                {
                    var fit = FitRect(content, img.PixelWidth, img.PixelHeight);
                    gfx.DrawImage(img, fit);
                }
            }
            catch
            {
                gfx.DrawString("Failed to load image", fontSmall, XBrushes.Gray, content, XStringFormats.Center);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
        }

        private static XRect FitRect(XRect bounds, int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0)
                return bounds;

            var scale = Math.Min(bounds.Width / pixelWidth, bounds.Height / pixelHeight);
            var w = pixelWidth * scale;
            var h = pixelHeight * scale;
            var x = bounds.X + (bounds.Width - w) / 2;
            var y = bounds.Y + (bounds.Height - h) / 2;
            return new XRect(x, y, w, h);
        }

        private static string WriteTempJpeg(byte[] bytes)
        {
            var path = Path.Combine(Path.GetTempPath(), "YakultReceipt_" + Guid.NewGuid().ToString("N") + ".jpg");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static string NormalizeText(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            return s.Trim();
        }

        private static string GetReceiptStatus(ReceiptSetDto dto)
        {
            if (dto == null)
                return "Pending";

            bool hasSi = ResolveImagesForType(dto, "SI").Count > 0;
            bool hasDr = ResolveImagesForType(dto, "DR").Count > 0;
            bool hasPo = ResolveImagesForType(dto, "PO").Count > 0;
            return (hasSi && hasDr && hasPo) ? "Complete" : "Pending";
        }

        private static bool HasImage(byte[] blobBytes, string imagePath)
        {
            if (blobBytes != null && blobBytes.Length > 0)
                return true;

            return !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath);
        }

        private static byte[] GetImageBytesRaw(byte[] blobBytes, string imagePath)
        {
            if (blobBytes != null && blobBytes.Length > 0)
                return blobBytes;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return null;

            try
            {
                using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static byte[] NormalizeToJpeg(byte[] imageBytes, int maxEdge, long quality)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return null;

            try
            {
                using (var ms = new MemoryStream(imageBytes))
                using (var img = Image.FromStream(ms))
                using (var scaled = ScaleImage(img, maxEdge))
                {
                    return EncodeJpeg(scaled, quality);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap ScaleImage(Image img, int maxEdge)
        {
            if (img == null)
                return null;

            var w = img.Width;
            var h = img.Height;
            if (w <= 0 || h <= 0)
                return null;

            if (maxEdge <= 0 || (w <= maxEdge && h <= maxEdge))
            {
                return new Bitmap(img);
            }

            var ratio = (double)maxEdge / Math.Max(w, h);
            var newW = Math.Max(1, (int)Math.Round(w * ratio));
            var newH = Math.Max(1, (int)Math.Round(h * ratio));

            var bmp = new Bitmap(newW, newH);
            bmp.SetResolution(img.HorizontalResolution, img.VerticalResolution);

            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.White);
                g.DrawImage(img, 0, 0, newW, newH);
            }

            return bmp;
        }

        private static byte[] EncodeJpeg(Image img, long quality)
        {
            if (img == null)
                return null;

            quality = Math.Max(30L, Math.Min(95L, quality));

            var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => string.Equals(c.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));
            if (codec == null)
            {
                using (var fallback = new MemoryStream())
                {
                    img.Save(fallback, ImageFormat.Jpeg);
                    return fallback.ToArray();
                }
            }

            using (var ms = new MemoryStream())
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                img.Save(ms, codec, ep);
                return ms.ToArray();
            }
        }
    }
}
