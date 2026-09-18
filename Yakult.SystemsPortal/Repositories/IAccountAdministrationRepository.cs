using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

public interface IAccountAdministrationRepository
{
    Task<AccountRequestsViewModel> GetAccountAdministrationDataAsync();

    Task ApproveAccountRequestAsync(int accountRequestId, int? reviewedByUserId, string? remarks);

    Task RejectAccountRequestAsync(int accountRequestId, int? reviewedByUserId, string remarks);

    Task UpdateManagedUserAsync(UpdateManagedUserRequest request);

    Task<string?> ResetManagedUserPasswordAsync(ResetManagedUserPasswordRequest request);
}
