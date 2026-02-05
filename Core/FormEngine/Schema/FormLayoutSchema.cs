using System.Text.Json.Serialization;

namespace Core.FormEngine.Schema
{
    /// <summary>
    /// Контракт структуры формы.
    /// </summary>
    public class FormLayoutSchema
    {
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Дерево визуальных элементов
        /// </summary>
        public List<FormElement> Layout { get; set; } = new();

        // Заготовки под автоматизацию (скрипты), чтобы структура была готова к расширению
        public Dictionary<string, string> Scripts { get; set; } = new();
        public Dictionary<string, object> Triggers { get; set; } = new();
    }
}