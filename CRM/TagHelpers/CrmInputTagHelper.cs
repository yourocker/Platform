using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-input")]
    public class CrmInputTagHelper : TagHelper
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public string Placeholder { get; set; }
        public string Class { get; set; } // Дополнительные классы (например, instant-filter)

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Устанавливаем режим парного тега, чтобы избежать проблем с рендерингом
            output.TagMode = TagMode.StartTagAndEndTag;
            
            // Внешний контейнер теперь не нужен (тег crm-input сам станет этим контейнером)
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "mb-3");

            // 1. Создаем Label
            if (!string.IsNullOrEmpty(Label))
            {
                var label = new TagBuilder("label");
                label.AddCssClass("form-label small fw-bold text-muted text-uppercase");
                label.InnerHtml.Append(Label);
                output.Content.AppendHtml(label);
            }

            // 2. Создаем Input
            var input = new TagBuilder("input");
            input.TagRenderMode = TagRenderMode.SelfClosing;
            input.Attributes.Add("type", "text");
            input.Attributes.Add("name", Name);
            input.Attributes.Add("id", Name);
            
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