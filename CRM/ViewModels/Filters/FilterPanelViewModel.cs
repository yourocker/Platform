using System.Collections.Generic;

namespace CRM.ViewModels.Filters;

public class FilterPanelViewModel
{
    public string FormId { get; set; } = "mainFilterForm";

    public string PanelId { get; set; } = "filterPanel";

    public string ActionUrl { get; set; } = string.Empty;

    public string ResetUrl { get; set; } = string.Empty;

    public string EntityCode { get; set; } = string.Empty;

    public string ViewCode { get; set; } = "Index";

    public string SearchValue { get; set; } = string.Empty;

    public string SearchPlaceholder { get; set; } = "Поиск...";

    public int PageSize { get; set; } = 10;

    public bool ExpandedByDefault { get; set; }

    public List<FilterFieldViewModel> Fields { get; set; } = new();
}
