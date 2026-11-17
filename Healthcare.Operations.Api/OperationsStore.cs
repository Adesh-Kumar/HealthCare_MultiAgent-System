using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Healthcare.Operations.Api;

public sealed record OrderRecord(
    string OrderId,
    string TenantId,
    string Status,
    int OrderedQuantity,
    int ShippedQuantity,
    int DeliveredQuantity,
    DateTimeOffset? ConfirmedRemainingEta,
    string SourceSystem,
    DateTimeOffset RetrievedAt
);

public sealed record PolicyRecord(
    string DocumentId,
    string TenantId,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Section,
    int Page,
    string Excerpt,
    bool Approved
);

public sealed record SendRecord(
    string RequestId,
    string TenantId,
    string Subject,
    string Body,
    string ActorId,
    string IdempotencyKey,
    DateTimeOffset SentAt
);

public sealed class OperationsStore
{
    private readonly ConcurrentDictionary<string, OrderRecord> _orders = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, SendRecord> _sends = new();
    private readonly ConcurrentDictionary<string, string> _idempotency = new();
    private readonly List<PolicyRecord> _policies =
    [
        new(
            "POL-DEL-001",
            "demo",
            "Partial Shipment Policy",
            "3.2",
            new(2026, 8, 1),
            "4.1",
            5,
            "When an order is partially shipped, operations verifies the "
                + "remaining quantity and communicates only a confirmed "
                + "delivery estimate.",
            true
        ),
        new(
            "POL-TRK-001", "demo", "Tracking and Delivery Status Policy", "1.0",
            new(2026, 9, 1), "1.2", 1,
            "Support may share the latest carrier tracking event and recorded shipment status. A carrier scan is not a guaranteed delivery date.", true
        ),
        new(
            "POL-DLY-001", "demo", "Delayed Delivery and ETA Policy", "1.0",
            new(2026, 9, 1), "3.1", 3,
            "For delayed shipments, verify fulfillment and carrier events. Communicate an arrival date only when the carrier or order system confirms it.", true
        ),
        new(
            "POL-NDR-001", "demo", "Delivered but Not Received SOP", "1.0",
            new(2026, 9, 1), "3.4", 4,
            "When tracking says delivered but the customer cannot locate the parcel, verify the delivery scan and escalate for delivery investigation. Do not state that the customer received it without confirmation.", true
        ),
        new(
            "POL-ADR-001", "demo", "Delivery Address Change Policy", "1.0",
            new(2026, 9, 1), "2.1", 2,
            "Address changes and shipment redirects require an operations agent to confirm the shipment has not passed the carrier change cutoff.", true
        ),
        new(
            "POL-SPL-001", "demo", "Split Shipment Policy", "1.0",
            new(2026, 9, 1), "4.2", 5,
            "For split shipments, confirm shipped and delivered quantities for each package and explain which order quantities remain open.", true
        ),
        new(
            "POL-RET-001", "demo", "Damaged or Incorrect Item Returns Policy", "1.0",
            new(2026, 9, 1), "5.1", 7,
            "For damaged or incorrect items, collect the affected item and condition details and route the case for returns review. Do not promise a refund or replacement before approval.", true
        ),
        new(
            "POL-CHG-001", "demo", "Shipping Charges Policy", "1.0",
            new(2026, 9, 1), "6.1", 8,
            "Shipping charge questions must be checked against the order invoice and selected service. Escalate when the charge breakdown is unavailable.", true
        ),
        new(
            "POL-CUS-001", "demo", "Customs and Import Duties Policy", "1.0",
            new(2026, 9, 1), "7.1", 9,
            "For cross-border shipments, confirm customs status and any duty or tax from the carrier or customs record before advising the customer.", true
        ),
        new(
            "POL-CAN-001", "demo", "Cancellation and Returns Policy", "1.0",
            new(2026, 9, 1), "5.3", 7,
            "Cancellation eligibility depends on fulfillment status. A return or refund request must be reviewed under the returns process; do not imply an action is complete until confirmed.", true
        ),
        new(
            "POL-INV-001", "demo", "Invoice and Billing Inquiry Policy", "1.0",
            new(2026, 9, 1), "6.3", 8,
            "Invoice inquiries should be answered from the billing record. Escalate if the invoice or charge details are not present in the order system.", true
        ),
        new(
            "POL-GEN-001", "demo", "General Shipment Support Policy", "1.0",
            new(2026, 9, 1), "1.1", 1,
            "Use verified order, shipment, and approved policy evidence. If a fact or operational action cannot be confirmed, explain the gap and route the request for human investigation.", true
        ),
        new(
            "POL-DEL-002",
            "demo",
            "Delivery Communications SOP",
            "2.1",
            new(2026, 6, 1),
            "2.3",
            2,
            "Do not provide an estimated delivery date unless it is "
                + "confirmed in the order or carrier system.",
            true
        ),
    ];

