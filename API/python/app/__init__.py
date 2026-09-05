"""HealthGrid AI/ML service (FastAPI).

This service is deliberately independent of the ASP.NET Core API. The API owns
authentication, authorisation, district/PHC scope filtering, orchestration and
persistence. This service only ever receives already-authorised aggregate
features and returns interpretable predictions.
"""

__version__ = "0.1.0"
