using System.Globalization;
using System.Text.Json;
using Core.Data;
using Core.Entities.Company;
using Core.Entities.CRM;
using Core.Entities.Platform;
using Core.Interfaces.CRM;
using Core.Interfaces.Platform;
using CRM.Infrastructure;
using CRM.ViewModels.CRM;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers
{
    public class LeadsController : BasePlatformController
    {
        private readonly ICrmService _crmService;
        private readonly ICrmActivityService _crmActivityService;
        private readonly IEntityTimelineService _timelineService;

        public LeadsController(
            AppDbContext context,
            IWebHostEnvironment hostingEnvironment,
            ICrmService crmService,
            ICrmActivityService crmActivityService,
            IEntityTimelineService timelineService)
            : base(context, hostingEnvironment)
        {
            _crmService = crmService;
            _crmActivityService = crmActivityService;
            _timelineService = timelineService;
        }

        public async Task<IActionResult> Index(Guid? pipelineId, string? searchString, string? view, int pageNumber = 1, int pageSize = 20)
        {
            var pipelines = await LoadPipelinesAsync();
            var viewMode = NormalizeViewMode(view);

            if (viewMode == "kanban" && !pipelineId.HasValue)
            {
                pipelineId = pipelines.OrderBy(x => x.SortOrder).Select(x => (Guid?)x.Id).FirstOrDefault();
            }

            var query = _context.Leads
                .AsNoTracking()
                .Include(x => x.Pipeline)
                .Include(x => x.CurrentStage)
                .Include(x => x.Responsible)
                .Include(x => x.Contact)
                .Include(x => x.Company)
                .AsQueryable();

            if (pipelineId.HasValue)
            {
                query = query.Where(x => x.PipelineId == pipelineId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();
                query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search}%"));
            }

            pageNumber = Math.Max(1, pageNumber);
            pageSize = pageSize <= 0 ? 20 : pageSize;

            var totalItems = await query.CountAsync();
            List<Lead> items;
            List<CrmProcessKanbanColumnViewModel> kanbanColumns = new();
            var selectedPipeline = pipelineId.HasValue
                ? pipelines.FirstOrDefault(x => x.Id == pipelineId.Value)
                : null;

            if (viewMode == "kanban")
            {
                items = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(500)
                    .ToListAsync();

                if (selectedPipeline != null)
                {
                    var orderedStages = selectedPipeline.Stages
                        .OrderBy(x => x.SortOrder)
                        .ToList();

                    kanbanColumns = orderedStages
                        .Select(stage => new CrmProcessKanbanColumnViewModel
                        {
                            StageId = stage.Id,
                            StageName = stage.Name,
                            StageColor = stage.Color ?? "#6c757d",
                            IsFinal = stage.StageType != 0,
                            Cards = items
                                .Where(item => item.StageId == stage.Id)
                                .OrderByDescending(item => item.CreatedAt)
                                .Select(MapLeadToKanbanCard)
                                .ToList()
                        })
                        .ToList();
                }
            }
            else
            {
                items = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }

            var model = new CrmProcessIndexViewModel
            {
                EntityCode = "Lead",
                EntityNameSingular = "Лид",
                EntityNamePlural = "Лиды",
                ControllerName = "Leads",
                ViewMode = viewMode,
                SearchString = searchString ?? string.Empty,
                SelectedPipelineId = pipelineId,
                SelectedPipelineName = selectedPipeline?.Name,
                Pipelines = pipelines,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = viewMode == "list" ? items.Select(MapLeadToIndexItem).ToList() : new List<CrmProcessIndexItemViewModel>(),
                KanbanColumns = kanbanColumns
            };

            return View(model);
        }

        public async Task<IActionResult> Create(bool modal = false)
        {
            var pipelines = await LoadPipelinesAsync();
            var defaultPipeline = pipelines.FirstOrDefault();
            var defaultStage = defaultPipeline?.Stages.OrderBy(x => x.SortOrder).FirstOrDefault();

            var model = new CrmProcessFormViewModel
            {
                EntityCode = "Lead",
                PipelineId = defaultPipeline?.Id ?? Guid.Empty,
                StageId = defaultStage?.Id ?? Guid.Empty,
                Currency = "RUB"
            };

            ViewBag.IsModal = modal;
            await PopulateFormViewModelAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrmProcessFormViewModel model, bool modal = false)
        {
            model.Amount = ParseAmountInput(Request.Form, model.Amount);
            ModelState.Remove(nameof(CrmProcessFormViewModel.Amount));

            var lead = new Lead
            {
                Id = Guid.NewGuid(),
                EntityCode = "Lead",
                Name = NormalizeName(model.Name),
                PipelineId = model.PipelineId,
                StageId = model.StageId,
                ResponsibleId = model.ResponsibleId,
                ContactId = model.ContactId,
                CompanyId = model.CompanyId,
                Amount = model.Amount,
                Currency = NormalizeCurrency(model.Currency)
            };

            var contactIds = NormalizeContactIds(model.ContactIds, model.ContactId);
            model.ContactIds = contactIds;
            model.ContactId = NormalizePrimaryContact(model.ContactId, contactIds);
            lead.ContactId = model.ContactId;
            lead.ContactLinks = contactIds.Select(contactId => new CrmLeadContact
            {
                ContactId = contactId,
                IsPrimary = model.ContactId.HasValue && model.ContactId.Value == contactId
            }).ToList();

            await SaveDynamicProperties(lead, Request.Form, "Lead");
            model.DynamicValues = DeserializeDynamicValues(lead.Properties);

            if (!ModelState.IsValid)
            {
                if (IsInlineSaveRequest())
                {
                    return BuildInlineSaveValidationResult();
                }

                ViewBag.IsModal = modal;
                await PopulateFormViewModelAsync(model);
                return View(model);
            }

            var createdLead = await _crmService.CreateLeadAsync(lead);
            if (modal)
            {
                return ModalRequestHelper.BuildRedirectContent(
                    Url.Action(nameof(Details), new { id = createdLead.Id, modal = true }),
                    reloadOnClose: true);
            }

            return RedirectToAction(nameof(Details), new { id = createdLead.Id });
        }

        public async Task<IActionResult> Edit(Guid id, bool modal = false)
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, CrmProcessFormViewModel model, bool modal = false)
        {
            try
            {
                if (id != model.Id)
                {
                    return NotFound();
                }

                model.Amount = ParseAmountInput(Request.Form, model.Amount);
                ModelState.Remove(nameof(CrmProcessFormViewModel.Amount));

                var lead = await _context.Leads
                    .Include(x => x.ContactLinks)
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (lead == null)
                {
                    return NotFound();
                }

                var snapshot = new LeadSnapshot(
                    lead.Name,
                    lead.PipelineId,
                    lead.StageId,
                    lead.ResponsibleId,
                    lead.ContactId,
                    lead.CompanyId,
                    lead.Amount,
                    lead.Currency,
                    lead.Properties,
                    lead.ContactLinks.Select(x => x.ContactId).Distinct().ToList());

                var contactIds = NormalizeContactIds(model.ContactIds, model.ContactId);
                model.ContactIds = contactIds;
                model.ContactId = NormalizePrimaryContact(model.ContactId, contactIds);

                lead.Name = NormalizeName(model.Name);
                lead.PipelineId = model.PipelineId;
                lead.StageId = model.StageId;
                lead.ResponsibleId = model.ResponsibleId;
                lead.ContactId = model.ContactId;
                lead.CompanyId = model.CompanyId;
                lead.Amount = model.Amount;
                lead.Currency = NormalizeCurrency(model.Currency);

                if (snapshot.PipelineId != lead.PipelineId || snapshot.StageId != lead.StageId)
                {
                    lead.StageChangedAt = DateTime.UtcNow;
                }

                await SaveDynamicProperties(lead, Request.Form, "Lead");
                model.DynamicValues = DeserializeDynamicValues(lead.Properties);

                if (!ModelState.IsValid)
                {
                    if (IsInlineSaveRequest())
                    {
                        return BuildInlineSaveValidationResult();
                    }

                    ViewBag.IsModal = modal;
                    await PopulateFormViewModelAsync(model);
                    return View(model);
                }

                await SyncLeadContactLinksAsync(lead, contactIds);
                await _context.SaveChangesAsync();

                try
                {
                    var summary = await BuildLeadChangeSummaryAsync(snapshot, lead, contactIds);
                    await _timelineService.LogEventAsync(
                        lead.Id,
                        "Lead",
                        CrmEventType.FieldChange,
                        "Лид обновлён",
                        summary,
                        TryGetCurrentEmployeeId());
                }
                catch
                {
                    // История не должна ломать сохранение самой сущности.
                }

                if (IsInlineSaveRequest())
                {
                    return BuildInlineSaveSuccessResult("Lead", id, lead.Name);
                }

                if (modal)
                {
                    return BuildModalUpdatedContentResult("Lead", id, lead.Name);
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex) when (IsInlineSaveRequest())
            {
                var message = ex.GetBaseException().Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = string.IsNullOrWhiteSpace(message)
                        ? "Ошибка при сохранении лида."
                        : message
                });
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var lead = await _context.Leads
                .AsNoTracking()
                .Include(x => x.Pipeline)
                    .ThenInclude(x => x.Stages)
                .Include(x => x.CurrentStage)
                .Include(x => x.Responsible)
                .Include(x => x.Contact)
                .Include(x => x.Company)
                .Include(x => x.ContactLinks)
                    .ThenInclude(x => x.Contact)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (lead == null)
            {
                return NotFound();
            }

            var normalizedContactIds = NormalizeContactIds(
                lead.ContactLinks.Select(x => x.ContactId),
                lead.ContactId);

            var relatedContacts = await _context.Contacts
                .AsNoTracking()
                .Where(x => normalizedContactIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName);

            var inlineModel = new CrmProcessFormViewModel
            {
                Id = lead.Id,
                EntityCode = "Lead",
                Name = lead.Name,
                PipelineId = lead.PipelineId,
                StageId = lead.StageId,
                ResponsibleId = lead.ResponsibleId,
                ContactId = lead.ContactId,
                CompanyId = lead.CompanyId,
                Amount = lead.Amount,
                Currency = lead.Currency,
                IsConverted = lead.IsConverted,
                ConvertedAt = lead.ConvertedAt,
                DynamicValues = DeserializeDynamicValues(lead.Properties),
                ContactIds = normalizedContactIds
            };

            ViewBag.IsModal = true;
            await PopulateFormViewModelAsync(inlineModel);
            ViewBag.InlineFormModel = inlineModel;
            ViewBag.DynamicFieldsEditable = true;
            await _crmService.LogEventAsync(id, "Lead", CrmEventType.View, "Просмотр карточки", null, TryGetCurrentEmployeeId());
            var entityEvents = await _timelineService.GetEventsAsync(id, "Lead");
            ViewBag.TimelineFeed = await BuildActivityFeedAsync(
                lead.Id,
                lead.ResponsibleId,
                "Lead",
                "Leads",
                "Таймлайн пока пуст",
                "Добавьте комментарий, поставьте задачу или начните рабочую активность по этому лиду.",
                "Что произошло по этому лиду? Например: договорились перезвонить или получили уточнение от клиента.");
            ViewBag.HistoryEvents = entityEvents
                .Where(ShouldDisplayInHistory)
                .ToList();

            return View(new CrmProcessDetailsViewModel
            {
                Id = lead.Id,
                EntityCode = "Lead",
                Name = lead.Name,
                PipelineName = lead.Pipeline?.Name,
                StageName = lead.CurrentStage?.Name,
                StageColor = lead.CurrentStage?.Color ?? "#6c757d",
                ResponsibleName = lead.Responsible?.FullName,
                ContactName = lead.Contact?.FullName,
                CompanyName = lead.Company?.Name,
                Amount = lead.Amount,
                Currency = lead.Currency,
                IsConverted = lead.IsConverted,
                ConvertedAt = lead.ConvertedAt,
                CreatedAt = lead.CreatedAt,
                DynamicValues = DeserializeDynamicValues(lead.Properties),
                StageSteps = MapStageSteps(lead.Pipeline, lead.StageId),
                Contacts = normalizedContactIds
                    .Select(contactId => new CrmProcessRelatedContactViewModel
                    {
                        Id = contactId,
                        FullName = relatedContacts.TryGetValue(contactId, out var fullName) ? fullName : contactId.ToString(),
                        IsPrimary = lead.ContactId.HasValue && lead.ContactId.Value == contactId
                    })
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.FullName)
                    .ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var lead = await _context.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null)
            {
                return NotFound();
            }

            var leadName = lead.Name;
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _timelineService.LogEventAsync(
                id,
                "Lead",
                CrmEventType.System,
                "Лид перемещён в корзину",
                $"Лид \"{leadName}\" перемещён в корзину.",
                TryGetCurrentEmployeeId());

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(Guid id, string text)
        {
            var normalizedText = text?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return ModalRequestHelper.IsModalRequest(Request)
                    ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id, modal = true }))
                    : RedirectToAction(nameof(Details), new { id });
            }

            var leadExists = await _context.Leads
                .AsNoTracking()
                .AnyAsync(x => x.Id == id);

            if (!leadExists)
            {
                return NotFound();
            }

            var authorId = TryGetCurrentEmployeeId();
            if (!authorId.HasValue)
            {
                return Forbid();
            }

            await _crmActivityService.AddCommentAsync(id, "Lead", normalizedText, authorId.Value);

            return ModalRequestHelper.IsModalRequest(Request)
                ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id, modal = true }))
                : RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTask(Guid id, string title, string? description, Guid? assigneeId, DateTime? deadline)
        {
            var lead = await _context.Leads
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.ResponsibleId
                })
                .FirstOrDefaultAsync();

            if (lead == null)
            {
                return NotFound();
            }

            var normalizedTitle = title?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                SetTimelineFeedback("Укажите название задачи.", composer: "task");
                return ModalRequestHelper.IsModalRequest(Request)
                    ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id, modal = true }))
                    : RedirectToAction(nameof(Details), new { id });
            }

            var authorId = TryGetCurrentEmployeeId();
            if (!authorId.HasValue)
            {
                return Forbid();
            }

            var resolvedAssigneeId = assigneeId.HasValue && assigneeId.Value != Guid.Empty
                ? assigneeId.Value
                : lead.ResponsibleId ?? authorId.Value;

            await _crmActivityService.CreateTaskAsync(
                id,
                "Lead",
                normalizedTitle,
                description,
                authorId.Value,
                resolvedAssigneeId,
                deadline);

            return ModalRequestHelper.IsModalRequest(Request)
                ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id, modal = true }))
                : RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToDeal(Guid id)
        {
            var targetPipelineId = await _context.CrmPipelines
                .AsNoTracking()
                .Where(x => x.TargetEntityCode == "Deal" && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();

            if (!targetPipelineId.HasValue)
            {
                return BadRequest("Не настроена воронка сделок.");
            }

            var deal = await _crmService.ConvertLeadToDealAsync(id, targetPipelineId.Value);
            return RedirectToAction("Details", "Deals", new { id = deal.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStage(Guid leadId, Guid newStageId)
        {
            var validation = await _crmService.ValidateStageTransitionAsync(leadId, "Lead", newStageId);
            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    message = "Переход невозможен, пока не заполнены обязательные поля этапа.",
                    missingFields = validation.MissingFieldNames
                });
            }

            var success = await _crmService.ChangeStageAsync(leadId, "Lead", newStageId);
            return success
                ? Json(new { success = true })
                : BadRequest(new { message = "Ошибка перехода." });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProperty(Guid id, string name, string value)
        {
            var success = await _crmService.UpdatePropertyAsync(id, "Lead", name, value, TryGetCurrentEmployeeId() ?? Guid.Empty);
            return success
                ? Json(new { success = true })
                : Json(new { success = false, message = "Не удалось обновить поле" });
        }

        private async Task<List<CrmPipeline>> LoadPipelinesAsync()
        {
            return await _context.CrmPipelines
                .AsNoTracking()
                .Include(x => x.Stages)
                .Where(x => x.TargetEntityCode == "Lead" && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync();
        }

        private async Task LoadDynamicMetadataAsync(CrmProcessFormViewModel formModel)
        {
            var appDefinition = await _context.AppDefinitions
                .AsNoTracking()
                .Include(x => x.Fields)
                .FirstOrDefaultAsync(x => x.EntityCode == "Lead");

            var fields = appDefinition?.Fields
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ToList() ?? new List<AppFieldDefinition>();

            var quickCreatableEntityCodes = await BuildQuickCreatableEntityCodeSetAsync(fields);
            var layoutEntity = formModel.PipelineId != Guid.Empty
                ? await _context.CrmPipelineCardLayouts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PipelineId == formModel.PipelineId)
                : null;
            var cardLayout = CrmCardLayoutCatalog.ParseOrDefault(layoutEntity?.Layout, "Lead", fields);

            ViewBag.DynamicFields = fields;
            ViewBag.LookupData = await BuildEntityLinkLookupDataAsync(fields);
            ViewBag.EntityCode = "Lead";
            ViewBag.ContactCreateUrl = BuildModalCreateUrl("Contact", quickCreatableEntityCodes) ?? "/Contacts/Create?modal=true";
            ViewBag.CompanyCreateUrl = "/Data/Company/Create?modal=true";
            ViewBag.DynamicFieldCreateUrls = fields
                .Where(x => x.DataType == FieldDataType.EntityLink && !string.IsNullOrWhiteSpace(x.TargetEntityCode))
                .Select(x => new
                {
                    x.SystemName,
                    Url = BuildModalCreateUrl(x.TargetEntityCode, quickCreatableEntityCodes)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .ToDictionary(x => x.SystemName, x => x.Url!, StringComparer.OrdinalIgnoreCase);
            ViewBag.CrmCardLayoutRenderModel = new CrmCardLayoutRenderViewModel
            {
                EntityCode = "Lead",
                AppDefinitionId = appDefinition?.Id ?? Guid.Empty,
                PipelineId = formModel.PipelineId,
                FormModel = formModel,
                Layout = cardLayout,
                DynamicFields = fields,
                LookupData = ViewBag.LookupData,
                DynamicFieldCreateUrls = ViewBag.DynamicFieldCreateUrls,
                AllDefinitions = await _context.AppDefinitions
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .ToListAsync()
            };
        }

        private async Task PopulateFormViewModelAsync(CrmProcessFormViewModel model)
        {
            var pipelines = await LoadPipelinesAsync();
            if (!pipelines.Any())
            {
                return;
            }

            if (model.PipelineId == Guid.Empty || pipelines.All(x => x.Id != model.PipelineId))
            {
                model.PipelineId = pipelines.First().Id;
            }

            var stagesByPipeline = pipelines.ToDictionary(
                pipeline => pipeline.Id,
                pipeline => pipeline.Stages
                    .OrderBy(stage => stage.SortOrder)
                    .Select(stage => new SelectListItem(stage.Name, stage.Id.ToString()))
                    .ToList());

            model.StagesByPipeline = stagesByPipeline;
            model.PipelineOptions = pipelines
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();

            model.StageOptions = stagesByPipeline.TryGetValue(model.PipelineId, out var selectedStages)
                ? selectedStages
                : new List<SelectListItem>();

            if (model.StageId == Guid.Empty || model.StageOptions.All(x => x.Value != model.StageId.ToString()))
            {
                model.StageId = model.StageOptions
                    .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
                    .FirstOrDefault(x => x != Guid.Empty);
            }

            model.ResponsibleOptions = await _context.Employees
                .AsNoTracking()
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
                .ToListAsync();

            model.ContactOptions = await _context.Contacts
                .AsNoTracking()
                .OrderBy(x => x.FullName)
                .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
                .ToListAsync();

            model.CompanyOptions = await _context.CrmCompanies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToListAsync();

            await LoadDynamicMetadataAsync(model);
        }

        private static CrmProcessIndexItemViewModel MapLeadToIndexItem(Lead lead)
        {
            return new CrmProcessIndexItemViewModel
            {
                Id = lead.Id,
                Name = lead.Name,
                PipelineName = lead.Pipeline?.Name,
                StageName = lead.CurrentStage?.Name,
                StageColor = lead.CurrentStage?.Color ?? "#6c757d",
                ResponsibleName = lead.Responsible?.FullName,
                ContactName = lead.Contact?.FullName,
                CompanyName = lead.Company?.Name,
                Amount = lead.Amount,
                Currency = lead.Currency,
                CreatedAt = lead.CreatedAt,
                IsConverted = lead.IsConverted,
                ConvertedAt = lead.ConvertedAt
            };
        }

        private static CrmProcessKanbanCardViewModel MapLeadToKanbanCard(Lead lead)
        {
            return new CrmProcessKanbanCardViewModel
            {
                Id = lead.Id,
                Name = lead.Name,
                StageId = lead.StageId,
                ResponsibleName = lead.Responsible?.FullName,
                ContactName = lead.Contact?.FullName,
                CompanyName = lead.Company?.Name,
                Amount = lead.Amount,
                Currency = lead.Currency,
                IsConverted = lead.IsConverted,
                CreatedAt = lead.CreatedAt
            };
        }

        private async Task<string?> BuildLeadChangeSummaryAsync(LeadSnapshot before, Lead after, IReadOnlyCollection<Guid> currentContactIds)
        {
            var pipelineNames = await _context.CrmPipelines
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var stageNames = await _context.CrmStages
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var employeeNames = await _context.Employees
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.FullName);

            var contactNames = await _context.Contacts
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.FullName);

            var companyNames = await _context.CrmCompanies
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var fieldLabels = await LoadFieldLabelMapAsync("Lead");
            var beforeProps = TimelineChangeFormatter.ParseDynamicProperties(before.Properties);
            var afterProps = TimelineChangeFormatter.ParseDynamicProperties(after.Properties);

            var changes = new List<string>();
            TimelineChangeFormatter.AddScalarChange(changes, "Название", before.Name, after.Name);
            TimelineChangeFormatter.AddScalarChange(changes, "Воронка", ResolveName(pipelineNames, before.PipelineId), ResolveName(pipelineNames, after.PipelineId));
            TimelineChangeFormatter.AddScalarChange(changes, "Стадия", ResolveName(stageNames, before.StageId), ResolveName(stageNames, after.StageId));
            TimelineChangeFormatter.AddScalarChange(changes, "Ответственный", ResolveName(employeeNames, before.ResponsibleId), ResolveName(employeeNames, after.ResponsibleId));
            TimelineChangeFormatter.AddScalarChange(changes, "Основной контакт", ResolveName(contactNames, before.ContactId), ResolveName(contactNames, after.ContactId));
            TimelineChangeFormatter.AddScalarChange(changes, "Компания", ResolveName(companyNames, before.CompanyId), ResolveName(companyNames, after.CompanyId));
            TimelineChangeFormatter.AddScalarChange(changes, "Сумма", before.Amount.ToString("N2"), after.Amount.ToString("N2"));
            TimelineChangeFormatter.AddScalarChange(changes, "Валюта", before.Currency, after.Currency);
            TimelineChangeFormatter.AddCollectionChange(
                changes,
                "Контакты лида",
                before.ContactIds.Select(id => ResolveName(contactNames, id) ?? id.ToString()),
                currentContactIds.Select(id => ResolveName(contactNames, id) ?? id.ToString()));
            TimelineChangeFormatter.AddDictionaryChanges(
                changes,
                beforeProps,
                afterProps,
                key => fieldLabels.TryGetValue(key, out var label) ? label : key);

            return TimelineChangeFormatter.BuildSummary(changes);
        }

        private static Dictionary<string, object> DeserializeDynamicValues(string? properties)
        {
            if (string.IsNullOrWhiteSpace(properties))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(properties) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static string NormalizeName(string? value) => value?.Trim() ?? string.Empty;

        private static string NormalizeCurrency(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "RUB"
                : value.Trim().ToUpperInvariant();
        }

        private async Task SyncLeadContactLinksAsync(Lead lead, IReadOnlyCollection<Guid> contactIds)
        {
            var normalizedContactIds = contactIds
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToHashSet();

            var existingLinks = await _context.CrmLeadContacts
                .Where(x => x.LeadId == lead.Id)
                .ToListAsync();

            var toRemove = existingLinks
                .Where(x => !normalizedContactIds.Contains(x.ContactId))
                .ToList();

            if (toRemove.Count > 0)
            {
                _context.CrmLeadContacts.RemoveRange(toRemove);
            }

            foreach (var existingLink in existingLinks.Except(toRemove))
            {
                existingLink.IsPrimary = lead.ContactId.HasValue && existingLink.ContactId == lead.ContactId.Value;
            }

            foreach (var contactId in normalizedContactIds.Except(existingLinks.Select(x => x.ContactId)))
            {
                _context.CrmLeadContacts.Add(new CrmLeadContact
                {
                    LeadId = lead.Id,
                    ContactId = contactId,
                    IsPrimary = lead.ContactId.HasValue && lead.ContactId.Value == contactId
                });
            }
        }

        private static List<Guid> NormalizeContactIds(IEnumerable<Guid>? contactIds, Guid? primaryContactId)
        {
            var normalized = (contactIds ?? Array.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (primaryContactId.HasValue &&
                primaryContactId.Value != Guid.Empty &&
                !normalized.Contains(primaryContactId.Value))
            {
                normalized.Insert(0, primaryContactId.Value);
            }

            return normalized;
        }

        private static Guid? NormalizePrimaryContact(Guid? primaryContactId, IReadOnlyCollection<Guid> contactIds)
        {
            if (primaryContactId.HasValue && primaryContactId.Value != Guid.Empty)
            {
                return primaryContactId;
            }

            var fallbackContactId = contactIds.FirstOrDefault();
            return fallbackContactId != Guid.Empty
                ? fallbackContactId
                : null;
        }

        private static string NormalizeViewMode(string? value)
        {
            return string.Equals(value, "list", StringComparison.OrdinalIgnoreCase)
                ? "list"
                : "kanban";
        }

        private static decimal ParseAmountInput(IFormCollection form, decimal fallback)
        {
            var raw = form[nameof(CrmProcessFormViewModel.Amount)].ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0m;
            }

            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) ||
                decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return amount;
            }

            var normalized = raw.Replace(" ", string.Empty).Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                ? amount
                : fallback;
        }

        private static List<CrmProcessStageStepViewModel> MapStageSteps(CrmPipeline? pipeline, Guid currentStageId)
        {
            if (pipeline?.Stages == null || pipeline.Stages.Count == 0)
            {
                return new List<CrmProcessStageStepViewModel>();
            }

            var orderedStages = pipeline.Stages
                .OrderBy(x => x.SortOrder)
                .ToList();

            var currentStage = orderedStages.FirstOrDefault(x => x.Id == currentStageId);
            var currentSortOrder = currentStage?.SortOrder ?? int.MaxValue;

            return orderedStages
                .Select(stage => new CrmProcessStageStepViewModel
                {
                    StageId = stage.Id,
                    Name = stage.Name,
                    Color = stage.Color ?? "#cbd5e1",
                    IsCurrent = stage.Id == currentStageId,
                    IsReached = stage.SortOrder <= currentSortOrder,
                    IsFinal = stage.StageType != 0
                })
                .ToList();
        }

        private async Task<CrmActivityFeedViewModel> BuildActivityFeedAsync(
            Guid entityId,
            Guid? responsibleId,
            string entityCode,
            string controllerName,
            string emptyTitle,
            string emptyDescription,
            string commentPlaceholder)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(x => !x.IsDismissed)
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.FullName
                })
                .ToListAsync();

            var currentEmployeeId = TryGetCurrentEmployeeId();
            var defaultAssigneeId = responsibleId ?? currentEmployeeId;

            return new CrmActivityFeedViewModel
            {
                EntityId = entityId,
                EntityCode = entityCode,
                ControllerName = controllerName,
                EmptyTitle = emptyTitle,
                EmptyDescription = emptyDescription,
                CommentComposerTitle = "Комментарий по лиду",
                CommentComposerPlaceholder = commentPlaceholder,
                TaskComposerTitle = "Задача по лиду",
                TaskTitlePlaceholder = "Например: уточнить бюджет или согласовать повторный звонок",
                TaskDescriptionPlaceholder = "Что именно нужно сделать и какой контекст важно не потерять?",
                DefaultTaskAssigneeId = defaultAssigneeId,
                ActiveComposer = TempData["CrmTimelineComposer"] as string ?? "comment",
                FeedbackMessage = TempData["CrmTimelineFeedbackMessage"] as string,
                FeedbackTone = TempData["CrmTimelineFeedbackTone"] as string ?? "danger",
                TaskAssigneeOptions = employees,
                Activities = (await _crmActivityService.GetActivitiesAsync(entityId, entityCode)).ToList()
            };
        }

        private static bool ShouldDisplayInHistory(CrmEvent entityEvent)
        {
            return entityEvent.Type == CrmEventType.FieldChange
                   || entityEvent.Type == CrmEventType.System;
        }

        private void SetTimelineFeedback(string message, string tone = "danger", string composer = "comment")
        {
            TempData["CrmTimelineFeedbackMessage"] = message;
            TempData["CrmTimelineFeedbackTone"] = tone;
            TempData["CrmTimelineComposer"] = composer;
        }

        private static string? ResolveName(IReadOnlyDictionary<Guid, string> names, Guid? id)
        {
            return id.HasValue && names.TryGetValue(id.Value, out var name)
                ? name
                : null;
        }

        private sealed record LeadSnapshot(
            string Name,
            Guid PipelineId,
            Guid StageId,
            Guid? ResponsibleId,
            Guid? ContactId,
            Guid? CompanyId,
            decimal Amount,
            string Currency,
            string? Properties,
            IReadOnlyCollection<Guid> ContactIds);
    }
}
