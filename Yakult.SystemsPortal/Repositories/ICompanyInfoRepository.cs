using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

public interface ICompanyInfoRepository
{
    Task<IReadOnlyList<CompanyFaqItem>> GetPublishedFaqsAsync(string? query = null, int take = 200);
    Task<IReadOnlyList<CompanyFaqItem>> GetManagedFaqsAsync();
    Task<CompanyFaqItem?> GetFaqByIdAsync(int faqId);
    Task<CompanyFaqItem> SaveFaqAsync(SaveCompanyFaqRequest request, int userId);
    Task SetFaqPublishedAsync(int faqId, bool published, int userId);
    Task DeleteFaqAsync(int faqId);

    Task<IReadOnlyList<CompanyPolicyItem>> GetPublishedPoliciesAsync(string? query = null, int take = 200);
    Task<CompanyPolicyItem?> GetPublishedPolicyBySlugAsync(string slug);
    Task<IReadOnlyList<CompanyPolicyItem>> GetManagedPoliciesAsync();
    Task<CompanyPolicyItem?> GetPolicyByIdAsync(int policyId);
    Task<CompanyPolicyItem> SavePolicyAsync(SaveCompanyPolicyRequest request, int userId);
    Task SetPolicyPublishedAsync(int policyId, bool published, int userId);
    Task DeletePolicyAsync(int policyId);
}
