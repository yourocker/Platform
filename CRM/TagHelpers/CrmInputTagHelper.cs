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

        [HtmlAttributeName("asp-for")]
        public ModelExpression? For { get; set; }

        public string? Label { get; set; }
        public string? Placeholder { get; set; }
        public string? Class { get; set; }
        public string? Type { get; set; } = "text";
        public string? Name { get; set; }
        public string? Value { get; set; }

        // УДАЛЕНО ЛЮБОЕ СВОЙСТВО READONLY
        // Это предотвращает попытки Razor сгенерировать C# код с ключевым словом "readonly",
        // что и вызывало ошибку CS1525.

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();
            
            // 1. ПРЯМАЯ ПРОВЕРКА HTML АТРИБУТА
            // Мы просто смотрим, написал ли ты 'readonly' в HTML. 
            // Это работает и для readonly="readonly", и для просто readonly.
            bool isReadonly = context.AllAttributes.ContainsName("readonly");

            // Если атрибут есть, стираем его с обертки (div), чтобы перенести на input внутри
            if (isReadonly)
            {
                output.Attributes.RemoveAll("readonly");
            }

            output.TagMode = TagMode.StartTagAndEndTag;
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "mb-3");

            string fontSizeStyle = $"font-size: {settings.BaseFontSize}px;";
            
            // 2. Рендеринг Label
            if (!string.IsNullOrEmpty(Label))
            {
                var label = new TagBuilder("label");
                label.AddCssClass("form-label text-muted small fw-bold text-uppercase");
                label.Attributes.Add("style", "font-size: 0.75rem; letter-spacing: 0.05em;");
                
                string inputId = For?.Name ?? Name ?? "";
                if (!string.IsNullOrEmpty(inputId))
                {
                    label.Attributes.Add("for", inputId);
                }

                label.InnerHtml.Append(Label);
                output.Content.AppendHtml(label);
            }

            var childContent = await output.GetChildContentAsync();
            if (!childContent.IsEmptyOrWhiteSpace)
            {
                output.Content.AppendHtml(childContent);
            }
            else
            {
                // 3. Рендеринг Input
                var input = new TagBuilder("input");
                input.TagRenderMode = TagRenderMode.SelfClosing;
                input.Attributes.Add("type", Type ?? "text");
                
                string inputName = For?.Name ?? Name ?? "";
                if (!string.IsNullOrEmpty(inputName))
                {
                    input.Attributes.Add("name", inputName);
                    input.Attributes.Add("id", inputName);
                }
                
                // Значение
                var finalValue = For?.Model?.ToString() ?? Value ?? "";
                if (!string.IsNullOrEmpty(finalValue))
                {
                    input.Attributes.Add("value", finalValue);
                }

                // ВРУЧНУЮ ДОБАВЛЯЕМ АТРИБУТ НА INPUT
                if (isReadonly)
                {
                    input.Attributes.Add("readonly", "readonly");
                    input.AddCssClass("bg-light"); 
                }

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