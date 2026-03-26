using System.Text.Encodings.Web;
using System.Text.Json;
using System.IO;
using Core.Entities.Platform;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRM.Infrastructure;

public static class TableCellDisplayFormatter
{
    private const int MaxVisibleLines = 3;

    public static IHtmlContent FormatRawValue(
        object? rawValue,
        AppFieldDefinition? field = null,
        IReadOnlyDictionary<string, List<SelectListItem>>? lookupData = null,
        IReadOnlyDictionary<Guid, string>? namesMap = null)
    {
        var values = ExtractValues(rawValue)
            .Select(value => FormatSingleValue(value, field, lookupData, namesMap))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return BuildLines(values);
    }

    public static IHtmlContent FormatStringCollection(IEnumerable<string>? values)
    {
        var items = (values ?? Enumerable.Empty<string>())
            .Select(value => FormatPlainText(value, collapseToFirstLine: true))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return BuildLines(items);
    }

    private static List<string> ExtractValues(object? rawValue)
    {
        if (rawValue == null)
        {
            return new List<string>();
        }

        if (rawValue is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray()
                    .SelectMany(item => ExtractValues(item))
                    .ToList();
            }

            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                return new List<string>();
            }

            return new List<string> { element.ToString() };
        }

        if (rawValue is string text)
        {
            return new List<string> { text };
        }

        if (rawValue is IEnumerable<string> stringValues)
        {
            return stringValues.ToList();
        }

        if (rawValue is System.Collections.IEnumerable enumerable && rawValue is not IDictionary<string, object>)
        {
            var result = new List<string>();
            foreach (var item in enumerable)
            {
                result.AddRange(ExtractValues(item));
            }

            return result;
        }

        return new List<string> { rawValue.ToString() ?? string.Empty };
    }

    private static string FormatSingleValue(
        string rawValue,
        AppFieldDefinition? field,
        IReadOnlyDictionary<string, List<SelectListItem>>? lookupData,
        IReadOnlyDictionary<Guid, string>? namesMap)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var normalized = DecodeTechnicalLineBreaks(rawValue).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (field == null)
        {
            return FormatPlainText(normalized, collapseToFirstLine: true);
        }

        switch (field.DataType)
        {
            case FieldDataType.EntityLink:
                {
                    var display = ResolveLookupValue(field.SystemName, normalized, lookupData)
                                  ?? ResolveNameMap(normalized, namesMap)
                                  ?? normalized;
                    return FormatPlainText(display, collapseToFirstLine: true);
                }
            case FieldDataType.Select:
                return FormatPlainText(
                    ResolveLookupValue(field.SystemName, normalized, lookupData) ?? normalized,
                    collapseToFirstLine: true);
            case FieldDataType.Boolean:
                return normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ? "Да" : "Нет";
            case FieldDataType.Date:
                if (DateTime.TryParse(normalized, out var date))
                {
                    return date.ToString("dd.MM.yyyy");
                }
                return FormatPlainText(normalized, collapseToFirstLine: true);
            case FieldDataType.DateTime:
                if (DateTime.TryParse(normalized, out var dateTime))
                {
                    return dateTime.ToString("dd.MM.yyyy HH:mm");
                }
                return FormatPlainText(normalized, collapseToFirstLine: true);
            case FieldDataType.File:
                return FormatPlainText(Path.GetFileName(normalized), collapseToFirstLine: true);
            case FieldDataType.Text:
                return FormatPlainText(normalized, collapseToFirstLine: true);
            default:
                return FormatPlainText(normalized, collapseToFirstLine: true);
        }
    }

    private static string FormatPlainText(string value, bool collapseToFirstLine)
    {
        var normalized = DecodeTechnicalLineBreaks(value).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lines = normalized
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (!lines.Any())
        {
            return string.Empty;
        }

        var firstLine = lines[0];
        if (!collapseToFirstLine)
        {
            return firstLine;
        }

        return lines.Count > 1 && !firstLine.EndsWith("...", StringComparison.Ordinal)
            ? firstLine + "..."
            : firstLine;
    }

    private static string DecodeTechnicalLineBreaks(string value)
    {
        return value
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string? ResolveLookupValue(
        string systemName,
        string rawValue,
        IReadOnlyDictionary<string, List<SelectListItem>>? lookupData)
    {
        if (lookupData == null || !lookupData.TryGetValue(systemName, out var items))
        {
            return null;
        }

        return items.FirstOrDefault(item => string.Equals(item.Value, rawValue, StringComparison.OrdinalIgnoreCase))?.Text;
    }

    private static string? ResolveNameMap(string rawValue, IReadOnlyDictionary<Guid, string>? namesMap)
    {
        if (namesMap == null || !Guid.TryParse(rawValue.Trim('"', ' '), out var id))
        {
            return null;
        }

        return namesMap.TryGetValue(id, out var name) ? name : null;
    }

    private static IHtmlContent BuildLines(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return new HtmlString("<span class='text-muted small'>—</span>");
        }

        var encoder = HtmlEncoder.Default;
        var visibleValues = values.Take(MaxVisibleLines).ToList();
        var html = new System.Text.StringBuilder("<div class='crm-table-cell-lines'>");

        foreach (var value in visibleValues)
        {
            html.Append("<div class='crm-table-cell-line' title='")
                .Append(encoder.Encode(value))
                .Append("'>")
                .Append(encoder.Encode(value))
                .Append("</div>");
        }

        if (values.Count > MaxVisibleLines)
        {
            html.Append("<div class='crm-table-cell-line crm-table-cell-ellipsis'>...</div>");
        }

        html.Append("</div>");
        return new HtmlString(html.ToString());
    }
}
