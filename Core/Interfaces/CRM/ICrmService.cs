using Core.Entities.CRM;

namespace Core.Interfaces.CRM
{
    /// <summary>
    /// Интерфейс для управления бизнес-логикой CRM (Лиды и Сделки).
    /// </summary>
    public interface ICrmService
    {
        // --- Управление Лидами ---
        Task<Lead> CreateLeadAsync(Lead lead);
        Task<Deal> ConvertLeadToDealAsync(Guid leadId, Guid targetPipelineId);

        // --- Управление Сделками ---
        Task<Deal> CreateDealAsync(Deal deal);

        // --- Общая логика процессов ---
        
        /// <summary>
        /// Смена этапа сущности с валидацией обязательных полей.
        /// </summary>
        /// <param name="entityId">ID Лида или Сделки</param>
        /// <param name="entityCode">"Lead" или "Deal"</param>
        /// <param name="newStageId">ID целевого этапа</param>
        Task<bool> ChangeStageAsync(Guid entityId, string entityCode, Guid newStageId);

        /// <summary>
        /// Проверка, заполнены ли все обязательные поля для перехода на этап.
        /// </summary>
        Task<(bool IsValid, List<string> MissingFieldNames)> ValidateStageTransitionAsync(Guid entityId, string entityCode, Guid newStageId);
        
        /// <summary>
        /// Метод для пересчета суммы сделки.
        /// </summary>
        Task CalculateDealAmountAsync(Guid dealId);

        // --- СОБЫТИЯ И ИСТОРИЯ (ДОПОЛНЕНО) ---
        
        /// <summary>
        /// Регистрация события в таймлайне/истории карточки.
        /// </summary>
        Task LogEventAsync(Guid targetId, string entityCode, CrmEventType type, string title, string? content, Guid? employeeId);
        
        Task<bool> UpdatePropertyAsync(Guid id, string entityCode, string propertyName, string newValue, Guid editorId);
    }
}