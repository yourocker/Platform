namespace CRM.ViewModels.Shared;

public class EntityDetailsTabsViewModel
{
    public string MainTabTitle { get; set; } = "Основное";
    public string MainPartialViewName { get; set; } = string.Empty;
    public object? MainModel { get; set; }
    public List<EntityDetailsTabDefinition> AdditionalTabs { get; set; } = new();
}

public class EntityDetailsTabDefinition
{
    public string Key { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string PartialViewName { get; set; } = string.Empty;
    public object? Model { get; set; }
}
