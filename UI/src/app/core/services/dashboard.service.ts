import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { toParams } from './http-params';
import { AiAlert, DashboardSummary, ForecastPoint, StockoutRisk, TimeSeriesPoint } from '../models/models';

const base = AppConfig.apiBaseUrl;

/** Analytics dashboard + AI prediction endpoints (Phases 9 and 12). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  summary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${base}/dashboard`);
  }

  patientVolume(from: string, to: string): Observable<TimeSeriesPoint[]> {
    return this.http.get<TimeSeriesPoint[]>(`${base}/analytics/patient-volume`, {
      params: toParams({ from, to }),
    });
  }

  diseaseTrends(from: string, to: string): Observable<{ diseaseName: string; series: TimeSeriesPoint[] }[]> {
    return this.http.get<{ diseaseName: string; series: TimeSeriesPoint[] }[]>(
      `${base}/analytics/diseases`,
      { params: toParams({ from, to }) },
    );
  }

  // AI --------------------------------------------------------------------
  predictions(): Observable<{ forecast: ForecastPoint[]; stockoutRisks: StockoutRisk[] }> {
    return this.http.get<{ forecast: ForecastPoint[]; stockoutRisks: StockoutRisk[] }>(
      `${base}/ai/predictions`,
    );
  }

  aiAlerts(): Observable<AiAlert[]> {
    return this.http.get<AiAlert[]>(`${base}/ai/alerts`);
  }

  acknowledgeAiAlert(id: string): Observable<void> {
    return this.http.post<void>(`${base}/ai/alerts/${id}/acknowledge`, {});
  }

  /** Admin-only: run the prediction pipeline now (demo button). */
  runAi(): Observable<{ startedAtUtc: string }> {
    return this.http.post<{ startedAtUtc: string }>(`${base}/ai/run`, {});
  }
}
