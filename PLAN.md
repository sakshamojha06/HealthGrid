# HealthGrid Implementation Plan

## 1. Product Scope

HealthGrid is an internal operations platform for Primary Health Centres (PHCs) and healthcare administrators across Jharkhand. It will manage:

- Districts and PHCs
- Authenticated users and role-based access
- Patient visits and diagnoses
- Disease trends and analytics
- Medicine inventory and stock transactions
- Same-district PHC medicine transfers
- Doctors, specialists, and availability
- Real-time operational notifications
- AI-assisted forecasting, anomaly detection, and redistribution recommendations

Initial districts:

- Ranchi
- Bokaro
- Dhanbad
- Deoghar
- Chatra

The application is not public-facing. All patient, doctor, medicine, inventory, PHC, analytics, and AI data requires authentication.

## 2. Architecture

```text
Angular + TypeScript + Bootstrap
              |
              v
ASP.NET Core Web API + SignalR
              |
              +--> Entity Framework Core --> SQL Server
              |
              +--> FastAPI AI/ML Service
```

The repository will use a modular monorepo structure:

```text
HealthGrid/
├── API/
│   ├── src/
│   │   ├── HealthGrid.Api/
│   │   ├── HealthGrid.Application/
│   │   ├── HealthGrid.Domain/
│   │   └── HealthGrid.Infrastructure/
│   └── tests/
├── UI/
├── AI/
├── infra/
├── docs/
└── docker-compose.yml
```

### Backend layers

- `Domain`: entities, enums, value objects, and business invariants.
- `Application`: use cases, DTOs, validators, authorization contracts, interfaces, and mapping.
- `Infrastructure`: EF Core, SQL Server, Identity, repositories, SignalR adapters, AI client, and persistence.
- `Api`: controllers, middleware, authentication setup, exception handling, OpenAPI, health checks, and hubs.

The API is the only system-of-record boundary. Controllers must not expose EF entities directly or mutate inventory without going through application services.

### Angular structure

```text
UI/src/app/
├── core/
│   ├── auth/
│   ├── guards/
│   ├── interceptors/
│   ├── services/
│   └── signalr/
├── shared/
├── layout/
└── features/
    ├── dashboard/
    ├── patients/
    ├── diseases/
    ├── inventory/
    ├── medicine-requests/
    ├── doctors/
    ├── alerts/
    └── administration/
```

## 3. District and PHC Data Isolation

District isolation is the highest-priority security requirement.

Every PHC-owned operational entity must include both `DistrictId` and `PhcId`, including:

- Patient visits
- Diagnoses and prescriptions
- Medicine inventories
- Inventory transactions
- Medicine requests and transfers
- Doctors and availability
- Analytics snapshots
- AI predictions and alerts
- Notifications
- Audit records where applicable

The backend must:

1. Resolve district and PHC scope from the authenticated user.
2. Apply scope predicates before materializing database results.
3. Validate route IDs, query filters, and request bodies against that scope.
4. Validate related entities, such as transfer source and destination PHCs.
5. Return no Bokaro records to a Ranchi user, even if the request is manually modified.
6. Return `404` for out-of-scope resources where appropriate to avoid leaking existence.
7. Apply the same authorization rules to REST APIs, SignalR connections, background jobs, and AI data aggregation.

Cross-district medicine exchange is disabled by default. Any future exception requires a dedicated policy, elevated authorization, and an audit event.

## 4. Roles and Authorization

Roles:

- **System Administrator**: system-wide configuration, users, roles, districts, PHCs, and reference data.
- **District Administrator**: manage and analyze authorized district data; patient-level access must be explicitly approved by policy.
- **PHC Administrator**: manage one PHC's staff, doctors, inventory, and operational records.
- **Doctor**: view authorized patient visits and record clinical operational information for the assigned PHC.
- **PHC Staff**: record visits, manage permitted inventory workflows, and handle assigned operational tasks.

Use ASP.NET Core Identity for password hashing and user management. Use JWT access tokens with refresh-token rotation.

JWT claims should contain:

- `sub`: user ID
- Role claims
- District ID
- PHC ID
- Security-stamp or token-version claim

Validate token signature, issuer, audience, expiry, security stamp, role, district membership, and PHC membership on every protected request. Do not trust district or PHC values supplied by the client.

Implement a scoped authorization service used by every application use case. Add integration tests proving that manipulated route IDs, body IDs, and query parameters cannot cross district boundaries.

## 5. Database Design

Core tables:

