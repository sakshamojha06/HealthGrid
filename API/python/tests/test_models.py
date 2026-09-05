"""Smoke tests for the baseline models. Run with: pytest (from API/python)."""

from datetime import date, timedelta

from app.schemas import (
    DiseaseAnomalyRequest,
    DiseaseSeries,
    PatientVolumeForecastRequest,
    Point,
    StockoutForecastRequest,
    StockoutItem,
)
from app.services.anomaly import detect_disease_anomalies
from app.services.forecasting import forecast_patient_volume, forecast_stockout


def _series(values: list[float]) -> list[Point]:
    start = date.today() - timedelta(days=len(values))
    return [Point(date=start + timedelta(days=i), value=v) for i, v in enumerate(values)]


def test_patient_volume_forecast_returns_horizon_points() -> None:
    history = _series([10, 12, 9, 11, 13, 8, 10] * 6)
    result = forecast_patient_volume(PatientVolumeForecastRequest(history=history, horizon=7))
    assert result.status == "Ready"
    assert len(result.points) == 7
    assert all(p.lower <= p.yhat <= p.upper for p in result.points)


def test_patient_volume_flags_insufficient_data() -> None:
    result = forecast_patient_volume(
        PatientVolumeForecastRequest(history=_series([1, 2]), horizon=7)
    )
    assert result.status == "InsufficientData"
    assert result.points == []


def test_stockout_projects_days_and_reorder() -> None:
    req = StockoutForecastRequest(
        items=[
            StockoutItem(
                medicine_id="m1", phc_id="p1", on_hand=20, safety_stock=10, daily_consumption=5
            )
        ],
        horizon=7,
    )
    risk = forecast_stockout(req).risks[0]
    assert risk.days_to_stockout == 4
    assert risk.risk == "Warning"
    assert risk.recommended_reorder_qty == 25.0


def test_disease_anomaly_detects_spike() -> None:
    values = [4, 5, 3, 4, 6, 5, 4, 5, 3, 4, 5, 4, 6, 4, 5, 4, 3, 5, 4, 6, 30]
    result = detect_disease_anomalies(
        DiseaseAnomalyRequest(
            series=[DiseaseSeries(disease_id="d1", disease_name="Dengue", values=_series(values))]
        )
    )
    assert len(result.anomalies) == 1
    assert result.anomalies[0].severity in {"Warning", "Critical"}
