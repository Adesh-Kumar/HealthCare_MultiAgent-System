# Healthcare Customer Operations AI

Two-service starter built from the attached blueprint:

- `python-ai/`: FastAPI request API and LangGraph investigation/drafting workflow.
- `Healthcare.Operations.Api/`: ASP.NET Core API for tenant-scoped order and approved policy reads plus an idempotent demo email outbox.

The FastAPI service serves a demo customer-operations workbench at `http://localhost:8000/`. Submit the prefilled email or choose a shipment question example, then review the classified intent, shipment/policy evidence, and response draft. Approving sends only to the local demo outbox. Shipment detail fields beyond the seeded order quantities are illustrative mock values.

The local .NET store contains three synthetic orders (`ORD-9001` through `ORD-9003`) and approved sample shipment policies for tenant `demo`. It is a development adapter, not a production database or actual email transport. Replace it with the existing .NET business APIs and persistence before production. The Python workflow stores state in memory; production should use PostgreSQL checkpoints and durable request/audit storage.

## Local run

Start the .NET API:

```powershell
dotnet run --project Healthcare.Operations.Api --urls http://127.0.0.1:5080
```

Start Python in another terminal:

```powershell
cd python-ai
py -m venv .venv
.venv\Scripts\Activate.ps1
pip install -e .
Copy-Item .env.example .env
uvicorn app.main:app --reload --port 8000
```

Submit a request with `Authorization: Bearer local-dev-token` and `X-Tenant-Id: demo`. Configure `OPENAI_API_KEY` to use structured model extraction and drafting. Without it, a conservative fallback is used; it does not fabricate business evidence. API docs are available at `/docs` in development.

## Safety boundary

The Python request body cannot assign its tenant. Python requires authenticated bearer token and tenant header; .NET requires a service bearer token plus tenant and actor context. Order/policy records are tenant filtered. Outbound delivery is reachable only through the human approval route and requires an idempotency key. Local auth is a shared development token and must be replaced with enterprise identity, trusted claims propagation, durable audit, and least-privilege service credentials before deployment.
