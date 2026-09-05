import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { toParams } from './http-params';
import { Paged, PatientVisit, RecordVisitRequest } from '../models/models';

const base = AppConfig.apiBaseUrl;

/** Patient visits, diagnoses and prescriptions (Phase 5). */
@Injectable({ providedIn: 'root' })
export class VisitsService {
  private readonly http = inject(HttpClient);

  list(query?: {
    page?: number;
    pageSize?: number;
    from?: string;
    to?: string;
    diseaseId?: string;
    search?: string;
  }): Observable<Paged<PatientVisit>> {
    return this.http.get<Paged<PatientVisit>>(`${base}/patient-visits`, {
      params: toParams(query),
    });
  }

  get(id: string): Observable<PatientVisit> {
    return this.http.get<PatientVisit>(`${base}/patient-visits/${id}`);
  }

  record(body: RecordVisitRequest): Observable<PatientVisit> {
    return this.http.post<PatientVisit>(`${base}/patient-visits`, body);
  }
}
