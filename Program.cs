using Group3RetailEcommercePrjct.Core;
using System.Text.Json;

var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.json");
appSettingsPath = Path.GetFullPath(appSettingsPath);
var projectRoot = Path.GetDirectoryName(appSettingsPath) ?? appSettingsPath;

if (!File.Exists(appSettingsPath))
{
	throw new FileNotFoundException($"appsettings.json was not found at: {appSettingsPath}");
}

var appSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(appSettingsPath))
	?? throw new InvalidOperationException("Unable to parse appsettings.json.");

if (string.IsNullOrWhiteSpace(appSettings.PROJECT_ENDPOINT)
	|| string.IsNullOrWhiteSpace(appSettings.MODEL_DEPLOYMENT_NAME)
	|| string.IsNullOrWhiteSpace(appSettings.AGENT_NAME))
{
	throw new InvalidOperationException("appsettings.json must include PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME, and AGENT_NAME.");
}

Console.WriteLine($"Agent: {appSettings.AGENT_NAME}");
Console.WriteLine($"Model deployment: {appSettings.MODEL_DEPLOYMENT_NAME}");
Console.WriteLine(new string('=', 60));

var store = new SimulatedBackendStore();
var identity = new IdentityService(store);
var safety = new ContentSafetyService();
var tools = new CommerceTools(store);
var audit = new AuditLogger();
var runtimeConfig = new AgentRuntimeConfig
{
	ProjectEndpoint = appSettings.PROJECT_ENDPOINT,
	ModelDeploymentName = appSettings.MODEL_DEPLOYMENT_NAME,
	AgentName = appSettings.AGENT_NAME
};
var orchestrator = new AgentOrchestrator(identity, safety, tools, audit, runtimeConfig);

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

ToolSchemas.WriteJson(Path.Combine(projectRoot, "tool-schemas.json"));
audit.WriteJson(Path.Combine(projectRoot, "audit-log-sample.json"));

Console.WriteLine("Artifacts generated: tool-schemas.json, audit-log-sample.json");

internal sealed record AppSettings
{
	public string PROJECT_ENDPOINT { get; init; } = string.Empty;
	public string MODEL_DEPLOYMENT_NAME { get; init; } = string.Empty;
	public string AGENT_NAME { get; init; } = string.Empty;
}
