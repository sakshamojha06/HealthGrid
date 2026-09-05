"""Wire contracts shared with the ASP.NET Core ``AiClient``.

The .NET client serialises with a snake_case naming policy, so these field names
must stay snake_case. Dates cross the wire as ISO ``YYYY-MM-DD`` strings.
"""

from __future__ import annotations

from datetime import date

from pydantic import BaseModel, Field


class Point(BaseModel):
    date: date
    value: float


class ForecastPoint(BaseModel):
    date: date
    yhat: float
    lower: float
    upper: float


# -- Patient volume --------------------------------------------------------
class PatientVolumeForecastRequest(BaseModel):
    history: list[Point]
    horizon: int = Field(default=7, ge=1, le=60)


class PatientVolumeForecastResponse(BaseModel):
    model_version: str
    status: str  # Ready | InsufficientData | Stale
    points: list[ForecastPoint]
    pct_change: float | None = None


# -- Stock-out risk -------------------------------------------------------
class StockoutItem(BaseModel):
    medicine_id: str
    phc_id: str
    on_hand: float
    safety_stock: float
    daily_consumption: float


class StockoutForecastRequest(BaseModel):
    items: list[StockoutItem]
    horizon: int = Field(default=7, ge=1, le=90)


class StockoutRisk(BaseModel):
    medicine_id: str
    phc_id: str
    days_to_stockout: int | None
    stockout_date: date | None
    risk: str  # Info | Warning | Critical
    recommended_reorder_qty: float


class StockoutForecastResponse(BaseModel):
    model_version: str
    risks: list[StockoutRisk]


# -- Disease anomaly ----------------------------------------------------
class DiseaseSeries(BaseModel):
    disease_id: str
    disease_name: str
    values: list[Point]


class DiseaseAnomalyRequest(BaseModel):
    series: list[DiseaseSeries]


class DiseaseAnomaly(BaseModel):
    disease_id: str
    disease_name: str
    message: str
    severity: str  # Info | Warning | Critical
    score: float
    baseline: float


class DiseaseAnomalyResponse(BaseModel):
    model_version: str
    anomalies: list[DiseaseAnomaly]


# -- Redistribution ---------------------------------------------------
class RedistributionCandidate(BaseModel):
    phc_id: str
    on_hand: float
    safety_stock: float
    predicted_demand_7d: float


class RedistributionRequest(BaseModel):
    medicine_id: str
    requesting_phc_id: str
    quantity_needed: float
    candidates: list[RedistributionCandidate]


class RedistributionRecommendation(BaseModel):
    phc_id: str
    available_surplus: float
    predicted_demand_7d: float
    suggested_transfer_qty: float


class RedistributionResponse(BaseModel):
    model_version: str
    recommendations: list[RedistributionRecommendation]
