using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Entities.Platform.Form;

/// <summary>
/// Корневой объект схемы Layout
/// </summary>
public class FormLayoutSchema
{
    public List<LayoutNode> Nodes { get; set; } = new();

    public static FormLayoutSchema? TryParse(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson)) return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var parsed = JsonSerializer.Deserialize<FormLayoutSchema>(layoutJson, options);
            // Если десериализация отработала, но узлы не распознаны (например, табы/группы),
            // пробуем "мягкий" парсер ниже.
            if (parsed != null && parsed.Nodes != null && parsed.Nodes.Any()) return parsed;
        }
        catch
        {
            // fallback below
        }

        return TryParseLoose(layoutJson);
    }

    private static FormLayoutSchema? TryParseLoose(string layoutJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(layoutJson);
            var root = doc.RootElement;

            JsonElement nodesElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (!TryGetProperty(root, "nodes", out nodesElement) && !TryGetProperty(root, "Nodes", out nodesElement))
                {
                    return new FormLayoutSchema();
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                nodesElement = root;
            }
            else
            {
                return null;
            }

            var nodes = ParseNodes(nodesElement, NodeParseContext.Default);
            return new FormLayoutSchema { Nodes = nodes };
        }
        catch
        {
            return null;
        }
    }

    private enum NodeParseContext
    {
        Default,
        Tabs
    }

    private static List<LayoutNode> ParseNodes(JsonElement element, NodeParseContext context)
    {
        var nodes = new List<LayoutNode>();
        if (element.ValueKind != JsonValueKind.Array) return nodes;

        foreach (var item in element.EnumerateArray())
        {
            var node = ParseNode(item, context);
            if (node != null) nodes.Add(node);
        }

        return nodes;
    }

    private static LayoutNode? ParseNode(JsonElement element, NodeParseContext context)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        var type = GetString(element, "type", "Type");
        type ??= InferNodeType(element, context);
        if (string.IsNullOrWhiteSpace(type)) return null;

        switch (type)
        {
            case "field":
                return new FieldNode
                {
                    FieldId = GetGuid(element, "FieldId", "fieldId"),
                    IsReadOnly = GetBool(element, "IsReadOnly", "isReadOnly"),
                    IsRequiredOverride = GetBool(element, "IsRequiredOverride", "isRequiredOverride"),
                    CanCreate = GetBool(element, "CanCreate", "canCreate"),
                    CanView = GetBool(element, "CanView", "canView"),
                    CustomLabel = GetString(element, "CustomLabel", "customLabel")
                };
            case "tabControl":
                {
                    var tabsEl = GetProperty(element, "Tabs", "tabs");
                    var tabs = tabsEl.HasValue
                        ? ParseNodes(tabsEl.Value, NodeParseContext.Tabs)
                            .OfType<TabNode>()
                            .ToList()
                        : new List<TabNode>();
                    return new TabControlNode
                    {
                        CustomLabel = GetString(element, "CustomLabel", "customLabel"),
                        Tabs = tabs
                    };
                }
            case "tab":
                {
                    var childrenEl = GetProperty(element, "Children", "children");
                    var children = childrenEl.HasValue
                        ? ParseNodes(childrenEl.Value, NodeParseContext.Default)
                        : new List<LayoutNode>();
                    return new TabNode
                    {
                        Title = GetString(element, "Title", "title") ?? "Новая вкладка",
                        CustomLabel = GetString(element, "CustomLabel", "customLabel"),
                        Children = children
                    };
                }
            case "group":
                {
                    var childrenEl = GetProperty(element, "Children", "children");
                    var children = childrenEl.HasValue
                        ? ParseNodes(childrenEl.Value, NodeParseContext.Default)
                        : new List<LayoutNode>();
                    return new GroupNode
                    {
                        Title = GetString(element, "Title", "title") ?? "Группа",
                        IsCollapsed = GetBool(element, "IsCollapsed", "isCollapsed"),
                        CustomLabel = GetString(element, "CustomLabel", "customLabel"),
                        Children = children
                    };
                }
            case "row":
                {
                    var columnsEl = GetProperty(element, "Columns", "columns");
                    var columns = columnsEl.HasValue
                        ? ParseNodes(columnsEl.Value, NodeParseContext.Default)
                            .OfType<ColumnNode>()
                            .ToList()
                        : new List<ColumnNode>();
                    return new RowNode
                    {
                        CustomLabel = GetString(element, "CustomLabel", "customLabel"),
                        Columns = columns
                    };
                }
            case "column":
                {
                    var childrenEl = GetProperty(element, "Children", "children");
                    var children = childrenEl.HasValue
                        ? ParseNodes(childrenEl.Value, NodeParseContext.Default)
                        : new List<LayoutNode>();
                    return new ColumnNode
                    {
                        Width = GetInt(element, "Width", "width") ?? 12,
                        CustomLabel = GetString(element, "CustomLabel", "customLabel"),
                        Children = children
                    };
                }
            default:
                return null;
        }
    }

    private static string? InferNodeType(JsonElement element, NodeParseContext context)
    {
        if (HasProperty(element, "FieldId", "fieldId")) return "field";
        if (HasProperty(element, "Tabs", "tabs")) return "tabControl";
        if (HasProperty(element, "Columns", "columns")) return "row";
        if (HasProperty(element, "Width", "width") && HasProperty(element, "Children", "children")) return "column";
        if (HasProperty(element, "IsCollapsed", "isCollapsed")) return "group";
        if (HasProperty(element, "Children", "children")) return context == NodeParseContext.Tabs ? "tab" : "group";
        return null;
    }

    private static bool HasProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out _)) return true;
        }
        foreach (var property in element.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static JsonElement? GetProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value)) return value;
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (!prop.HasValue) return null;
        return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
    }

    private static Guid GetGuid(JsonElement element, params string[] names)
    {
        var str = GetString(element, names);
        return Guid.TryParse(str, out var id) ? id : Guid.Empty;
    }

    private static bool GetBool(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (!prop.HasValue) return false;

        return prop.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(prop.Value.GetString(), out var result) && result,
            _ => false
        };
    }

    private static int? GetInt(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (!prop.HasValue) return null;

        if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var number)) return number;
        if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out var parsed)) return parsed;
        return null;
    }
}

