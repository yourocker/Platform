using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Platform;

namespace Core.Entities.CRM
{
    [Table("ServiceCategories")]
    public class ServiceCategory : GenericObject
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DisplayId { get; set; }

        public Guid? ParentCategoryId { get; set; }

        [ForeignKey("ParentCategoryId")]
        public virtual ServiceCategory? ParentCategory { get; set; }

        public virtual ICollection<ServiceCategory> Children { get; set; } = new List<ServiceCategory>();
        public virtual ICollection<ServiceItem> Services { get; set; } = new List<ServiceItem>();
    }

    [Table("ServiceItems")]
    public class ServiceItem : GenericObject
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DisplayId { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual ServiceCategory Category { get; set; } = null!;
    }
}