using Core.Entities.Platform.Form;

namespace CRM.ViewModels.FormConfig;

public class FormBuilderViewModel
{
    public Guid AppDefinitionId { get; set; }
    public string AppDefinitionName { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;

    // Словарь: Тип формы -> JSON макета
    // Пример: Key = FormType.Create, Value = "{...}"
    public Dictionary<FormType, string> FormLayouts { get; set; } = new();
    
    // ID форм в базе (если они уже существуют), чтобы знать, обновлять или создавать
    public Dictionary<FormType, Guid?> FormIds { get; set; } = new();
}