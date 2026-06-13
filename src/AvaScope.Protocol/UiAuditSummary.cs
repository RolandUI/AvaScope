using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record UiAuditSummary
{
    [JsonConstructor]
    public UiAuditSummary(
        int totalNodes,
        int actionableNodes,
        int nodesWithAutomationId,
        int nodesWithAccessibilityName,
        int nodesWithValidationMetadata,
        int nodesWithValidationErrors,
        int distinctControlTypes,
        int distinctClasses,
        int repeatedPatternCount,
        int issueCount,
        int inventoryItemCount,
        string accessibilityStatus,
        string validationStatus,
        string focusOrderStatus,
        bool truncated = false)
    {
        if (totalNodes < 0
            || actionableNodes < 0
            || nodesWithAutomationId < 0
            || nodesWithAccessibilityName < 0
            || nodesWithValidationMetadata < 0
            || nodesWithValidationErrors < 0
            || distinctControlTypes < 0
            || distinctClasses < 0
            || repeatedPatternCount < 0
            || issueCount < 0
            || inventoryItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalNodes), "Audit summary counts cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(accessibilityStatus))
        {
            throw new ArgumentException("Accessibility status cannot be empty.", nameof(accessibilityStatus));
        }

        if (string.IsNullOrWhiteSpace(validationStatus))
        {
            throw new ArgumentException("Validation status cannot be empty.", nameof(validationStatus));
        }

        if (string.IsNullOrWhiteSpace(focusOrderStatus))
        {
            throw new ArgumentException("Focus order status cannot be empty.", nameof(focusOrderStatus));
        }

        TotalNodes = totalNodes;
        ActionableNodes = actionableNodes;
        NodesWithAutomationId = nodesWithAutomationId;
        NodesWithAccessibilityName = nodesWithAccessibilityName;
        NodesWithValidationMetadata = nodesWithValidationMetadata;
        NodesWithValidationErrors = nodesWithValidationErrors;
        DistinctControlTypes = distinctControlTypes;
        DistinctClasses = distinctClasses;
        RepeatedPatternCount = repeatedPatternCount;
        IssueCount = issueCount;
        InventoryItemCount = inventoryItemCount;
        AccessibilityStatus = accessibilityStatus.Trim();
        ValidationStatus = validationStatus.Trim();
        FocusOrderStatus = focusOrderStatus.Trim();
        Truncated = truncated;
    }

    [JsonPropertyName("totalNodes")]
    public int TotalNodes { get; }

    [JsonPropertyName("actionableNodes")]
    public int ActionableNodes { get; }

    [JsonPropertyName("nodesWithAutomationId")]
    public int NodesWithAutomationId { get; }

    [JsonPropertyName("nodesWithAccessibilityName")]
    public int NodesWithAccessibilityName { get; }

    [JsonPropertyName("nodesWithValidationMetadata")]
    public int NodesWithValidationMetadata { get; }

    [JsonPropertyName("nodesWithValidationErrors")]
    public int NodesWithValidationErrors { get; }

    [JsonPropertyName("distinctControlTypes")]
    public int DistinctControlTypes { get; }

    [JsonPropertyName("distinctClasses")]
    public int DistinctClasses { get; }

    [JsonPropertyName("repeatedPatternCount")]
    public int RepeatedPatternCount { get; }

    [JsonPropertyName("issueCount")]
    public int IssueCount { get; }

    [JsonPropertyName("inventoryItemCount")]
    public int InventoryItemCount { get; }

    [JsonPropertyName("accessibilityStatus")]
    public string AccessibilityStatus { get; }

    [JsonPropertyName("validationStatus")]
    public string ValidationStatus { get; }

    [JsonPropertyName("focusOrderStatus")]
    public string FocusOrderStatus { get; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; }
}
