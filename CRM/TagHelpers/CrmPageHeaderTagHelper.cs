using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-page-header")]
    public class CrmPageHeaderTagHelper : TagHelper
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public string Subtitle { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // Устанавливаем режим парного тега
            output.TagMode = TagMode.StartTagAndEndTag;
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "row mb-4 align-items-center justify-content-between");

            // --- Левая часть: Заголовок, Иконка, Сабтайтл ---
            var leftCol = new TagBuilder("div");
            leftCol.AddCssClass("col-auto");

            // Контейнер для иконки и заголовка (в одну линию)
            var titleWrapper = new TagBuilder("div");
            titleWrapper.AddCssClass("d-flex align-items-center");

            if (!string.IsNullOrEmpty(Icon))
            {
                titleWrapper.InnerHtml.AppendHtml($"<i class=\"bi bi-{Icon} me-2 fs-3 text-secondary\"></i>");
            }

            var h3 = new TagBuilder("h3");
            h3.AddCssClass("mb-0 fw-bold text-secondary");
            h3.InnerHtml.Append(Title);
            titleWrapper.InnerHtml.AppendHtml(h3);
            
            leftCol.InnerHtml.AppendHtml(titleWrapper);

            if (!string.IsNullOrEmpty(Subtitle))
            {
                var sub = new TagBuilder("span");
                sub.AddCssClass("text-muted small");
                sub.InnerHtml.AppendHtml(Subtitle);
                leftCol.InnerHtml.AppendHtml(sub);
            }

            // --- Правая часть: Кнопки (контент внутри тега) ---
            var rightCol = new TagBuilder("div");
            rightCol.AddCssClass("col-auto d-flex gap-2");
            
            // Получаем всё, что вложено в <crm-page-header>...</crm-page-header>
            var actions = await output.GetChildContentAsync();
            rightCol.InnerHtml.AppendHtml(actions);

            // Собираем всё вместе
            output.Content.AppendHtml(leftCol);
            output.Content.AppendHtml(rightCol);
        }
    }
}