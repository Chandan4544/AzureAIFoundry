namespace Group3RetailEcommercePrjct.Core;

public sealed class ToolExecutionException(string message) : Exception(message);

public sealed class CommerceTools(SimulatedBackendStore store)
{
    public object OrderStatusTool(string orderId, string customerEmail)
    {
        var order = GetAuthorizedOrder(orderId, customerEmail);

        return new
        {
            order_id = order.OrderId,
            status = order.Status,
            carrier = order.Carrier,
            eta = order.EstimatedDeliveryDate
        };
    }

    public object ReturnInitiationTool(string orderId, string customerEmail)
    {
        var order = GetAuthorizedOrder(orderId, customerEmail);
        if (order.DeliveredDate is null)
        {
            throw new ToolExecutionException("Return not allowed: order is not yet delivered.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var deliveryAge = today.DayNumber - order.DeliveredDate.Value.DayNumber;
        if (deliveryAge > 30)
        {
            throw new ToolExecutionException("Return not allowed: return window exceeded 30 days.");
        }

        var returnId = $"RET-{Random.Shared.Next(1000, 9999)}";
        var created = new ReturnRecord
        {
            ReturnId = returnId,
            OrderId = order.OrderId,
            CustomerEmail = customerEmail,
            Stage = "Initiated",
            ExpectedCompletionDate = today.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow
        };

        store.Returns[created.ReturnId] = created;
        return new
        {
            return_id = created.ReturnId,
            stage = created.Stage,
            expected_completion_date = created.ExpectedCompletionDate
        };
    }

    public object DeliveryReschedulingTool(string orderId, string customerEmail, DateOnly newDeliveryDate)
    {
        var order = GetAuthorizedOrder(orderId, customerEmail);
        if (order.DeliveredDate is not null || string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolExecutionException("Reschedule not allowed: order already delivered.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (newDeliveryDate <= today)
        {
            throw new ToolExecutionException("Reschedule date must be in the future.");
        }

        order.EstimatedDeliveryDate = newDeliveryDate;
        return new
        {
            order_id = order.OrderId,
            new_delivery_date = order.EstimatedDeliveryDate,
            confirmation = "Delivery schedule updated"
        };
    }

    public object RefundStatusTool(string returnId, string customerEmail)
    {
        if (!store.Returns.TryGetValue(returnId, out var ret))
        {
            throw new ToolExecutionException("Return ID not found.");
        }

        if (!string.Equals(ret.CustomerEmail, customerEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolExecutionException("Unauthorized: customer email does not match return record.");
        }

        return new
        {
            return_id = ret.ReturnId,
            stage = ret.Stage,
            expected_completion_date = ret.ExpectedCompletionDate
        };
    }

    private OrderRecord GetAuthorizedOrder(string orderId, string customerEmail)
    {
        if (!store.Orders.TryGetValue(orderId, out var order))
        {
            throw new ToolExecutionException("Order not found.");
        }

        if (!string.Equals(order.CustomerEmail, customerEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolExecutionException("Unauthorized: customer email does not match order record.");
        }

        return order;
    }
}
