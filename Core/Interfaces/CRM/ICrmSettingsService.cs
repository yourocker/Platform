using Core.Entities.CRM;

namespace Core.Interfaces.CRM
{
    public interface ICrmSettingsService
    {
        Task<CrmSettings> GetAsync();

        Task<bool> UseLeadsAsync();

        Task SetUseLeadsAsync(bool useLeads);
    }
}
