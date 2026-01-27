using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Бронирование ресурса под конкретную задачу или позицию сделки.
    /// </summary>
    [Table("CrmResourceBookings")]
    public class CrmResourceBooking
    {
        [Key]
        public Guid Id { get; set; }

        public Guid ResourceId { get; set; }

        [ForeignKey(nameof(ResourceId))]
        public virtual CrmResource Resource { get; set; } = null!;

        public Guid? DealItemId { get; set; }

        [ForeignKey(nameof(DealItemId))]
        public virtual CrmDealItem? DealItem { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        /// <summary>
        /// Флаг овербукинга (если разрешено администратором)
        /// </summary>
        public bool IsOverbooking { get; set; }

        public string? Comment { get; set; }
    }
}