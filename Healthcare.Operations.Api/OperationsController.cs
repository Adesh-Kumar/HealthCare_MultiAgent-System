using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Operations.Api;

[ApiController]
[Route("api/v1")]
public sealed class OperationsController(OperationsStore store) : ControllerBase
{
    private string Tenant => Request.Headers["X-Tenant-Id"].ToString();

    private string Actor => Request.Headers["X-Actor-Id"].ToString();

    [HttpGet("orders/{orderId}")]
    public IActionResult GetOrder(string orderId)
    {
        var order = store.GetOrder(orderId, Tenant);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(
            new
            {
                orderId = order.OrderId,
                status = order.Status,
                orderedQuantity = order.OrderedQuantity,
                shippedQuantity = order.ShippedQuantity,
                deliveredQuantity = order.DeliveredQuantity,
                remainingQuantity = Math.Max(
                    order.OrderedQuantity - order.DeliveredQuantity,
                    0
                ),
                confirmedRemainingEta = order.ConfirmedRemainingEta,
                sourceSystem = order.SourceSystem,
                retrievedAt = order.RetrievedAt,
            }
        );
    }

    [HttpGet("shipments/{orderId}")]
    public IActionResult GetShipment(string orderId, [FromQuery] string intent = "order_status")
    {
        var shipment = store.GetShipment(orderId, Tenant);
        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpGet("policies/search")]
    public IActionResult SearchPolicies([FromQuery] string q = "")
    {
        return Ok(new { items = store.SearchPolicies(q, Tenant) });
    }

    public sealed record ApprovedEmail(
        string RequestId,
        string Subject,
        string Body,
        string IdempotencyKey
    );

    [HttpPost("email/send-approved")]
    public IActionResult SendApproved(
        [FromBody] ApprovedEmail message,
        [FromHeader(Name = "Idempotency-Key")] string key
    )
    {
        if (message.IdempotencyKey != key || string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { error = "Matching idempotency key is required" });
        }

        var (record, duplicate) = store.Send(
            message.RequestId,
            Tenant,
            Actor,
            message.Subject,
            message.Body,
            key
        );
        return Ok(
            new
            {
                accepted = record is not null,
                duplicate,
                requestId = record?.RequestId,
                sentAt = record?.SentAt,
                channel = "local-demo-outbox",
            }
        );
    }
}
