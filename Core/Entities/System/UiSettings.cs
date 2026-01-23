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

        // --- БРЕНД И АКЦЕНТЫ ---

        /// <summary>
        /// Основной цвет интерфейса (Primary). Используется для кнопок и активных элементов.
        /// </summary>
        public string PrimaryColor { get; set; } = "#0d6efd";

        /// <summary>
        /// Цвет контента (текста и иконок) внутри элементов с PrimaryColor.
        /// </summary>
        public string PrimaryContentColor { get; set; } = "#ffffff";

        /// <summary>
        /// Акцентный цвет для выделения элементов.
        /// </summary>
        public string AccentColor { get; set; } = "#0d6efd";

        // --- ЛЕВОЕ МЕНЮ 1 (Узкое / SIDEBAR) ---

        /// <summary>
        /// Цвет фона первого (узкого) левого меню.
        /// </summary>
        public string MainBgColor { get; set; } = "#1e2229";

        /// <summary>
        /// Цвет текста и иконок в первом левом меню.
        /// </summary>
        public string MenuTextColor { get; set; } = "#ffffff";

        // --- ЛЕВОЕ МЕНЮ 2 (Широкое / SUB-MENU) ---

        /// <summary>
        /// Цвет фона второго (широкого) левого меню.
        /// </summary>
        public string SubMenuBgColor { get; set; } = "#ffffff";

        /// <summary>
        /// Цвет текста во втором левом меню.
        /// </summary>
        public string SubMenuTextColor { get; set; } = "#495057";

        // --- РАБОЧАЯ ОБЛАСТЬ (CONTENT) ---

        /// <summary>
        /// Цвет фона основной рабочей области (общий фон страниц).
        /// </summary>
        public string PageBgColor { get; set; } = "#f4f7f6";

        /// <summary>
        /// Цвет основного текста на страницах.
        /// </summary>
        public string PageTextColor { get; set; } = "#212529";

        // --- КАРТОЧКИ И ТАБЛИЦЫ ---

        /// <summary>
        /// Цвет фона для карточек и таблиц (белые блоки на сером фоне).
        /// </summary>
        public string CardBgColor { get; set; } = "#ffffff";

        /// <summary>
        /// Цвет текста внутри карточек и таблиц.
        /// </summary>
        public string CardTextColor { get; set; } = "#212529";

        // --- ТИПОГРАФИКА И СИСТЕМА ---

        /// <summary>
        /// Базовый размер шрифта в пикселях.
        /// </summary>
        public int BaseFontSize { get; set; } = 14;

        /// <summary>
        /// Путь к файлу логотипа компании. 
        /// String? Позволяет полю быть NULL в базе данных.
        /// </summary>
        public string? LogoPath { get; set; }

        /// <summary>
        /// Идентификатор сотрудника, если настройки индивидуальные.
        /// </summary>
        public Guid? EmployeeId { get; set; }

        /// <summary>
        /// Дата и время последнего обновления настроек.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}