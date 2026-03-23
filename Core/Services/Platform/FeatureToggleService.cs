using Core.Data;
using Core.Entities.System;
using Core.Interfaces.Platform;
using Microsoft.EntityFrameworkCore;

namespace Core.Services.Platform
{
    public class FeatureToggleService : IFeatureToggleService
    {
        private readonly AppDbContext _context;

        public FeatureToggleService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsEnabledAsync(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
            {
                return false;
            }

            var normalized = Normalize(featureCode);

            var value = await _context.FeatureToggles
                .AsNoTracking()
                .Where(x => x.FeatureCode == normalized)
                .Select(x => (bool?)x.IsEnabled)
                .FirstOrDefaultAsync();

            // Для неизвестного кода сохраняем "мягкое" поведение: модуль доступен.
            return value ?? true;
        }

        public async Task<IReadOnlyDictionary<string, bool>> GetAllAsync()
        {
            return await _context.FeatureToggles
                .AsNoTracking()
                .OrderBy(x => x.FeatureCode)
                .ToDictionaryAsync(x => x.FeatureCode, x => x.IsEnabled);
        }

        public async Task SetAsync(string featureCode, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
            {
                throw new ArgumentException("Feature code is required", nameof(featureCode));
            }

            var normalized = Normalize(featureCode);

            var toggle = await _context.FeatureToggles
                .FirstOrDefaultAsync(x => x.FeatureCode == normalized);

            if (toggle == null)
            {
                toggle = new FeatureToggle
                {
                    Id = Guid.NewGuid(),
                    FeatureCode = normalized,
                    IsEnabled = isEnabled,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.FeatureToggles.Add(toggle);
            }
            else
            {
                toggle.IsEnabled = isEnabled;
                toggle.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private static string Normalize(string featureCode)
        {
            return featureCode.Trim().ToUpperInvariant();
        }
    }
}
