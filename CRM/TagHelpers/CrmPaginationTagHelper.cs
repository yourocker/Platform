using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Core.Services;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-pagination")]
    public class CrmPaginationTagHelper : TagHelper
    {
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly ICrmStyleService _styleService;

        public CrmPaginationTagHelper(IUrlHelperFactory urlHelperFactory, ICrmStyleService styleService)
        {
            _urlHelperFactory = urlHelperFactory;
            _styleService = styleService;
        }

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; }

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string Action { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (TotalPages <= 1)
            {
                output.SuppressOutput();
                return;
            }

            var settings = _styleService.GetSettings();
            var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);
            
            output.TagName = "nav";
            output.TagMode = TagMode.StartTagAndEndTag;

            var ul = new TagBuilder("ul");
            ul.AddCssClass("pagination pagination-sm m-0");

            for (int i = 1; i <= TotalPages; i++)
            {
                var li = new TagBuilder("li");
                li.AddCssClass("page-item");
                
                var a = new TagBuilder("a");
                a.AddCssClass("page-link shadow-none");
                
                // Формируем URL с сохранением существующих параметров поиска
                var routeValues = new RouteValueDictionary();
                foreach (var query in ViewContext.HttpContext.Request.Query)
                {
                    routeValues[query.Key] = query.Value;
                }
                routeValues["pageNumber"] = i;
                
                a.Attributes["href"] = urlHelper.Action(Action, routeValues);
                a.InnerHtml.Append(i.ToString());

                if (i == CurrentPage)
                {
                    li.AddCssClass("active");
                    // ВНИМАНИЕ: Применяем PrimaryColor из БД для активной кнопки пагинации
                    a.Attributes["style"] = $"background-color: {settings.PrimaryColor}; border-color: {settings.PrimaryColor}; color: white;";
                }
                else
                {
                    // Для неактивных кнопок используем цвет текста из PrimaryColor для единообразия
                    a.Attributes["style"] = $"color: {settings.PrimaryColor};";
                }

                li.InnerHtml.AppendHtml(a);
                ul.InnerHtml.AppendHtml(li);
            }

            output.Content.AppendHtml(ul);
        }
    }
}