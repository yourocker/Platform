using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    [Table("CrmResourceBookingItems")]
    public class CrmResourceBookingItem
    {
        [Key]
        public Guid Id { get; set; }

        public Guid BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public virtual CrmResourceBooking Booking { get; set; } = null!;

        public Guid ServiceItemId { get; set; }

        [ForeignKey(nameof(ServiceItemId))]
        public virtual ServiceItem ServiceItem { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; } = 1m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomUnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }
    }
}
