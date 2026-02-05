using Core.Entities.Platform;
using Core.FormEngine.Domain;

namespace CRM.ViewModels
{
    /// <summary>
    /// Модель данных для инициализации конструктора форм.
    /// Передает контекст сущности и список доступных полей (системных + динамических).
    /// </summary>
    public class FormDesignerViewModel
    {
        /// <summary>
        /// ID сущности (Contact, Task и т.д.), для которой настраиваем форму
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// Название сущности (для заголовка)
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Список всех доступных полей этой сущности (Палитра).
        /// Включает и динамические поля из БД, и системные свойства (FirstName, Phones) из C# кода.
        /// </summary>
        public List<DesignerFieldDto> AvailableFields { get; set; } = new();

        /// <summary>
        /// Список уже существующих макетов (для переключения)
        /// </summary>
        public List<AppFormDefinition> ExistingForms { get; set; } = new();

        /// <summary>
        /// Текущая редактируемая форма (если null - создаем новую)
        /// </summary>
        public AppFormDefinition? CurrentForm { get; set; }

        /// <summary>
        /// Готовый JSON макета для инициализации JS-редактора.
        /// Если null - редактор будет пуст.
        /// </summary>
        public string? LayoutJson { get; set; }
    }

    /// <summary>
    /// Универсальное описание поля для палитры конструктора.
    /// Объединяет AppFieldDefinition (БД) и PropertyInfo (Code).
    /// </summary>
    public class DesignerFieldDto
    {
        /// <summary>
        /// Системное имя поля (например: "FirstName" или "custom_field_123")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Человекочитаемое название (Заголовок)
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Тип данных (String, Number, DateTime, EntityLink, Collection)
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// Флаг: true - поле определено в C# коде, false - динамическое поле из БД
        /// </summary>
        public bool IsSystem { get; set; }

        /// <summary>
        /// Флаг: true - это список связанных объектов (например, Телефоны, Email)
        /// </summary>
        public bool IsCollection { get; set; }
    }
}