using Core.DTOs.Interfaces;
using Core.Entities.CRM;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CRM.ViewModels.CRM;

public class CrmProcessIndexViewModel
{
    public string EntityCode { get; set; } = string.Empty;
    public string EntityNameSingular { get; set; } = string.Empty;
    public string EntityNamePlural { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ViewMode { get; set; } = "kanban";
    public string SearchString { get; set; } = string.Empty;
    public Guid? SelectedPipelineId { get; set; }
    public string? SelectedPipelineName { get; set; }
    public List<CrmPipeline> Pipelines { get; set; } = new();
    public List<CrmProcessIndexItemViewModel> Items { get; set; } = new();
    public List<CrmProcessKanbanColumnViewModel> KanbanColumns { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));
    public bool SupportsLeadConversion => EntityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase);
    public bool IsKanbanView => ViewMode.Equals("kanban", StringComparison.OrdinalIgnoreCase);
    public bool IsListView => !IsKanbanView;
    public string StageChangeEntityIdParameter => EntityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase) ? "leadId" : "dealId";
}

public class CrmProcessIndexItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PipelineName { get; set; }
    public string? StageName { get; set; }
    public string StageColor { get; set; } = "#6c757d";
    public string? ResponsibleName { get; set; }
    public string? ContactName { get; set; }
    public string? CompanyName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
    public DateTime CreatedAt { get; set; }
    public bool IsConverted { get; set; }
    public DateTime? ConvertedAt { get; set; }
}

public class CrmProcessFormViewModel : IDynamicValues
{
    public Guid Id { get; set; }
    public string EntityCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Название обязательно")]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid PipelineId { get; set; }

    [Required]
    public Guid StageId { get; set; }
    public Guid? ResponsibleId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? SourceLeadId { get; set; }
    public decimal Amount { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "RUB";
    public bool IsConverted { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public Dictionary<string, object> DynamicValues { get; set; } = new();
    public List<Guid> ContactIds { get; set; } = new();

    public List<SelectListItem> PipelineOptions { get; set; } = new();
    public List<SelectListItem> StageOptions { get; set; } = new();
    public List<SelectListItem> ResponsibleOptions { get; set; } = new();
    public List<SelectListItem> ContactOptions { get; set; } = new();
    public List<SelectListItem> CompanyOptions { get; set; } = new();

    public Dictionary<Guid, List<SelectListItem>> StagesByPipeline { get; set; } = new();

    public bool IsLead => EntityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase);
    public bool IsDeal => EntityCode.Equals("Deal", StringComparison.OrdinalIgnoreCase);
}

public class CrmProcessDetailsViewModel : IDynamicValues
{
    public Guid Id { get; set; }
    public string EntityCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PipelineName { get; set; }
    public string? StageName { get; set; }
    public string StageColor { get; set; } = "#6c757d";
    public string? ResponsibleName { get; set; }
    public string? ContactName { get; set; }
    public string? CompanyName { get; set; }
    public string? SourceLeadName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
    public bool IsConverted { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> DynamicValues { get; set; } = new();
    public List<CrmProcessRelatedContactViewModel> Contacts { get; set; } = new();
    public List<CrmProcessStageStepViewModel> StageSteps { get; set; } = new();

    public bool IsLead => EntityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase);
    public bool IsDeal => EntityCode.Equals("Deal", StringComparison.OrdinalIgnoreCase);
}

public class CrmProcessRelatedContactViewModel
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class CrmProcessStageStepViewModel
{
    public Guid StageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6c757d";
    public bool IsCurrent { get; set; }
    public bool IsReached { get; set; }
    public bool IsFinal { get; set; }
}

public class CrmProcessKanbanColumnViewModel
{
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string StageColor { get; set; } = "#6c757d";
    public bool IsFinal { get; set; }
    public List<CrmProcessKanbanCardViewModel> Cards { get; set; } = new();
}

public class CrmProcessKanbanCardViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid StageId { get; set; }
    public string? ResponsibleName { get; set; }
    public string? ContactName { get; set; }
    public string? CompanyName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
    public bool IsConverted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmActivityFeedViewModel
{
    public Guid EntityId { get; set; }
    public string EntityCode { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string EmptyTitle { get; set; } = "Активностей пока нет";
    public string EmptyDescription { get; set; } = "Здесь появятся комментарии, задачи и другие рабочие действия по карточке.";
    public string CommentComposerTitle { get; set; } = "Комментарий";
    public string CommentComposerPlaceholder { get; set; } = "Напишите комментарий для команды";
    public string TaskComposerTitle { get; set; } = "Задача";
    public string TaskTitlePlaceholder { get; set; } = "Что нужно сделать?";
    public string TaskDescriptionPlaceholder { get; set; } = "Детали задачи, договоренности, контекст";
    public bool AllowCommentCreate { get; set; } = true;
    public bool AllowTaskCreate { get; set; } = true;
    public Guid? DefaultTaskAssigneeId { get; set; }
    public string ActiveComposer { get; set; } = "comment";
    public string? FeedbackMessage { get; set; }
    public string FeedbackTone { get; set; } = "danger";
    public List<SelectListItem> TaskAssigneeOptions { get; set; } = new();
    public List<CrmActivity> Activities { get; set; } = new();
}
