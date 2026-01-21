using System;

namespace Core.Entities.System
{
    /// <summary>
    /// Сущность для хранения настроек интерфейса CRM (цвета, шрифты, темы).
    /// </summary>
    public class UiSettings
    {
        /// <summary>
        /// Уникальный идентификатор настроек.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Основной цвет интерфейса (Primary color).
        /// По умолчанию: стандартный синий Bootstrap.
        /// </summary>
        public string PrimaryColor { get; set; } = "#0d6efd";

        /// <summary>
        /// Цвет фона левого меню.
        /// По умолчанию: темный из текущего дизайна.
        /// </summary>
        public string MainBgColor { get; set; } = "#1e2229";

        /// <summary>
        /// Акцентный цвет для выделения элементов.
        /// </summary>
        public string AccentColor { get; set; } = "#0d6efd";

        /// <summary>
        /// Базовый размер шрифта в пикселях.
        /// По умолчанию: 14px.
        /// </summary>
        public int BaseFontSize { get; set; } = 14;

        /// <summary>
        /// Идентификатор сотрудника, если это индивидуальная настройка.
        /// Если null — это глобальные настройки для всей компании.
        /// </summary>
        public Guid? EmployeeId { get; set; }

        /// <summary>
        /// Флаг включения темного режима.
        /// </summary>
        public bool IsDarkMode { get; set; } = false;

        /// <summary>
        /// Дата и время последнего обновления настроек.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}