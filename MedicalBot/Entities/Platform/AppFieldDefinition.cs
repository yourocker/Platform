using System.ComponentModel.DataAnnotations;

namespace MedicalBot.Entities.Platform
{
    public enum FieldDataType
    {
        String,
        Text,
        Number,
        Money,
        DateTime,
        Boolean,
        EntityLink, // Ссылка на другую сущность/приложение
        Table,      // Динамическая таблица
        File
    }

    public class AppFieldDefinition
    {
        [Key]
        public Guid Id { get; set; }

        public Guid AppDefinitionId { get; set; }
        public virtual AppDefinition AppDefinition { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Label { get; set; } // "Группа крови", "Оклад"

        [Required]
        [MaxLength(50)]
        public string SystemName { get; set; } // "blood_type", "salary"

        public FieldDataType DataType { get; set; }

        public bool IsRequired { get; set; }

        public int SortOrder { get; set; }

        // Дополнительные настройки в JSON (например, на какую сущность ссылается EntityLink 
        // или список колонок для Table)
        public string? SettingsJson { get; set; } 
    }
}