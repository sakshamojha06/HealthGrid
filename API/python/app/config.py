from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Runtime configuration, overridable via environment variables or a .env file."""

    model_config = SettingsConfigDict(env_prefix="HEALTHGRID_AI_", env_file=".env", extra="ignore")

    # Optional shared secret. When set, callers must send `X-Api-Key: <value>`.
    # The ASP.NET Core API is the only expected caller.
    api_key: str | None = None

    # Minimum non-zero observations before a forecast is considered trustworthy.
    min_history_points: int = 14

    # Default forecast horizon in days when the caller does not specify one.
    default_horizon: int = 7

    model_version: str = "fastapi-baseline-1"


@lru_cache
def get_settings() -> Settings:
    return Settings()
