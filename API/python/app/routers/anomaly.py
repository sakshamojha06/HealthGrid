from fastapi import APIRouter, Depends

from ..schemas import DiseaseAnomalyRequest, DiseaseAnomalyResponse
from ..security import require_api_key
from ..services.anomaly import detect_disease_anomalies

router = APIRouter(prefix="/anomaly", tags=["anomaly"], dependencies=[Depends(require_api_key)])


@router.post("/disease", response_model=DiseaseAnomalyResponse)
async def disease(request: DiseaseAnomalyRequest) -> DiseaseAnomalyResponse:
    """Flag diseases whose latest case count breaks from their recent baseline."""
    return detect_disease_anomalies(request)
