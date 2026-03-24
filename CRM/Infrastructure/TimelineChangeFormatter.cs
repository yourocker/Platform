using System.Text.Json;

namespace CRM.Infrastructure;

public static class TimelineChangeFormatter
{
    public static Dictionary<string, string> ParseDynamicProperties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (values == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return values.ToDictionary(
                pair => pair.Key,
                pair => ConvertJsonElementToDisplayValue(pair.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void AddScalarChange(ICollection<string> changes, string label, string? before, string? after)
    {
        var normalizedBefore = NormalizeDisplayValue(before);
        var normalizedAfter = NormalizeDisplayValue(after);
        if (string.Equals(normalizedBefore, normalizedAfter, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add($"{label}: {FormatValue(normalizedBefore)} -> {FormatValue(normalizedAfter)}");
    }

    public static void AddCollectionChange(
        ICollection<string> changes,
        string label,
        IEnumerable<string>? before,
        IEnumerable<string>? after)
    {
        var normalizedBefore = NormalizeCollection(before);
        var normalizedAfter = NormalizeCollection(after);

        if (string.Equals(normalizedBefore, normalizedAfter, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add($"{label}: {FormatValue(normalizedBefore)} -> {FormatValue(normalizedAfter)}");
    }

    public static void AddDictionaryChanges(
        ICollection<string> changes,
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        Func<string, string> resolveLabel)
    {
        foreach (var key in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => resolveLabel(x), StringComparer.CurrentCultureIgnoreCase))
        {
            before.TryGetValue(key, out var beforeValue);
            after.TryGetValue(key, out var afterValue);
            AddScalarChange(changes, resolveLabel(key), beforeValue, afterValue);
        }
    }

    public static string? BuildSummary(IReadOnlyCollection<string> changes, int maxItems = 8)
    {
        if (changes == null || changes.Count == 0)
        {
            return null;
        }

        var visibleChanges = changes.Take(maxItems).ToList();
        if (changes.Count > maxItems)
        {
            visibleChanges.Add($"И ещё изменений: {changes.Count - maxItems}");
        }

        return string.Join(Environment.NewLine, visibleChanges);
    }

    private static string ConvertJsonElementToDisplayValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => string.Join(", ",
                element.EnumerateArray()
                    .Select(ConvertJsonElementToDisplayValue)
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
            JsonValueKind.Object => element.ToString(),
            JsonValueKind.True => "Да",
            JsonValueKind.False => "Нет",
            JsonValueKind.Null => string.Empty,
            _ => element.ToString()
        };
    }

    private static string NormalizeCollection(IEnumerable<string>? values)
    {
        return string.Join(", ",
            (values ?? Array.Empty<string>())
            .Select(NormalizeDisplayValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));
    }

    private static string NormalizeDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "не заполнено" : $"\"{value}\"";
    }
}
