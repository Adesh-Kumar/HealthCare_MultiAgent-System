import httpx

from app.config import settings


def _headers(tenant_id: str, actor_id: str) -> dict[str, str]:
    return {
        "Authorization": f"Bearer {settings.api_bearer_token}",
        "X-Tenant-Id": tenant_id,
        "X-Actor-Id": actor_id,
    }


class DotNetApi:
    async def get_shipment(
        self, order_reference: str, intent: str, tenant_id: str, actor_id: str
    ):
        async with httpx.AsyncClient(
            base_url=settings.dotnet_api_base_url,
            timeout=settings.dotnet_api_timeout_seconds,
        ) as client:
            response = await client.get(
                f"/api/v1/shipments/{order_reference}",
                params={"intent": intent},
                headers=_headers(tenant_id, actor_id),
            )
            if response.status_code == 404:
                return None
            response.raise_for_status()
            return response.json()

    async def get_order(
        self, order_reference: str, tenant_id: str, actor_id: str
    ):
        async with httpx.AsyncClient(
            base_url=settings.dotnet_api_base_url,
            timeout=settings.dotnet_api_timeout_seconds,
        ) as client:
            response = await client.get(
                f"/api/v1/orders/{order_reference}",
                headers=_headers(tenant_id, actor_id),
            )
            if response.status_code == 404:
                return None
            response.raise_for_status()
            return response.json()

    async def search_policy(self, query: str, tenant_id: str, actor_id: str):
        async with httpx.AsyncClient(
            base_url=settings.dotnet_api_base_url,
            timeout=settings.dotnet_api_timeout_seconds,
        ) as client:
            response = await client.get(
                "/api/v1/policies/search",
                params={"q": query},
                headers=_headers(tenant_id, actor_id),
            )
            response.raise_for_status()
            return response.json()

    async def send_approved_email(
        self,
        request_id: str,
        subject: str,
        body: str,
        tenant_id: str,
        actor_id: str,
        idempotency_key: str,
    ):
        async with httpx.AsyncClient(
            base_url=settings.dotnet_api_base_url,
            timeout=settings.dotnet_api_timeout_seconds,
        ) as client:
            response = await client.post(
                "/api/v1/email/send-approved",
                json={
                    "requestId": request_id,
                    "subject": subject,
                    "body": body,
                    "idempotencyKey": idempotency_key,
                },
                headers={
                    **_headers(tenant_id, actor_id),
                    "Idempotency-Key": idempotency_key,
                },
            )
            response.raise_for_status()
            return response.json()
