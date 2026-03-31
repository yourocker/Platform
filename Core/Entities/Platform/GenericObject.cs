using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities;
using Core.MultiTenancy;

namespace Core.Entities.Platform
{
    public class GenericObject : IHasDynamicProperties, ITenantEntity, ISoftDeletable
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        // Код типа объекта (например, "Equipment"), чтобы отличать их
        [Required]
        public string EntityCode { get; set; }

        // Основное имя/заголовок объекта (чтобы не искать в JSON для списков)
        [Required]
        public string Name { get; set; }

        // Дата создания
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        // Наш универсальный JSON-контейнер
        public string? Properties { get; set; }
    }
}
