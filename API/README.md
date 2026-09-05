# HealthGrid Backend

The backend is split into two independently deployable services, one per folder:

| Folder                | Stack                          | Responsibility |
|-----------------------|--------------------------------|----------------|
| [`csharp/`](./csharp) | ASP.NET Core 10 Web API + SignalR + EF Core (SQL Server) | System of record. Authentication, role + district/PHC authorisation, all business rules, persistence, real-time notifications, and orchestration of the AI service. |
| [`python/`](./python) | FastAPI + NumPy                 | Stateless AI/ML service. Interpretable forecasting, disease-anomaly detection, and redistribution recommendations. Receives only already-authorised aggregate features. |

The C# API is the only client of the Python service and is the only public
boundary. If the Python service is unavailable, the C# API falls back to an
equivalent local baseline (`Services/ForecastingMath.cs`) so the product keeps working.

```
Angular UI ──► ASP.NET Core API ──► SQL Server
                     │
                     └──► FastAPI AI service (aggregate features only)
```

## Controllers (C#)

Each area of work has its own controller under `csharp/HealthGrid.Api/Controllers/`:

| Controller | Routes |
|------------|--------|
| `AuthController` | `POST /api/auth/login\|refresh\|logout`, `GET /api/auth/me` |
| `DistrictsController` | `GET/POST/PUT/DELETE /api/districts` |
| `PhcsController` | `GET/POST/PUT/DELETE /api/phcs` |
| `MedicinesController` | `GET/POST/PUT /api/medicines` |
| `DiseasesController` | `GET/POST/PUT /api/diseases` |
| `SpecializationsController` | `GET/POST /api/specializations` |
| `UsersController` | `GET/POST/PUT /api/users` |
| `PatientVisitsController` | `GET /api/patient-visits`, `GET /api/patient-visits/{id}`, `POST /api/patient-visits` |
| `AnalyticsController` | `GET /api/analytics/patient-volume\|diseases` |
| `InventoryController` | `GET /api/inventory`, `GET /api/inventory/transactions`, `POST /api/inventory/receipts\|adjustments\|issues` |
| `MedicineRequestsController` | `GET/POST /api/medicine-requests`, `.../{id}`, `.../{id}/accept\|reject\|fulfill\|complete`, `.../recommended-sources` |
| `DoctorsController` | `GET /api/doctors/search`, `GET/POST/PUT/DELETE /api/doctors/{id}`, `PUT /api/doctors/{id}/availability` |
| `NotificationsController` | `GET /api/notifications`, `POST /api/notifications/{id}/acknowledge` |
| `DashboardController` | `GET /api/dashboard` |
| `AiController` | `GET /api/ai/predictions\|alerts`, `POST /api/ai/alerts/{id}/acknowledge`, `POST /api/ai/run` |
| `HealthController` | `GET /api/health/live` (plus `GET /api/health/ready` from health checks) |

Real-time hub: `NotificationsHub` at `/hubs/notifications` (events: `notification`,
`medicineRequest`, `aiAlert`).

## Quick start

```bash
# 1. SQL Server (Docker) — set a strong SA password
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_strong_Passw0rd" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# 2. AI service
cd python && python -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/uvicorn app.main:app --port 8000

# 3. API (creates + seeds the database on first run in Development)
cd ../csharp && dotnet run --project HealthGrid.Api      # http://localhost:5030
```

The Angular dev server proxies `/api` and `/hubs` to `http://localhost:5030`
(`UI/proxy.conf.json`), so no CORS configuration is needed for local development.

### Seeded demo accounts (password `HealthGrid!12345`)

| Email | Role |
|-------|------|
| `admin@healthgrid.local` | System Administrator |
| `ranchi.admin@healthgrid.local` | District Administrator (Ranchi) |
| `ranchi.phc@healthgrid.local` | PHC Administrator (Ranchi Central) |
| `ranchi.doctor@healthgrid.local` | Doctor (Ranchi Central) |
| `bokaro.staff@healthgrid.local` | PHC Staff (Bokaro Central) |

## District isolation

Every operational row carries `DistrictId` + `PhcId`. `ICurrentUser` resolves the
caller's scope from JWT claims (never from the request body), and every service
filters on that scope before materialising results. Out-of-scope resources return
`404` to avoid leaking their existence. See `PLAN.md` sections 3–4.
