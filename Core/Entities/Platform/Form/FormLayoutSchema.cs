using System.Text.Json.Serialization;

namespace Core.Entities.Platform.Form;

/// <summary>
/// Корневой объект схемы Layout
/// </summary>
public class FormLayoutSchema
{
    public List<LayoutNode> Nodes { get; set; } = new();
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