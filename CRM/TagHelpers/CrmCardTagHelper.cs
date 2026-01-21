using Microsoft.AspNetCore.Razor.TagHelpers;

namespace CRM.TagHelpers
{
    // Работаем с обычным div, у которого есть атрибут crm-card
    [HtmlTargetElement("div", Attributes = "crm-card")]
    public class CrmCardTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Базовые классы для всех карточек в системе
            var baseClasses = "card shadow-sm border-0";
            
            var existingClasses = output.Attributes["class"]?.Value?.ToString();
            
            // Объединяем системные классы с теми, что ты пропишешь вручную (например, mb-4)
            output.Attributes.SetAttribute("class", string.IsNullOrEmpty(existingClasses) 
                ? baseClasses 
                : $"{baseClasses} {existingClasses}");
        }
    }
}