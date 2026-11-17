# Shipment Customer Support Demo: Workflow Guide

This guide is a speaking aid for presenting the application. It describes the current demo implementation and distinguishes working workflow steps from simulated business data.

## One sentence overview

An operator submits a customer email, the Python service classifies the question and gathers shipment and policy evidence through the .NET API, then prepares a response for a human to review before it is placed in a local demo outbox.

## High-level flow

```mermaid
flowchart LR
    A[Operator enters customer email in ShipDesk UI] --> B[POST /api/v1/requests]
    B --> C[Classify intent and extract order reference]
    C --> D[GET .NET /api/v1/shipments/{orderId}]
    C --> E[GET .NET /api/v1/policies/search]
    D --> F[Resolve evidence and gaps]
    E --> F
    F --> G[Draft customer response]
    G --> H[Review draft]
    H --> I{Evidence ready and review passed?}
    I -->|Yes| J[awaiting_approval]
    I -->|No| K[human_escalation]
    J --> L[Operator inspects status, evidence, and draft]
    L --> M{Operator decision}
    M -->|Approve| N[POST /approve]
    N --> O[.NET email/send-approved]
    O --> P[Local demo outbox; status sent]
    M -->|Reject| Q[POST /reject; status rejected]
    K --> R[Investigate or retry]
    R --> S[POST /retry]
```

## Presentation walkthrough

1. Start the .NET API and Python FastAPI service using the commands in [README.md](README.md).
2. Open `http://localhost:8000/`. The browser page is the ShipDesk operator workbench. It has a sample email prefilled for order `ORD-9001`.
3. Choose one of the sample question buttons (for example **Tracking**, **Late delivery**, **Address change**, **Damaged item**, or **Return**) or edit the subject and body. Keep `ORD-9001` in the message so the demo order can be looked up.
4. Select **Submit email · run workflow**. The browser calls `POST /api/v1/requests` with the email. This endpoint runs the complete LangGraph workflow before responding with a request ID and status.
5. Explain the displayed intent, extracted reference, order/shipment data, policy passages, resolution, and draft. The UI reads those through the status, evidence, and draft endpoints after submit returns.
6. If the status is `awaiting_approval`, edit the draft if desired and choose **Approve and send demo email**. The approved text is passed to the .NET API and recorded in its in-memory local outbox. The demo does not deliver a real email.
7. If the status is `human_escalation`, explain that the current evidence is insufficient for a safe response. A retry re-runs the workflow; a human still needs to investigate unresolved cases.

## What the workflow does

### 1. Accept an email

`POST /api/v1/requests` accepts a channel, subject, and body. The tenant is taken from the authenticated request context, not the submitted JSON. The service creates a request ID and builds the initial workflow state.

### 2. Understand the customer question

The `understand_email` LangGraph node produces a structured request with an intent, order reference, customer questions, and confidence. When `OPENAI_API_KEY` is configured, a structured-output model performs extraction. Without it, a regex fallback recognizes common topics and order IDs.

The intent vocabulary includes order status, partial delivery, shipment delay, invoice, policy question, complaint, tracking, delivery ETA, delivered-but-not-received, address change, split shipment, damage, wrong item, shipping cost, customs, cancellation, return, and other.

### 3. Gather business evidence

After classification, two evidence lookups run as workflow branches:

- `GET /api/v1/shipments/{orderId}` on the .NET service retrieves the order quantities/status and the demo shipment fields.
- `GET /api/v1/policies/search?q=...` retrieves approved policy passages relevant to the email.

The Python service passes tenant and actor context to the .NET service. If the email has no usable order reference, the order lookup is skipped. Lookup errors are recorded as missing evidence rather than being treated as verified facts.

### 4. Resolve and draft

The `prepare_resolution` node calculates remaining quantity from ordered and delivered quantities, records known shipment details, and lists unresolved questions such as a missing confirmed ETA or missing policy result. The request is considered ready only when order evidence and at least one policy result exist.

The `draft_response` node asks the model to use only verified facts when a model is configured and evidence is ready. Otherwise it uses a conservative acknowledgement asking the team to verify details. The `review_response` node checks for dates in the draft that are not present in verified facts. The workflow always requires a human approval before sending.

### 5. Let an operator decide

After the workflow completes, the UI invokes:

| Route | Purpose |
|---|---|
| `GET /api/v1/requests/{id}` | Show current request status |
| `GET /api/v1/requests/{id}/evidence` | Show extracted request, shipment/order evidence, policy results, and resolution |
| `GET /api/v1/requests/{id}/draft` | Populate the editable response |
| `POST /api/v1/requests/{id}/approve` | Submit the operator-approved (possibly edited) email |
| `POST /api/v1/requests/{id}/reject` | Mark the request rejected |
| `POST /api/v1/requests/{id}/retry` | Re-run the workflow for an escalated request |

On approval, Python calls the .NET `POST /api/v1/email/send-approved` endpoint with an idempotency key. The .NET service writes to its local in-memory outbox and returns a simulated send result.

## What the demo supports today

The workflow can classify the shipment-related topics above and route questions through shared shipment and policy lookups. The .NET demo has a seeded tenant (`demo`), three orders (`ORD-9001` through `ORD-9003`), and approved example policies for shipment topics. `ORD-9001` is partially fulfilled (50 ordered, 30 delivered); `ORD-9002` and `ORD-9003` provide in-transit and delivered examples. Tracking, customs, returns, invoice, address-change, and charge descriptions are static illustrative fields, not live carrier, billing, warehouse, or returns data. The application does not execute address changes, cancellations, refunds, or returns.

The strongest live-through-the-demo scenario is a status or partial-delivery question about `ORD-9001`: the quantity evidence is calculated from the seeded order. Other topics show intent classification and the relevant mock evidence, while underscoring where a real integration or human investigation would be needed.

## Important demo boundaries

- Request state and the .NET sample store/outbox live in memory and reset when services restart.
- The email outbox is simulated; no external email provider is connected.
- `ORD-9001` through `ORD-9003` and their shipment facts are synthetic demonstration data.
- The intent selects which shipment field the resolution references, but all shipment attributes currently come from one demo shipment lookup endpoint.
- A production version needs real authenticated identity, durable request/audit storage, production business APIs, carrier and billing integrations, and operational controls appropriate to the organization.
