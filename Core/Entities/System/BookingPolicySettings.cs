using System.ComponentModel.DataAnnotations;
using Core.MultiTenancy;

namespace Core.Entities.System
{
    /// <summary>
    /// Глобальная политика бронирования ресурсов на уровне компании.
    /// </summary>
    public class BookingPolicySettings : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        /// <summary>
        /// Разрешает создавать записи в пересекающиеся интервалы времени.
        /// </summary>
        public bool AllowOverbooking { get; set; } = false;

        /// <summary>
        /// Максимально допустимое количество параллельных броней на один ресурс.
        /// Используется только если AllowOverbooking = true.
        /// </summary>
        public int MaxParallelBookings { get; set; } = 2;

        /// <summary>
        /// Разрешает менять цену услуги/товара в рамках конкретной записи.
        /// Изменение не влияет на прайс-лист.
        /// </summary>
        public bool AllowManualItemPriceChange { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
