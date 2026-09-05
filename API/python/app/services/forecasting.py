"""Interpretable, data-efficient forecasting baselines.

Patient volume: a day-of-week seasonal baseline plus a damped linear trend, with
a prediction interval from in-sample residuals. Stock-out: deterministic
projection of days-to-empty from average daily consumption.

These are intentionally simple and explainable — the dashboard presents their
output as decision support, never as an automated action.
"""

from __future__ import annotations

from datetime import date, timedelta

import numpy as np

from ..config import get_settings
from ..schemas import (
    ForecastPoint,
    PatientVolumeForecastRequest,
    PatientVolumeForecastResponse,
    StockoutForecastRequest,
    StockoutForecastResponse,
    StockoutRisk,
)

_Z = 1.96  # ~95% interval


def forecast_patient_volume(
    request: PatientVolumeForecastRequest,
) -> PatientVolumeForecastResponse:
    settings = get_settings()
    horizon = request.horizon or settings.default_horizon
    history = sorted(request.history, key=lambda p: p.date)

    non_zero = sum(1 for p in history if p.value > 0)
    if len(history) < 3:
        return PatientVolumeForecastResponse(
            model_version=settings.model_version,
            status="InsufficientData",
            points=[],
            pct_change=None,
        )

    dates = [p.date for p in history]
    values = np.array([float(p.value) for p in history])
    idx = np.arange(len(values), dtype=float)

    # Linear trend via least squares, then damp it so the forecast does not run away.
    slope, intercept = np.polyfit(idx, values, 1)
    damping = 0.6

    # Day-of-week seasonal offsets around the detrended mean.
    detrended = values - (slope * idx + intercept)
    dow = np.array([d.weekday() for d in dates])
    seasonal = np.zeros(7)
    for wd in range(7):
        mask = dow == wd
        seasonal[wd] = detrended[mask].mean() if mask.any() else 0.0

    fitted = slope * idx + intercept + seasonal[dow]
    residual_std = float(np.std(values - fitted, ddof=1)) if len(values) > 2 else max(1.0, values.mean() * 0.25)
    margin = _Z * max(residual_std, 1e-6)

    last_date = dates[-1]
    points: list[ForecastPoint] = []
    for step in range(1, horizon + 1):
        future_idx = len(values) - 1 + step
        trend = intercept + slope * (len(values) - 1) + slope * step * damping
        target = date_add(last_date, step)
        yhat = max(0.0, trend + seasonal[target.weekday()])
        points.append(
            ForecastPoint(
                date=target,
                yhat=round(yhat, 2),
                lower=round(max(0.0, yhat - margin), 2),
                upper=round(yhat + margin, 2),
            )
        )

    pct_change = _week_over_week_change(values)
    status = "Ready" if non_zero >= settings.min_history_points else "InsufficientData"

    return PatientVolumeForecastResponse(
        model_version=settings.model_version,
        status=status,
        points=points,
        pct_change=pct_change,
    )


def forecast_stockout(request: StockoutForecastRequest) -> StockoutForecastResponse:
    settings = get_settings()
    horizon = request.horizon
    today = date.today()
    risks: list[StockoutRisk] = []

    for item in request.items:
        if item.daily_consumption <= 0:
            risks.append(
                StockoutRisk(
                    medicine_id=item.medicine_id,
                    phc_id=item.phc_id,
                    days_to_stockout=None,
                    stockout_date=None,
                    risk="Info",
                    recommended_reorder_qty=0.0,
                )
            )
            continue

        days = int(np.floor(item.on_hand / item.daily_consumption))
        if days <= 3:
            risk = "Critical"
        elif days <= horizon:
            risk = "Warning"
        else:
            risk = "Info"

        reorder = max(
            0.0,
            round(item.daily_consumption * horizon + item.safety_stock - item.on_hand, 2),
        )
        risks.append(
            StockoutRisk(
                medicine_id=item.medicine_id,
                phc_id=item.phc_id,
                days_to_stockout=days,
                stockout_date=today + timedelta(days=days),
                risk=risk,
                recommended_reorder_qty=reorder,
            )
        )

    return StockoutForecastResponse(model_version=settings.model_version, risks=risks)


def _week_over_week_change(values: np.ndarray) -> float | None:
    if len(values) < 14:
        return None
    recent = values[-7:].mean()
    prior = values[-14:-7].mean()
    if prior <= 0:
        return None
    return round((recent - prior) / prior * 100.0, 1)


def date_add(value: date, days: int) -> date:
    return value + timedelta(days=days)
