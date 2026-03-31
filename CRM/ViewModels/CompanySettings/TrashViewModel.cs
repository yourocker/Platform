using System;
using System.Collections.Generic;

namespace CRM.ViewModels.CompanySettings
{
    public class TrashFilterInput
    {
        public string? Search { get; set; }
        public string? EntityCode { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? DeletedFrom { get; set; }
        public DateTime? DeletedTo { get; set; }
    }

    public class TrashEntityOptionViewModel
    {
        public string EntityCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class TrashItemViewModel
    {
        public string SelectionKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public string EntityCode { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class TrashEntityStatViewModel
    {
        public string EntityCode { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TrashPageViewModel
    {
        public TrashFilterInput Filters { get; set; } = new();
        public List<TrashEntityOptionViewModel> EntityOptions { get; set; } = new();
        public List<TrashItemViewModel> Items { get; set; } = new();
        public List<TrashEntityStatViewModel> Stats { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
