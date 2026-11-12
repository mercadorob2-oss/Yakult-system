using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public sealed class MessengerDevice
    {
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public DateTime? LastSeenUtc { get; set; }
        public bool Online { get; set; }
        public int PendingCount { get; set; }
        public string Key => MachineName + "\n" + UserName;
        public string Presence => Online ? "Active" : "Offline";
    }

    public sealed class TemporaryDeviceMessage
    {
        public long MessageId { get; set; }
        public string Body { get; set; }
        public string SenderMachine { get; set; }
        public string SenderUser { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsOutgoing { get; set; }
    }

    public sealed class DeviceMessengerRepository
    {
        private static void RequireSession()
        {
            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0 || string.IsNullOrWhiteSpace(AppSession.CurrentUserName))
                throw new UnauthorizedAccessException("Sign in to use device messenger.");
        }
        private static async Task<SqlConnection> OpenAsync()
        {
            RequireSession();
            var sessionUserId = AppSession.CurrentUserId;
            DatabaseConfig.EnsureConfigured();
            var connection = new SqlConnection(DatabaseConfig.ConnectionString);
            try
            {
                await connection.OpenAsync();
                if (await connection.ExecuteScalarAsync<int>("SELECT CASE WHEN OBJECT_ID('dbo.DesktopDeviceMessage', 'U') IS NOT NULL AND OBJECT_ID('dbo.CallClientPresence', 'U') IS NOT NULL THEN 1 ELSE 0 END") != 1)
                    throw new InvalidOperationException("Device messenger needs database setup. Ask an administrator to run Migration_DesktopDeviceMessage_CreateTable.sql and enable device presence.");
                RequireSession();
                if (AppSession.CurrentUserId != sessionUserId) throw new UnauthorizedAccessException("The signed-in user changed. Reopen device messenger.");
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }
        public async Task<List<TemporaryDeviceMessage>> GetPendingIncomingAsync()
        {
            using (var con = await OpenAsync())
                return (await con.QueryAsync<TemporaryDeviceMessage>(@"
SELECT MessageId, Body, CreatedAtUtc, SenderMachine, SenderUser
FROM dbo.DesktopDeviceMessage
WHERE RecipientMachine=@Machine AND RecipientUser=@User AND RecipientDismissedAtUtc IS NULL
ORDER BY MessageId;", new { Machine = Environment.MachineName, User = AppSession.CurrentUserName })).ToList();
        }

        public async Task<List<MessengerDevice>> GetDevicesAsync()
        {
            var machine = Environment.MachineName; var user = AppSession.CurrentUserName;
            using (var con = await OpenAsync())
            {
                return (await con.QueryAsync<MessengerDevice>(@"
WITH devices AS (
 SELECT MachineName, UserName, LastSeenUtc FROM dbo.CallClientPresence
 UNION ALL SELECT SenderMachine, SenderUser, NULL FROM dbo.DesktopDeviceMessage
 WHERE RecipientMachine=@Machine AND RecipientUser=@User AND RecipientDismissedAtUtc IS NULL
 UNION ALL SELECT RecipientMachine, RecipientUser, NULL FROM dbo.DesktopDeviceMessage
 WHERE SenderMachine=@Machine AND SenderUser=@User AND SenderDismissedAtUtc IS NULL
), grouped AS (
 SELECT MachineName, UserName, MAX(LastSeenUtc) LastSeenUtc FROM devices GROUP BY MachineName, UserName
)
SELECT d.*, CONVERT(bit,CASE WHEN LastSeenUtc >= DATEADD(minute,-15,SYSUTCDATETIME()) THEN 1 ELSE 0 END) Online,
 (SELECT COUNT(*) FROM dbo.DesktopDeviceMessage m WHERE m.SenderMachine=d.MachineName AND m.SenderUser=d.UserName
 AND m.RecipientMachine=@Machine AND m.RecipientUser=@User AND m.RecipientDismissedAtUtc IS NULL) PendingCount
FROM grouped d WHERE NOT (MachineName=@Machine AND UserName=@User)
ORDER BY Online DESC, PendingCount DESC, UserName, MachineName;", new { Machine = machine, User = user })).ToList();
            }
        }
        public async Task<List<TemporaryDeviceMessage>> GetConversationAsync(MessengerDevice peer)
        {
            if (peer == null) return new List<TemporaryDeviceMessage>();
            using (var con = await OpenAsync())
                return (await con.QueryAsync<TemporaryDeviceMessage>(@"
SELECT MessageId, Body, CreatedAtUtc,
 CONVERT(bit,CASE WHEN SenderMachine=@Machine AND SenderUser=@User THEN 1 ELSE 0 END) IsOutgoing
FROM dbo.DesktopDeviceMessage
WHERE (SenderMachine=@Machine AND SenderUser=@User AND RecipientMachine=@PeerMachine AND RecipientUser=@PeerUser AND SenderDismissedAtUtc IS NULL)
 OR (RecipientMachine=@Machine AND RecipientUser=@User AND SenderMachine=@PeerMachine AND SenderUser=@PeerUser AND RecipientDismissedAtUtc IS NULL)
ORDER BY MessageId;", new { Machine = Environment.MachineName, User = AppSession.CurrentUserName, PeerMachine = peer.MachineName, PeerUser = peer.UserName })).ToList();
        }
        public async Task SendAsync(MessengerDevice peer, string body)
        {
            RequireSession();
            body = (body ?? "").Trim();
            if (peer == null || string.IsNullOrWhiteSpace(peer.MachineName) || string.IsNullOrWhiteSpace(peer.UserName))
                throw new ArgumentException("Choose a device and user first.");
            if (peer.MachineName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) && peer.UserName.Equals(AppSession.CurrentUserName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose another device or user.");
            if (body.Length == 0 || body.Length > 1000) throw new ArgumentException("Enter a message between 1 and 1,000 characters.");
            using (var con = await OpenAsync())
            {
                var count = await con.ExecuteAsync(@"
INSERT INTO dbo.DesktopDeviceMessage (SenderMachine, SenderUser, SenderUserId, RecipientMachine, RecipientUser, Body)
SELECT @Machine, @User, @UserId, @PeerMachine, @PeerUser, @Body
WHERE EXISTS (SELECT 1 FROM dbo.CallClientPresence WHERE MachineName=@PeerMachine AND UserName=@PeerUser
 AND LastSeenUtc >= DATEADD(minute,-15,SYSUTCDATETIME()));", new { Machine = Environment.MachineName, User = AppSession.CurrentUserName,
                    UserId = AppSession.CurrentUserId, PeerMachine = peer.MachineName, PeerUser = peer.UserName, Body = body });
                if (count != 1) throw new InvalidOperationException("That device is no longer active. Your draft is kept; try again when the user reconnects.");
            }
        }
        public async Task DismissAsync(long messageId)
        {
            using (var con = await OpenAsync())
            using (var tx = con.BeginTransaction())
            {
                var args = new { Id = messageId, Machine = Environment.MachineName, User = AppSession.CurrentUserName };
                await con.ExecuteAsync(@"
UPDATE dbo.DesktopDeviceMessage SET
 SenderDismissedAtUtc=CASE WHEN SenderMachine=@Machine AND SenderUser=@User THEN COALESCE(SenderDismissedAtUtc,SYSUTCDATETIME()) ELSE SenderDismissedAtUtc END,
 RecipientDismissedAtUtc=CASE WHEN RecipientMachine=@Machine AND RecipientUser=@User THEN COALESCE(RecipientDismissedAtUtc,SYSUTCDATETIME()) ELSE RecipientDismissedAtUtc END
WHERE MessageId=@Id AND ((SenderMachine=@Machine AND SenderUser=@User) OR (RecipientMachine=@Machine AND RecipientUser=@User));
DELETE FROM dbo.DesktopDeviceMessage WHERE MessageId=@Id AND SenderDismissedAtUtc IS NOT NULL AND RecipientDismissedAtUtc IS NOT NULL
 AND ((SenderMachine=@Machine AND SenderUser=@User) OR (RecipientMachine=@Machine AND RecipientUser=@User));", args, tx);
                tx.Commit();
            }
        }
    }
}
