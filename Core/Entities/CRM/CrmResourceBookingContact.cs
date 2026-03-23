using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    [Table("CrmResourceBookingContacts")]
    public class CrmResourceBookingContact
    {
        public Guid BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public virtual CrmResourceBooking Booking { get; set; } = null!;

        public Guid ContactId { get; set; }

        [ForeignKey(nameof(ContactId))]
        public virtual Contact Contact { get; set; } = null!;
    }
}
