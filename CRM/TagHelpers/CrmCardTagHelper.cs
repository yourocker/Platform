using Microsoft.AspNetCore.Razor.TagHelpers;
using Core.Services;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("div", Attributes = "crm-card")]
    public class CrmCardTagHelper : TagHelper
    {
        private readonly ICrmStyleService _styleService;

        public CrmCardTagHelper(ICrmStyleService styleService)
        {
            _styleService = styleService;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();
            
            // Добавляем стандартные классы Bootstrap для карточки
            output.Attributes.SetAttribute("class", "card shadow-sm border-0 mb-4");
            
            // Применяем базовый размер шрифта
            output.Attributes.SetAttribute("style", $"font-size: {settings.BaseFontSize}px;");
        }
    }
}