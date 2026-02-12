using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.Platform.Form;

public enum FormType
{
    Create,
    Edit,
    View
}

/// <summary>
/// Макет формы (п. 2.2 ТЗ v5.0)
/// </summary>
public class AppFormDefinition
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid AppDefinitionId { get; set; }
    public AppDefinition AppDefinition { get; set; } = null!;

    /// <summary>
    /// Название формы (например, "Основная", "Для менеджеров").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "Основная форма";

    public FormType Type { get; set; }

    /// <summary>
    /// Флаг стандартной формы.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Хранит иерархическую структуру дерева элементов (JSONB).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Layout { get; set; } = "{}";
}