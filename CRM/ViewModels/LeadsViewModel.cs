using Core.Entities.CRM;
using Core.Entities.Platform;

namespace CRM.ViewModels.CRM
{
    public class LeadsViewModel
    {
        // Данные воронки
        public CrmPipeline CurrentPipeline { get; set; } = null!;
        public List<CrmPipeline> AllPipelines { get; set; } = new();
        public List<CrmStage> Stages { get; set; } = new();

        // Список лидов (для текущей страницы)
        public List<Lead> Leads { get; set; } = new();

        // Динамические поля для фильтров и таблицы
        public List<AppFieldDefinition> DynamicFields { get; set; } = new();

        // Параметры отображения и пагинации
        public string ViewMode { get; set; } = "kanban"; // kanban, list, calendar
        public string SearchString { get; set; } = string.Empty;
        public Dictionary<string, string> ActiveFilters { get; set; } = new();
        
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}