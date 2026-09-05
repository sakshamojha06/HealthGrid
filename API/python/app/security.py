from fastapi import Header, HTTPException, status

from .config import get_settings


async def require_api_key(x_api_key: str | None = Header(default=None)) -> None:
    """Optional shared-secret gate.

    When ``HEALTHGRID_AI_API_KEY`` is configured, every request must carry a
    matching ``X-Api-Key`` header. When it is not configured (local dev), the
    check is a no-op.
    """
    expected = get_settings().api_key
    if expected and x_api_key != expected:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing API key.",
        )
