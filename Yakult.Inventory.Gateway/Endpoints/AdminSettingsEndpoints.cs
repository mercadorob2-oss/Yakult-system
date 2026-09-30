using System.Data;
using Microsoft.Data.SqlClient;
using Yakult.Inventory.Gateway.Data;
using Yakult.Inventory.Gateway.Models;
using Yakult.Inventory.Gateway.Security;

namespace Yakult.Inventory.Gateway.Endpoints;

/// <summary>
/// Company holidays and the global SMTP send switch. Ported from the desktop's
/// HolidayRepository and SystemSettingRepository (the desktop keeps its SQL for
/// direct mode and for the ITCM scheduler, so change both together).
/// </summary>
public static class AdminSettingsEndpoints
{
    private const string SmtpEnabledKey = "SmtpEnabled";

    public static void MapAdminSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // ── Holidays ────────────────────────────────────────────────────────
        api.MapGet("/holidays", async (DbSession db, CancellationToken ct) =>
        {
            var result = new List<HolidayModel>();
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                SELECT HolidayId, HolidayName, HolidayDate, HolidayType,
                       IsRecurring, Notes, IsActive, CreatedBy, CreatedDate
                FROM   dbo.CompanyHoliday
                ORDER  BY HolidayDate", con);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                result.Add(new HolidayModel
                {
                    HolidayId = r.GetInt32(0),
                    HolidayName = r.GetString(1),
                    HolidayDate = r.GetDateTime(2),
                    HolidayType = r.IsDBNull(3) ? "Regular Holiday" : r.GetString(3),
                    IsRecurring = r.GetBoolean(4),
                    Notes = r.IsDBNull(5) ? null : r.GetString(5),
                    IsActive = r.GetBoolean(6),
                    CreatedBy = r.IsDBNull(7) ? null : r.GetInt32(7),
                    CreatedDate = r.GetDateTime(8)
                });
            }
            return Results.Ok(result);
        });

        // Active holiday dates (recurring ones expanded per year) within [start, end].
        api.MapGet("/holidays/active-dates", async (DateTime start, DateTime end, DbSession db, CancellationToken ct) =>
        {
            var dates = new HashSet<DateTime>();
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT HolidayDate, IsRecurring FROM dbo.CompanyHoliday WHERE IsActive = 1", con);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var date = r.GetDateTime(0).Date;
                if (r.GetBoolean(1))
                {
                    for (int year = start.Year; year <= end.Year; year++)
                    {
                        if (date.Month == 2 && date.Day == 29 && !DateTime.IsLeapYear(year))
                            continue; // same as the desktop's skipped invalid date
                        var d = new DateTime(year, date.Month, date.Day);
                        if (d >= start && d <= end) dates.Add(d);
                    }
                }
                else if (date >= start && date <= end)
                {
                    dates.Add(date);
                }
            }
            return Results.Ok(dates.OrderBy(d => d));
        });

        api.MapPost("/holidays", async (HolidayModel dto, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!access.CanEditHolidays()) return Http.Forbidden();
            if (string.IsNullOrWhiteSpace(dto.HolidayName)) return Http.BadRequest("Holiday name is required.");

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                INSERT INTO dbo.CompanyHoliday
                    (HolidayName, HolidayDate, HolidayType, IsRecurring, Notes, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@Name, @Date, @Type, @Recurring, @Notes, 1, @CreatedBy, GETDATE());
                SELECT SCOPE_IDENTITY();", con);
            AddHolidayParameters(cmd, dto);
            cmd.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = db.UserId;
            return Results.Ok(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)));
        });

        api.MapPut("/holidays/{holidayId:int}", async (int holidayId, HolidayModel dto, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!access.CanEditHolidays()) return Http.Forbidden();
            if (string.IsNullOrWhiteSpace(dto.HolidayName)) return Http.BadRequest("Holiday name is required.");

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                UPDATE dbo.CompanyHoliday
                SET    HolidayName = @Name,
                       HolidayDate = @Date,
                       HolidayType = @Type,
                       IsRecurring = @Recurring,
                       Notes       = @Notes,
                       IsActive    = @IsActive
                WHERE  HolidayId   = @Id", con);
            AddHolidayParameters(cmd, dto);
            cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = dto.IsActive;
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = holidayId;
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.Ok();
        });

        api.MapDelete("/holidays/{holidayId:int}", async (int holidayId, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!access.CanEditHolidays()) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand("DELETE FROM dbo.CompanyHoliday WHERE HolidayId = @Id", con);
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = holidayId;
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.Ok();
        });

        // ── SMTP send switch ────────────────────────────────────────────────
        // Defaults to enabled when the table or row is missing, like the desktop,
        // so a settings problem never blocks email.
        api.MapGet("/settings/smtp-enabled", async (DbSession db, ILogger<DbSession> log, CancellationToken ct) =>
        {
            try
            {
                await using var con = await db.OpenAsync(ct);
                if (!await TableExistsAsync(con, "SystemSetting", ct))
                    return Results.Ok(true);

                await using var cmd = new SqlCommand("SELECT TOP 1 SettingValue FROM dbo.SystemSetting WHERE SettingKey = @Key", con);
                cmd.Parameters.AddWithValue("@Key", SmtpEnabledKey);
                var result = await cmd.ExecuteScalarAsync(ct);
                return Results.Ok(result == null || result == DBNull.Value || result.ToString() == "1");
            }
            catch (SqlException ex)
            {
                log.LogWarning(ex, "SMTP switch read failed; reporting enabled.");
                return Results.Ok(true);
            }
        });

        api.MapPut("/settings/smtp-enabled", async (SmtpEnabledRequest request, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!access.CanEditSmtpSwitch()) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            if (!await TableExistsAsync(con, "SystemSetting", ct))
                return Http.BadRequest("Table dbo.SystemSetting not found. Please run Migration_SystemSetting_CreateAndSeedSmtpToggle.sql first.");

            await using var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM dbo.SystemSetting WHERE SettingKey = @Key)
                    UPDATE dbo.SystemSetting
                    SET    SettingValue      = @Value,
                           DateModified      = @DateModified,
                           ModifiedByUserId  = @ModifiedByUserId
                    WHERE  SettingKey = @Key
                ELSE
                    INSERT INTO dbo.SystemSetting
                           (SettingKey, SettingValue, Description, ModifiedByUserId)
                    VALUES (@Key, @Value,
                            N'Global SMTP send toggle. ''1'' = outgoing email enabled; ''0'' = all SMTP dispatch suppressed system-wide.',
                            @ModifiedByUserId)", con);
            cmd.Parameters.AddWithValue("@Key", SmtpEnabledKey);
            cmd.Parameters.AddWithValue("@Value", request.Enabled ? "1" : "0");
            cmd.Parameters.AddWithValue("@DateModified", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@ModifiedByUserId", db.UserId);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.Ok();
        });
    }

    private static void AddHolidayParameters(SqlCommand cmd, HolidayModel dto)
    {
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = dto.HolidayName!.Trim();
        cmd.Parameters.Add("@Date", SqlDbType.Date).Value = dto.HolidayDate.Date;
        cmd.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = dto.HolidayType ?? "Regular Holiday";
        cmd.Parameters.Add("@Recurring", SqlDbType.Bit).Value = dto.IsRecurring;
        cmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 255).Value = (object?)dto.Notes ?? DBNull.Value;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection con, string tableName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM sys.objects o
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE s.name = 'dbo' AND o.name = @TableName AND o.type = 'U'
            ) THEN 1 ELSE 0 END", con);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }
}
