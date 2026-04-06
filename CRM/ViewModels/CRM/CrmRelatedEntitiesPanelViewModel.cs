namespace CRM.ViewModels.CRM;

public class CrmRelatedEntitiesPanelViewModel
{
    public string Title { get; set; } = string.Empty;
    public string EmptyText { get; set; } = "Связанных элементов пока нет.";
    public List<CrmRelatedEntityItemViewModel> Items { get; set; } = new();
}

public class CrmRelatedEntityItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ModalSize { get; set; } = "xl";
    public string? Subtitle { get; set; }
    public string? BadgeText { get; set; }
}
