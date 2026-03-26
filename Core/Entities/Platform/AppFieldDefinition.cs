using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Core.Entities.Platform;

public enum FieldDataType
{
    String = 0,
    Text = 1,
    Number = 2,
    Money = 3,
    DateTime = 4,
    Date = 5,
    Boolean = 6,
    EntityLink = 7,
    File = 8,
    Select = 9
}

public class FieldSelectOption
{
    [Required]
    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }
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

    public string? OptionsJson { get; set; }

    public List<FieldSelectOption> GetSelectOptions()
    {
        if (string.IsNullOrWhiteSpace(OptionsJson))
        {
            return new List<FieldSelectOption>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<FieldSelectOption>>(OptionsJson)
                   ?.Where(option => !string.IsNullOrWhiteSpace(option.Label))
                   .OrderBy(option => option.SortOrder)
                   .ThenBy(option => option.Label)
                   .ToList()
                   ?? new List<FieldSelectOption>();
        }
        catch
        {
            return new List<FieldSelectOption>();
        }
    }

    public void SetSelectOptions(IEnumerable<FieldSelectOption>? options)
    {
        var normalizedOptions = (options ?? Enumerable.Empty<FieldSelectOption>())
            .Where(option => !string.IsNullOrWhiteSpace(option.Label))
            .Select((option, index) => new FieldSelectOption
            {
                Value = string.IsNullOrWhiteSpace(option.Value) ? Guid.NewGuid().ToString("N") : option.Value.Trim(),
                Label = option.Label.Trim(),
                SortOrder = index
            })
            .ToList();

        OptionsJson = normalizedOptions.Count == 0
            ? null
            : JsonSerializer.Serialize(normalizedOptions);
    }
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
        FieldDataType.Select => "Выпадающий список",
        _ => type.ToString()
    };
    
}
