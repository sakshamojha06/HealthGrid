# HealthGrid — Web UI (Angular)

Angular 20 (standalone components + signals) front-end for the HealthGrid PHC
operations platform. See `../.claude/plans/…` / the project plan for the full
architecture; this app is **Phase 4 onward** — the presentation layer that talks
to the ASP.NET Core API and the SignalR notifications hub.

## Run

```bash
npm install
npm start            # ng serve on http://localhost:4200, proxies /api and /hubs -> :5030
```

The API is expected on `http://localhost:5030` (configurable in `proxy.conf.json`
for dev, or via `window.__HG_API_BASE__` at runtime for a deployed build).

```bash
npm run build        # production build -> dist/UI
npm test             # unit tests (Karma)
```

## Structure

```
src/app/
├── core/                     Cross-cutting singletons — no UI
│   ├── config/app-config.ts  API + hub URLs
│   ├── models/models.ts      TypeScript mirrors of the API DTOs
│   ├── auth/                  AuthService (JWT + refresh), route guards
│   ├── interceptors/          bearer-token + silent-refresh, error toast
│   ├── realtime/             RealtimeService — SignalR hub wrapper
│   └── services/             one service per API area (reference, visits,
│                             inventory, medicine-requests, doctors, dashboard,
│                             notifications)
├── layout/
│   ├── shell/                app frame: sidenav + toolbar, role-aware nav
│   └── notification-bell/    unread-count menu fed by SignalR
├── shared/
│   ├── chart/                Chart.js wrapper component + palette
│   └── ui/                   page-header, stat-card, data-state, confirm-dialog
└── features/                 one folder per screen area — lazy-loaded routes
    ├── auth/                 login (+ demo-account quick fill)
    ├── dashboard/            KPIs, patient-volume forecast, disease trends,
    │                         stock-out risk, AI alerts, "Run AI now"
    ├── visits/               record-visit form, visit list with filters
    ├── inventory/            district-wide stock list, receipt/adjust dialog,
    │                         transaction ledger
    ├── requests/             incoming/outgoing tabs, new request (with
    │                         AI-suggested sources), request detail
    │                         (accept / reject / partial fulfil / complete)
    ├── doctors/              specialist search, doctor + availability admin
    ├── alerts/               AI alerts + notification centre
    └── admin/                districts, PHCs, medicines, diseases,
                              specializations (generic CRUD), users (dialog)
```

## Conventions

- **Standalone components only**, no NgModules. Routes use `loadComponent`.
- **Signals** for component state; RxJS only at the HTTP boundary.
- District/PHC scoping is enforced by the **API** — the UI only hides controls a
  role can't use (`AuthService.hasAnyRole`, `roleGuard`).
- All colours come from the `--hg-*` CSS custom properties in `src/styles.scss`
  (light + dark). Charts use `shared/chart/palette.ts`.
- Every list screen renders `<hg-data-state>` for loading / empty / error.

## Backend contract expected

`GET /api/auth/me`, `POST /api/auth/{login,refresh,logout}`,
`/api/{districts,phcs,medicines,diseases,specializations,users}`,
`/api/patient-visits`, `/api/inventory[/transactions|/receipts|/adjustments]`,
`/api/medicine-requests[/:id/{accept,reject,fulfill,complete}|/recommended-sources]`,
`/api/doctors[/search|/:id/availability]`, `/api/notifications`,
`/api/dashboard`, `/api/analytics/{patient-volume,diseases}`,
`/api/ai/{predictions,alerts,run}`, and the SignalR hub at `/hubs/notifications`
emitting `notification`, `medicineRequest`, `aiAlert` events.
