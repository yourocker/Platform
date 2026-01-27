using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using Core.Services;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-page-header")]
    public class CrmPageHeaderTagHelper : TagHelper
    {
        private readonly ICrmStyleService _styleService;

        public CrmPageHeaderTagHelper(ICrmStyleService styleService)
        {
            _styleService = styleService;
        }

        public string Title { get; set; }
        public string Icon { get; set; }
        public string Subtitle { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();
            
            output.TagMode = TagMode.StartTagAndEndTag;
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "row mb-4 align-items-center justify-content-between");

            // Левая часть: Иконка + Заголовок
            var leftCol = new TagBuilder("div");
            leftCol.AddCssClass("col-auto");

            var titleWrapper = new TagBuilder("div");
            titleWrapper.AddCssClass("d-flex align-items-center");

            if (!string.IsNullOrEmpty(Icon))
            {
                var iconTag = new TagBuilder("i");
                iconTag.AddCssClass($"bi bi-{Icon} me-2 fs-3");
                iconTag.Attributes.Add("style", $"color: {settings.PrimaryColor};");
                titleWrapper.InnerHtml.AppendHtml(iconTag);
            }

            var h3 = new TagBuilder("h3");
            h3.AddCssClass("mb-0 fw-bold text-secondary");
            h3.InnerHtml.Append(Title);
            titleWrapper.InnerHtml.AppendHtml(h3);
            leftCol.InnerHtml.AppendHtml(titleWrapper);

            if (!string.IsNullOrEmpty(Subtitle))
            {
                var sub = new TagBuilder("span");
                sub.AddCssClass("text-muted small ms-1");
                sub.InnerHtml.Append(Subtitle);
                leftCol.InnerHtml.AppendHtml(sub);
            }

            // Правая часть: Кнопки
            var rightCol = new TagBuilder("div");
            rightCol.AddCssClass("col-auto d-flex gap-2 align-items-center");
            
            var actions = await output.GetChildContentAsync();
            rightCol.InnerHtml.AppendHtml(actions);

            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Очищаем буфер перед выводом колонок
            output.Content.Clear(); 
            output.Content.AppendHtml(leftCol);
            output.Content.AppendHtml(rightCol);
        }
    }
}