using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using System.Text.Json;

namespace Group3RetailEcommercePrjct.Core;

/// <summary>
/// Runs the ShopAxis agent against real Azure AI Foundry using the
/// Azure.AI.Agents.Persistent package (via Azure.AI.Projects 1.x).
///
/// Required keys in appsettings.json:
///   Foundry:ProjectEndpoint      – Azure AI Foundry project endpoint URL
///   Foundry:ModelDeploymentName  – deployed model name, e.g. gpt-4o-1
///   Foundry:AgentName            – display name for the created agent
///   Foundry:ApiKey               – project API key (leave blank to use az login)
/// </summary>
public sealed class FoundryAgentRunner
{
    private readonly ContentSafetyService _contentSafety;
    private readonly CommerceTools _tools;
    private readonly AuditLogger _audit;
    private readonly PersistentAgentsClient _agentsClient;
    private readonly string _modelDeployment;
    private readonly string _agentName;
    private readonly string _systemInstructions;

    // Cached so the same agent definition is reused across conversations.
    private PersistentAgent? _cachedAgent;

    public FoundryAgentRunner(
        IConfiguration config,
        ContentSafetyService contentSafety,
        CommerceTools tools,
        AuditLogger audit)
    {
        _contentSafety = contentSafety;
        _tools = tools;
        _audit = audit;

        var endpoint = config["Foundry:ProjectEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException(
                "Foundry:ProjectEndpoint is missing from appsettings.json.");

        _modelDeployment = config["Foundry:ModelDeploymentName"]
            ?? throw new InvalidOperationException(
                "Foundry:ModelDeploymentName is missing from appsettings.json.");

        _agentName = config["Foundry:AgentName"] ?? "ShopAxis-Agent";

        _systemInstructions = File.Exists("SYSTEM_INSTRUCTIONS.md")
            ? File.ReadAllText("SYSTEM_INSTRUCTIONS.md")
            : "You are a ShopAxis customer service agent. " +
              "Always verify customer identity before executing any transaction.";

        // Ensure Azure CLI is discoverable regardless of which terminal launched
        // the app — needed so AzureCliCredential inside DefaultAzureCredential works.
        const string azCliWbin = @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin";
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!currentPath.Contains(azCliWbin, StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", currentPath + ";" + azCliWbin);

        var tenantId = config["Foundry:TenantId"];
        var credOptions = new DefaultAzureCredentialOptions
        {
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId
        };
        AIProjectClient projectClient = new AIProjectClient(
            new Uri(endpoint), new DefaultAzureCredential(credOptions));

        _agentsClient = projectClient.GetPersistentAgentsClient();
    }

    /// <summary>
    /// Sends <paramref name="customerMessage"/> to the Foundry-hosted agent,
    /// dispatches any tool calls to <see cref="CommerceTools"/>, logs every
    /// invocation to <see cref="AuditLogger"/>, and returns the agent reply.
    /// </summary>
    public async Task<AgentResponse> RunAsync(
        SessionContext session, string customerMessage, CancellationToken ct = default)
    {
        // ── 1. Local content safety check ────────────────────────────────────
        var safety = _contentSafety.Evaluate(customerMessage);
        var localRunId = Guid.NewGuid().ToString("N");

        if (safety.BlockTransaction)
        {
            _audit.Log(new ToolAuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ThreadId     = session.ThreadId,
                RunId        = localRunId,
                ToolName     = "NONE",
                Outcome      = "Suspended",
                Summary      = safety.Reason,
                Input        = new { customerMessage },
                Output       = null
            });

            return new AgentResponse
            {
                Message = "I understand your frustration. I am escalating this to a " +
                          "human support specialist and pausing transactional actions for safety.",
                SuspendedForEscalation = true,
                Data = null
            };
        }

        // ── 2. Ensure agent exists (created once, reused) ─────────────────────
        _cachedAgent ??= await CreateAgentAsync(ct);

        // ── 3. Reuse the existing Foundry thread or create one on the first turn ──
        //       A single thread keeps full conversation history so the agent
        //       remembers identity info provided in earlier messages.
        PersistentAgentThread thread;
        if (string.IsNullOrEmpty(session.FoundryThreadId))
        {
            thread = await _agentsClient.Threads.CreateThreadAsync(cancellationToken: ct);
            session.FoundryThreadId = thread.Id;
        }
        else
        {
            thread = await _agentsClient.Threads.GetThreadAsync(
                session.FoundryThreadId, cancellationToken: ct);
        }

        // ── 4. Post customer message ──────────────────────────────────────────
        await _agentsClient.Messages.CreateMessageAsync(
            threadId: thread.Id,
            role: MessageRole.User,
            content: customerMessage,
            cancellationToken: ct);

        // ── 5. Start streaming run — tokens are printed as they arrive ─────────
        Console.Write("Agent: ");
        var replyBuilder = new System.Text.StringBuilder();
        ThreadRun? lastRun = null; // tracked so the fallback can call GetMessagesAsync

        // Debug log — records each update kind + runtime type so we can diagnose
        // streaming issues.  Written to debug.log in the working directory.
        var debugLog = System.IO.Path.Combine(AppContext.BaseDirectory, "debug.log");

        // The stream is restarted after each tool-call round-trip via
        // SubmitToolOutputsToStreamAsync; null means we are done.
        AsyncCollectionResult<StreamingUpdate>? streamingUpdates =
            _agentsClient.Runs.CreateRunStreamingAsync(
                thread.Id, _cachedAgent.Id, cancellationToken: ct);

        while (streamingUpdates is not null)
        {
            AsyncCollectionResult<StreamingUpdate>? nextStream = null;

            await foreach (StreamingUpdate update in streamingUpdates)
            {
                System.IO.File.AppendAllText(debugLog,
                    $"{update.UpdateKind} | {update.GetType().FullName}\n");

                // ── Tool call required ────────────────────────────────────────
                if (update.UpdateKind == StreamingUpdateReason.RunRequiresAction
                    && update is StreamingUpdate<ThreadRun> reqActionUpdate
                    && reqActionUpdate.Value.RequiredAction is SubmitToolOutputsAction submitAction)
                {
                    lastRun = reqActionUpdate.Value;
                    var toolOutputs = new List<ToolOutput>();

                    foreach (RequiredToolCall toolCall in submitAction.ToolCalls)
                    {
                        if (toolCall is not RequiredFunctionToolCall funcCall) continue;

                        var (result, outcome, summary) = DispatchTool(
                            funcCall.Name, funcCall.Arguments);

                        toolOutputs.Add(new ToolOutput(toolCall, JsonSerializer.Serialize(result)));

                        _audit.Log(new ToolAuditEntry
                        {
                            TimestampUtc = DateTime.UtcNow,
                            ThreadId     = session.ThreadId,
                            RunId        = lastRun.Id,
                            ToolName     = funcCall.Name,
                            Outcome      = outcome,
                            Summary      = summary,
                            Input        = funcCall.Arguments,
                            Output       = result
                        });
                    }

                    // Resume streaming after submitting tool outputs
                    nextStream = _agentsClient.Runs.SubmitToolOutputsToStreamAsync(
                        lastRun, toolOutputs, cancellationToken: ct);
                    break; // exit inner foreach; outer while continues with nextStream
                }

                // ── Track run ID for fallback ─────────────────────────────────
                if (update is StreamingUpdate<ThreadRun> runStatusUpdate)
                    lastRun = runStatusUpdate.Value;

                // ── Streaming token delta (primary path) ──────────────────────
                if (update.UpdateKind == StreamingUpdateReason.MessageUpdated
                    && update is StreamingUpdate<MessageDeltaChunk> deltaUpdate)
                {
                    foreach (MessageDeltaContent content in deltaUpdate.Value.Delta.Content)
                    {
                        if (content is MessageDeltaTextContent textDelta)
                        {
                            var token = textDelta.Text?.Value ?? string.Empty;
                            if (token.Length > 0)
                            {
                                Console.Write(token);
                                replyBuilder.Append(token);
                            }
                        }
                    }
                }

                // ── MessageCompleted fallback (if no deltas were streamed) ─────
                // Some deployments send a single completed message instead of
                // per-token deltas. Capture it here so the user always sees output.
                if (update.UpdateKind == StreamingUpdateReason.MessageCompleted
                    && update is StreamingUpdate<PersistentThreadMessage> completedMsg
                    && completedMsg.Value.Role == MessageRole.Agent
                    && replyBuilder.Length == 0)
                {
                    foreach (MessageContent content in completedMsg.Value.ContentItems)
                    {
                        if (content is MessageTextContent textContent)
                        {
                            Console.Write(textContent.Text);
                            replyBuilder.Append(textContent.Text);
                        }
                    }
                }

                // ── Non-completion terminal state ─────────────────────────────
                if (update.UpdateKind is StreamingUpdateReason.RunFailed
                                      or StreamingUpdateReason.RunCancelled
                                      or StreamingUpdateReason.RunExpired)
                {
                    Console.WriteLine();
                    return new AgentResponse
                    {
                        Message = $"The agent run ended with status '{update.UpdateKind}'.",
                        SuspendedForEscalation = false,
                        Data = null
                    };
                }
            }

            streamingUpdates = nextStream;
        }

        // ── Final fallback: if neither deltas nor MessageCompleted produced text,
        //    fetch the agent's latest message directly (handles APIs that do not
        //    stream token-level events at all). ──────────────────────────────────
        if (replyBuilder.Length == 0)
        {
            await foreach (PersistentThreadMessage msg in
                _agentsClient.Messages.GetMessagesAsync(thread.Id, cancellationToken: ct))
            {
                if (msg.Role != MessageRole.Agent) continue;
                foreach (MessageContent content in msg.ContentItems)
                {
                    if (content is MessageTextContent textContent)
                    {
                        Console.Write(textContent.Text);
                        replyBuilder.Append(textContent.Text);
                        break;
                    }
                }
                if (replyBuilder.Length > 0) break;
            }
        }

        Console.WriteLine(); // newline after streamed/fallback tokens

        var replyText = replyBuilder.ToString();
        if (string.IsNullOrEmpty(replyText))
            replyText = "No response received from the agent.";

        if (safety.ToneAdjustRequired)
            replyText = "I am sorry this has been a frustrating experience. " + replyText;

        return new AgentResponse
        {
            Message = replyText,
            SuspendedForEscalation = false,
            Data = null
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<PersistentAgent> CreateAgentAsync(CancellationToken ct)
    {
        return await _agentsClient.Administration.CreateAgentAsync(
            model:        _modelDeployment,
            name:         _agentName,
            instructions: _systemInstructions,
            tools:        BuildToolDefinitions(),
            cancellationToken: ct);
    }

    private (object result, string outcome, string summary) DispatchTool(
        string toolName, string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            string Get(string key) =>
                root.TryGetProperty(key, out var v) ? v.GetString() ?? string.Empty : string.Empty;

            object result = toolName switch
            {
                "OrderStatusTool" =>
                    _tools.OrderStatusTool(Get("order_id"), Get("customer_email")),

                "ReturnInitiationTool" =>
                    _tools.ReturnInitiationTool(Get("order_id"), Get("customer_email")),

                "DeliveryReschedulingTool" =>
                    _tools.DeliveryReschedulingTool(
                        Get("order_id"),
                        Get("customer_email"),
                        DateOnly.Parse(Get("new_delivery_date"))),

                "RefundStatusTool" =>
                    _tools.RefundStatusTool(Get("return_id"), Get("customer_email")),

                _ => throw new ToolExecutionException($"Unknown tool: {toolName}")
            };

            return (result, "Success", "Tool executed successfully.");
        }
        catch (ToolExecutionException ex)
        {
            return (new { error = ex.Message }, "Rejected", ex.Message);
        }
        catch (Exception ex)
        {
            return (new { error = "Internal tool error." }, "Error", ex.Message);
        }
    }

    private static bool IsTerminalStatus(RunStatus status) =>
        status == RunStatus.Completed
        || status == RunStatus.Failed
        || status == RunStatus.Cancelled
        || status == RunStatus.Expired;

    private static List<ToolDefinition> BuildToolDefinitions() =>
    [
        new FunctionToolDefinition(
            name: "OrderStatusTool",
            description: "Look up order status, carrier, and estimated delivery date. " +
                         "Only call after identity has been verified.",
            parameters: BinaryData.FromString("""
                {
                  "type": "object",
                  "required": ["order_id", "customer_email"],
                  "properties": {
                    "order_id":       { "type": "string", "pattern": "^ORD-[0-9]{4}$" },
                    "customer_email": { "type": "string", "format": "email" }
                  },
                  "additionalProperties": false
                }
                """)),

        new FunctionToolDefinition(
            name: "ReturnInitiationTool",
            description: "Initiate a return for a delivered order. " +
                         "Only allowed within 30 days of delivery. " +
                         "Do not call unless identity is verified.",
            parameters: BinaryData.FromString("""
                {
                  "type": "object",
                  "required": ["order_id", "customer_email"],
                  "properties": {
                    "order_id":       { "type": "string", "pattern": "^ORD-[0-9]{4}$" },
                    "customer_email": { "type": "string", "format": "email" }
                  },
                  "additionalProperties": false
                }
                """)),

        new FunctionToolDefinition(
            name: "DeliveryReschedulingTool",
            description: "Reschedule delivery for an order not yet delivered. " +
                         "new_delivery_date must be a future date in yyyy-MM-dd format. " +
                         "Do not call unless identity is verified.",
            parameters: BinaryData.FromString("""
                {
                  "type": "object",
                  "required": ["order_id", "customer_email", "new_delivery_date"],
                  "properties": {
                    "order_id":          { "type": "string", "pattern": "^ORD-[0-9]{4}$" },
                    "customer_email":    { "type": "string", "format": "email" },
                    "new_delivery_date": { "type": "string", "pattern": "^[0-9]{4}-[0-9]{2}-[0-9]{2}$" }
                  },
                  "additionalProperties": false
                }
                """)),

        new FunctionToolDefinition(
            name: "RefundStatusTool",
            description: "Get the current refund pipeline stage and expected completion date. " +
                         "Do not call unless identity is verified.",
            parameters: BinaryData.FromString("""
                {
                  "type": "object",
                  "required": ["return_id", "customer_email"],
                  "properties": {
                    "return_id":      { "type": "string", "pattern": "^RET-[0-9]{4}$" },
                    "customer_email": { "type": "string", "format": "email" }
                  },
                  "additionalProperties": false
                }
                """))
    ];
}
