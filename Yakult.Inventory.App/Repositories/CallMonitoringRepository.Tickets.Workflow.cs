using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public async Task SetTicketStatusAsync(int ticketId, string newStatus, int? changedByUserId, string note = null)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.ExecuteAsync(
                    "dbo.sp_Call_SetTicketStatus",
                    new
                    {
                        TicketId = ticketId,
                        NewStatus = newStatus,
                        ChangedByUserId = changedByUserId,
                        Note = note
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }

        public async Task SetTicketPriorityAsync(int ticketId, string newPriority, int? changedByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.ExecuteAsync(
                    "dbo.sp_Call_SetTicketPriority",
                    new
                    {
                        TicketId = ticketId,
                        NewPriority = newPriority,
                        ChangedByUserId = changedByUserId
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }

        public async Task AddTicketNoteAsync(int ticketId, string noteType, string noteText, int? createdByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.ExecuteAsync(
                    "dbo.sp_Call_AddTicketNote",
                    new
                    {
                        TicketId = ticketId,
                        NoteType = noteType,
                        NoteText = noteText,
                        CreatedByUserId = createdByUserId
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }
    }
}
