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

    // ИСПРАВЛЕНО: Guid вместо int (так как AppDefinition использует Guid)
    public Guid AppDefinitionId { get; set; }
    public AppDefinition AppDefinition { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    // ИСПРАВЛЕНО: Вернули название Label, которое используется в контроллерах
    public string Label { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SystemName { get; set; } = string.Empty;

    public FieldDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    // ИСПРАВЛЕНО: Вернули SortOrder, который используется в сортировке
    public int SortOrder { get; set; }

    // --- Новые поля оставили ---
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