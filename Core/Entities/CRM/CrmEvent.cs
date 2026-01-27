using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Company;

namespace Core.Entities.CRM
{
    public enum CrmEventType 
    { 
        System,      // Создание, смена этапа (неудаляемые)
        Comment,     // Свободный текст пользователя
        FieldChange, // Изменение любого поля (для Истории)
        View,        // Факт просмотра карточки
        TaskLink     // Ссылка на созданную задачу
    }

    [Table("CrmEvents")]
    public class CrmEvent
    {
        [Key]
        public Guid Id { get; set; }

        // К какой сущности привязано (Lead или Deal)
        public Guid TargetId { get; set; }
        public string TargetEntityCode { get; set; } = string.Empty;

        public CrmEventType Type { get; set; }

        // Заголовок (например, "Стадия изменена")
        public string Title { get; set; } = string.Empty;

        // Основной контент (текст комментария или JSON с изменениями полей)
        public string? Content { get; set; }

        // Кто совершил действие
        public Guid? EmployeeId { get; set; }
        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Инструменты управления лентой
        public bool IsPinned { get; set; } // Закреплено наверху
        
        // Для системных событий запрещаем удаление на уровне логики
        public bool IsSystem => Type == CrmEventType.System;
    }
}