- `Districts`
- `Phcs`
- `ApplicationUsers`
- `Roles`
- `UserPhcMemberships`
- `Medicines`
- `Diseases`
- `Specializations`
- `Doctors`
- `DoctorAvailabilities`
- `Patients`
- `PatientVisits`
- `VisitDiagnoses`
- `Prescriptions`
- `PrescriptionItems`
- `MedicineInventories`
- `InventoryTransactions`
- `MedicineRequests`
- `MedicineRequestItems`
- `MedicineTransfers`
- `Notifications`
- `PredictionSnapshots`
- `PredictionValues`
- `AiAlerts`
- `ModelVersions`
- `AuditLogs`

Use GUID primary keys, UTC timestamps, foreign keys, unique constraints, check constraints, and optimistic concurrency tokens. Add indexes for:

- `(DistrictId, PhcId, CreatedAt)`
- Visit date and diagnosis
- Medicine and PHC inventory lookups
- Inventory transaction time
- Request status and destination PHC
- Alert severity and acknowledgement status

Patient information must follow data minimization. Start with a PHC-local pseudonymous patient identifier and only collect fields required for operations, such as age or age band, gender where justified, visit date, diagnosis, symptoms, prescription, and attending healthcare worker.

## 6. API Surface

Authentication:

- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

Administration:

- `GET/POST/PUT/DELETE /api/districts`
- `GET/POST/PUT/DELETE /api/phcs`
- `GET/POST/PUT/DELETE /api/users`
- `GET/POST/PUT/DELETE /api/medicines`
- `GET/POST/PUT/DELETE /api/diseases`
- `GET/POST/PUT/DELETE /api/specializations`

Patients and disease data:

- `GET /api/patients`
- `POST /api/patient-visits`
- `GET /api/patient-visits/{id}`
- `PUT /api/patient-visits/{id}`
- `GET /api/analytics/diseases`
- `GET /api/analytics/patient-volume`

Inventory:

- `GET /api/inventory`
- `GET /api/inventory/{medicineId}`
- `POST /api/inventory/receipts`
- `POST /api/inventory/issues`
- `POST /api/inventory/adjustments`
- `GET /api/inventory/transactions`

Medicine exchange:

- `GET /api/medicine-requests`
- `POST /api/medicine-requests`
- `GET /api/medicine-requests/{id}`
- `POST /api/medicine-requests/{id}/accept`
- `POST /api/medicine-requests/{id}/reject`
- `POST /api/medicine-requests/{id}/fulfill`
- `POST /api/medicine-requests/{id}/complete`
- `GET /api/medicine-requests/recommended-sources`

Doctors and notifications:

- `GET /api/doctors/search`
- `POST/PUT/DELETE /api/doctors`
- `GET/POST/PUT/DELETE /api/doctors/{id}/availability`
- `GET /api/notifications`
- `POST /api/notifications/{id}/acknowledge`

AI and dashboard:

- `GET /api/dashboard`
- `GET /api/ai/predictions`
- `GET /api/ai/alerts`
- `POST /api/ai/alerts/{id}/acknowledge`

All endpoints require authentication except login and token refresh. Use DTOs, validation, pagination, bounded date ranges, consistent problem responses, correlation IDs, and audit logging.

## 7. Inventory and Transfer Rules

Inventory changes must be centralized in an application service and recorded in an immutable ledger.

Transaction types:

- Receipt
- Patient issue
- Adjustment
- Transfer out
- Transfer in

Use database transactions, concurrency tokens or appropriate SQL locking, positive-quantity validation, non-negative stock constraints, and idempotency keys.

A medicine transfer must:

1. Confirm the requester is authorized for the destination PHC.
2. Confirm source and destination PHCs are in the same district.
3. Reject self-transfers.
4. Consider available stock, safety stock, predicted demand, and pending requests.
5. Support accept, reject, and partial fulfillment.
6. Atomically write transfer-out and transfer-in transactions on completion.
7. Prevent duplicate completion after retries.
8. Generate an audit record and relevant notification.

## 8. SignalR Notifications

Use SignalR for:

- New medicine requests
- Request acceptance or rejection
- Completed medicine transfers
- Low-stock alerts
- Predicted stock-outs
- Disease anomaly alerts
- Expected patient-volume increases

Authorize hub connections before joining district or PHC groups. Group membership must be derived from authenticated claims, not client-provided IDs. Do not broadcast patient-level or cross-district data.

## 9. AI/ML Architecture

The FastAPI service will be independent of the ASP.NET Core API. ASP.NET Core owns authentication, authorization, scope filtering, orchestration, and persistence. The AI service receives only authorized aggregate features or scoped datasets.

