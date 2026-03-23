using Core.Data;
using Core.Entities.CRM;
using Core.Entities.System;
using Core.Interfaces.Platform;
using Microsoft.EntityFrameworkCore;

namespace Core.Services.Platform
{
    public class BookingPolicyService : IBookingPolicyService
    {
        private readonly AppDbContext _context;

        public BookingPolicyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BookingPolicySettings> GetGlobalPolicyAsync()
        {
            var policy = await _context.BookingPolicySettings
                .FirstOrDefaultAsync();

            if (policy != null)
            {
                return policy;
            }

            policy = new BookingPolicySettings
            {
                Id = Guid.NewGuid(),
                AllowOverbooking = false,
                MaxParallelBookings = 2,
                AllowManualItemPriceChange = false,
                UpdatedAt = DateTime.UtcNow
            };

            _context.BookingPolicySettings.Add(policy);
            await _context.SaveChangesAsync();

            return policy;
        }

        public async Task SaveGlobalPolicyAsync(bool allowOverbooking, int maxParallelBookings, bool allowManualItemPriceChange)
        {
            var policy = await GetGlobalPolicyAsync();

            policy.AllowOverbooking = allowOverbooking;
            policy.MaxParallelBookings = NormalizeMaxParallel(allowOverbooking, maxParallelBookings);
            policy.AllowManualItemPriceChange = allowManualItemPriceChange;
            policy.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<(bool AllowOverbooking, int MaxParallelBookings)> GetEffectivePolicyAsync(CrmResource resource)
        {
            var global = await GetGlobalPolicyAsync();

            var allowOverbooking = resource.AllowOverbooking ?? global.AllowOverbooking;

            var maxParallel = allowOverbooking
                ? (resource.MaxParallelBookings ?? global.MaxParallelBookings)
                : 1;

            return (allowOverbooking, NormalizeMaxParallel(allowOverbooking, maxParallel));
        }

        private static int NormalizeMaxParallel(bool allowOverbooking, int maxParallelBookings)
        {
            if (!allowOverbooking)
            {
                return 1;
            }

            return Math.Max(2, maxParallelBookings);
        }
    }
}
