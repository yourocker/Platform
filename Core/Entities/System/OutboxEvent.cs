using System;

namespace Core.Entities.System
{
    // Модель для хранения исходящих событий (согласно паттерну Outbox)
    public class OutboxEvent
    {
        public Guid Id { get; set; }
        
        // Тип события (например: TASK_ASSIGNED, TASK_COMMENT_ADDED)
        public string EventType { get; set; } = string.Empty;
        
        // Данные события в формате JSON (кто, кому, ссылка, текст)
        public string Payload { get; set; } = string.Empty;
        
        // Дата создания события
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Когда событие было успешно передано в микросервис уведомлений
        public DateTime? ProcessedAt { get; set; }
    }
}