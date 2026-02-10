using System.ComponentModel.DataAnnotations;
using Core.Entities.Platform;

namespace CRM.ViewModels.FormConfig;

public class FieldDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty; // String, Number...
    public bool IsRequired { get; set; }
    public bool IsArray { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDeleted { get; set; }
}

public class CreateFieldRequest
{
    [Required]
    public Guid AppDefinitionId { get; set; }

    [Required(ErrorMessage = "Название обязательно")]
    public string Label { get; set; } = string.Empty;

    [Required]
    public FieldDataType DataType { get; set; }

    public bool IsArray { get; set; }
    public bool IsRequired { get; set; }
}

public class SaveLayoutRequest
{
    [Required]
    public Guid FormId { get; set; }

    [Required]
    public string LayoutJson { get; set; } = "{}";
}