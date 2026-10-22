using System.Globalization;

namespace Yakult.SystemsPortal.Extensions;

public static class PortalDisplayTime
{
    public static string Format(DateTime? utc) => utc.HasValue
        ? DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).AddHours(8)
            .ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " PHT"
        : "Not checked";

    public static string? Iso(DateTime? utc) => utc.HasValue
        ? DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture)
        : null;
}
