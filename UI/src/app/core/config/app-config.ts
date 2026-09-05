/**
 * Central runtime configuration for the HealthGrid UI.
 *
 * URLs are relative so that:
 *  - `ng serve` routes them through `proxy.conf.json` to the API on :5030
 *  - a production build served behind nginx hits the same origin
 * Override `window.__HG_API_BASE__` at runtime (e.g. in index.html) if the API
 * lives on another host.
 */
const w = window as unknown as { __HG_API_BASE__?: string; __HG_HUB_BASE__?: string };

export const AppConfig = {
  /** Base URL of the ASP.NET Core Web API. */
  apiBaseUrl: w.__HG_API_BASE__ ?? '/api',
  /** SignalR notifications hub (see Phase 8 of the plan). */
  notificationsHubUrl: (w.__HG_HUB_BASE__ ?? '') + '/hubs/notifications',
  /** Access-token lifetime hint (server is authoritative); used only for proactive refresh. */
  accessTokenSkewSeconds: 30,
  /** Default page size for paged lists. */
  defaultPageSize: 20,
};
