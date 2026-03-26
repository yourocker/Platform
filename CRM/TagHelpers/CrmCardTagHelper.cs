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

            var existingClass = output.Attributes["class"]?.Value?.ToString();
            var classParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(existingClass))
            {
                classParts.Add(existingClass.Trim());
            }

            classParts.Add("card");
            classParts.Add("shadow-sm");
            classParts.Add("border-0");
            classParts.Add("mb-4");

            output.Attributes.SetAttribute("class", string.Join(" ", classParts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)));

            var existingStyle = output.Attributes["style"]?.Value?.ToString();
            var fontSizeStyle = $"font-size: {settings.BaseFontSize}px;";
            var mergedStyle = string.IsNullOrWhiteSpace(existingStyle)
                ? fontSizeStyle
                : $"{existingStyle.Trim().TrimEnd(';')}; {fontSizeStyle}";

            output.Attributes.SetAttribute("style", mergedStyle);
        }
    }
}
