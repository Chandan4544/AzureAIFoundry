using System.Collections.Concurrent;
using System.Text.Json;

namespace Group3RetailEcommercePrjct.Core;

public static class ApiEndpoints
{
    // In-memory chat sessions keyed by session ID
    private static readonly ConcurrentDictionary<string, SessionContext> _chatSessions = new();

    public static void MapShopAxisApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/validate", HandleValidate);
        api.MapPost("/order-status", HandleOrderStatus);
        api.MapPost("/return", HandleReturnInitiation);
        api.MapPost("/reschedule", HandleReschedule);
        api.MapPost("/refund-status", HandleRefundStatus);
        api.MapPost("/chat", HandleChat);
    }

    private static IResult HandleValidate(
        ValidateRequest req, SimulatedBackendStore store)
    {
        if (string.IsNullOrWhiteSpace(req.Id) || string.IsNullOrWhiteSpace(req.Email))
            return Results.BadRequest(new ApiResult { Success = false, Message = "ID and Email are required." });

        if (req.IdType.Equals("return", StringComparison.OrdinalIgnoreCase))
        {
            if (!store.Returns.TryGetValue(req.Id, out var ret))
                return Results.Ok(new ValidateResponse { Valid = false, Message = $"No data found for Return ID '{req.Id}'." });

            if (!string.Equals(ret.CustomerEmail, req.Email, StringComparison.OrdinalIgnoreCase))
                return Results.Ok(new ValidateResponse { Valid = false, Message = $"Email does not match records for Return ID '{req.Id}'." });

            return Results.Ok(new ValidateResponse
            {
                Valid = true,
                Data = new
                {
                    ret.ReturnId,
                    ret.OrderId,
                    ret.CustomerEmail,
                    ret.Stage,
                    ExpectedCompletionDate = ret.ExpectedCompletionDate.ToString("yyyy-MM-dd")
                }
            });
        }
        else
        {
            if (!store.Orders.TryGetValue(req.Id, out var order))
                return Results.Ok(new ValidateResponse { Valid = false, Message = $"No data found for Order ID '{req.Id}'." });

            if (!string.Equals(order.CustomerEmail, req.Email, StringComparison.OrdinalIgnoreCase))
                return Results.Ok(new ValidateResponse { Valid = false, Message = $"Email does not match records for Order ID '{req.Id}'." });

            return Results.Ok(new ValidateResponse
            {
                Valid = true,
                Data = new
                {
                    order.OrderId,
                    order.CustomerEmail,
                    order.Status,
                    order.Carrier,
                    EstimatedDeliveryDate = order.EstimatedDeliveryDate.ToString("yyyy-MM-dd"),
                    DeliveredDate = order.DeliveredDate?.ToString("yyyy-MM-dd")
                }
            });
        }
    }

    private static IResult HandleOrderStatus(
        OrderStatusRequest req, CommerceTools tools)
    {
        try
        {
            var result = tools.OrderStatusTool(req.OrderId, req.Email);
            return Results.Ok(new ApiResult { Success = true, Data = result });
        }
        catch (ToolExecutionException ex)
        {
            return Results.Ok(new ApiResult { Success = false, Message = ex.Message });
        }
    }

    private static IResult HandleReturnInitiation(
        ReturnRequest req, CommerceTools tools)
    {
        try
        {
            var result = tools.ReturnInitiationTool(req.OrderId, req.Email);
            return Results.Ok(new ApiResult { Success = true, Data = result });
        }
        catch (ToolExecutionException ex)
        {
            return Results.Ok(new ApiResult { Success = false, Message = ex.Message });
        }
    }

    private static IResult HandleReschedule(
        RescheduleRequest req, CommerceTools tools)
    {
        if (!DateOnly.TryParse(req.NewDeliveryDate, out var newDate))
            return Results.BadRequest(new ApiResult { Success = false, Message = "Invalid date format. Use yyyy-MM-dd." });

        try
        {
            var result = tools.DeliveryReschedulingTool(req.OrderId, req.Email, newDate);
            return Results.Ok(new ApiResult { Success = true, Data = result });
        }
        catch (ToolExecutionException ex)
        {
            return Results.Ok(new ApiResult { Success = false, Message = ex.Message });
        }
    }

    private static IResult HandleRefundStatus(
        RefundStatusRequest req, CommerceTools tools)
    {
        try
        {
            var result = tools.RefundStatusTool(req.ReturnId, req.Email);
            return Results.Ok(new ApiResult { Success = true, Data = result });
        }
        catch (ToolExecutionException ex)
        {
            return Results.Ok(new ApiResult { Success = false, Message = ex.Message });
        }
    }

    private static async Task<IResult> HandleChat(
        ChatRequest req,
        FoundryAgentRunner runner,
        ContentSafetyService safety)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return Results.BadRequest(new ApiResult { Success = false, Message = "Message is required." });

        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        var session = _chatSessions.GetOrAdd(sessionId, _ => new SessionContext
        {
            ThreadId = sessionId,
            IdentityVerified = false,
            VerifiedEmail = null
        });

        var response = await runner.RunAsync(session, req.Message);

        return Results.Ok(new ChatResponse
        {
            Reply = response.Message,
            SessionId = sessionId,
            Escalated = response.SuspendedForEscalation
        });
    }
}
