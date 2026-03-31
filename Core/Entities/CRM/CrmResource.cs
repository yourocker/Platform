using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Ресурс компании (кабинет, автомобиль, оборудование и т.д.), который можно забронировать.
    /// Тип ресурса больше не хардкодится — пользователь задает его через конструктор полей.
    /// </summary>
    [Table("CrmResources")]
    public class CrmResource : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Локальное переопределение политики овербукинга для конкретного ресурса.
        /// null = использовать глобальные настройки компании.
        /// </summary>
        public bool? AllowOverbooking { get; set; }

        /// <summary>
        /// Локальный лимит параллельных броней.
        /// null = использовать глобальные настройки компании.
        /// </summary>
        public int? MaxParallelBookings { get; set; }

        public string? Description { get; set; }
        
        public virtual List<CrmResourceBooking> Bookings { get; set; } = new();
    }
}
