using System.Text.Json;

namespace Group3RetailEcommercePrjct.Core;

public sealed class SimulatedBackendStore
{
    public Dictionary<string, OrderRecord> Orders { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ORD-1001"] = new()
        {
            OrderId = "ORD-1001",
            CustomerEmail = "alex@shopaxis.com",
            Status = "Shipped",
            Carrier = "ContosoShip",
            EstimatedDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))
        },
        ["ORD-1002"] = new()
        {
            OrderId = "ORD-1002",
            CustomerEmail = "alex@shopaxis.com",
            Status = "Delivered",
            Carrier = "ContosoShip",
            EstimatedDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)),
            DeliveredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3))
        },
        ["ORD-2001"] = new()
        {
            OrderId = "ORD-2001",
            CustomerEmail = "jamie@shopaxis.com",
            Status = "In Transit",
            Carrier = "FabrikamExpress",
            EstimatedDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        }
    };

    public Dictionary<string, ReturnRecord> Returns { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RET-9001"] = new()
        {
            ReturnId = "RET-9001",
            OrderId = "ORD-1002",
            CustomerEmail = "alex@shopaxis.com",
            Stage = "Inspection",
            ExpectedCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        }
    };
}

public sealed class IdentityService(SimulatedBackendStore store)
{
    public bool VerifyCustomer(string orderId, string customerEmail)
    {
        if (!store.Orders.TryGetValue(orderId, out var order))
        {
            return false;
        }

        return string.Equals(order.CustomerEmail, customerEmail, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ContentSafetyService
{
    private static readonly string[] BlockTerms = ["abuse", "lawsuit", "threat", "violent", "fraud"];
    private static readonly string[] ToneAdjustTerms = ["angry", "frustrated", "upset", "terrible", "worst"];

    public SafetyAssessment Evaluate(string text)
    {
        var normalized = text.ToLowerInvariant();

        var blocked = BlockTerms.Any(normalized.Contains);
        if (blocked)
        {
            return new SafetyAssessment
            {
                Score = 90,
                BlockTransaction = true,
                ToneAdjustRequired = true,
                Reason = "High-risk language detected"
            };
        }

        var toneAdjust = ToneAdjustTerms.Any(normalized.Contains);
        return new SafetyAssessment
        {
            Score = toneAdjust ? 55 : 10,
            BlockTransaction = false,
            ToneAdjustRequired = toneAdjust,
            Reason = toneAdjust ? "Frustration detected" : "No safety concern"
        };
    }
}

public sealed class AuditLogger
{
    private readonly List<ToolAuditEntry> _entries = [];

    public IReadOnlyList<ToolAuditEntry> Entries => _entries;

    public void Log(ToolAuditEntry entry) => _entries.Add(entry);

    public void WriteJson(string outputPath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_entries, options);
        File.WriteAllText(outputPath, json);
    }
}
