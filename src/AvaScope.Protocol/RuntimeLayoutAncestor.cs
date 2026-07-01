using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeLayoutAncestor
{
    [JsonConstructor]
    public RuntimeLayoutAncestor(
        string nodeId,
        string nodeType,
        string? name = null,
        NodeBounds? bounds = null,
        RuntimeSize? desiredSize = null,
        RuntimeSize? arrangedSize = null,
        bool? clipToBounds = null,
        string? panelKind = null,
        string? gridRow = null,
        string? gridColumn = null,
        string? gridRowSpan = null,
        string? gridColumnSpan = null,
        string? rowHeights = null,
        string? columnWidths = null,
        RuntimeScrollState? scrollState = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(nodeType))
        {
            throw new ArgumentException("Node type cannot be empty.", nameof(nodeType));
        }

        NodeId = nodeId;
        NodeType = nodeType;
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
        Bounds = bounds;
        DesiredSize = desiredSize;
        ArrangedSize = arrangedSize;
        ClipToBounds = clipToBounds;
        PanelKind = string.IsNullOrWhiteSpace(panelKind) ? null : panelKind;
        GridRow = string.IsNullOrWhiteSpace(gridRow) ? null : gridRow;
        GridColumn = string.IsNullOrWhiteSpace(gridColumn) ? null : gridColumn;
        GridRowSpan = string.IsNullOrWhiteSpace(gridRowSpan) ? null : gridRowSpan;
        GridColumnSpan = string.IsNullOrWhiteSpace(gridColumnSpan) ? null : gridColumnSpan;
        RowHeights = string.IsNullOrWhiteSpace(rowHeights) ? null : rowHeights;
        ColumnWidths = string.IsNullOrWhiteSpace(columnWidths) ? null : columnWidths;
        ScrollState = scrollState;
    }

    [JsonPropertyName("nodeId")]
    public string NodeId { get; }

    [JsonPropertyName("nodeType")]
    public string NodeType { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? Bounds { get; }

    [JsonPropertyName("desiredSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? DesiredSize { get; }

    [JsonPropertyName("arrangedSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? ArrangedSize { get; }

    [JsonPropertyName("clipToBounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ClipToBounds { get; }

    [JsonPropertyName("panelKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PanelKind { get; }

    [JsonPropertyName("gridRow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GridRow { get; }

    [JsonPropertyName("gridColumn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GridColumn { get; }

    [JsonPropertyName("gridRowSpan")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GridRowSpan { get; }

    [JsonPropertyName("gridColumnSpan")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GridColumnSpan { get; }

    [JsonPropertyName("rowHeights")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RowHeights { get; }

    [JsonPropertyName("columnWidths")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ColumnWidths { get; }

    [JsonPropertyName("scrollState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeScrollState? ScrollState { get; }
}
