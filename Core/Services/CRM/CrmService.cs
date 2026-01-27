using Core.Data;
using Core.Entities.CRM;
using Core.Interfaces.CRM;
using Microsoft.EntityFrameworkCore;
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

            _context.Leads.Add(lead);
            await LogEventInternalAsync(lead.Id, "Lead", CrmEventType.System, "Лид создан", null, lead.ResponsibleId);
            await _context.SaveChangesAsync();
            return lead;
        }

        public async Task<Deal> CreateDealAsync(Deal deal)
        {
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

            if (entityCode == "Lead")
            {
                var entity = await _context.Leads.Include(l => l.CurrentStage).FirstOrDefaultAsync(l => l.Id == entityId);
                if (entity == null) return false;
                oldStageName = entity.CurrentStage?.Name ?? "Начало";
                entity.StageId = newStageId;
                entity.StageChangedAt = DateTime.UtcNow;
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
            var lead = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId);
            if (lead == null) throw new Exception("Лид не найден");

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = lead.Name,
                EntityCode = "Deal",
                PipelineId = targetPipelineId,
                ContactId = lead.ContactId,
                ResponsibleId = lead.ResponsibleId,
                Amount = lead.Amount,
                Properties = lead.Properties,
                SourceLeadId = lead.Id,
                CreatedAt = DateTime.UtcNow
            };

            var leadToUpdate = await _context.Leads.FindAsync(leadId);
            if (leadToUpdate != null)
            {
                leadToUpdate.IsConverted = true;
                leadToUpdate.ConvertedDealId = deal.Id;
            }

            await LogEventInternalAsync(leadId, "Lead", CrmEventType.System, "Лид сконвертирован", "Создана сделка на базе лида", null);
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
                    break;
                case "Amount":
                    oldValue = entity.Amount.ToString();
                    entity.Amount = decimal.TryParse(newValue, out var amt) ? amt : 0;
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
    }
}