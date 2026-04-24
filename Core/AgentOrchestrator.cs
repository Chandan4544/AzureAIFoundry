namespace Group3RetailEcommercePrjct.Core;

public sealed class AgentOrchestrator(
    IdentityService identityService,
    ContentSafetyService contentSafety,
    CommerceTools tools,
    AuditLogger auditLogger)
{
    public AgentResponse Process(SessionContext session, UserRequest request)
    {
        var runId = Guid.NewGuid().ToString("N");
        var safety = contentSafety.Evaluate(request.CustomerMessage);

        if (safety.BlockTransaction)
        {
            var blockedMessage = "I understand your frustration. I am escalating this to a human support specialist and pausing transactional actions for safety.";
            auditLogger.Log(new ToolAuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ThreadId = session.ThreadId,
                RunId = runId,
                ToolName = "NONE",
                Outcome = "Suspended",
                Summary = safety.Reason,
                Input = request,
                Output = null
            });

            return new AgentResponse
            {
                Message = blockedMessage,
                SuspendedForEscalation = true,
                Data = null
            };
        }

        try
        {
            if (string.Equals(request.Action, "verify_identity", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.CustomerEmail))
                {
                    throw new ToolExecutionException("Identity verification requires order_id and customer_email.");
                }

                var verified = identityService.VerifyCustomer(request.OrderId, request.CustomerEmail);
                session.IdentityVerified = verified;
                session.VerifiedEmail = verified ? request.CustomerEmail : null;

                var identityOutput = new { verified };
                auditLogger.Log(new ToolAuditEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    ThreadId = session.ThreadId,
                    RunId = runId,
                    ToolName = "IdentityVerification",
                    Outcome = verified ? "Success" : "Rejected",
                    Summary = verified ? "Identity verified." : "Identity mismatch.",
                    Input = new { request.OrderId, request.CustomerEmail },
                    Output = identityOutput
                });

                return new AgentResponse
                {
                    Message = verified
                        ? "Identity verified. I can process order transactions now."
                        : "I could not verify your identity with that order and email.",
                    SuspendedForEscalation = false,
                    Data = identityOutput
                };
            }

            EnsureIdentityPrecondition(session, request.CustomerEmail);

            var output = ExecuteTool(request);
            var tonePrefix = safety.ToneAdjustRequired
                ? "I am sorry this has been frustrating. "
                : string.Empty;

            auditLogger.Log(new ToolAuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ThreadId = session.ThreadId,
                RunId = runId,
                ToolName = request.Action,
                Outcome = "Success",
                Summary = "Tool call completed.",
                Input = request,
                Output = output
            });

            return new AgentResponse
            {
                Message = tonePrefix + "Request completed successfully.",
                SuspendedForEscalation = false,
                Data = output
            };
        }
        catch (ToolExecutionException ex)
        {
            auditLogger.Log(new ToolAuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ThreadId = session.ThreadId,
                RunId = runId,
                ToolName = request.Action,
                Outcome = "Rejected",
                Summary = ex.Message,
                Input = request,
                Output = null
            });

            return new AgentResponse
            {
                Message = ex.Message,
                SuspendedForEscalation = false,
                Data = null
            };
        }
    }

    private static void EnsureIdentityPrecondition(SessionContext session, string? customerEmail)
    {
        if (!session.IdentityVerified || string.IsNullOrWhiteSpace(session.VerifiedEmail))
        {
            throw new ToolExecutionException("Identity validation required before any transactional tool call.");
        }

        if (!string.Equals(session.VerifiedEmail, customerEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolExecutionException("Customer context mismatch. Re-verify identity before transaction.");
        }
    }

    private object ExecuteTool(UserRequest request)
    {
        return request.Action.ToLowerInvariant() switch
        {
            "order_status" when !string.IsNullOrWhiteSpace(request.OrderId) && !string.IsNullOrWhiteSpace(request.CustomerEmail)
                => tools.OrderStatusTool(request.OrderId, request.CustomerEmail),

            "return_initiation" when !string.IsNullOrWhiteSpace(request.OrderId) && !string.IsNullOrWhiteSpace(request.CustomerEmail)
                => tools.ReturnInitiationTool(request.OrderId, request.CustomerEmail),

            "delivery_reschedule" when !string.IsNullOrWhiteSpace(request.OrderId) && !string.IsNullOrWhiteSpace(request.CustomerEmail) && request.NewDeliveryDate is not null
                => tools.DeliveryReschedulingTool(request.OrderId, request.CustomerEmail, request.NewDeliveryDate.Value),

            "refund_status" when !string.IsNullOrWhiteSpace(request.ReturnId) && !string.IsNullOrWhiteSpace(request.CustomerEmail)
                => tools.RefundStatusTool(request.ReturnId, request.CustomerEmail),

            _ => throw new ToolExecutionException("Unsupported action or missing required parameters.")
        };
    }
}
