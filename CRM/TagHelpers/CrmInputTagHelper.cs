using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Core.Services;
using System.Threading.Tasks;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-input")]
    public class CrmInputTagHelper : TagHelper
    {
        private readonly ICrmStyleService _styleService;

        public CrmInputTagHelper(ICrmStyleService styleService)
        {
            _styleService = styleService;
        }

        // Поддержка стандартного asp-for
        [HtmlAttributeName("asp-for")]
        public ModelExpression? For { get; set; }

        public string? Label { get; set; }
        public string? Placeholder { get; set; }
        public string? Class { get; set; }
        public string? Type { get; set; } = "text";
        
        // Оставляем Name для случаев, когда поле не привязано к модели напрямую
        public string? Name { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();
            
            output.TagMode = TagMode.StartTagAndEndTag;
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "mb-3");

            string fontSizeStyle = $"font-size: {settings.BaseFontSize}px;";

            // 1. Рендерим Label
            if (!string.IsNullOrEmpty(Label))
            {
                var label = new TagBuilder("label");
                label.AddCssClass("form-label small fw-bold text-muted text-uppercase");
                label.Attributes.Add("style", fontSizeStyle);
                label.InnerHtml.Append(Label);
                output.Content.AppendHtml(label);
            }

            var childContent = await output.GetChildContentAsync();

            if (!childContent.IsEmptyOrWhiteSpace)
            {
                // Если есть вложенный контент (как в случае с выбором иконки), выводим его
                output.Content.AppendHtml(childContent);
            }
            else
            {
                // 2. Рендерим инпут с поддержкой привязки
                var input = new TagBuilder("input");
                input.TagRenderMode = TagRenderMode.SelfClosing;
                input.Attributes.Add("type", Type ?? "text");
                
                // Приоритет отдаем asp-for (For.Name), если его нет - используем Name
                string inputName = For?.Name ?? Name ?? "";
                input.Attributes.Add("name", inputName);
                input.Attributes.Add("id", inputName);
                
                // Берем значение из модели автоматически
                var value = For?.Model?.ToString() ?? "";
                if (!string.IsNullOrEmpty(value))
                    input.Attributes.Add("value", value);

                input.Attributes.Add("style", fontSizeStyle);
                
                if (!string.IsNullOrEmpty(Placeholder)) 
                    input.Attributes.Add("placeholder", Placeholder);

                input.AddCssClass("form-control form-control-sm");
                if (!string.IsNullOrEmpty(Class)) 
                    input.AddCssClass(Class);

                output.Content.AppendHtml(input);
            }
        }
    }
}