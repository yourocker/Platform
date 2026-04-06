using Core.Data;
using Core.Entities.CRM;
using Core.Interfaces.CRM;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using Core.Entities.Platform;
using Core.Entities.System;
using Newtonsoft.Json;

namespace Core.Services.CRM
{
    public class CrmService : ICrmService
    {
        private readonly AppDbContext _context;

        public CrmService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Lead> CreateLeadAsync(Lead lead)
        {
            if (lead.Id == Guid.Empty)
            {
                lead.Id = Guid.NewGuid();
            }

            if (string.IsNullOrWhiteSpace(lead.EntityCode))
            {
                lead.EntityCode = "Lead";
            }

            if (lead.StageId == Guid.Empty)
            {
                var firstStage = await _context.CrmStages
                    .Where(s => s.PipelineId == lead.PipelineId)
                    .OrderBy(s => s.SortOrder)
                    .FirstOrDefaultAsync();
                
                if (firstStage != null) lead.StageId = firstStage.Id;
            }

            lead.CreatedAt = DateTime.UtcNow;
            lead.StageChangedAt = DateTime.UtcNow;
            EnsureLeadContactLinks(lead);

            _context.Leads.Add(lead);
            await LogEventInternalAsync(lead.Id, "Lead", CrmEventType.System, "Лид создан", null, lead.ResponsibleId);
            await _context.SaveChangesAsync();
            return lead;
        }

        public async Task<Deal> CreateDealAsync(Deal deal)
        {
            if (deal.Id == Guid.Empty)
            {
                deal.Id = Guid.NewGuid();
            }

            if (string.IsNullOrWhiteSpace(deal.EntityCode))
            {
                deal.EntityCode = "Deal";
            }

            if (deal.StageId == Guid.Empty)
            {
                var firstStage = await _context.CrmStages
                    .Where(s => s.PipelineId == deal.PipelineId)
                    .OrderBy(s => s.SortOrder)
                    .FirstOrDefaultAsync();
                
                if (firstStage != null) deal.StageId = firstStage.Id;
            }

            deal.CreatedAt = DateTime.UtcNow;
            deal.StageChangedAt = DateTime.UtcNow;
            EnsureDealContactLinks(deal);

            _context.Deals.Add(deal);
            await LogEventInternalAsync(deal.Id, "Deal", CrmEventType.System, "Сделка создана", null, deal.ResponsibleId);
            await _context.SaveChangesAsync();
            return deal;
        }

