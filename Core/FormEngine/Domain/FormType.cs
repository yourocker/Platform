namespace Core.FormEngine.Domain
{
    /// <summary>
    /// Тип формы, определяющий, где она будет использоваться.
    /// </summary>
    public enum FormType
    {
        Create,   // Создание объекта (Create.cshtml)
        Edit,     // Редактирование (Edit.cshtml)
        Details,  // Просмотр (Details.cshtml)
        List      // Списки (пока задел на будущее)
    }
}