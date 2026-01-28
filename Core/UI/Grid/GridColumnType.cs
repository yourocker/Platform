namespace Core.UI.Grid
{
    /// <summary>
    /// Определяет, как рендерить содержимое ячейки таблицы
    /// </summary>
    public enum GridColumnType
    {
        Text,           // Обычный текст
        Link,           // Ссылка на Details
        LinkBold,       // Жирная ссылка на Details (для главного поля)
        List,           // Список строк через запятую
        EmailList,      // Список email-ов (кликабельные)
        PhoneList,      // Список телефонов
        Badge,          // Цветной бейдж (для статусов)
        Dynamic,        // Поле из DynamicValues
        Actions         // Кнопки действий (Редактировать/Удалить)
    }
}