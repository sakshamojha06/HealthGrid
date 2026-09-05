from fastapi import FastAPI

from . import __version__
from .routers import anomaly, forecast, health, redistribution

app = FastAPI(
    title="HealthGrid AI Service",
    version=__version__,
    description=(
        "Interpretable forecasting, anomaly detection and redistribution baselines "
        "for HealthGrid. Receives only authorised aggregate features from the "
        "ASP.NET Core API."
    ),
)

app.include_router(health.router)
app.include_router(forecast.router)
app.include_router(anomaly.router)
app.include_router(redistribution.router)


@app.get("/", tags=["health"])
async def root() -> dict:
    return {"service": "healthgrid-ai", "version": __version__, "docs": "/docs"}