Initial methods should be interpretable and data-efficient:

- Patient volume: seasonal baseline, moving average, and regression.
- Medicine demand: regression or gradient-boosted regression using visits, diseases, consumption, seasonality, and current stock.
- Stock-out prediction: projected consumption with uncertainty intervals and current stock.
- Disease anomaly detection: robust rolling baseline, z-score, and seasonal decomposition.
- Redistribution: constrained rules or optimization considering safety stock, projected demand, pending requests, and same-district scope.

Feature inputs may include date, day of week, season, patient counts, diagnosis counts, medicine consumption, inventory, thresholds, pending transfers, and approved external factors where available.

Prediction outputs should include:

- Forecast horizon
- Point forecast
- Prediction interval
- Risk level
- Predicted stock-out date
- Recommended reorder quantity
- Anomaly score and baseline
- Recommendation candidates
- Model version
- Data window
- Confidence
- Data sufficiency status
- Explanation/evidence fields

Use explicit `InsufficientData` status when historical data is inadequate. Document missing-value handling and reject impossible values. Evaluate forecasting with rolling time-series backtesting using MAE, RMSE, and MASE. Evaluate anomaly detection with precision, recall, and false-alert rate. Evaluate redistribution recommendations against safety-stock constraints.

Persist prediction snapshots, model versions, AI alerts, run metadata, evidence, acknowledgement state, and district/PHC scope in SQL Server. Retrain on a scheduled batch after sufficient new data, with model versioning and rollback.

Synthetic data must cover all five districts, multiple PHCs, seasonality, disease spikes, linked medicine consumption, missing values, and stock-out scenarios. Use a repeatable seed and clearly mark all synthetic records.

## 10. Dashboard

The dashboard should provide one scoped aggregation endpoint containing:

- Today's patient count
- Patient-volume trend
- Disease trends
- Current inventory
- Low-stock medicines
- Predicted stock-outs
- AI alerts
- Disease anomaly alerts
- Pending and incoming medicine requests
- Available specialists
- Recent activity
- Data freshness and prediction status

Display AI output as decision support. Example alert text:

- `Paracetamol may stock out in approximately 5 days.`
- `Fever-related cases increased by 23% compared with the previous baseline.`
- `Patient volume is expected to increase by 18% next week.`
- `Potential abnormal increase in Dengue cases detected. Verification is recommended.`

The system must never automatically declare an outbreak or perform a medicine transfer based solely on a model output.

## 11. Development Phases

### Phase 1: Project setup and architecture

**Tasks:** Create the .NET solution, Angular application, FastAPI service, tests, Docker Compose, environment templates, documentation, health checks, OpenAPI, and CI foundation.

**Output:** All services build and start with placeholder health endpoints.

### Phase 2: Database and Entity Framework

**Tasks:** Create entities, relationships, EF configurations, indexes, constraints, migrations, SQL Server connection, and development seed data.

**Output:** A clean database can be created and seeded with districts, PHCs, roles, medicines, diseases, users, and synthetic data.

### Phase 3: Authentication and authorization

**Tasks:** Add ASP.NET Identity, JWT, refresh tokens, role policies, scope resolution, secure CORS, rate limiting, audit events, and cross-district authorization tests.

**Output:** Authenticated users can access only their permitted district and PHC data.

### Phase 4: District and PHC management

**Tasks:** Add district, PHC, user, role, medicine, disease, and specialization APIs and Angular administration pages.

**Output:** Authorized administrators can manage reference and organizational data.

### Phase 5: Patient and disease management

**Tasks:** Add patient visits, diagnoses, prescriptions, validation, analytics endpoints, forms, tables, charts, and auditing.

**Output:** Staff can record visits and view scoped disease and patient-volume trends.

### Phase 6: Medicine inventory

**Tasks:** Add inventories, transaction ledger, receipts, issues, adjustments, thresholds, expiry tracking, concurrency handling, and inventory UI.

**Output:** Stock updates are transactional, traceable, and cannot become negative.

### Phase 7: PHC-to-PHC medicine requests

**Tasks:** Add requests, items, source recommendations, accept/reject/partial fulfillment, approval rules, same-district validation, and transfer history.

**Output:** PHCs can exchange medicines within their district without bypassing stock safeguards.

### Phase 8: Real-time notifications

**Tasks:** Add SignalR hubs, authorized district/PHC groups, notification persistence, notification center, and live request/alert updates.

**Output:** Relevant users receive real-time operational notifications.

### Phase 9: Doctor and specialist management

