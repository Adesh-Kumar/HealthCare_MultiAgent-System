from datetime import datetime, timezone
from typing import Literal

from pydantic import BaseModel, Field


class CustomerRequest(BaseModel):
    intent: Literal[
        "order_status",
        "partial_delivery",
        "shipment_delay",
        "invoice_query",
        "policy_question",
        "complaint",
        "tracking_status",
        "delivery_eta",
        "delivered_not_received",
        "address_change",
        "split_shipment",
        "damaged_item",
        "wrong_item",
        "shipping_cost",
        "customs",
        "cancellation",
        "return_request",
        "other",
    ] = "other"
    customer_reference: str | None = None
    order_reference: str | None = None
    invoice_reference: str | None = None
    product_reference: str | None = None
    requested_quantities: list[str] = Field(default_factory=list)
    questions: list[str] = Field(default_factory=list)
    missing_information: list[str] = Field(default_factory=list)
    confidence: float = Field(default=0.3, ge=0, le=1)


class SubmitRequest(BaseModel):
    channel: Literal["email", "operations"] = "email"
    subject: str = Field(min_length=1, max_length=300)
    body: str = Field(min_length=1, max_length=20000)
    received_at: datetime = Field(
        default_factory=lambda: datetime.now(timezone.utc)
    )


class Decision(BaseModel):
    approved: bool
    edited_subject: str | None = None
    edited_body: str | None = None
    idempotency_key: str = Field(min_length=8, max_length=128)
