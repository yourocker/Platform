using System.ComponentModel.DataAnnotations;

namespace Core.Entities.Platform;

public enum FieldDataType
{
    String,
    Text,
    Number,
    Money,
    DateTime,
    Date,
    Boolean,
    EntityLink,
    File
}

public class AppFieldDefinition
{
    public Guid Id { get; set; }

    public Guid AppDefinitionId { get; set; }
    public AppDefinition AppDefinition { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SystemName { get; set; } = string.Empty;

    public FieldDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    // --- Параметры согласно ТЗ v5.0 ---

    /// <summary>
    /// Флаг системного поля (например, Name). Запрещено удалять.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Мягкое удаление.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Описание поля для администратора/пользователя.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    // --- Существующие поля ---
    
    public bool IsArray { get; set; } = false;
    
    [MaxLength(50)]
    public string? TargetEntityCode { get; set; }
}

public static class FieldDataTypeExtensions
{
    public static string ToRuName(this FieldDataType type) => type switch
    {
        FieldDataType.String => "Строка",
        FieldDataType.Text => "Многострочный текст",
        FieldDataType.Number => "Число",
        FieldDataType.Money => "Деньги (₽)",
        FieldDataType.DateTime => "Дата и время",
        FieldDataType.Date => "Дата",
        FieldDataType.Boolean => "Логическое (Да/Нет)",
        FieldDataType.EntityLink => "Связь с объектом",
        FieldDataType.File => "Файл",
        _ => type.ToString()
    };
    
}