using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Core.Services;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("button", Attributes = "crm-button")]
    [HtmlTargetElement("a", Attributes = "crm-button")]
    public class CrmButtonTagHelper : TagHelper
    {
        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; }

        private readonly ICrmStyleService _styleService;

        // Внедряем сервис через конструктор
        public CrmButtonTagHelper(ICrmStyleService styleService)
        {
            _styleService = styleService;
        }

        public string Variant { get; set; } = "primary";
        public string Icon { get; set; }
        public string IconClass { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();

            var existingClass = output.Attributes["class"]?.Value?.ToString();
            var classParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(existingClass))
            {
                classParts.Add(existingClass.Trim());
            }

            classParts.Add("btn");
            classParts.Add($"btn-{Variant}");
            classParts.Add("shadow-sm");

            output.Attributes.SetAttribute("class", string.Join(" ", classParts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)));

            // Применяем PrimaryColor из БД для кнопок типа primary
            if (Variant == "primary")
            {
                output.Attributes.SetAttribute("style", 
                    $"background-color: {settings.PrimaryColor}; border-color: {settings.PrimaryColor}; color: white;");
            }

            // Рендерим иконку в PreContent (самое начало внутреннего содержимого тега)
            if (!string.IsNullOrEmpty(Icon))
            {
                // Добавляем иконку и пробел me-2 перед основным текстом кнопки
                output.PreContent.AppendHtml($"<i class=\"bi bi-{Icon} me-2 {IconClass}\"></i>");
            }
        }
    }
}
