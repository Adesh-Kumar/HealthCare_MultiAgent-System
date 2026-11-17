from uuid import uuid4
from pathlib import Path
from fastapi import Depends, FastAPI, HTTPException
from fastapi.responses import RedirectResponse
from fastapi.staticfiles import StaticFiles
from agents.workflow import workflow
from app.security import actor_context
from integrations.dotnet_api import DotNetApi
from schemas.models import Decision, SubmitRequest

app = FastAPI(
    title="Healthcare Customer Operations AI",
    version="0.1.0",
)
records: dict[str, dict] = {}
DEMO_DIR = Path(__file__).resolve().parent.parent / "demo"


@app.get("/", include_in_schema=False)
async def demo_ui():
    return RedirectResponse("/demo/")


app.mount("/demo", StaticFiles(directory=DEMO_DIR, html=True), name="demo")


@app.get("/health")
async def health():
    return {"status": "ok"}


@app.post("/api/v1/requests", status_code=202)
async def submit(payload: SubmitRequest, actor=Depends(actor_context)):
    request_id = str(uuid4())
    state = {
        "request_id": request_id,
        **actor,
        **payload.model_dump(mode="json"),
    }
    result = await workflow.ainvoke(state)

    can_approve = (
        result["review_result"]["passed"]
        and result["resolution"]["status"] == "ready_to_respond"
    )
    request_status = "awaiting_approval" if can_approve else "human_escalation"
    records[request_id] = {
        "request_id": request_id,
        "tenant_id": actor["tenant_id"],
        "status": request_status,
        "state": result,
        "audit": [],
    }
    return {"request_id": request_id, "status": request_status}


def get_owned_request(request_id: str, actor: dict):
    record = records.get(request_id)
    if not record or record["tenant_id"] != actor["tenant_id"]:
        raise HTTPException(status_code=404, detail="Request not found")
    return record


@app.get("/api/v1/requests/{request_id}")
async def get_status(request_id: str, actor=Depends(actor_context)):
    record = get_owned_request(request_id, actor)
    return {"request_id": request_id, "status": record["status"]}


@app.get("/api/v1/requests/{request_id}/evidence")
async def get_evidence(request_id: str, actor=Depends(actor_context)):
    state = get_owned_request(request_id, actor)["state"]
    return {
        "request": state.get("email_request"),
        "order": state.get("order_evidence"),
        "policies": state.get("policy_evidence", []),
        "shipment_lookup_error": state.get("shipment_lookup_error"),
        "policy_lookup_error": state.get("policy_lookup_error"),
        "resolution": state.get("resolution"),
    }


@app.get("/api/v1/requests/{request_id}/draft")
async def get_draft(request_id: str, actor=Depends(actor_context)):
    return get_owned_request(request_id, actor)["state"].get("email_draft")


@app.post("/api/v1/requests/{request_id}/approve")
async def approve(
    request_id: str,
    decision: Decision,
    actor=Depends(actor_context),
):
    record = get_owned_request(request_id, actor)
    if record["status"] != "awaiting_approval":
        raise HTTPException(
            status_code=409,
            detail="Request is not awaiting approval",
        )

    if not decision.approved:
        record["status"] = "rejected"
        record["audit"].append(
            {"action": "rejected", "actor": actor["actor_id"]}
        )
        return {"status": record["status"]}

    draft = record["state"]["email_draft"]
    send_result = await DotNetApi().send_approved_email(
        request_id,
        decision.edited_subject or draft["subject"],
        decision.edited_body or draft["body"],
        actor["tenant_id"],
        actor["actor_id"],
        decision.idempotency_key,
    )
    record["status"] = "sent"
    record["audit"].append(
        {
            "action": "approved_and_sent",
            "actor": actor["actor_id"],
            "result": send_result,
        }
    )
    return {"status": record["status"], "send_result": send_result}


@app.post("/api/v1/requests/{request_id}/reject")
async def reject(request_id: str, actor=Depends(actor_context)):
    record = get_owned_request(request_id, actor)
    record["status"] = "rejected"
    record["audit"].append(
        {"action": "rejected", "actor": actor["actor_id"]}
    )
    return {"status": record["status"]}


@app.post("/api/v1/requests/{request_id}/retry")
async def retry(request_id: str, actor=Depends(actor_context)):
    record = get_owned_request(request_id, actor)
    if record["status"] not in {"failed", "human_escalation"}:
        raise HTTPException(
            status_code=409,
            detail="Only failed or escalated requests may be retried",
        )

    result = await workflow.ainvoke(record["state"])
    record["state"] = result
    can_approve = (
        result["review_result"]["passed"]
        and result["resolution"]["status"] == "ready_to_respond"
    )
    record["status"] = "awaiting_approval" if can_approve else "human_escalation"
    return {"status": record["status"]}
