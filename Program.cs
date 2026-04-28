using Group3RetailEcommercePrjct.Core;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

// ── Build configuration ───────────────────────────────────────────────────────
// Loads appsettings.json first, then appsettings.Development.json (if present),
// then environment variables (which override both files).
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var store  = await SimulatedBackendStore.LoadAsync(config);
var safety = new ContentSafetyService();
var tools  = new CommerceTools(store);
var audit  = new AuditLogger();

// ── Mode selection ────────────────────────────────────────────────────────────
// Runs in LIVE mode when Foundry:ProjectEndpoint is filled in appsettings.json
// (or overridden by an environment variable). Otherwise runs fully locally.
var useFoundry = !string.IsNullOrWhiteSpace(config["Foundry:ProjectEndpoint"]);

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine(useFoundry
    ? "Mode: LIVE  — Azure AI Foundry (GPT-4o)"
    : "Mode: LOCAL — Simulated orchestrator");
Console.WriteLine(new string('-', 60));

if (useFoundry)
{
    // ── Live Azure AI Foundry path — interactive chat ─────────────────────────
    var runner  = new FoundryAgentRunner(config, safety, tools, audit);
    var session = new SessionContext
    {
        ThreadId         = Guid.NewGuid().ToString("N"),
        IdentityVerified = false,
        VerifiedEmail    = null
    };

    Console.WriteLine("Type your message and press Enter. Type 'exit' to quit.");
    Console.WriteLine(new string('-', 60));

    while (true)
    {
        Console.Write("You: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;
        if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

        var response = await runner.RunAsync(session, input);
        // Streaming already printed "Agent: <tokens>\n" for normal replies.
        // For suspension/safety blocks, nothing was streamed, so print the message here.
        if (response.SuspendedForEscalation)
        {
            Console.WriteLine($"Agent: {response.Message}");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine("[Session suspended — escalated to human agent]");
            break;
        }
        Console.WriteLine(new string('-', 60));
    }
}
else
{
    // ── Local simulation path ─────────────────────────────────────────────────
    var identity    = new IdentityService(store);
    var orchestrator = new AgentOrchestrator(identity, safety, tools, audit);
    var session = new SessionContext
    {
        ThreadId         = Guid.NewGuid().ToString("N"),
        IdentityVerified = false,
        VerifiedEmail    = null
    };

    var scriptedRequests = new List<UserRequest>
    {
        new() { Action = "verify_identity",   CustomerMessage = "I need help with my order.",                   OrderId = "ORD-1002", CustomerEmail = "alex@shopaxis.com" },
        new() { Action = "order_status",      CustomerMessage = "Please check order status",                    OrderId = "ORD-1002", CustomerEmail = "alex@shopaxis.com" },
        new() { Action = "return_initiation", CustomerMessage = "I am frustrated but I need a return.",         OrderId = "ORD-1002", CustomerEmail = "alex@shopaxis.com" },
        new() { Action = "refund_status",     CustomerMessage = "Check my refund status",                       ReturnId = "RET-9001", CustomerEmail = "alex@shopaxis.com" },
        new() { Action = "order_status",      CustomerMessage = "This is fraud and I will file a lawsuit",      OrderId = "ORD-1001", CustomerEmail = "alex@shopaxis.com" }
    };

    foreach (var request in scriptedRequests)
    {
        var response = orchestrator.Process(session, request);
        var data = response.Data is null ? "null" : JsonSerializer.Serialize(response.Data);
        Console.WriteLine($"Action  : {request.Action}");
        Console.WriteLine($"Response: {response.Message}");
        Console.WriteLine($"Data    : {data}");
        Console.WriteLine(new string('-', 60));
    }
}

// ── Write artifacts ───────────────────────────────────────────────────────────
var projectRoot = AppContext.BaseDirectory;
for (var i = 0; i < 4; i++)
    projectRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;

ToolSchemas.WriteJson(Path.Combine(projectRoot, "tool-schemas.json"));
audit.WriteJson(Path.Combine(projectRoot, "audit-log-sample.json"));

Console.WriteLine("Artifacts saved: tool-schemas.json, audit-log-sample.json");
