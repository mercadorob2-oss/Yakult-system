using System;
using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Computes analytics for the User Activity Insights form.
    /// Delegates data retrieval to <see cref="UserActivityRepository.GetFiltered"/> so
    /// the charts always reflect the exact same combined dataset shown in the main table
    /// (UserActivityLog + historical invoices, sets, renewals, branches, etc.).
    /// </summary>
    public class UserActivityInsightsService
    {
        private readonly UserActivityRepository _repository;
        private readonly HolidayRepository      _holidays;

        public UserActivityInsightsService()
        {
            _repository = new UserActivityRepository();
            _holidays   = new HolidayRepository();
        }

        // ─────────────────────────────────────────────────────────────
        //  Analytics — all computed from the same GetFiltered result
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns activity count grouped by hour of day (0–23), ordered by hour.
        /// </summary>
        public List<(int Hour, int Count)> GetMostActiveHours(UserActivityFilter filter)
        {
            return _repository.GetFiltered(filter)
                .GroupBy(r => r.CreatedDate.Hour)
                .Select(g => (Hour: g.Key, Count: g.Count()))
                .OrderBy(x => x.Hour)
                .ToList();
        }

        /// <summary>
        /// Returns the top N users by total activity count, ordered descending.
        /// </summary>
        public List<(string UserName, int Count)> GetTopUsers(UserActivityFilter filter, int topN = 10)
        {
            return _repository.GetFiltered(filter)
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Name) ? "(Unknown)" : r.Name)
                .Select(g => (UserName: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Returns activity count grouped by entity type, ordered descending by count.
        /// </summary>
        public List<(string EntityType, int Count)> GetByEntityType(UserActivityFilter filter)
        {
            return _repository.GetFiltered(filter)
                .GroupBy(r => string.IsNullOrWhiteSpace(r.EntityType) ? "(None)" : r.EntityType)
                .Select(g => (EntityType: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList();
        }

        /// <summary>
        /// Returns activity count per day for the given year/month.
        /// Builds a month-scoped filter so the query covers exactly that month,
        /// while preserving the User/Action/EntityType filters from <paramref name="filter"/>.
        /// </summary>
        public List<(int Day, int Count)> GetActivityByDayOfMonth(UserActivityFilter filter, int year, int month)
        {
            var monthFilter = new UserActivityFilter
            {
                DateFrom         = new DateTime(year, month, 1),
                DateTo           = new DateTime(year, month, DateTime.DaysInMonth(year, month)),
                UserId           = filter.UserId,
                ActionType       = filter.ActionType,
                EntityType       = filter.EntityType,
                IncludeAuditTrail = filter.IncludeAuditTrail
            };

            return _repository.GetFiltered(monthFilter)
                .GroupBy(r => r.CreatedDate.Day)
                .Select(g => (Day: g.Key, Count: g.Count()))
                .OrderBy(x => x.Day)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────
        //  Monthly Trend (independent of global date range)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns activity count per month for a full calendar year.
        /// Respects User, Action, and EntityType filters but ignores the global DateFrom/DateTo.
        /// Returns Jan → current month when year == current year; Jan → Dec otherwise.
        /// All months are included even when count is 0.
        /// </summary>
        public List<(int Month, int Count)> GetMonthlyTrendData(int year, int? userId, string actionType, string entityType)
        {
            var yearFilter = new UserActivityFilter
            {
                DateFrom   = new DateTime(year, 1, 1),
                DateTo     = new DateTime(year + 1, 1, 1), // exclusive upper bound so Dec 31 is fully included
                UserId     = userId,
                ActionType = actionType,
                EntityType = entityType
            };

            var lookup = _repository.GetFiltered(yearFilter)
                .GroupBy(r => r.CreatedDate.Month)
                .ToDictionary(g => g.Key, g => g.Count());

            int endMonth = (year == DateTime.Today.Year) ? DateTime.Today.Month : 12;
            var result   = new List<(int Month, int Count)>(endMonth);
            for (int m = 1; m <= endMonth; m++)
            {
                lookup.TryGetValue(m, out int count);
                result.Add((m, count));
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  Usage Duration Insights
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates estimated active usage by grouping consecutive activities within 15 minutes
        /// into sessions. Returns average daily, weekly, and monthly usage durations.
        /// </summary>
        public (TimeSpan AvgDaily, TimeSpan AvgWeekly, TimeSpan AvgMonthly) GetUsageDurationInsights(UserActivityFilter filter)
        {
            var data = _repository.GetFiltered(filter)
                .OrderBy(r => r.CreatedDate)
                .ToList();

            if (data.Count == 0)
                return (TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

            // Build sessions — activities within 15 min belong to the same session
            var sessions = new List<(DateTime Start, DateTime End)>();
            var sessionStart = data[0].CreatedDate;
            var sessionLast  = data[0].CreatedDate;

            for (int i = 1; i < data.Count; i++)
            {
                var curr = data[i].CreatedDate;
                if ((curr - sessionLast).TotalMinutes <= 15)
                {
                    sessionLast = curr;
                }
                else
                {
                    sessions.Add((sessionStart, sessionLast));
                    sessionStart = curr;
                    sessionLast  = curr;
                }
            }
            sessions.Add((sessionStart, sessionLast));

            DateTime StartOfWeek(DateTime dt)
            {
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-diff).Date;
            }

            var dailySeconds = sessions
                .GroupBy(s => s.Start.Date)
                .Select(g => g.Sum(s => (s.End - s.Start).TotalSeconds))
                .ToList();

            var weeklySeconds = sessions
                .GroupBy(s => StartOfWeek(s.Start))
                .Select(g => g.Sum(s => (s.End - s.Start).TotalSeconds))
                .ToList();

            var monthlySeconds = sessions
                .GroupBy(s => new { s.Start.Year, s.Start.Month })
                .Select(g => g.Sum(s => (s.End - s.Start).TotalSeconds))
                .ToList();

            var avgDaily   = dailySeconds.Count   > 0 ? TimeSpan.FromSeconds(dailySeconds.Average())   : TimeSpan.Zero;
            var avgWeekly  = weeklySeconds.Count  > 0 ? TimeSpan.FromSeconds(weeklySeconds.Average())  : TimeSpan.Zero;
            var avgMonthly = monthlySeconds.Count > 0 ? TimeSpan.FromSeconds(monthlySeconds.Average()) : TimeSpan.Zero;

            return (avgDaily, avgWeekly, avgMonthly);
        }

        // ─────────────────────────────────────────────────────────────
        //  Inactivity Insight
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the longest consecutive gap between active days.
        /// Same-day activities are de-duplicated. Returns (null, null, 0) when there is
        /// fewer than two distinct active dates.
        /// </summary>
        public (DateTime? GapStart, DateTime? GapEnd, int GapDays) GetLongestInactivity(UserActivityFilter filter)
        {
            var activeDates = _repository.GetFiltered(filter)
                .Select(r => r.CreatedDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (activeDates.Count < 2)
                return (null, null, 0);

            DateTime? bestStart = null, bestEnd = null;
            int maxDays = 0;

            for (int i = 1; i < activeDates.Count; i++)
            {
                int days = (activeDates[i] - activeDates[i - 1]).Days;
                if (days > maxDays)
                {
                    maxDays   = days;
                    bestStart = activeDates[i - 1];
                    bestEnd   = activeDates[i];
                }
            }

            return (bestStart, bestEnd, maxDays);
        }

        // ─────────────────────────────────────────────────────────────
        //  Week-based breakdown (Mon–Sat, 4 or 5 weeks per month)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns Mon–Sat activity counts grouped into 7-day blocks (Week 1 = days 1–7,
        /// Week 2 = 8–14, …, Week 5 = 29–31 when present). Sundays are excluded.
        /// Returns 4 entries for 28-day months, 5 for months with 29+ days.
        /// </summary>
        public List<(int WeekNum, int Count)> GetWeeklyTrendData(
            int year, int month, int? userId, string actionType, string entityType)
        {
            var nextMonth = month == 12
                ? new DateTime(year + 1, 1, 1)
                : new DateTime(year, month + 1, 1);

            var weekFilter = new UserActivityFilter
            {
                DateFrom   = new DateTime(year, month, 1),
                DateTo     = nextMonth,
                UserId     = userId,
                ActionType = actionType,
                EntityType = entityType
            };

            var weekCounts = new int[5];
            foreach (var r in _repository.GetFiltered(weekFilter))
            {
                if (r.CreatedDate.DayOfWeek == DayOfWeek.Sunday) continue;
                int day     = r.CreatedDate.Day;
                int weekIdx = day <= 7 ? 0 : day <= 14 ? 1 : day <= 21 ? 2 : day <= 28 ? 3 : 4;
                weekCounts[weekIdx]++;
            }

            int daysInMonth = DateTime.DaysInMonth(year, month);
            int numWeeks    = daysInMonth > 28 ? 5 : 4;

            return Enumerable.Range(1, numWeeks)
                .Select(w => (WeekNum: w, Count: weekCounts[w - 1]))
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────
        //  Activity day summary — active vs. missed days
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the total calendar days from the earliest active date to today,
        /// how many of those days had at least one activity, and how many were missed.
        /// </summary>
        public (int ActiveDays, int TotalDays, int MissedDays) GetActivityDaySummary(UserActivityFilter filter)
        {
            var activeDates = _repository.GetFiltered(filter)
                .Select(r => r.CreatedDate.Date)
                .Distinct()
                .ToList();

            // Use the filter's explicit date range as the denominator so the totals
            // match exactly what the user selected in the From/To pickers.
            DateTime start = filter.DateFrom?.Date ?? (activeDates.Count > 0 ? activeDates.Min() : DateTime.Today);
            DateTime end   = filter.DateTo?.Date   ?? DateTime.Today;
            if (end > DateTime.Today) end = DateTime.Today;   // never count future days

            if (start > end) return (0, 0, 0);

            // Exclude company holidays from the denominator so missed-day counts
            // only reflect actual working days.
            var holidayDates = _holidays.GetActiveDatesInRange(start, end);

            int totalDays  = (end - start).Days + 1 - holidayDates.Count;
            // An active date on a holiday doesn't count toward missed days (user worked a holiday),
            // but it still counts as an active day.
            int activeDays = activeDates.Count;
            int missedDays = Math.Max(0, totalDays - activeDates.Count(d => !holidayDates.Contains(d)));

            return (activeDays, totalDays, missedDays);
        }

        // ─────────────────────────────────────────────────────────────
        //  Average active days per week (Mon–Sat, Sundays excluded)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the average number of distinct Mon–Sat days per week that had at least one activity.
        /// Weeks are keyed by their Monday date so year-boundary weeks are handled correctly.
        /// </summary>
        public double GetAvgDaysPerWeekUsed(UserActivityFilter filter)
        {
            var activeDates = _repository.GetFiltered(filter)
                .Select(r => r.CreatedDate.Date)
                .Where(d => d.DayOfWeek != DayOfWeek.Sunday)
                .Distinct()
                .ToList();

            if (activeDates.Count == 0) return 0;

            var byWeek = activeDates
                .GroupBy(d =>
                {
                    int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                    return d.AddDays(-diff).Date;   // Monday of that week
                })
                .Select(g => g.Count())
                .ToList();

            return byWeek.Count > 0 ? byWeek.Average() : 0;
        }

        // ─────────────────────────────────────────────────────────────
        //  Daily breakdown (Mon–Sat only, Sundays excluded)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns Mon–Sat activity counts per calendar day for the given year/month.
        /// Sundays are excluded from the result. Days with no activity are included with Count = 0.
        /// </summary>
        public List<(int Day, int Count)> GetDailyTrendData(
            int year, int month, int? userId, string actionType, string entityType)
        {
            var nextMonth = month == 12
                ? new DateTime(year + 1, 1, 1)
                : new DateTime(year, month + 1, 1);

            var filter = new UserActivityFilter
            {
                DateFrom   = new DateTime(year, month, 1),
                DateTo     = nextMonth,
                UserId     = userId,
                ActionType = actionType,
                EntityType = entityType
            };

            var lookup = _repository.GetFiltered(filter)
                .Where(r => r.CreatedDate.DayOfWeek != DayOfWeek.Sunday)
                .GroupBy(r => r.CreatedDate.Day)
                .ToDictionary(g => g.Key, g => g.Count());

            int daysInMonth = DateTime.DaysInMonth(year, month);
            var result = new List<(int Day, int Count)>();
            for (int d = 1; d <= daysInMonth; d++)
            {
                if (new DateTime(year, month, d).DayOfWeek == DayOfWeek.Sunday) continue;
                lookup.TryGetValue(d, out int count);
                result.Add((d, count));
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  Quarterly trend (Q1–Q4 aggregates)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns total activity counts for each calendar quarter (Q1=Jan-Mar … Q4=Oct-Dec)
        /// for the given year. All four quarters are always returned, even when count is 0.
        /// </summary>
        public List<(int Quarter, int Count)> GetQuarterlyTrendData(
            int year, int? userId, string actionType, string entityType)
        {
            var yearFilter = new UserActivityFilter
            {
                DateFrom   = new DateTime(year, 1, 1),
                DateTo     = new DateTime(year + 1, 1, 1),
                UserId     = userId,
                ActionType = actionType,
                EntityType = entityType
            };

            var lookup = _repository.GetFiltered(yearFilter)
                .GroupBy(r => (r.CreatedDate.Month - 1) / 3 + 1)
                .ToDictionary(g => g.Key, g => g.Count());

            return Enumerable.Range(1, 4)
                .Select(q => { lookup.TryGetValue(q, out int count); return (Quarter: q, Count: count); })
                .ToList();
        }
    }
}
