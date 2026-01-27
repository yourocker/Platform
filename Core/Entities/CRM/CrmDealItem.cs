using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Позиция в корзине сделки. Фиксирует стоимость услуги на момент продажи.
    /// </summary>
    [Table("CrmDealItems")]
    public class CrmDealItem
    {
        [Key]
        public Guid Id { get; set; }

        public Guid DealId { get; set; }

        [ForeignKey(nameof(DealId))]
        public virtual Deal Deal { get; set; } = null!;

        public Guid ServiceItemId { get; set; }

        [ForeignKey(nameof(ServiceItemId))]
        public virtual ServiceItem ServiceItem { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public decimal Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// Итоговая сумма по позиции за вычетом скидки
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice => (Price * Quantity) - DiscountAmount;
        
        public string? Note { get; set; }
    }
}