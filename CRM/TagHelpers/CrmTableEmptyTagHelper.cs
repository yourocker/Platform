using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Core.Services;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-table-empty")]
    public class CrmTableEmptyTagHelper : TagHelper
    {
        private readonly ICrmStyleService _styleService;

        public CrmTableEmptyTagHelper(ICrmStyleService styleService)
        {
            _styleService = styleService;
        }

        /// <summary>
        /// Флаг: является ли это состояние результатом фильтрации/поиска
        /// </summary>
        public bool IsSearch { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var settings = _styleService.GetSettings();
            
            output.TagName = "tr";
            output.TagMode = TagMode.StartTagAndEndTag;

            // Определяем иконку и текст (системная заглушка)
            string icon = IsSearch ? "search" : "inbox-fill";
            string title = IsSearch ? "Ничего не найдено" : "Список пуст";
            string description = IsSearch 
                ? "По вашему запросу совпадений нет. Попробуйте изменить параметры поиска или сбросить фильтры." 
                : "В этом разделе еще нет записей. Создайте новую запись, чтобы начать работу.";

            // Монументальная верстка пустой строки
            var content = $@"
                <td colspan=""100"" class=""text-center py-5"">
                    <div class=""py-5"">
                        <i class=""bi bi-{icon} mb-3 d-block opacity-25"" style=""font-size: 4.5rem; color: {settings.PrimaryColor};""></i>
                        <h4 class=""fw-bold text-secondary mb-2"">{title}</h4>
                        <p class=""text-muted mx-auto mb-0"" style=""max-width: 450px; font-size: {settings.BaseFontSize}px;"">
                            {description}
                        </p>
                    </div>
                </td>";

            output.Content.SetHtmlContent(content);
        }
    }
}