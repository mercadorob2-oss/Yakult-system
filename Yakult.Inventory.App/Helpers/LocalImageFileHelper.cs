using System;
using System.Collections.Generic;
using System.IO;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Shared helpers for resolving/reading local image files that were saved before
    /// image storage moved into the database (SetImages.ImageData, Set.QRImageData).
    /// </summary>
    public static class LocalImageFileHelper
    {
        private static readonly Dictionary<string, string> MimeTypesByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" }
        };

        public static string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath) ?? string.Empty;
            return MimeTypesByExtension.TryGetValue(extension, out var mime) ? mime : "application/octet-stream";
        }

        /// <summary>
        /// Resolves a SetImages.ImagePath value to an actual local file system path.
        /// Desktop uploads store an absolute path; mobile uploads store ~/App_Data/SetImages/filename.jpg,
        /// which only resolves if this machine has direct filesystem access to the IIS server's folder.
        /// </summary>
        public static string ResolveImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            if (File.Exists(imagePath))
                return imagePath;

            if (imagePath.StartsWith("~/"))
            {
                string relativePath = imagePath.Substring(2).Replace('/', Path.DirectorySeparatorChar);

                string[] possiblePaths =
                {
                    @"C:\inetpub\wwwroot\Yakult.Inventory.Api2",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Yakult.Inventory.Api2"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Yakult.Inventory.Api2", "PrecompiledWeb", "Yakult.Inventory.Api")
                };

                foreach (var basePath in possiblePaths)
                {
                    string fullPath = Path.Combine(basePath, relativePath);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            return imagePath;
        }
    }
}
