using System.ComponentModel.DataAnnotations;
using Core.Entities.Platform;
using Core.Entities.Platform.Form;

namespace CRM.ViewModels.FormConfig;

/// <summary>
/// DTO для передачи данных о поле сущности.
/// </summary>
public class FieldDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsArray { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDeleted { get; set; }
    public string? Description { get; set; }
    public string? TargetEntityCode { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Запрос на создание нового поля.
/// </summary>
public class CreateFieldRequest
{
    [Required]
    public Guid AppDefinitionId { get; set; }

    [Required(ErrorMessage = "Название обязательно")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Если пусто — генерируется автоматически из Label с префиксом UF_.
    /// </summary>
    public string? SystemName { get; set; }

    [Required]
    public FieldDataType DataType { get; set; }

    public bool IsArray { get; set; }
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    public string? TargetEntityCode { get; set; }
}

/// <summary>
/// Запрос на обновление метаданных поля.
/// </summary>
public class UpdateFieldRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Название обязательно")]
    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// Компактная модель макета формы для списков.
/// </summary>
public class FormDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FormType Type { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Запрос на создание нового макета формы.
/// </summary>
public class CreateFormRequest
{
    [Required]
    public Guid AppDefinitionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public FormType Type { get; set; }
}

/// <summary>
/// Запрос на сохранение структуры макета.
/// </summary>
public class SaveLayoutRequest
{
    [Required]
    public Guid FormId { get; set; }

    [Required]
    public string LayoutJson { get; set; } = "{}";

    /// <summary>
    /// Позволяет сохранить форму, даже если пропущены обязательные поля сущности.
    /// </summary>
    public bool ForceSave { get; set; }
}