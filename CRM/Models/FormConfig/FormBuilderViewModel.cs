using Core.Entities.Platform.Form;

namespace CRM.ViewModels.FormConfig;

public class FormBuilderViewModel
{
    public Guid AppDefinitionId { get; set; }
    public string AppDefinitionName { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;

    // Все формы для конструктора (по всем типам отображения)
    public List<FormBuilderFormDto> Forms { get; set; } = new();
}

public class FormBuilderFormDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FormType Type { get; set; }
    public bool IsDefault { get; set; }
    public string LayoutJson { get; set; } = "{\"nodes\":[]}";
}
