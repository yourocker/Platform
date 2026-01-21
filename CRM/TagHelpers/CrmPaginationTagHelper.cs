using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http; // Для работы с QueryString
using System;
using System.Linq;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-pagination")]
    public class CrmPaginationTagHelper : TagHelper
    {
        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; }

        [HtmlAttributeName("total-pages")]
        public int TotalPages { get; set; }

        [HtmlAttributeName("current-page")]
        public int CurrentPage { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            try
            {
                if (TotalPages <= 1)
                {
                    output.SuppressOutput();
                    return;
                }

                // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ:
                // Разрешаем тегу иметь вложенный контент (<li>), даже если в .cshtml он вызван как <crm-pagination />
                output.TagMode = TagMode.StartTagAndEndTag;
                
                output.TagName = "ul";
                output.Attributes.SetAttribute("class", "pagination pagination-sm mb-0");

                // 1. Кнопка "Назад"
                output.Content.AppendHtml(CreateItem(CurrentPage - 1, "chevron-left", CurrentPage <= 1));

                // 2. Номера страниц
                for (int i = 1; i <= TotalPages; i++)
                {
                    if (i == 1 || i == TotalPages || (i >= CurrentPage - 2 && i <= CurrentPage + 2))
                    {
                        output.Content.AppendHtml(CreateItem(i, i.ToString(), false, i == CurrentPage));
                    }
                    else if (i == CurrentPage - 3 || i == CurrentPage + 3)
                    {
                        output.Content.AppendHtml("<li class=\"page-item disabled\"><span class=\"page-link\">...</span></li>");
                    }
                }

                // 3. Кнопка "Вперед"
                output.Content.AppendHtml(CreateItem(CurrentPage + 1, "chevron-right", CurrentPage >= TotalPages));
            }
            catch (Exception ex)
            {
                output.TagName = "div";
                output.Attributes.SetAttribute("class", "text-danger small");
                output.Content.SetContent($"Ошибка пагинации: {ex.Message}");
            }
        }

        private TagBuilder CreateItem(int page, string text, bool disabled, bool active = false)
        {
            var li = new TagBuilder("li");
            li.AddCssClass("page-item");
            if (disabled) li.AddCssClass("disabled");
            if (active) li.AddCssClass("active");

            var a = new TagBuilder("a");
            a.AddCssClass("page-link");

            if (!disabled && !active)
            {
                var queryParams = ViewContext.HttpContext.Request.Query;
                var routeValues = new RouteValueDictionary();

                foreach (var key in queryParams.Keys)
                {
                    routeValues[key] = queryParams[key].ToString();
                }
                routeValues["pageNumber"] = page.ToString();

                var path = ViewContext.HttpContext.Request.Path;
                // Формируем QueryString вручную
                var queryString = QueryString.Create(routeValues.ToDictionary(k => k.Key, v => v.Value?.ToString()));
                
                a.Attributes["href"] = $"{path}{queryString}";
            }
            else
            {
                a.Attributes["href"] = "javascript:void(0)";
            }

            if (text.Contains("chevron"))
            {
                a.InnerHtml.AppendHtml($"<i class=\"bi bi-{text}\"></i>");
            }
            else
            {
                a.InnerHtml.Append(text);
            }

            li.InnerHtml.AppendHtml(a);
            return li;
        }
    }
}