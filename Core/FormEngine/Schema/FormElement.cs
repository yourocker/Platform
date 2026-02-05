namespace Core.FormEngine.Schema
{
    /// <summary>
    /// Универсальный элемент формы (Вкладка, Группа, Поле)
    /// </summary>
    public class FormElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Типы: "TabControl", "Tab", "Group", "Row", "Field", "Text"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Все настройки UI (Label, ReadOnly, Colors) храним здесь,
        /// чтобы не менять C# код при каждом изменении дизайна конструктора.
        /// </summary>
        public Dictionary<string, object> Props { get; set; } = new();

        public List<FormElement> Children { get; set; } = new();
    }
}