        public async Task<bool> ChangeStageAsync(Guid entityId, string entityCode, Guid newStageId)
        {
            var validation = await ValidateStageTransitionAsync(entityId, entityCode, newStageId);
            if (!validation.IsValid) return false;

            string oldStageName = "";
            var newStage = await _context.CrmStages.FindAsync(newStageId);
            string newStageName = newStage?.Name ?? "Неизвестный этап";
            var shouldConvertLead = false;

            if (entityCode == "Lead")
            {
                var entity = await _context.Leads.Include(l => l.CurrentStage).FirstOrDefaultAsync(l => l.Id == entityId);
                if (entity == null) return false;
                oldStageName = entity.CurrentStage?.Name ?? "Начало";
                entity.StageId = newStageId;
                entity.StageChangedAt = DateTime.UtcNow;
                shouldConvertLead = newStage?.StageType == 1 && !entity.IsConverted;
                await LogEventInternalAsync(entityId, "Lead", CrmEventType.System, "Смена этапа", $"Этап изменен с \"{oldStageName}\" на \"{newStageName}\"", null);
            }
            else
            {
                var entity = await _context.Deals.Include(d => d.CurrentStage).FirstOrDefaultAsync(d => d.Id == entityId);
                if (entity == null) return false;
                oldStageName = entity.CurrentStage?.Name ?? "Начало";
                entity.StageId = newStageId;
                entity.StageChangedAt = DateTime.UtcNow;
                await LogEventInternalAsync(entityId, "Deal", CrmEventType.System, "Смена этапа", $"Этап изменен с \"{oldStageName}\" на \"{newStageName}\"", null);
            }

            await _context.SaveChangesAsync();

            if (shouldConvertLead)
            {
                var targetPipelineId = await _context.CrmPipelines
                    .AsNoTracking()
                    .Where(x => x.TargetEntityCode == "Deal" && x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefaultAsync();

                if (targetPipelineId.HasValue)
                {
                    await ConvertLeadToDealAsync(entityId, targetPipelineId.Value);
                }
            }

            return true;
        }

        public async Task<(bool IsValid, List<string> MissingFieldNames)> ValidateStageTransitionAsync(Guid entityId, string entityCode, Guid newStageId)
        {
            var stage = await _context.CrmStages.FindAsync(newStageId);
            if (stage == null || string.IsNullOrEmpty(stage.RequiredFieldIdsJson)) return (true, new List<string>());

            var requiredFieldIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(stage.RequiredFieldIdsJson) ?? new();
            if (!requiredFieldIds.Any()) return (true, new List<string>());

            CrmBaseProcessEntity? entity = entityCode == "Lead" 
                ? await _context.Leads.FindAsync(entityId) 
                : await _context.Deals.FindAsync(entityId);

            if (entity == null) return (false, new List<string> { "Сущность не найдена" });

            var properties = string.IsNullOrEmpty(entity.Properties) 
                ? new Dictionary<string, object>() 
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Properties) ?? new();

            var missingFields = new List<string>();
            var fieldDefinitions = await _context.AppFieldDefinitions
                .Where(f => requiredFieldIds.Contains(f.Id))
                .ToListAsync();

            foreach (var field in fieldDefinitions)
            {
                if (!properties.TryGetValue(field.SystemName, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    missingFields.Add(field.Label);
                }
            }

            return (!missingFields.Any(), missingFields);
        }

        public async Task<Deal> ConvertLeadToDealAsync(Guid leadId, Guid targetPipelineId)
        {
            var lead = await _context.Leads
                .AsNoTracking()
                .Include(l => l.ContactLinks)
                .FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead == null) throw new Exception("Лид не найден");

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = lead.Name,
                EntityCode = "Deal",
                PipelineId = targetPipelineId,
                ContactId = lead.ContactId,
                CompanyId = lead.CompanyId,
                ResponsibleId = lead.ResponsibleId,
                Amount = lead.Amount,
                Currency = lead.Currency,
                Properties = lead.Properties,
                SourceLeadId = lead.Id,
                CreatedAt = DateTime.UtcNow,
                ContactLinks = lead.ContactLinks
                    .Where(link => link.ContactId != Guid.Empty)
                    .Select(link => new CrmDealContact
                    {
                        ContactId = link.ContactId,
                        IsPrimary = link.IsPrimary
                    })
                    .ToList()
            };

            var leadToUpdate = await _context.Leads.FindAsync(leadId);
            if (leadToUpdate != null)
            {
                leadToUpdate.IsConverted = true;
                leadToUpdate.ConvertedAt ??= DateTime.UtcNow;

                var convertedStageId = await _context.CrmStages
                    .AsNoTracking()
                    .Where(s => s.PipelineId == leadToUpdate.PipelineId && s.StageType == 1)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => (Guid?)s.Id)
                    .FirstOrDefaultAsync();

                if (convertedStageId.HasValue)
                {
                    leadToUpdate.StageId = convertedStageId.Value;
                    leadToUpdate.StageChangedAt = DateTime.UtcNow;
                }
            }

