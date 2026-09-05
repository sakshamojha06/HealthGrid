import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { toParams } from './http-params';
import {
  CreateMedicineRequest,
  FulfillMedicineRequest,
  MedicineRequest,
  RecommendedSource,
} from '../models/models';

const base = AppConfig.apiBaseUrl;

/** PHC-to-PHC medicine requests and transfers (Phase 7). */
@Injectable({ providedIn: 'root' })
export class MedicineRequestsService {
  private readonly http = inject(HttpClient);

  list(direction: 'incoming' | 'outgoing', status?: string): Observable<MedicineRequest[]> {
    return this.http.get<MedicineRequest[]>(`${base}/medicine-requests`, {
      params: toParams({ direction, status }),
    });
  }

  get(id: string): Observable<MedicineRequest> {
    return this.http.get<MedicineRequest>(`${base}/medicine-requests/${id}`);
  }

  create(body: CreateMedicineRequest): Observable<MedicineRequest> {
    return this.http.post<MedicineRequest>(`${base}/medicine-requests`, body);
  }

  accept(id: string): Observable<MedicineRequest> {
    return this.http.post<MedicineRequest>(`${base}/medicine-requests/${id}/accept`, {});
  }

  reject(id: string, reason: string): Observable<MedicineRequest> {
    return this.http.post<MedicineRequest>(`${base}/medicine-requests/${id}/reject`, { reason });
  }

  fulfill(id: string, body: FulfillMedicineRequest): Observable<MedicineRequest> {
    return this.http.post<MedicineRequest>(`${base}/medicine-requests/${id}/fulfill`, body);
  }

  complete(id: string): Observable<MedicineRequest> {
    return this.http.post<MedicineRequest>(`${base}/medicine-requests/${id}/complete`, {});
  }

  recommendedSources(medicineId: string, qty: number): Observable<RecommendedSource[]> {
    return this.http.get<RecommendedSource[]>(`${base}/medicine-requests/recommended-sources`, {
      params: toParams({ medicineId, qty }),
    });
  }
}
