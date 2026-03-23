using Core.Entities.CRM;
using Core.Entities.System;

namespace Core.Interfaces.Platform
{
    public interface IBookingPolicyService
    {
        Task<BookingPolicySettings> GetGlobalPolicyAsync();

        Task SaveGlobalPolicyAsync(bool allowOverbooking, int maxParallelBookings, bool allowManualItemPriceChange);

        Task<(bool AllowOverbooking, int MaxParallelBookings)> GetEffectivePolicyAsync(CrmResource resource);
    }
}
