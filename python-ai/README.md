# Healthcare Customer AI

FastAPI + LangGraph vertical slice for customer operations. The service extracts a request, investigates approved business APIs, prepares a grounded draft, reviews it, and holds for human approval. It never sends mail until approval and delegates sending to the .NET API.

## Run locally

```powershell
cd python-ai
py -m venv .venv
.venv\Scripts\Activate.ps1
pip install -e .
Copy-Item .env.example .env
uvicorn app.main:app --reload --port 8000
```

## Debug both applications in VS Code

Open the workspace root in VS Code. Install the Python extension and C# Dev Kit, select `python-ai\.venv\Scripts\python.exe` as the Python interpreter, and set breakpoints in the Python and .NET source files.

1. **Start the .NET API under the debugger.** In Run and Debug (`Ctrl+Shift+D`), select the `Healthcare.Operations.Api` project and its `http` launch profile, then press `F5`. It listens on `http://localhost:5185`.

   The equivalent terminal command is:

   ```powershell
   dotnet run --project .\Healthcare.Operations.Api --configuration Debug --launch-profile http
   ```

   This command builds and runs the app in Debug configuration, but does not attach a debugger. Use `F5` in VS Code to hit .NET breakpoints.

2. **Start the Python API under the debugger.** In Run and Debug, select **Python: FastAPI** and press `F5`. This configuration is in the workspace root's `.vscode\launch.json`; it starts Uvicorn on port `8000`, loads `python-ai\.env`, and sets `DOTNET_API_BASE_URL` to `http://localhost:5185`.

   To run Python from a terminal without attaching the debugger:

   ```powershell
   Set-Location .\python-ai
   .\.venv\Scripts\Activate.ps1
   uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
   ```

3. **Test the complete workflow.** Open `http://localhost:8000/`, submit an email in the demo UI, and step through the Python and .NET breakpoints. Start the .NET API before submitting a request from the UI.

Set `OPENAI_API_KEY` to enable model-backed extraction and drafting. Without it, the workflow uses a conservative deterministic fallback and reports an escalation when operational evidence is unavailable. Local request storage is in-memory; configure a durable LangGraph checkpointer before production.

Start the .NET API before submitting an email. The Python service calls it for shipment and policy evidence; when it is stopped or its base URL/token does not match, the UI displays the failed lookup and setup hint. The demo includes orders `ORD-9001`, `ORD-9002`, and `ORD-9003` plus approved sample policies for common shipment questions.

Open `http://localhost:8000/` for the shipment support demo workbench. It includes an editable sample email, one-click sample shipment questions, workflow/evidence display, and operator approve/reject/retry actions. The page calls the same authenticated FastAPI routes as an integrating client; enter the bearer token and tenant shown in the local setup. The .NET demo exposes a shipment lookup with illustrative tracking, returns, customs, invoice, and delivery-change evidence for the seeded `ORD-9001` order. These values are mock presentation data, not live carrier or billing integrations.

All API routes require `Authorization: Bearer <API_BEARER_TOKEN>`. Tenant identity is read from `X-Tenant-Id` after authentication; the body cannot set the tenant.
