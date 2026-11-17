import secrets
from fastapi import Header, HTTPException

from app.config import settings


async def actor_context(
    authorization: str | None = Header(default=None),
    x_tenant_id: str | None = Header(default=None),
):
    scheme, _, token = (authorization or "").partition(" ")
    if scheme.lower() != "bearer" or not secrets.compare_digest(
        token, settings.api_bearer_token
    ):
        raise HTTPException(
            status_code=401,
            detail="Valid bearer authentication is required",
        )
    if not x_tenant_id:
        raise HTTPException(
            status_code=400,
            detail="X-Tenant-Id is required",
        )
    return {"tenant_id": x_tenant_id, "actor_id": "authenticated-operator"}
