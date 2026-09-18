using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    public class HolidayDto
    {
        public int      HolidayId   { get; set; }
        public string   HolidayName { get; set; }
        public DateTime HolidayDate { get; set; }
        public string   HolidayType { get; set; }
        public bool     IsRecurring { get; set; }
        public string   Notes       { get; set; }
        public bool     IsActive    { get; set; }
        public int?     CreatedBy   { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class HolidayRepository
    {
        private readonly string _conn;

        public HolidayRepository()
        {
            _conn = DatabaseConfig.ConnectionString;
        }

        public List<HolidayDto> GetAll()
        {
            var result = new List<HolidayDto>();
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                const string sql = @"
                    SELECT HolidayId, HolidayName, HolidayDate, HolidayType,
                           IsRecurring, Notes, IsActive, CreatedBy, CreatedDate
                    FROM   dbo.CompanyHoliday
                    ORDER  BY HolidayDate";

                using (var cmd = new SqlCommand(sql, con))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        result.Add(Map(r));
                }
            }
            return result;
        }

        // Returns all active holiday dates (including expanded recurring) within [start, end].
        public HashSet<DateTime> GetActiveDatesInRange(DateTime start, DateTime end)
        {
            var dates = new HashSet<DateTime>();
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                const string sql = @"
                    SELECT HolidayDate, IsRecurring
                    FROM   dbo.CompanyHoliday
                    WHERE  IsActive = 1";

                using (var cmd = new SqlCommand(sql, con))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var date        = r.GetDateTime(0).Date;
                        var isRecurring = r.GetBoolean(1);

                        if (isRecurring)
                        {
                            // Expand recurring holiday to every year in the range
                            for (int year = start.Year; year <= end.Year; year++)
                            {
                                try
                                {
                                    var d = new DateTime(year, date.Month, date.Day);
                                    if (d >= start && d <= end) dates.Add(d);
                                }
                                catch { } // skip invalid dates (e.g., Feb 29 in non-leap year)
                            }
                        }
                        else if (date >= start && date <= end)
                        {
                            dates.Add(date);
                        }
                    }
                }
            }
            return dates;
        }

        public int Insert(HolidayDto dto)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                const string sql = @"
                    INSERT INTO dbo.CompanyHoliday
                        (HolidayName, HolidayDate, HolidayType, IsRecurring, Notes, IsActive, CreatedBy, CreatedDate)
                    VALUES
                        (@Name, @Date, @Type, @Recurring, @Notes, 1, @CreatedBy, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@Name",      SqlDbType.NVarChar, 100).Value = dto.HolidayName.Trim();
                    cmd.Parameters.Add("@Date",      SqlDbType.Date).Value           = dto.HolidayDate.Date;
                    cmd.Parameters.Add("@Type",      SqlDbType.NVarChar, 50).Value   = dto.HolidayType ?? "Regular Holiday";
                    cmd.Parameters.Add("@Recurring", SqlDbType.Bit).Value            = dto.IsRecurring;
                    cmd.Parameters.Add("@Notes",     SqlDbType.NVarChar, 255).Value  = (object)dto.Notes ?? DBNull.Value;
                    cmd.Parameters.Add("@CreatedBy", SqlDbType.Int).Value            = (object)dto.CreatedBy ?? DBNull.Value;
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Update(HolidayDto dto)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                const string sql = @"
                    UPDATE dbo.CompanyHoliday
                    SET    HolidayName = @Name,
                           HolidayDate = @Date,
                           HolidayType = @Type,
                           IsRecurring = @Recurring,
                           Notes       = @Notes,
                           IsActive    = @IsActive
                    WHERE  HolidayId   = @Id";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@Name",      SqlDbType.NVarChar, 100).Value = dto.HolidayName.Trim();
                    cmd.Parameters.Add("@Date",      SqlDbType.Date).Value           = dto.HolidayDate.Date;
                    cmd.Parameters.Add("@Type",      SqlDbType.NVarChar, 50).Value   = dto.HolidayType ?? "Regular Holiday";
                    cmd.Parameters.Add("@Recurring", SqlDbType.Bit).Value            = dto.IsRecurring;
                    cmd.Parameters.Add("@Notes",     SqlDbType.NVarChar, 255).Value  = (object)dto.Notes ?? DBNull.Value;
                    cmd.Parameters.Add("@IsActive",  SqlDbType.Bit).Value            = dto.IsActive;
                    cmd.Parameters.Add("@Id",        SqlDbType.Int).Value            = dto.HolidayId;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int holidayId)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                const string sql = "DELETE FROM dbo.CompanyHoliday WHERE HolidayId = @Id";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = holidayId;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static HolidayDto Map(IDataReader r) => new HolidayDto
        {
            HolidayId   = r.GetInt32(0),
            HolidayName = r.GetString(1),
            HolidayDate = r.GetDateTime(2),
            HolidayType = r.IsDBNull(3) ? "Regular Holiday" : r.GetString(3),
            IsRecurring = r.GetBoolean(4),
            Notes       = r.IsDBNull(5) ? null : r.GetString(5),
            IsActive    = r.GetBoolean(6),
            CreatedBy   = r.IsDBNull(7) ? (int?)null : r.GetInt32(7),
            CreatedDate = r.GetDateTime(8)
        };
    }
}
