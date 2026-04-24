using Group3RetailEcommercePrjct.Core;
using System.Text.Json;

var store = new SimulatedBackendStore();
var identity = new IdentityService(store);
var safety = new ContentSafetyService();
var tools = new CommerceTools(store);
var audit = new AuditLogger();
var orchestrator = new AgentOrchestrator(identity, safety, tools, audit);

var session = new SessionContext
{
	ThreadId = Guid.NewGuid().ToString("N"),
	IdentityVerified = false,
	VerifiedEmail = null
};

var scriptedRequests = new List<UserRequest>
{
	new()
	{
		Action = "verify_identity",
		CustomerMessage = "I need help with my order.",
		OrderId = "ORD-1002",
		CustomerEmail = "alex@shopaxis.com"
	},
	new()
	{
		Action = "order_status",
		CustomerMessage = "Please check order status",
		OrderId = "ORD-1002",
		CustomerEmail = "alex@shopaxis.com"
	},
	new()
	{
		Action = "return_initiation",
		CustomerMessage = "I am frustrated but I need a return.",
		OrderId = "ORD-1002",
		CustomerEmail = "alex@shopaxis.com"
	},
	new()
	{
		Action = "refund_status",
		CustomerMessage = "Check my refund status",
		ReturnId = "RET-9001",
		CustomerEmail = "alex@shopaxis.com"
	},
	new()
	{
		Action = "order_status",
		CustomerMessage = "This is fraud and I will file a lawsuit",
		OrderId = "ORD-1001",
		CustomerEmail = "alex@shopaxis.com"
	}
};

foreach (var request in scriptedRequests)
{
	var response = orchestrator.Process(session, request);
	var data = response.Data is null ? "null" : JsonSerializer.Serialize(response.Data);
	Console.WriteLine($"Action: {request.Action}");
	Console.WriteLine($"Response: {response.Message}");
	Console.WriteLine($"Data: {data}");
	Console.WriteLine(new string('-', 60));
}

var projectRoot = AppContext.BaseDirectory;
for (var i = 0; i < 4; i++)
{
	projectRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;
}

ToolSchemas.WriteJson(Path.Combine(projectRoot, "tool-schemas.json"));
audit.WriteJson(Path.Combine(projectRoot, "audit-log-sample.json"));

Console.WriteLine("Artifacts generated: tool-schemas.json, audit-log-sample.json");