**Tasks:** Add doctors, specializations, availability, OPD days/times, scoped search, and administration screens.

**Output:** Authorized users can find specialists within their district.

### Phase 10: Analytics dashboard

**Tasks:** Add the dashboard aggregation API, responsive Bootstrap layout, charts, tables, filters, pagination, caching, and empty/stale states.

**Output:** Users have one operational view of current PHC conditions.

### Phase 11: AI/ML pipeline

**Tasks:** Add feature builders, synthetic data generation, baseline models, FastAPI contracts, training jobs, evaluation, and model metadata.

**Output:** The AI service can generate repeatable predictions from operational data.

### Phase 12: AI predictions and alerts

**Tasks:** Add scheduled prediction jobs, API-to-AI client, retries, timeouts, circuit breaking, prediction persistence, alert generation, and dashboard integration.

**Output:** Users see versioned forecasts, stock-out risks, disease anomalies, and redistribution recommendations.

### Phase 13: Security hardening

**Tasks:** Perform threat modeling, IDOR testing, dependency scanning, security-header configuration, retention review, secret-management review, audit review, and access review.

**Output:** Security controls are tested and documented.

### Phase 14: Testing

**Tasks:** Add domain unit tests, application tests, SQL Server integration tests, API authorization tests, Angular tests, FastAPI contract/model tests, concurrency tests, and end-to-end workflows.

**Output:** Critical workflows are covered from data entry through operational action.

### Phase 15: Dockerization

**Tasks:** Create production-oriented Dockerfiles, local Compose configuration, environment handling, service health checks, and container vulnerability scanning.

**Output:** The complete stack can run consistently in local and staging environments.

### Phase 16: Deployment

**Tasks:** Deploy Angular to static hosting/CDN, API and AI to managed containers or app services, SQL Server to a managed SQL platform, and SignalR through the API or a managed SignalR service. Add HTTPS, secrets, private networking where required, logging, monitoring, backups, migrations, rollback, and CI/CD approvals.

**Output:** A monitored staging and production deployment with repeatable releases.

## 12. Local Development

SQL Server is already running in Docker. Configure the API connection string through environment variables or .NET user secrets, for example:

```text
Server=localhost,1433;Database=HealthGrid;User Id=sa;Password=<local-secret>;TrustServerCertificate=True;
```

Run EF Core migrations from the API project. Development startup may initialize a local database, but production migrations must run through a controlled release job.

Run the FastAPI service separately during development and configure its URL in the ASP.NET Core environment configuration. Never commit passwords, JWT signing keys, AI credentials, or connection strings containing secrets.

## 13. Deployment Strategy

Keep the application cloud-provider-independent with Docker and environment-based configuration.

Production components:

- Angular: static hosting and CDN.
- ASP.NET Core: managed container or app service with multiple instances.
- SQL Server: managed SQL Server-compatible database with encryption, backups, and high availability appropriate to risk.
- FastAPI: managed container or app service on a private network where possible.
- SignalR: API-hosted SignalR or a managed SignalR service.
- Secrets: managed secret store or platform secret injection.
- Observability: structured logs, metrics, distributed tracing, health probes, and alerting.

CI/CD gates should include formatting, compilation, unit tests, integration tests, authorization tests, UI tests, ML tests, migration validation, container scanning, infrastructure validation, staging smoke tests, manual production approval, and post-deployment health checks.

## 14. Required Verification Gates

1. A clean SQL Server database can be migrated and seeded.
2. A Ranchi user cannot read or mutate Bokaro records through any API, query, route ID, request body, background aggregation, or SignalR group.
3. Inventory operations remain correct under concurrent issues and duplicate retries.
4. Cross-district transfers are rejected by backend policy.
5. A visit and prescription update inventory and analytics correctly.
6. AI jobs produce versioned predictions and alerts with freshness and data-sufficiency status.
7. The dashboard renders operational data and clearly surfaces stale or failed predictions.
8. End-to-end tests cover visit entry, medicine issue, prediction generation, alert display, medicine request, and transfer completion.
9. Staging validates migrations, backups, restore, monitoring, security scanning, and rollback.

## 15. Open Decisions Before Implementation

- Confirm whether patients need longitudinal identification across visits. Recommended initial approach: PHC-local pseudonymous identifiers.
- Confirm whether district administrators can access patient-level records or only aggregates. Recommended default: aggregates only.
- Confirm expected user count and deployment provider before choosing the production SQL and SignalR topology.
- Confirm retention, privacy, audit, and healthcare compliance requirements with the responsible organization before production use.
