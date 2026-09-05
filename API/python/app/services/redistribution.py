"""Rule-based medicine redistribution recommendations.

Greedy allocation from the PHCs with the most usable surplus (on-hand minus
safety stock minus predicted 7-day demand), constrained to the requested
quantity. Same-district scoping is enforced by the API before this is called.
"""

from __future__ import annotations

from ..config import get_settings
from ..schemas import (
    RedistributionRecommendation,
    RedistributionRequest,
    RedistributionResponse,
)


def recommend_redistribution(request: RedistributionRequest) -> RedistributionResponse:
    settings = get_settings()

    scored = []
    for candidate in request.candidates:
        if candidate.phc_id == request.requesting_phc_id:
            continue
        usable = candidate.on_hand - candidate.safety_stock - candidate.predicted_demand_7d
        if usable <= 0:
            continue
        scored.append((usable, candidate))

    scored.sort(key=lambda pair: pair[0], reverse=True)

    remaining = request.quantity_needed
    recommendations: list[RedistributionRecommendation] = []
    for usable, candidate in scored:
        if remaining <= 0:
            break
        transfer = min(usable, remaining)
        remaining -= transfer
        recommendations.append(
            RedistributionRecommendation(
                phc_id=candidate.phc_id,
                available_surplus=round(usable, 2),
                predicted_demand_7d=round(candidate.predicted_demand_7d, 2),
                suggested_transfer_qty=round(transfer, 2),
            )
        )

    return RedistributionResponse(
        model_version=settings.model_version,
        recommendations=recommendations,
    )