            await LogEventInternalAsync(
                leadId,
                "Lead",
                CrmEventType.System,
                "Лид сконвертирован",
                "Создана сделка на базе лида. Лид сохранён и может быть сконвертирован повторно.",
                null);
            return await CreateDealAsync(deal);
        }

        public async Task CalculateDealAmountAsync(Guid dealId)
        {
            var deal = await _context.Deals.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == dealId);
            if (deal == null) return;
            deal.Amount = deal.Items.Sum(i => i.TotalPrice);
            await _context.SaveChangesAsync();
        }

        public async Task LogEventAsync(Guid targetId, string entityCode, CrmEventType type, string title, string? content, Guid? employeeId)
        {
            await LogEventInternalAsync(targetId, entityCode, type, title, content, employeeId);
            await _context.SaveChangesAsync();
        }

        private async Task LogEventInternalAsync(Guid targetId, string entityCode, CrmEventType type, string title, string? content, Guid? employeeId)
        {
            var evt = new CrmEvent
            {
                Id = Guid.NewGuid(),
                TargetId = targetId,
                TargetEntityCode = entityCode,
                Type = type,
                Title = title,
                Content = content,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            };
            _context.CrmEvents.Add(evt);
            if (type == CrmEventType.System || type == CrmEventType.Comment)
            {
                await CreateNotificationAsync(targetId, entityCode, title, content ?? "");
            }
        }

        private async Task CreateNotificationAsync(Guid targetId, string entityCode, string title, string message)
        {
            Guid? recipientId = null;
            if (entityCode == "Lead") 
                recipientId = (await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == targetId))?.ResponsibleId;
            else 
                recipientId = (await _context.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == targetId))?.ResponsibleId;

            if (recipientId.HasValue && recipientId != Guid.Empty)
            {
                var payload = new { RecipientId = recipientId.Value, Title = $"CRM: {title}", Message = message, Url = $"/{entityCode}s/Details/{targetId}" };
                var outbox = new OutboxEvent { Id = Guid.NewGuid(), EventType = "CRM_NOTIFICATION", Payload = JsonConvert.SerializeObject(payload), CreatedAt = DateTime.UtcNow };
                _context.Set<OutboxEvent>().Add(outbox);
            }
        }

        // --- НОВЫЙ МЕТОД: УНИВЕРСАЛЬНОЕ ОБНОВЛЕНИЕ СВОЙСТВА (ШАГ 4) ---
        public async Task<bool> UpdatePropertyAsync(Guid id, string entityCode, string propertyName, string newValue, Guid editorId)
        {
            CrmBaseProcessEntity? entity = entityCode == "Lead" 
                ? await _context.Leads.FindAsync(id) 
                : await _context.Deals.FindAsync(id);

            if (entity == null) return false;

            string oldValue = "";
            bool isSystemField = true;
            var dealEntity = entity as Deal;
            var leadEntity = entity as Lead;

            // 1. Проверяем системные поля
            switch (propertyName)
            {
                case "Name":
                    oldValue = entity.Name;
                    entity.Name = newValue;
                    break;
                case "ResponsibleId":
                    oldValue = entity.ResponsibleId?.ToString() ?? "Не назначен";
                    entity.ResponsibleId = Guid.TryParse(newValue, out var rId) ? rId : null;
                    break;
                case "ContactId":
                    oldValue = entity.ContactId?.ToString() ?? "Нет связи";
                    entity.ContactId = Guid.TryParse(newValue, out var cId) ? cId : null;
                    if (dealEntity != null)
                    {
                        await SyncDealPrimaryContactAsync(dealEntity);
                    }
                    else if (leadEntity != null)
                    {
                        await SyncLeadPrimaryContactAsync(leadEntity);
                    }
                    break;
                case "CompanyId" when dealEntity != null || leadEntity != null:
                    oldValue = dealEntity?.CompanyId?.ToString() ?? leadEntity?.CompanyId?.ToString() ?? "Нет связи";
                    var parsedCompanyId = Guid.TryParse(newValue, out var companyId) ? companyId : (Guid?)null;
                    if (dealEntity != null)
                    {
                        dealEntity.CompanyId = parsedCompanyId;
                    }
                    else if (leadEntity != null)
                    {
                        leadEntity.CompanyId = parsedCompanyId;
                    }
                    break;
                case "Amount":
                    oldValue = entity.Amount.ToString();
                    entity.Amount = TryParseAmount(newValue, out var amt) ? amt : 0;
                    break;
                default:
                    isSystemField = false;
                    break;
            }

            // 2. Если не системное - обновляем в JSON Properties
            if (!isSystemField)
            {
                var props = string.IsNullOrEmpty(entity.Properties) 
                    ? new Dictionary<string, object>() 
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Properties) ?? new();

                if (props.TryGetValue(propertyName, out var oldVal)) oldValue = oldVal?.ToString() ?? "";
                props[propertyName] = newValue;
                entity.Properties = System.Text.Json.JsonSerializer.Serialize(props);
            }

            // 3. Логируем изменение для "Истории"
            await LogEventInternalAsync(id, entityCode, CrmEventType.FieldChange, "Изменение поля", $"Поле '{propertyName}' изменено. Старое значение: {oldValue}, Новое: {newValue}", editorId);
            
            await _context.SaveChangesAsync();
            return true;
        }

        private static bool TryParseAmount(string? rawValue, out decimal amount)
        {
            amount = 0m;
            var raw = rawValue?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) ||
                decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return true;
            }

            var normalized = raw.Replace(" ", string.Empty).Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        }

        private void EnsureDealContactLinks(Deal deal)
        {
            deal.ContactLinks ??= new List<CrmDealContact>();

            var normalizedLinks = deal.ContactLinks
                .Where(link => link.ContactId != Guid.Empty)
                .GroupBy(link => link.ContactId)
                .Select(group =>
                {
                    var first = group.First();
                    first.IsPrimary = group.Any(x => x.IsPrimary);
                    return first;
                })
                .ToList();

            if (deal.ContactId.HasValue && deal.ContactId.Value != Guid.Empty)
            {
                var primaryLink = normalizedLinks.FirstOrDefault(link => link.ContactId == deal.ContactId.Value);
                if (primaryLink == null)
                {
                    normalizedLinks.Add(new CrmDealContact
                    {
                        DealId = deal.Id,
                        ContactId = deal.ContactId.Value,
                        IsPrimary = true
                    });
                }
                else
                {
                    primaryLink.IsPrimary = true;
                }
            }
            else
            {
                var primaryLink = normalizedLinks.FirstOrDefault(link => link.IsPrimary) ?? normalizedLinks.FirstOrDefault();
                if (primaryLink != null)
                {
                    primaryLink.IsPrimary = true;
                    deal.ContactId = primaryLink.ContactId;
                }
            }

            foreach (var link in normalizedLinks)
            {
                link.DealId = deal.Id;
                if (deal.ContactId.HasValue && deal.ContactId.Value != Guid.Empty)
                {
                    link.IsPrimary = link.ContactId == deal.ContactId.Value;
                }
            }

            deal.ContactLinks = normalizedLinks;
        }

        private void EnsureLeadContactLinks(Lead lead)
        {
            lead.ContactLinks ??= new List<CrmLeadContact>();

            var normalizedLinks = lead.ContactLinks
                .Where(link => link.ContactId != Guid.Empty)
                .GroupBy(link => link.ContactId)
                .Select(group =>
                {
                    var first = group.First();
                    first.IsPrimary = group.Any(x => x.IsPrimary);
                    return first;
                })
                .ToList();

            if (lead.ContactId.HasValue && lead.ContactId.Value != Guid.Empty)
            {
                var primaryLink = normalizedLinks.FirstOrDefault(link => link.ContactId == lead.ContactId.Value);
                if (primaryLink == null)
                {
                    normalizedLinks.Add(new CrmLeadContact
                    {
                        LeadId = lead.Id,
                        ContactId = lead.ContactId.Value,
                        IsPrimary = true
                    });
                }
                else
                {
                    primaryLink.IsPrimary = true;
                }
            }
            else
            {
                var primaryLink = normalizedLinks.FirstOrDefault(link => link.IsPrimary) ?? normalizedLinks.FirstOrDefault();
                if (primaryLink != null)
                {
                    primaryLink.IsPrimary = true;
                    lead.ContactId = primaryLink.ContactId;
                }
            }

            foreach (var link in normalizedLinks)
            {
                link.LeadId = lead.Id;
                if (lead.ContactId.HasValue && lead.ContactId.Value != Guid.Empty)
                {
                    link.IsPrimary = link.ContactId == lead.ContactId.Value;
                }
            }

            lead.ContactLinks = normalizedLinks;
        }

        private async Task SyncDealPrimaryContactAsync(Deal deal)
        {
            var existingLinks = await _context.CrmDealContacts
                .Where(link => link.DealId == deal.Id)
                .ToListAsync();

            if (!deal.ContactId.HasValue || deal.ContactId.Value == Guid.Empty)
            {
                foreach (var link in existingLinks)
                {
                    link.IsPrimary = false;
                }

                return;
            }

            var primaryContactId = deal.ContactId.Value;
            var primaryLink = existingLinks.FirstOrDefault(link => link.ContactId == primaryContactId);
            if (primaryLink == null)
            {
                primaryLink = new CrmDealContact
                {
                    DealId = deal.Id,
                    ContactId = primaryContactId,
                    IsPrimary = true
                };
                _context.CrmDealContacts.Add(primaryLink);
                existingLinks.Add(primaryLink);
            }

            foreach (var link in existingLinks)
            {
                link.IsPrimary = link.ContactId == primaryContactId;
            }
        }

        private async Task SyncLeadPrimaryContactAsync(Lead lead)
        {
            var existingLinks = await _context.CrmLeadContacts
                .Where(link => link.LeadId == lead.Id)
                .ToListAsync();

            if (!lead.ContactId.HasValue || lead.ContactId.Value == Guid.Empty)
            {
                foreach (var link in existingLinks)
                {
                    link.IsPrimary = false;
                }

                return;
            }

            var primaryContactId = lead.ContactId.Value;
            var primaryLink = existingLinks.FirstOrDefault(link => link.ContactId == primaryContactId);
            if (primaryLink == null)
            {
                primaryLink = new CrmLeadContact
                {
                    LeadId = lead.Id,
                    ContactId = primaryContactId,
                    IsPrimary = true
                };
                _context.CrmLeadContacts.Add(primaryLink);
                existingLinks.Add(primaryLink);
            }

            foreach (var link in existingLinks)
            {
                link.IsPrimary = link.ContactId == primaryContactId;
            }
        }
    }
}
