# HealthGrid API (C#)

ASP.NET Core 10 Web API — the system of record for HealthGrid.

## Layout

```
HealthGrid.Api/
├── Domain/          Entities + enums (POCO, no EF attributes)
├── Data/            DbContext, ApplicationUser (Identity), migrations, dev seeder
├── Auth/            Roles, ICurrentUser (scope resolution), JWT + refresh tokens
├── Contracts/       Request/response DTOs (mirrored by UI/src/app/core/models/models.ts)
├── Services/        Business rules — one class per area + AI client + exception handler
├── Hubs/            SignalR NotificationsHub
└── Controllers/     One controller per area of work
```

## Run locally

```bash
dotnet run --project HealthGrid.Api
```

- Listens on `http://localhost:5030` (see `Properties/launchSettings.json`).
- In `Development` it applies EF migrations and seeds demo data on startup.
- OpenAPI document at `/openapi/v1.json` (Development only).

### Configuration

| Key | Purpose | Default |
|-----|---------|---------|
| `ConnectionStrings:HealthGrid` | SQL Server connection | `localhost,1433` / `sa` |
| `Jwt:SigningKey` | HMAC signing key (≥ 32 chars) — **required** | set in `appsettings.Development.json` |
| `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays` | token lifetimes | 30 / 14 |
| `Ai:BaseUrl` | FastAPI service base URL; empty = use local baseline | `http://localhost:8000` (Development) |
| `Cors:Origins` | allowed browser origins | `http://localhost:4200` |

Use user-secrets for anything sensitive:

```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project HealthGrid.Api
dotnet user-secrets set "ConnectionStrings:HealthGrid" "Server=...;Password=...;" --project HealthGrid.Api
```

## Migrations

```bash
dotnet ef migrations add <Name> -o Data/Migrations --project HealthGrid.Api
dotnet ef database update --project HealthGrid.Api          # production: run as a release step
```

## Tokens & scope

`TokenService` issues a short-lived JWT (claims: `sub`, `email`, `name`, `role`,
`district_id`, `phc_id`, `token_version`) and a rotating refresh token stored only
as a SHA-256 hash. `OnTokenValidated` rejects tokens once a user is deactivated or
their `TokenVersion` is bumped. `ICurrentUser.EnsureAccess(districtId, phcId)`
guards every write.
