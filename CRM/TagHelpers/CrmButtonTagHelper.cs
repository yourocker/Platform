using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("a", Attributes = "crm-button")]
    [HtmlTargetElement("button", Attributes = "crm-button")]
    public class CrmButtonTagHelper : TagHelper
    {
        public string Variant { get; set; } = "primary";
        public string Icon { get; set; }
        public string IconClass { get; set; }
        public string Size { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // 1. Базовые классы
            var sizeClass = !string.IsNullOrEmpty(Size) ? $"btn-{Size}" : "";
            var btnClasses = $"btn btn-{Variant} {sizeClass}";
            
            var existingClasses = output.Attributes["class"]?.Value?.ToString();
            output.Attributes.SetAttribute("class", string.IsNullOrEmpty(existingClasses) 
                ? btnClasses 
                : $"{btnClasses} {existingClasses}");

            // 2. Проверяем, есть ли текст внутри тега
            var content = await output.GetChildContentAsync();
            var hasText = !content.IsEmptyOrWhiteSpace;

            // 3. Добавляем иконку
            if (!string.IsNullOrEmpty(Icon))
            {
                // Если текст есть — добавляем отступ me-1, если нет — иконка будет ровно по центру
                var marginClass = hasText ? "me-1" : "";
                var iClass = string.IsNullOrEmpty(IconClass) ? "" : $" {IconClass}";
                
                output.PreContent.SetHtmlContent($"<i class=\"bi bi-{Icon}{iClass} {marginClass}\"></i>");
            }
        }
    }
}