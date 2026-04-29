using Group3RetailEcommercePrjct.Core;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    // ── Welcome message ───────────────────────────────────────────────────────
    Console.WriteLine("Welcome to ShopAxis Customer Support! 🛒");
    Console.WriteLine("We can help you with the following services:");
    Console.WriteLine();
    Console.WriteLine("  1. Order Status          — e.g. Check status of ORD-0001");
    Console.WriteLine("  2. Return Initiation     — e.g. I want to return ORD-0001");
    Console.WriteLine("  3. Delivery Rescheduling — e.g. Reschedule delivery for ORD-0001 to 2026-05-15");
    Console.WriteLine("  4. Refund Status         — e.g. Check refund for RET-9001");
    Console.WriteLine();

    // ── Service loop — repeats until user says no ─────────────────────────────
    bool isFirstRound = true;
    bool userWantsToExit = false;

    while (!userWantsToExit)
    {
        // ── Service selection ─────────────────────────────────────────────
        int serviceChoice;
        while (true)
        {
            if (!isFirstRound)
            {
                Console.WriteLine("  1. Order Status          — e.g. Check status of ORD-0001");
                Console.WriteLine("  2. Return Initiation     — e.g. I want to return ORD-0001");
                Console.WriteLine("  3. Delivery Rescheduling — e.g. Reschedule delivery for ORD-0001 to 2026-05-15");
                Console.WriteLine("  4. Refund Status         — e.g. Check refund for RET-9001");
                Console.WriteLine();
            }
            Console.Write("Please select a service (1-4): ");
            var choiceInput = Console.ReadLine()?.Trim();
            if (int.TryParse(choiceInput, out serviceChoice) && serviceChoice >= 1 && serviceChoice <= 4)
                break;
            Console.WriteLine("Invalid selection. Please enter a number between 1 and 4.");
        }

        var serviceNames = new Dictionary<int, string>
        {
            { 1, "Order Status" },
            { 2, "Return Initiation" },
            { 3, "Delivery Rescheduling" },
            { 4, "Refund Status" }
        };
        Console.WriteLine($"You selected: {serviceNames[serviceChoice]}");
        Console.WriteLine(new string('-', 60));
        isFirstRound = false;

        // ── Collect and validate fields based on service selection ─────────
        string orderId = string.Empty;
        string customerEmail;
        string? returnId = null;

        while (true)
        {
            if (serviceChoice == 4)
            {
                while (true)
                {
                    Console.Write("Enter your Return ID (e.g. RET-9001): ");
                    var retInput = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(retInput))
                    {
                        returnId = retInput;
                        break;
                    }
                    Console.WriteLine("Return ID is a mandatory field. Please provide your Return ID to continue.");
                }
            }
            else
            {
                while (true)
                {
                    Console.Write("Enter your Order ID (e.g. ORD-0001): ");
                    var orderInput = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(orderInput))
                    {
                        orderId = orderInput;
                        break;
                    }
                    Console.WriteLine("Order ID is a mandatory field. Please provide your Order ID to continue.");
                }
            }

            while (true)
            {
                Console.Write("Enter your Email Address: ");
                var emailInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(emailInput))
                {
                    customerEmail = emailInput;
                    break;
                }
                Console.WriteLine("Email Address is a mandatory field. Please provide your Email Address to continue.");
            }

            // ── Validate against the store ────────────────────────────────
            if (serviceChoice == 4)
            {
                if (!store.Returns.TryGetValue(returnId!, out var matchedReturn))
                {
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine($"No data found for Return ID '{returnId}'. Please verify your Return ID and try again.");
                    Console.WriteLine(new string('-', 60));
                    continue;
                }
                if (!string.Equals(matchedReturn.CustomerEmail, customerEmail, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine($"No data found. The Email Address '{customerEmail}' does not match the records for Return ID '{returnId}'. Please verify your details and try again.");
                    Console.WriteLine(new string('-', 60));
                    continue;
                }
                orderId = matchedReturn.OrderId;
            }
            else
            {
                if (!store.Orders.TryGetValue(orderId, out var matchedOrder))
                {
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine($"No data found for Order ID '{orderId}'. Please verify your Order ID and try again.");
                    Console.WriteLine(new string('-', 60));
                    continue;
                }
                if (!string.Equals(matchedOrder.CustomerEmail, customerEmail, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine($"No data found. The Email Address '{customerEmail}' does not match the records for Order ID '{orderId}'. Please verify your details and try again.");
                    Console.WriteLine(new string('-', 60));
                    continue;
                }
            }

            break;
        }

        // ── Execute action based on service selection ─────────────────────
        Console.WriteLine(new string('-', 60));
        Console.WriteLine("✅ Identity verified.");

        if (serviceChoice == 1)
        {
            // Order Status
            var orderInfo = store.Orders[orderId];
            Console.WriteLine("Here are your order details:");
            Console.WriteLine($"  Order ID          : {orderInfo.OrderId}");
            Console.WriteLine($"  Email             : {orderInfo.CustomerEmail}");
            Console.WriteLine($"  Status            : {orderInfo.Status}");
            Console.WriteLine($"  Carrier           : {orderInfo.Carrier}");
            Console.WriteLine($"  Est. Delivery Date: {orderInfo.EstimatedDeliveryDate:yyyy-MM-dd}");
            if (orderInfo.DeliveredDate.HasValue)
                Console.WriteLine($"  Delivered Date    : {orderInfo.DeliveredDate.Value:yyyy-MM-dd}");
        }
        else if (serviceChoice == 2)
        {
            // Return Initiation
            try
            {
                var returnResult = tools.ReturnInitiationTool(orderId, customerEmail);
                var resultJson = JsonSerializer.Serialize(returnResult);
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;
                Console.WriteLine("Your return has been initiated successfully:");
                Console.WriteLine($"  Return ID               : {root.GetProperty("return_id").GetString()}");
                Console.WriteLine($"  Stage                   : {root.GetProperty("stage").GetString()}");
                Console.WriteLine($"  Expected Completion Date: {root.GetProperty("expected_completion_date").GetString()}");
            }
            catch (ToolExecutionException ex)
            {
                Console.WriteLine($"Return could not be initiated: {ex.Message}");
            }
        }
        else if (serviceChoice == 3)
        {
            // Delivery Rescheduling — ask for new date, max 3 attempts
            int attempts = 0;
            const int maxAttempts = 3;
            while (attempts < maxAttempts)
            {
                DateOnly newDate;
                while (true)
                {
                    Console.Write("Enter the new delivery date (yyyy-MM-dd): ");
                    var dateInput = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(dateInput) && DateOnly.TryParse(dateInput, out newDate))
                        break;
                    Console.WriteLine("Invalid date. Please enter a valid date in yyyy-MM-dd format.");
                }

                attempts++;
                try
                {
                    var rescheduleResult = tools.DeliveryReschedulingTool(orderId, customerEmail, newDate);
                    var resultJson = JsonSerializer.Serialize(rescheduleResult);
                    using var doc = JsonDocument.Parse(resultJson);
                    var root = doc.RootElement;
                    Console.WriteLine("Delivery has been rescheduled successfully:");
                    Console.WriteLine($"  Order ID          : {root.GetProperty("order_id").GetString()}");
                    Console.WriteLine($"  New Delivery Date : {root.GetProperty("new_delivery_date").GetString()}");
                    Console.WriteLine($"  Confirmation      : {root.GetProperty("confirmation").GetString()}");
                    break;
                }
                catch (ToolExecutionException ex)
                {
                    Console.WriteLine($"Delivery could not be rescheduled: {ex.Message}");
                    if (attempts < maxAttempts)
                        Console.WriteLine($"Please try again with a valid future date. ({maxAttempts - attempts} attempt(s) remaining)");
                    else
                        Console.WriteLine("Maximum attempts reached. Please try again later.");
                }
            }
        }
        else if (serviceChoice == 4)
        {
            // Refund Status
            var retRecord = store.Returns[returnId!];
            Console.WriteLine("Here are your return/refund details:");
            Console.WriteLine($"  Return ID               : {retRecord.ReturnId}");
            Console.WriteLine($"  Order ID                : {retRecord.OrderId}");
            Console.WriteLine($"  Email                   : {retRecord.CustomerEmail}");
            Console.WriteLine($"  Stage                   : {retRecord.Stage}");
            Console.WriteLine($"  Expected Completion Date: {retRecord.ExpectedCompletionDate:yyyy-MM-dd}");
        }
        Console.WriteLine(new string('-', 60));

        // ── Ask if user wants to check something else ─────────────────────
        while (true)
        {
            Console.Write("Is there anything else I can help you with? (yes/no): ");
            var continueChoice = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(continueChoice)
                || continueChoice.Equals("no", StringComparison.OrdinalIgnoreCase)
                || continueChoice.Equals("n", StringComparison.OrdinalIgnoreCase))
            {
                userWantsToExit = true;
                break;
            }
            if (continueChoice.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || continueChoice.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(new string('-', 60));
                break; // go back to service selection
            }
            Console.WriteLine("Please enter 'yes' or 'no'.");
        }
    }

    if (userWantsToExit)
    {
        Console.WriteLine(new string('-', 60));
        Console.WriteLine("Thank you for using ShopAxis Customer Support! Please visit us again. 👋");
        Console.WriteLine(new string('-', 60));
    }
}
else
{
    // ── Local simulation path ─────────────────────────────────────────────────
    var identity    = new IdentityService(store);
    var orchestrator = new AgentOrchestrator(
        identity,
        safety,
        tools,
        audit,
        new AgentRuntimeConfig
        {
            AgentName = config["Foundry:AgentName"] ?? "ShopAxis-Agent",
            ModelDeploymentName = config["Foundry:ModelDeploymentName"] ?? string.Empty,
            ProjectEndpoint = config["Foundry:ProjectEndpoint"] ?? string.Empty
        });
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
