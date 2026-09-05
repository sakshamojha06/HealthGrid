# HealthGrid AI Service (Python)

A small, stateless FastAPI service with interpretable baseline models. It never
touches the database or user identity — the ASP.NET Core API aggregates
already-authorised, district/PHC-scoped features and calls these endpoints.

## Layout

```
app/
├── main.py            FastAPI app + router wiring
├── config.py          Settings (env: HEALTHGRID_AI_*)
├── security.py        Optional X-Api-Key gate
├── schemas.py         Pydantic wire contracts (snake_case, shared with the C# AiClient)
├── routers/           health, forecast, anomaly, redistribution
└── services/          forecasting, anomaly, redistribution — the models
tests/                 pytest smoke tests
```

## Run locally

```bash
python -m venv .venv
.venv/bin/pip install -r requirements.txt
.venv/bin/uvicorn app.main:app --reload --port 8000
```

Interactive docs at `http://localhost:8000/docs`.

## Endpoints

| Method & path | Model |
|---------------|-------|
| `POST /forecast/patient-volume` | Day-of-week seasonal baseline + damped linear trend; 95% interval from residuals. Returns `status = Ready \| InsufficientData`. |
| `POST /forecast/stockout` | Days-to-empty from average daily consumption; recommended reorder covers the horizon + safety stock. |
| `POST /anomaly/disease` | Robust rolling z-score (median / MAD) over a trailing window. |
| `POST /redistribution/recommend` | Greedy allocation from PHCs with the most usable surplus. |
| `GET /health/live`, `GET /health/ready` | Probes. |

## Testing

```bash
.venv/bin/pip install pytest
.venv/bin/pytest -q
```

## Notes

- Models are deliberately interpretable and data-efficient (NumPy only, no heavy
  ML dependencies) per `PLAN.md` section 9.
- Set `HEALTHGRID_AI_API_KEY` in production and configure the same value as
  `Ai:ApiKey` on the C# side (sent as `X-Api-Key`).
- Retraining / model registry is out of scope for this baseline; `model_version`
  is returned with every response to support versioning and rollback later.
