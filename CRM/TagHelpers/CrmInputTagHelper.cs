using System.Globalization;
using System.Linq;
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

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext? ViewContext { get; set; }

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
                
                var fieldKey = For?.Name ?? Name ?? "";
                var validationError = !string.IsNullOrWhiteSpace(fieldKey)
                    && ViewContext?.ViewData.ModelState.TryGetValue(fieldKey, out var state) == true
                    ? state.Errors.Select(x => x.ErrorMessage).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    : null;

                var attemptedValue = !string.IsNullOrWhiteSpace(fieldKey)
                    && ViewContext?.ViewData.ModelState.TryGetValue(fieldKey, out var modelState) == true
                    ? modelState.AttemptedValue
                    : null;

                var finalValue = !string.IsNullOrWhiteSpace(attemptedValue)
                    ? attemptedValue
                    : !string.IsNullOrWhiteSpace(Value)
                        ? Value
                        : FormatValue(For?.Model, Type);

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
                if (!string.IsNullOrWhiteSpace(validationError))
                    input.AddCssClass("is-invalid");
                if (!string.IsNullOrEmpty(Class)) 
                    input.AddCssClass(Class);

                var excludedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "asp-for",
                    "label",
                    "placeholder",
                    "class",
                    "type",
                    "name",
                    "value",
                    "readonly"
                };

                foreach (var attribute in context.AllAttributes.ToList())
                {
                    if (excludedAttributes.Contains(attribute.Name) ||
                        attribute.Name.StartsWith("asp-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    input.Attributes[attribute.Name] = attribute.Value?.ToString() ?? string.Empty;
                    output.Attributes.RemoveAll(attribute.Name);
                }

                output.Content.AppendHtml(input);

                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    var feedback = new TagBuilder("div");
                    feedback.AddCssClass("invalid-feedback d-block");
                    feedback.InnerHtml.Append(validationError);
                    output.Content.AppendHtml(feedback);
                }
            }
        }

        private static string FormatValue(object? value, string? type)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var normalizedType = (type ?? "text").Trim().ToLowerInvariant();
            return normalizedType switch
            {
                "number" or "range" => value is IFormattable number
                    ? number.ToString(null, CultureInfo.InvariantCulture)
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                "date" when value is DateTime dateValue => dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "datetime-local" when value is DateTime dateTimeValue => dateTimeValue.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
            };
        }
    }
}
