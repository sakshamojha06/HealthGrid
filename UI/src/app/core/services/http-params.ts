import { HttpParams } from '@angular/common/http';

/** Builds an HttpParams from a plain object, skipping null/undefined/'' values. */
export function toParams(query: object | undefined | null): HttpParams {
  let params = new HttpParams();
  if (!query) {
    return params;
  }
  for (const [key, value] of Object.entries(query as Record<string, unknown>)) {
    if (value === null || value === undefined || value === '') {
      continue;
    }
    params = params.set(key, String(value));
  }
  return params;
}
