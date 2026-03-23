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
        Task<(bool Success, bool IsOverbooking, string Message)> CheckAvailabilityAsync(
            Guid resourceId,
            DateTime start,
            DateTime end,
            Guid? performerEmployeeId = null,
            bool allowOutsideCompanyWorkHours = false);

        /// <summary>
        /// Создает бронирование с учетом правил овербукинга.
        /// </summary>
        Task<CrmResourceBooking> BookResourceAsync(
            CrmResourceBooking booking,
            bool allowOutsideCompanyWorkHours = false);

        /// <summary>
        /// Обновляет существующее бронирование с повторной проверкой доступности.
        /// </summary>
        Task<CrmResourceBooking> UpdateBookingAsync(
            CrmResourceBooking booking,
            bool allowOutsideCompanyWorkHours = false);
        
        /// <summary>
        /// Получает список свободных слотов для ресурса на конкретный день.
        /// </summary>
        Task<List<DateTime>> GetAvailableSlotsAsync(
            Guid resourceId,
            DateTime date,
            int durationMinutes = 15,
            Guid? performerEmployeeId = null);
    }
}