public enum LayoutNodeType
{
    Field,
    TabControl,
    Tab,
    Group,
    Row,
    Column
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FieldNode), typeDiscriminator: "field")]
[JsonDerivedType(typeof(TabControlNode), typeDiscriminator: "tabControl")]
[JsonDerivedType(typeof(TabNode), typeDiscriminator: "tab")]
[JsonDerivedType(typeof(GroupNode), typeDiscriminator: "group")]
[JsonDerivedType(typeof(RowNode), typeDiscriminator: "row")]
[JsonDerivedType(typeof(ColumnNode), typeDiscriminator: "column")]
public abstract class LayoutNode
{
    public abstract LayoutNodeType NodeType { get; }
    
    // Общие свойства узла
    public string? CustomLabel { get; set; }
}

/// <summary>
/// Узел: Поле сущности
/// </summary>
public class FieldNode : LayoutNode
{
    public override LayoutNodeType NodeType => LayoutNodeType.Field;
    
    public Guid FieldId { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsRequiredOverride { get; set; }

    /// <summary>
    /// Разрешить создание связанного объекта (кнопка +).
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Разрешить переход к просмотру связанного объекта (ссылка).
    /// </summary>
    public bool CanView { get; set; }
}

/// <summary>
/// Узел: Контейнер вкладок
/// </summary>
public class TabControlNode : LayoutNode
{
    public override LayoutNodeType NodeType => LayoutNodeType.TabControl;
    
    public List<TabNode> Tabs { get; set; } = new();
}

/// <summary>
/// Узел: Отдельная вкладка
/// </summary>
public class TabNode : LayoutNode
{
    public override LayoutNodeType NodeType => LayoutNodeType.Tab;
    
    public string Title { get; set; } = "Новая вкладка";
    public List<LayoutNode> Children { get; set; } = new();
}

/// <summary>
/// Узел: Группа (сворачиваемая)
/// </summary>
public class GroupNode : LayoutNode
{
    public override LayoutNodeType NodeType => LayoutNodeType.Group;
    
    public string Title { get; set; } = "Группа";
    public bool IsCollapsed { get; set; }
    public List<LayoutNode> Children { get; set; } = new();
}

/// <summary>
/// Узел: Строка (Grid Row)
/// </summary>
public class RowNode : LayoutNode
{
    public override LayoutNodeType NodeType => LayoutNodeType.Row;
    
    public List<ColumnNode> Columns { get; set; } = new();
}

/// <summary>
/// Узел: Колонка (Grid Column)
/// </summary>
public class ColumnNode : LayoutNode
{
    public override LayoutNodeType NodeType => LayoutNodeType.Column;
    
    public int Width { get; set; } = 12; // Bootstrap col-12
    public List<LayoutNode> Children { get; set; } = new();
}
