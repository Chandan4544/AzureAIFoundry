using System.Text.Json;

namespace Group3RetailEcommercePrjct.Core;

public static class ToolSchemas
{
    public static object GetAll()
    {
        return new
        {
            OrderStatusTool = new
            {
                type = "object",
                required = new[] { "order_id", "customer_email" },
                properties = new
                {
                    order_id = new { type = "string", pattern = "^ORD-[0-9]{4}$" },
                    customer_email = new { type = "string", format = "email" }
                },
                returns = new
                {
                    order_id = "string",
                    status = "string",
                    carrier = "string",
                    eta = "date"
                }
            },
            ReturnInitiationTool = new
            {
                type = "object",
                required = new[] { "order_id", "customer_email" },
                properties = new
                {
                    order_id = new { type = "string", pattern = "^ORD-[0-9]{4}$" },
                    customer_email = new { type = "string", format = "email" }
                },
                returns = new
                {
                    return_id = "string",
                    stage = "string",
                    expected_completion_date = "date"
                }
            },
            DeliveryReschedulingTool = new
            {
                type = "object",
                required = new[] { "order_id", "new_delivery_date", "customer_email" },
                properties = new
                {
                    order_id = new { type = "string", pattern = "^ORD-[0-9]{4}$" },
                    customer_email = new { type = "string", format = "email" },
                    new_delivery_date = new { type = "string", format = "date" }
                },
                returns = new
                {
                    order_id = "string",
                    new_delivery_date = "date",
                    confirmation = "string"
                }
            },
            RefundStatusTool = new
            {
                type = "object",
                required = new[] { "return_id", "customer_email" },
                properties = new
                {
                    return_id = new { type = "string", pattern = "^RET-[0-9]{4}$" },
                    customer_email = new { type = "string", format = "email" }
                },
                returns = new
                {
                    return_id = "string",
                    stage = "string",
                    expected_completion_date = "date"
                }
            }
        };
    }

    public static void WriteJson(string outputPath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(GetAll(), options));
    }
}
