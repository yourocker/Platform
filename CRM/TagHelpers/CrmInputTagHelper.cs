using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public string? Name { get; set; }
        public string? Label { get; set; }
        public string? Value { get; set; }
        public string? Placeholder { get; set; }
        public string? Class { get; set; }
        public string? Type { get; set; } = "text";

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();
            
            output.TagMode = TagMode.StartTagAndEndTag;
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "mb-3");

            // Единый стиль шрифта из настроек БД
            string fontSizeStyle = $"font-size: {settings.BaseFontSize}px;";

            // 1. Рендерим унифицированный Label
            if (!string.IsNullOrEmpty(Label))
            {
                var label = new TagBuilder("label");
                // Те же классы, что и у всех полей
                label.AddCssClass("form-label small fw-bold text-muted text-uppercase");
                label.Attributes.Add("style", fontSizeStyle);
                label.InnerHtml.Append(Label);
                output.Content.AppendHtml(label);
            }

            // Получаем вложенное содержимое (если оно есть)
            var childContent = await output.GetChildContentAsync();

            if (!childContent.IsEmptyOrWhiteSpace)
            {
                // 2а. Если внутри есть контент (например, для Иконки) - выводим его
                output.Content.AppendHtml(childContent);
            }
            else
            {
                // 2б. Иначе рендерим стандартный инпут
                var input = new TagBuilder("input");
                input.TagRenderMode = TagRenderMode.SelfClosing;
                input.Attributes.Add("type", Type ?? "text");
                input.Attributes.Add("name", Name);
                input.Attributes.Add("id", Name);
                input.Attributes.Add("style", fontSizeStyle);
                
                if (!string.IsNullOrEmpty(Value)) 
                    input.Attributes.Add("value", Value);
                    
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