    public OperationsStore()
    {
        _orders["ORD-9001"] = new(
            "ORD-9001",
            "demo",
            "partially_fulfilled",
            50,
            30,
            30,
            null,
            "order-api-demo",
            DateTimeOffset.UtcNow
        );
        _orders["ORD-9002"] = new(
            "ORD-9002", "demo", "in_transit", 12, 12, 0, null,
            "order-api-demo", DateTimeOffset.UtcNow
        );
        _orders["ORD-9003"] = new(
            "ORD-9003", "demo", "delivered", 3, 3, 3, null,
            "order-api-demo", DateTimeOffset.UtcNow
        );
    }

    public OrderRecord? GetOrder(string id, string tenant)
    {
        return _orders.TryGetValue(id, out var order) && order.TenantId == tenant
            ? order
            : null;
    }

    public object? GetShipment(string id, string tenant)
    {
        var order = GetOrder(id, tenant);
        if (order is null) return null;
        return new
        {
            orderId = order.OrderId,
            status = order.Status,
            orderedQuantity = order.OrderedQuantity,
            shippedQuantity = order.ShippedQuantity,
            deliveredQuantity = order.DeliveredQuantity,
            confirmedRemainingEta = order.ConfirmedRemainingEta,
            tracking = $"Demo carrier scan for {order.OrderId}: shipment status is {order.Status}; tracking reference TRK-{order.OrderId[4..]}.",
            delivery = order.ConfirmedRemainingEta is null
                ? "No carrier-confirmed delivery date is available; the customer must be given only a verified estimate."
                : $"Confirmed remaining delivery estimate: {order.ConfirmedRemainingEta:yyyy-MM-dd}.",
            deliveryChange = "Address or delivery changes require a support agent to verify fulfillment status.",
            splitShipments = $"Order is partially fulfilled: {order.DeliveredQuantity} of {order.OrderedQuantity} units delivered; {Math.Max(order.OrderedQuantity - order.DeliveredQuantity, 0)} remain.",
            returns = "Report damaged or incorrect items to support for review under the returns policy.",
            charges = "Shipping charge details are not available in this demo order record.",
            customs = "Customs status is not available for this domestic demo shipment.",
            cancellation = "Cancellation eligibility must be confirmed by an operations agent.",
            invoice = "Invoice details are not connected in this demo.",
        };
    }

    public IReadOnlyList<PolicyRecord> SearchPolicies(string query, string tenant)
    {
        var ignoredTerms = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "with", "order", "please", "can", "you",
            "what", "when", "where", "from", "this", "that", "shipment",
            "shipments", "delivery", "customer", "received", "only", "item",
        };
        var synonyms = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["part"] = "partial", ["rest"] = "remaining", ["late"] = "delay",
            ["tracking"] = "track", ["damaged"] = "damage", ["refund"] = "return",
            ["tax"] = "customs", ["redirect"] = "address",
        };
        var terms = Regex.Matches(query.ToLowerInvariant(), "[a-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 2)
            .Where(term => !ignoredTerms.Contains(term))
            .Select(term => synonyms.GetValueOrDefault(term, term))
            .Distinct()
            .ToArray();

        var scored = _policies
            .Where(policy => policy.Approved && policy.TenantId == tenant)
            .Select(policy =>
            {
                var text = $"{policy.Title} {policy.Section} {policy.Excerpt}".ToLowerInvariant();
                var score = terms.Count(term => text.Contains(term, StringComparison.Ordinal));
                return (Policy: policy, Score: score);
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Policy.DocumentId)
            .Take(4)
            .Select(result => result.Policy)
            .ToList();

        if (scored.Count == 0 && query.Length > 0)
        {
            var general = _policies.First(policy => policy.DocumentId == "POL-GEN-001");
            if (general.TenantId == tenant && general.Approved) scored.Add(general);
        }

        return scored;
    }

    public (SendRecord? Record, bool Duplicate) Send(
        string requestId,
        string tenant,
        string actor,
        string subject,
        string body,
        string key
    )
    {
        if (_idempotency.TryGetValue(key, out var priorRequestId))
        {
            return _sends.TryGetValue(priorRequestId, out var priorRecord)
                ? (priorRecord, true)
                : (null, true);
        }

        var record = new SendRecord(
            requestId,
            tenant,
            subject,
            body,
            actor,
            key,
            DateTimeOffset.UtcNow
        );
        if (!_idempotency.TryAdd(key, requestId))
        {
            return Send(requestId, tenant, actor, subject, body, key);
        }

        _sends[requestId] = record;
        return (record, false);
    }
}
