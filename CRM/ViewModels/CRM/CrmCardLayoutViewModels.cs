using System.Text.Json;
using Core.Entities.Platform;

namespace CRM.ViewModels.CRM;

public class CrmCardLayoutSchema
{
    public List<CrmCardLayoutSectionViewModel> Sections { get; set; } = new();
}

public class CrmCardLayoutSectionViewModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Новый раздел";
    public List<CrmCardLayoutItemViewModel> Items { get; set; } = new();
}

public class CrmCardLayoutItemViewModel
{
    public string Kind { get; set; } = "system";
    public string? Key { get; set; }
    public Guid? FieldId { get; set; }

    public string Identity =>
        string.Equals(Kind, "dynamic", StringComparison.OrdinalIgnoreCase) && FieldId.HasValue
            ? $"dynamic:{FieldId.Value:D}"
            : $"system:{Key?.Trim().ToLowerInvariant()}";
}

public class CrmCardLayoutPaletteItemViewModel
{
    public string Identity { get; set; } = string.Empty;
    public string Kind { get; set; } = "system";
    public string Label { get; set; } = string.Empty;
    public string? Key { get; set; }
    public Guid? FieldId { get; set; }
    public bool IsBuiltIn { get; set; }
}

public class CrmCardLayoutRenderViewModel
{
    public string EntityCode { get; set; } = string.Empty;
    public Guid AppDefinitionId { get; set; }
    public Guid PipelineId { get; set; }
    public CrmProcessFormViewModel FormModel { get; set; } = new();
    public CrmProcessDetailsViewModel DetailsModel { get; set; } = new();
    public CrmCardLayoutSchema Layout { get; set; } = new();
    public List<AppFieldDefinition> DynamicFields { get; set; } = new();
    public Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> LookupData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DynamicFieldCreateUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Core.Entities.Platform.AppDefinition> AllDefinitions { get; set; } = new();
}

public class CrmCardLayoutSaveRequest
{
    public Guid PipelineId { get; set; }
    public string LayoutJson { get; set; } = "{\"sections\":[]}";
}

public static class CrmCardLayoutCatalog
{
    private const string DefaultSectionTitle = "Новый раздел";

