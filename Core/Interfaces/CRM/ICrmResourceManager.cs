using Core.Entities.CRM;

namespace Core.Interfaces.CRM
{
    public interface ICrmResourceManager
    {
        /// <summary>
        /// Проверяет доступность ресурса на указанный период.
        /// </summary>
        /// <returns>
        /// (bool IsAvailable, bool NeedsWarning, string Message)
        /// NeedsWarning = true, если время занято, но овербукинг разрешен.
        /// </returns>
        Task<(bool Success, bool IsOverbooking, string Message)> CheckAvailabilityAsync(Guid resourceId, DateTime start, DateTime end);

        /// <summary>
        /// Создает бронирование с учетом правил овербукинга.
        /// </summary>
        Task<CrmResourceBooking> BookResourceAsync(CrmResourceBooking booking);
        
        /// <summary>
        /// Получает список свободных слотов для ресурса на конкретный день.
        /// </summary>
        Task<List<DateTime>> GetAvailableSlotsAsync(Guid resourceId, DateTime date, int durationMinutes = 15);
    }
}