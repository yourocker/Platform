using System.Collections.Generic;

namespace CRM.ViewModels.Filters;

public class FilterFieldViewModel
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public FilterInputKind Kind { get; set; } = FilterInputKind.Text;

    public string? Value { get; set; }

    public string? Placeholder { get; set; }

    public List<FilterOptionViewModel> Options { get; set; } = new();
}
