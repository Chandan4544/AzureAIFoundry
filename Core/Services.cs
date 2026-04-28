using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace Group3RetailEcommercePrjct.Core;

public sealed class SimulatedBackendStore
{
    public Dictionary<string, OrderRecord> Orders { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ReturnRecord> Returns { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, RefundRecord> Refunds { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    private SimulatedBackendStore() { }

    /// <summary>
    /// Creates the store. If Storage:AccountUrl is set in config, loads
    /// orders.json and returns.json from that Azure Blob container.
    /// Falls back to built-in seed data when storage is not configured.
    /// </summary>
    public static async Task<SimulatedBackendStore> LoadAsync(IConfiguration config)
    {
        var store = new SimulatedBackendStore();
        var accountUrl   = config["Storage:AccountUrl"];
        var containerName = config["Storage:ContainerName"] ?? "shopaxis-data";

        if (!string.IsNullOrWhiteSpace(accountUrl))
        {
            Console.WriteLine($"Loading dataset from storage: {accountUrl}/{containerName}");
            var connectionString = config["Storage:ConnectionString"];
            BlobContainerClient container;
            if (!string.IsNullOrWhiteSpace(connectionString))
                container = new BlobContainerClient(connectionString, containerName);
            else
                container = new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential())
                                .GetBlobContainerClient(containerName);

            store.Orders  = await LoadBlobJsonAsync<Dictionary<string, OrderRecord>>(container,  "orders.json")
                            ?? store.DefaultOrders();
            store.Returns = await LoadBlobJsonAsync<Dictionary<string, ReturnRecord>>(container, "returns.json")
                            ?? store.DefaultReturns();
            store.Refunds = await LoadBlobJsonAsync<Dictionary<string, RefundRecord>>(container, "refunds.json")
                            ?? store.DefaultRefunds();

            Console.WriteLine($"  Loaded {store.Orders.Count} orders, {store.Returns.Count} returns, {store.Refunds.Count} refunds.");
        }
        else
        {
            store.Orders  = store.DefaultOrders();
            store.Returns = store.DefaultReturns();
            store.Refunds = store.DefaultRefunds();
        }

        return store;
    }

    private static async Task<T?> LoadBlobJsonAsync<T>(BlobContainerClient container, string blobName)
    {
        var blob = container.GetBlobClient(blobName);
        if (!await blob.ExistsAsync()) return default;
        var response = await blob.DownloadContentAsync();

        // Strip UTF-8 BOM (0xEF 0xBB 0xBF) written by PowerShell Set-Content
        var bytes = response.Value.Content.ToArray();
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];

        return JsonSerializer.Deserialize<T>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private Dictionary<string, OrderRecord> DefaultOrders() =>
        new(StringComparer.OrdinalIgnoreCase)
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

    private Dictionary<string, ReturnRecord> DefaultReturns() =>
        new(StringComparer.OrdinalIgnoreCase)
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

    private Dictionary<string, RefundRecord> DefaultRefunds() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["REF-0001"] = new()
            {
                RefundId = "REF-0001",
                ReturnId = "RET-9001",
                OrderId = "ORD-1002",
                CustomerEmail = "alex@shopaxis.com",
                Status = "Processing",
                Amount = 49.99m,
                Method = "Original Payment Method",
                InitiatedAtUtc = DateTime.UtcNow.AddDays(-2),
                CompletedAtUtc = null
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
