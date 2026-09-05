from datetime import datetime, timezone

from fastapi import APIRouter

from .. import __version__

router = APIRouter(tags=["health"])


@router.get("/health/live")
async def live() -> dict:
    return {"status": "live", "version": __version__, "time_utc": datetime.now(timezone.utc)}


@router.get("/health/ready")
async def ready() -> dict:
    # The models are stateless and in-process, so readiness == liveness here.
    return {"status": "ready"}
