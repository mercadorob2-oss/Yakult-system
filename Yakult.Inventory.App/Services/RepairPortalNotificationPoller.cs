using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Generates Repair Technician Portal notifications while the portal shell is open. On a
    /// 60-second interval it scans dbo.RepairTicket for tickets created or marked Completed within
    /// the last <see cref="LookbackHours"/> hours and, for any not already recorded, inserts a row
    /// into the shared dbo.Notification table for the current technician (AppSession.CurrentUserId)
    /// and raises <see cref="NewNotificationsArrived"/>.
    ///
    /// Modeled on HomeNotificationPoller: WinForms Timer, a one-shot startup timer, IDisposable.
    /// De-duplication is by NotificationRepository.ExistsByTypeAndReference (Type + ReferenceId per
    /// user), so re-reading the same lookback window every poll — and running in more than one
    /// session for the same user — never produces duplicate notifications. A bounded lookback
    /// (rather than a silent session baseline) means a ticket created while the portal was closed
    /// is still surfaced once to the next technician who opens it.
    ///
    /// Limitation: because generation only runs while a technician has the portal open, a ticket
    /// created AND completed during a stretch when nobody has it open is only surfaced if it still
    /// falls inside the lookback window when someone next opens the portal.
    /// </summary>
    public sealed class RepairPortalNotificationPoller : IDisposable
    {
        private readonly Timer _timer;
        private readonly Timer _startupTimer;
        private bool _polling;
        private bool _disposed;

        private const int PollIntervalMs = 60_000;
        private const int StartupDelayMs = 3_000;
        private const int LookbackHours  = 12;

        /// <summary>Fires with the dbo.Notification rows this poll cycle just created (already
        /// persisted). Subscribers marshal to the UI thread themselves.</summary>
        public event Action<IReadOnlyList<NotificationDto>> NewNotificationsArrived;

        public RepairPortalNotificationPoller()
        {
            _startupTimer = new Timer { Interval = StartupDelayMs };
            _startupTimer.Tick += async (s, e) =>
            {
                _startupTimer.Stop();
                await PollAsync();
                if (!_disposed) _timer.Start();
            };

            _timer = new Timer { Interval = PollIntervalMs };
            _timer.Tick += async (s, e) => await PollAsync();

            _startupTimer.Start();
        }

        private async Task PollAsync()
        {
            if (_disposed || _polling) return;
            if (!AppSession.IsLoggedIn) return;
            if (!AppSession.NotificationsEnabled) return;
            if (AppSession.CurrentUserId <= 0) return;
            if (string.IsNullOrWhiteSpace(DatabaseConfig.ConnectionString)) return;

            _polling = true;
            try
            {
                int userId = AppSession.CurrentUserId;
                DateTime sinceUtc = DateTime.UtcNow.AddHours(-LookbackHours);

                var created = await new RepairTicketRepository().GetTicketsCreatedSinceAsync(sinceUtc);
                var completed = await new RepairTicketRepository().GetTicketsCompletedSinceAsync(sinceUtc);

                // The insert/exists checks are synchronous NotificationRepository calls — run the
                // whole batch off the UI thread so a slow DB round-trip can't stutter the shell.
                var fresh = await Task.Run(() =>
                {
                    var repo = new NotificationRepository();
                    var newRows = new List<NotificationDto>();

                    foreach (var t in created)
                    {
                        if (repo.ExistsByTypeAndReference(userId, NotificationType.RepairTicketNew, t.RepairTicketId))
                            continue;

                        var dto = new NotificationCreateDto
                        {
                            UserId           = userId,
                            Title            = "New repair ticket",
                            Message          = $"{t.TicketCode} — {t.ItemName}",
                            NotificationType = NotificationType.RepairTicketNew,
                            ReferenceId      = t.RepairTicketId,
                        };
                        repo.Create(dto);
                        newRows.Add(ToDisplayDto(dto));
                    }

                    foreach (var t in completed)
                    {
                        if (repo.ExistsByTypeAndReference(userId, NotificationType.RepairTicketCompleted, t.RepairTicketId))
                            continue;

                        var dto = new NotificationCreateDto
                        {
                            UserId           = userId,
                            Title            = "Ticket marked repaired",
                            Message          = $"{t.TicketCode} — {t.ItemName} was completed",
                            NotificationType = NotificationType.RepairTicketCompleted,
                            ReferenceId      = t.RepairTicketId,
                        };
                        repo.Create(dto);
                        newRows.Add(ToDisplayDto(dto));
                    }

                    return newRows;
                });

                if (fresh.Count > 0 && !_disposed)
                {
                    Debug.WriteLine($"[RepairPortalNotificationPoller] raised {fresh.Count} new notification(s)");
                    NewNotificationsArrived?.Invoke(fresh.AsReadOnly());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RepairPortalNotificationPoller] Poll failed: {ex}");
            }
            finally
            {
                _polling = false;
            }
        }

        private static NotificationDto ToDisplayDto(NotificationCreateDto dto) => new NotificationDto
        {
            UserId           = dto.UserId,
            Title            = dto.Title,
            Message          = dto.Message,
            NotificationType = dto.NotificationType,
            ReferenceId      = dto.ReferenceId,
            IsRead           = false,
            CreatedDate      = DateTime.UtcNow,
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}
