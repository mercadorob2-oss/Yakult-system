using System.Text;

namespace Inventory.RequestPortal.Extensions
{
    /// <summary>
    /// Minimal CSV builder shared by history/export actions (Request and Authorization
    /// history pages). Not a general-purpose CSV library — just RFC 4180-style quoting
    /// (quote a field containing a comma, quote, or newline; double up embedded quotes).
    /// </summary>
    public static class CsvExportExtensions
    {
        public static byte[] ToCsvBytes(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(EscapeField)));
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(v => EscapeField(v?.ToString() ?? string.Empty))));

            // UTF-8 BOM so Excel (still the primary consumer of a "download CSV" button
            // on an internal tool) auto-detects encoding instead of mangling non-ASCII text.
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[preamble.Length + body.Length];
            preamble.CopyTo(result, 0);
            body.CopyTo(result, preamble.Length);
            return result;
        }

        private static string EscapeField(string field)
        {
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }
}
