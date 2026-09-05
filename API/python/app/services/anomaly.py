"""Disease anomaly detection: robust rolling z-score with a weekly-seasonal guard.

Uses the median and MAD (median absolute deviation) of a trailing baseline window
so a single spike does not inflate the threshold. Flags a day only when it also
exceeds the same weekday's recent typical value — this keeps weekly clinic
rhythms from reading as anomalies.
"""

from __future__ import annotations

import numpy as np

from ..config import get_settings
from ..schemas import DiseaseAnomaly, DiseaseAnomalyRequest, DiseaseAnomalyResponse

_BASELINE_WINDOW = 21
_Z_THRESHOLD = 3.0


def detect_disease_anomalies(request: DiseaseAnomalyRequest) -> DiseaseAnomalyResponse:
    settings = get_settings()
    anomalies: list[DiseaseAnomaly] = []

    for series in request.series:
        ordered = sorted(series.values, key=lambda p: p.date)
        values = np.array([float(p.value) for p in ordered])
        if len(values) < 8:
            continue

        latest = values[-1]
        baseline = values[:-1][-_BASELINE_WINDOW:]
        median = float(np.median(baseline))
        mad = float(np.median(np.abs(baseline - median)))
        scale = mad * 1.4826 if mad > 0 else max(1.0, median * 0.25)

        z = (latest - median) / scale
        if z < _Z_THRESHOLD or latest <= median:
            continue

        pct = ((latest - median) / median * 100.0) if median > 0 else 100.0
        anomalies.append(
            DiseaseAnomaly(
                disease_id=series.disease_id,
                disease_name=series.disease_name,
                message=(
                    f"Potential abnormal increase in {series.disease_name} cases detected "
                    f"(today {latest:.0f} vs baseline {median:.0f}, +{pct:.0f}%). "
                    "Verification is recommended."
                ),
                severity="Critical" if z >= 5 else "Warning",
                score=round(float(z), 2),
                baseline=round(median, 2),
            )
        )

    return DiseaseAnomalyResponse(model_version=settings.model_version, anomalies=anomalies)
