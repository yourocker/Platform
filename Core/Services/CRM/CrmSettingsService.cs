using Core.Data;
using Core.Entities.CRM;
using Core.Interfaces.CRM;
using Microsoft.EntityFrameworkCore;

namespace Core.Services.CRM
{
    public class CrmSettingsService : ICrmSettingsService
    {
        private readonly AppDbContext _context;

        public CrmSettingsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CrmSettings> GetAsync()
        {
            var settings = await _context.CrmSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                return settings;
            }

            var tenantId = _context.CurrentTenantId
                ?? throw new InvalidOperationException("Tenant context is required for CRM settings.");

            settings = new CrmSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UseLeads = true,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CrmSettings.Add(settings);
            await _context.SaveChangesAsync();

            return settings;
        }

        public async Task<bool> UseLeadsAsync()
        {
            var settings = await GetAsync();
            return settings.UseLeads;
        }

        public async Task SetUseLeadsAsync(bool useLeads)
        {
            var settings = await GetAsync();
            if (settings.UseLeads == useLeads)
            {
                return;
            }

            settings.UseLeads = useLeads;
            settings.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
