using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CRM.Infrastructure;
using Core.DTOs.Interfaces;
using Core.Entities.Platform;
using Core.UI.Grid;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Http.Extensions;

namespace CRM.TagHelpers
{
    [HtmlTargetElement("crm-grid")]
    public class CrmGridTagHelper : TagHelper
    {
        private readonly IUrlHelperFactory _urlHelperFactory;

        public CrmGridTagHelper(IUrlHelperFactory urlHelperFactory)
        {
            _urlHelperFactory = urlHelperFactory;
        }

        [HtmlAttributeName("config")]
        public object Config { get; set; }

        [HtmlAttributeName("data")]
        public object Data { get; set; }

        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagMode = TagMode.StartTagAndEndTag;
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "table-responsive");

            if (Config == null || Data == null) return;

            var dataList = Data as IEnumerable;
            var configType = Config.GetType();
            var columnsProp = configType.GetProperty("Columns");
            var columnsList = columnsProp?.GetValue(Config) as IEnumerable;

            if (columnsList == null || dataList == null) return;

            var columns = columnsList.Cast<dynamic>().ToList();
            var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);
            var request = ViewContext.HttpContext.Request;
            var dynamicFields = (ViewContext.ViewData["DynamicFields"] as IEnumerable<AppFieldDefinition> ?? Enumerable.Empty<AppFieldDefinition>())
                .ToDictionary(field => field.SystemName, StringComparer.OrdinalIgnoreCase);
            var lookupData = ViewContext.ViewData["LookupData"] as Dictionary<string, List<SelectListItem>>
                             ?? new Dictionary<string, List<SelectListItem>>();
            
            string currentSort = request.Query["sortOrder"].ToString() ?? "";

            var table = new TagBuilder("table");
            table.AddCssClass("table table-hover align-middle mb-0");
            table.Attributes.Add("id", "entityTable");

            // --- HEADER ---
            var thead = new TagBuilder("thead");
            thead.AddCssClass("bg-light text-muted small text-uppercase");
            var trHead = new TagBuilder("tr");

            foreach (var col in columns)
            {
                var th = new TagBuilder("th");
                string cssClass = (string)col.SystemName;
                bool visible = (bool)col.VisibleByDefault;
                string title = (string)col.Title;
                string sortKey = (string)col.SortKey;

                th.Attributes.Add("data-name", cssClass);
                th.AddCssClass(cssClass);
                if (!visible) th.Attributes.Add("style", "display:none");
                if (columns.IndexOf(col) == 0) th.AddCssClass("ps-4");

                // --- НОВОЕ: Ручка для перетаскивания (Grip Handle) ---
                // Рендерим её только если это не колонка действий
                if (col.Type != GridColumnType.Actions)
                {
                    var handleIcon = new TagBuilder("i");
                    // crm-col-handle - класс для JS, чтобы таскать только за него
                    handleIcon.AddCssClass("bi bi-grip-vertical me-1 crm-col-handle text-muted");
                    handleIcon.Attributes.Add("style", "cursor: grab; opacity: 0.5;");
                    handleIcon.Attributes.Add("title", "Перетащить колонку");
                    th.InnerHtml.AppendHtml(handleIcon);
                }

                // ЛОГИКА СОРТИРОВКИ (Ссылка)
                if (!string.IsNullOrEmpty(sortKey))
                {
                    var a = new TagBuilder("a");
                    // d-inline-block чтобы ссылка была рядом с иконкой, а не с новой строки
                    a.AddCssClass("text-decoration-none text-muted d-inline-block user-select-none"); 
                    
                    string newSort = sortKey;
                    string iconHtml = "";
                    
                    if (currentSort == sortKey)
                    {
                        newSort = sortKey + "_desc";
                        iconHtml = " <i class='bi bi-sort-down-alt text-primary'></i>";
                        a.AddCssClass("fw-bold text-dark");
                    }
                    else if (currentSort == sortKey + "_desc")
                    {
                        newSort = sortKey;
                        iconHtml = " <i class='bi bi-sort-up text-primary'></i>";
                        a.AddCssClass("fw-bold text-dark");
                    }

                    string url = BuildSortUrl(request, newSort);
                    a.Attributes.Add("href", url);
                    
                    a.InnerHtml.AppendHtml(title + iconHtml);
                    th.InnerHtml.AppendHtml(a);
                }
                else
                {
                    th.InnerHtml.Append(title);
                }

                trHead.InnerHtml.AppendHtml(th);
            }
            thead.InnerHtml.AppendHtml(trHead);
            table.InnerHtml.AppendHtml(thead);

            // --- BODY (Остался без изменений, но привожу для полноты) ---
            var tbody = new TagBuilder("tbody");
            foreach (var item in dataList)
            {
                var tr = new TagBuilder("tr");
                tr.AddCssClass("crm-clickable-row");
                
                var idProp = item.GetType().GetProperty("Id");
                var idVal = idProp?.GetValue(item);
                
                if (idVal != null)
                {
                    var detailsUrl = urlHelper.Action("Details", new { id = idVal });
                    tr.Attributes.Add("data-href", detailsUrl);
                }

                foreach (var col in columns)
                {
                    var td = new TagBuilder("td");
                    string cssClass = (string)col.SystemName;
                    bool visible = (bool)col.VisibleByDefault;
                    GridColumnType type = (GridColumnType)col.Type;
                    
                    td.Attributes.Add("data-name", cssClass);
                    td.AddCssClass(cssClass);
                    if (!visible) td.Attributes.Add("style", "display:none");
                    if (columns.IndexOf(col) == 0) td.AddCssClass("ps-4");

                        if (type == GridColumnType.Actions)
                        {
                            td.AddCssClass("text-end pe-4 no-row-click");
                            if (idVal != null) td.InnerHtml.AppendHtml(RenderActions(urlHelper, idVal));
                    }
                        else
                        {
                            object? val = null;
                            AppFieldDefinition? dynamicField = null;
                            if (type == GridColumnType.Dynamic)
                            {
                                string key = (string)col.DynamicKey;
                                dynamicFields.TryGetValue(key, out dynamicField);
                                if (item is IDynamicValues dynItem && dynItem.DynamicValues != null && dynItem.DynamicValues.TryGetValue(key, out var dVal))
                                    val = dVal;
                            }
                            else
                            {
                                var provider = col.ValueProvider as Delegate;
                                if (provider != null) val = provider.DynamicInvoke(item);
                            }
                        td.InnerHtml.AppendHtml(RenderValue(val, type, dynamicField, lookupData));
                    }
                    tr.InnerHtml.AppendHtml(td);
                }
                tbody.InnerHtml.AppendHtml(tr);
            }
            table.InnerHtml.AppendHtml(tbody);
            output.Content.AppendHtml(table);
        }

        private string BuildSortUrl(Microsoft.AspNetCore.Http.HttpRequest request, string newSort)
        {
            var query = QueryHelpers.ParseQuery(request.QueryString.Value);
            var items = query.SelectMany(x => x.Value, (col, value) => new KeyValuePair<string, string>(col.Key, value)).ToList();
            
            items.RemoveAll(x => x.Key.Equals("sortOrder", StringComparison.OrdinalIgnoreCase));
            items.RemoveAll(x => x.Key.Equals("pageNumber", StringComparison.OrdinalIgnoreCase));
            
            items.Add(new KeyValuePair<string, string>("sortOrder", newSort));
            items.Add(new KeyValuePair<string, string>("pageNumber", "1"));

            var qb = new QueryBuilder(items);
            return request.Path + qb.ToQueryString();
        }

        private IHtmlContent RenderValue(
            object? value,
            GridColumnType type,
            AppFieldDefinition? dynamicField,
            IReadOnlyDictionary<string, List<SelectListItem>> lookupData)
        {
            if (value == null)
            {
                return TableCellDisplayFormatter.FormatRawValue(null, dynamicField, lookupData);
            }

            switch (type)
            {
                case GridColumnType.Text:
                case GridColumnType.LinkBold:
                case GridColumnType.Link:
                    return TableCellDisplayFormatter.FormatStringCollection(new[] { value.ToString() ?? string.Empty });
                case GridColumnType.List:
                case GridColumnType.PhoneList:
                    return value is IEnumerable<string> list
                        ? TableCellDisplayFormatter.FormatStringCollection(list)
                        : TableCellDisplayFormatter.FormatStringCollection(new[] { value.ToString() ?? string.Empty });
                case GridColumnType.EmailList:
                    return value is IEnumerable<string> emails
                        ? TableCellDisplayFormatter.FormatStringCollection(emails)
                        : TableCellDisplayFormatter.FormatStringCollection(new[] { value.ToString() ?? string.Empty });
                case GridColumnType.Dynamic:
                    return TableCellDisplayFormatter.FormatRawValue(value, dynamicField, lookupData);
                default:
                    return TableCellDisplayFormatter.FormatStringCollection(new[] { value.ToString() ?? string.Empty });
            }
        }

        private IHtmlContent RenderActions(IUrlHelper url, object id)
        {
            var editUrl = url.Action("Edit", new { id });
            var deleteUrl = url.Action("Delete", new { id });
            string html = $@"<div class='btn-group btn-group-sm shadow-sm crm-hover-actions crm-table-actions'>
                    <a href='{editUrl}' class='btn btn-white border crm-table-action' title='Редактировать'><i class='bi bi-pencil-fill text-primary'></i></a>
                    <a href='{deleteUrl}' class='btn btn-white border crm-table-action' title='Удалить'><i class='bi bi-trash-fill text-danger'></i></a>
                </div>";
            return new HtmlString(html);
        }
    }
}
