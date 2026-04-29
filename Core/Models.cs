using System.Text.Json.Serialization;

namespace Group3RetailEcommercePrjct.Core;

public sealed class OrderRecord
{
    public required string OrderId { get; init; }
    public required string CustomerEmail { get; init; }
    public required string Status { get; set; }
    public required string Carrier { get; set; }
    public required DateOnly EstimatedDeliveryDate { get; set; }
    public DateOnly? DeliveredDate { get; set; }
}

public sealed class ReturnRecord
{
    public required string ReturnId { get; init; }
    public required string OrderId { get; init; }
    public required string CustomerEmail { get; init; }
    public required string Stage { get; set; }
    public required DateOnly ExpectedCompletionDate { get; set; }
    public required DateTime CreatedAtUtc { get; init; }
}

public sealed class RefundRecord
{
    public required string RefundId { get; init; }
    public required string ReturnId { get; init; }
    public required string OrderId { get; init; }
    public required string CustomerEmail { get; init; }
    public required string Status { get; set; }
    public required decimal Amount { get; set; }
    public required string Method { get; set; }
    public required DateTime InitiatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class SessionContext
{
    public required string ThreadId { get; init; }
    /// <summary>Foundry thread ID — set on the first RunAsync call and reused for all subsequent turns.</summary>
    public string? FoundryThreadId { get; set; }
    public string? VerifiedEmail { get; set; }
    public bool IdentityVerified { get; set; }
    public string? OrderId { get; set; }
    public string? CustomerEmail { get; set; }
}

public sealed class UserRequest
{
    public required string Action { get; init; }
    public required string CustomerMessage { get; init; }
    public string? CustomerEmail { get; init; }
    public string? OrderId { get; init; }
    public string? ReturnId { get; init; }
    public DateOnly? NewDeliveryDate { get; init; }
}

public sealed class AgentResponse
{
    public required string Message { get; init; }
    public bool SuspendedForEscalation { get; init; }
    public object? Data { get; init; }
}

public sealed class SafetyAssessment
{
    public int Score { get; init; }
    public bool BlockTransaction { get; init; }
    public bool ToneAdjustRequired { get; init; }
    public required string Reason { get; init; }
}

public sealed class ToolAuditEntry
{
    public required DateTime TimestampUtc { get; init; }
    public required string ThreadId { get; init; }
    public required string RunId { get; init; }
    public required string ToolName { get; init; }
    public required string Outcome { get; init; }
    public required string Summary { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Input { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Output { get; init; }
}

public sealed class AgentRuntimeConfig
{
    public required string AgentName { get; init; }
    public required string ModelDeploymentName { get; init; }
    public required string ProjectEndpoint { get; init; }
}
