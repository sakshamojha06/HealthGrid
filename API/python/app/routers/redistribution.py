from fastapi import APIRouter, Depends

from ..schemas import RedistributionRequest, RedistributionResponse
from ..security import require_api_key
from ..services.redistribution import recommend_redistribution

router = APIRouter(
    prefix="/redistribution", tags=["redistribution"], dependencies=[Depends(require_api_key)]
)


@router.post("/recommend", response_model=RedistributionResponse)
async def recommend(request: RedistributionRequest) -> RedistributionResponse:
    """Suggest which same-district PHCs should transfer stock, and how much."""
    return recommend_redistribution(request)
