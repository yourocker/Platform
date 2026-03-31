using Core.Entities;
using Core.Entities.Company;
using Core.Entities.System;
using Core.MultiTenancy;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Бронирование ресурса под конкретную задачу или позицию сделки.
    /// </summary>
    [Table("CrmResourceBookings")]
    public class CrmResourceBooking : IHasDynamicProperties, ITenantEntity, ISoftDeletable
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        /// <summary>
        /// Исполнитель, к которому записали.
        /// </summary>
        public Guid? PerformerEmployeeId { get; set; }

        [ForeignKey(nameof(PerformerEmployeeId))]
        public virtual Employee? PerformerEmployee { get; set; }

        /// <summary>
        /// Сотрудник, который создал запись (из активной сессии).
        /// </summary>
        public Guid? CreatedByEmployeeId { get; set; }

        [ForeignKey(nameof(CreatedByEmployeeId))]
        public virtual Employee? CreatedByEmployee { get; set; }

        public Guid ResourceId { get; set; }

        [ForeignKey(nameof(ResourceId))]
        public virtual CrmResource Resource { get; set; } = null!;

        public Guid? ServiceItemId { get; set; }

        [ForeignKey(nameof(ServiceItemId))]
        public virtual ServiceItem? ServiceItem { get; set; }

        public string? Title { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public Guid? StatusId { get; set; }

        [ForeignKey(nameof(StatusId))]
        public virtual BookingStatus? Status { get; set; }

        public Guid? DealItemId { get; set; }

        [ForeignKey(nameof(DealItemId))]
        public virtual CrmDealItem? DealItem { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        /// <summary>
        /// Флаг овербукинга (если разрешено администратором)
        /// </summary>
        public bool IsOverbooking { get; set; }

        public string? DiscountReason { get; set; }

        public string? Comment { get; set; }

        /// <summary>
        /// Дополнительные настраиваемые поля (через конструктор сущностей).
        /// </summary>
        public string? Properties { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public virtual List<CrmResourceBookingItem> BookingItems { get; set; } = new();

        public virtual List<CrmResourceBookingContact> BookingContacts { get; set; } = new();
    }
}
