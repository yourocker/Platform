using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    public enum ResourceType { Staff, Room, Equipment }

    /// <summary>
    /// Ресурс компании (врач, кабинет или аппарат), который можно забронировать.
    /// </summary>
    [Table("CrmResources")]
    public class CrmResource
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public ResourceType Type { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Если ресурс - сотрудник, привязываем его ID
        /// </summary>
        public Guid? EmployeeId { get; set; }

        public string? Description { get; set; }
        
        public virtual List<CrmResourceBooking> Bookings { get; set; } = new();
    }
}