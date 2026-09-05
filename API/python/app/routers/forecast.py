from fastapi import APIRouter, Depends

from ..schemas import (
    PatientVolumeForecastRequest,
    PatientVolumeForecastResponse,
    StockoutForecastRequest,
    StockoutForecastResponse,
)
from ..security import require_api_key
from ..services.forecasting import forecast_patient_volume, forecast_stockout

router = APIRouter(prefix="/forecast", tags=["forecast"], dependencies=[Depends(require_api_key)])


@router.post("/patient-volume", response_model=PatientVolumeForecastResponse)
async def patient_volume(request: PatientVolumeForecastRequest) -> PatientVolumeForecastResponse:
    """Forecast daily patient volume over the requested horizon."""
    return forecast_patient_volume(request)


@router.post("/stockout", response_model=StockoutForecastResponse)
async def stockout(request: StockoutForecastRequest) -> StockoutForecastResponse:
    """Project days-to-stockout and a recommended reorder quantity per medicine/PHC."""
    return forecast_stockout(request)