    private static readonly Dictionary<string, List<CrmCardLayoutPaletteItemViewModel>> BuiltInFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Lead"] = new()
            {
                CreateBuiltIn("PipelineId", "Воронка"),
                CreateBuiltIn("CreatedAt", "Создано"),
                CreateBuiltIn("ConvertedAt", "Дата конверсии"),
                CreateBuiltIn("Amount", "Сумма"),
                CreateBuiltIn("Currency", "Валюта"),
                CreateBuiltIn("ResponsibleId", "Ответственный"),
                CreateBuiltIn("CompanyId", "Компания"),
                CreateBuiltIn("ContactLinks", "Контакты")
            },
            ["Deal"] = new()
            {
                CreateBuiltIn("PipelineId", "Воронка"),
                CreateBuiltIn("CreatedAt", "Создано"),
                CreateBuiltIn("SourceLeadId", "Источник"),
                CreateBuiltIn("Amount", "Сумма"),
                CreateBuiltIn("Currency", "Валюта"),
                CreateBuiltIn("ResponsibleId", "Ответственный"),
                CreateBuiltIn("CompanyId", "Компания"),
                CreateBuiltIn("ContactLinks", "Контакты")
            }
        };

    public static CrmCardLayoutSchema ParseOrDefault(string? layoutJson, string entityCode, IEnumerable<AppFieldDefinition> dynamicFields)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        CrmCardLayoutSchema? parsed = null;

        if (!string.IsNullOrWhiteSpace(layoutJson))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<CrmCardLayoutSchema>(layoutJson, options);
            }
            catch
            {
                parsed = null;
            }
        }

        var normalized = Normalize(parsed, entityCode, dynamicFields);
        normalized = EnsureOverviewSection(normalized, entityCode);
        return normalized.Sections.Count > 0
            ? normalized
            : BuildDefault(entityCode, dynamicFields);
    }

    public static CrmCardLayoutSchema Normalize(CrmCardLayoutSchema? schema, string entityCode, IEnumerable<AppFieldDefinition> dynamicFields)
    {
        var validDynamicIds = dynamicFields
            .Where(field => !field.IsDeleted && !string.Equals(field.SystemName, "Name", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Id)
            .ToHashSet();

        var validBuiltIns = GetBuiltInFields(entityCode)
            .Select(field => field.Key ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new CrmCardLayoutSchema();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in schema?.Sections ?? Enumerable.Empty<CrmCardLayoutSectionViewModel>())
        {
            var normalizedTitle = string.IsNullOrWhiteSpace(section.Title)
                ? DefaultSectionTitle
                : section.Title.Trim();
            var normalizedSection = new CrmCardLayoutSectionViewModel
            {
                Id = CreateUniqueSectionId(section.Id, seenSectionIds),
                Title = normalizedTitle
            };

            foreach (var item in section.Items ?? Enumerable.Empty<CrmCardLayoutItemViewModel>())
            {
                CrmCardLayoutItemViewModel? normalizedItem = null;

                if (string.Equals(item.Kind, "dynamic", StringComparison.OrdinalIgnoreCase) &&
                    item.FieldId.HasValue &&
                    validDynamicIds.Contains(item.FieldId.Value))
                {
                    normalizedItem = new CrmCardLayoutItemViewModel
                    {
                        Kind = "dynamic",
                        FieldId = item.FieldId.Value
                    };
                }
                else if (string.Equals(item.Kind, "system", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrWhiteSpace(item.Key) &&
                         validBuiltIns.Contains(item.Key))
                {
                    normalizedItem = new CrmCardLayoutItemViewModel
                    {
                        Kind = "system",
                        Key = item.Key.Trim()
                    };
                }

                if (normalizedItem == null || !seen.Add(normalizedItem.Identity))
                {
                    continue;
                }

                normalizedSection.Items.Add(normalizedItem);
            }

            if (normalizedSection.Items.Count == 0 &&
                string.Equals(normalizedSection.Title, DefaultSectionTitle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedSection.Items.Count > 0 || !string.IsNullOrWhiteSpace(normalizedSection.Title))
            {
                result.Sections.Add(normalizedSection);
            }
        }

        return result;
    }

    public static CrmCardLayoutSchema BuildDefault(string entityCode, IEnumerable<AppFieldDefinition> dynamicFields)
    {
        var dynamicFieldItems = dynamicFields
            .Where(field => !field.IsDeleted && !string.Equals(field.SystemName, "Name", StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field.SortOrder)
            .ThenBy(field => field.Label)
            .Select(field => new CrmCardLayoutItemViewModel
            {
                Kind = "dynamic",
                FieldId = field.Id
            })
            .ToList();

        var sections = new List<CrmCardLayoutSectionViewModel>
        {
            new()
            {
                Id = "overview",
                Title = entityCode.Equals("Deal", StringComparison.OrdinalIgnoreCase) ? "О сделке" : "О лиде",
                Items = BuildOverviewItems(entityCode)
            },
            new()
            {
                Id = "main",
                Title = "Основное",
                Items = new List<CrmCardLayoutItemViewModel>
                {
                    new() { Kind = "system", Key = "Amount" },
                    new() { Kind = "system", Key = "Currency" },
                    new() { Kind = "system", Key = "ResponsibleId" }
                }
            },
            new()
            {
                Id = "relations",
                Title = "Связи",
                Items = new List<CrmCardLayoutItemViewModel>
                {
                    new() { Kind = "system", Key = "CompanyId" },
                    new() { Kind = "system", Key = "ContactLinks" }
                }
            }
        };

        if (dynamicFieldItems.Count > 0)
        {
            sections.Add(new CrmCardLayoutSectionViewModel
            {
                Id = "additional",
                Title = "Дополнительно",
                Items = dynamicFieldItems
            });
        }

        return new CrmCardLayoutSchema
        {
            Sections = sections
        };
    }

    public static List<CrmCardLayoutPaletteItemViewModel> BuildPalette(string entityCode, IEnumerable<AppFieldDefinition> dynamicFields)
    {
        var palette = GetBuiltInFields(entityCode).ToList();

        palette.AddRange(dynamicFields
            .Where(field => !field.IsDeleted && !string.Equals(field.SystemName, "Name", StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field.SortOrder)
            .ThenBy(field => field.Label)
            .Select(field => new CrmCardLayoutPaletteItemViewModel
            {
                Identity = $"dynamic:{field.Id:D}",
                Kind = "dynamic",
                Label = field.Label,
                FieldId = field.Id,
                IsBuiltIn = false
            }));

        return palette;
    }

    public static IReadOnlyList<CrmCardLayoutPaletteItemViewModel> GetBuiltInFields(string entityCode)
    {
        return BuiltInFields.TryGetValue(entityCode, out var fields)
            ? fields
            : Array.Empty<CrmCardLayoutPaletteItemViewModel>();
    }

    private static CrmCardLayoutPaletteItemViewModel CreateBuiltIn(string key, string label)
    {
        return new CrmCardLayoutPaletteItemViewModel
        {
            Identity = $"system:{key.Trim().ToLowerInvariant()}",
            Kind = "system",
            Key = key,
            Label = label,
            IsBuiltIn = true
        };
    }

    private static string CreateUniqueSectionId(string? proposedId, HashSet<string> seenSectionIds)
    {
        var nextId = string.IsNullOrWhiteSpace(proposedId)
            ? Guid.NewGuid().ToString("N")
            : proposedId.Trim();

        while (!seenSectionIds.Add(nextId))
        {
            nextId = Guid.NewGuid().ToString("N");
        }

        return nextId;
    }

    private static CrmCardLayoutSchema EnsureOverviewSection(CrmCardLayoutSchema schema, string entityCode)
    {
        if (!entityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase) &&
            !entityCode.Equals("Deal", StringComparison.OrdinalIgnoreCase))
        {
            return schema;
        }

        var overviewItems = BuildOverviewItems(entityCode);
        var overviewIdentities = overviewItems
            .Select(item => item.Identity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasOverviewItems = schema.Sections
            .SelectMany(section => section.Items)
            .Any(item => overviewIdentities.Contains(item.Identity));
        var hasOverviewSection = schema.Sections.Any(section =>
            string.Equals(section.Id, "overview", StringComparison.OrdinalIgnoreCase));

        if (hasOverviewItems || hasOverviewSection)
        {
            return schema;
        }

        schema.Sections.Insert(0, new CrmCardLayoutSectionViewModel
        {
            Id = "overview",
            Title = entityCode.Equals("Deal", StringComparison.OrdinalIgnoreCase) ? "О сделке" : "О лиде",
            Items = overviewItems
        });

        return schema;
    }

    private static List<CrmCardLayoutItemViewModel> BuildOverviewItems(string entityCode)
    {
        var items = new List<CrmCardLayoutItemViewModel>
        {
            new() { Kind = "system", Key = "PipelineId" },
            new() { Kind = "system", Key = "CreatedAt" }
        };

        if (entityCode.Equals("Deal", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new CrmCardLayoutItemViewModel { Kind = "system", Key = "SourceLeadId" });
        }

        if (entityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new CrmCardLayoutItemViewModel { Kind = "system", Key = "ConvertedAt" });
        }

        return items;
    }
}
