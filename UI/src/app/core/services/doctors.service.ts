import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { toParams } from './http-params';
import { Doctor, DoctorAvailabilitySlot } from '../models/models';

const base = AppConfig.apiBaseUrl;

/** Doctor / specialist directory within a district (Phase 10). */
@Injectable({ providedIn: 'root' })
export class DoctorsService {
  private readonly http = inject(HttpClient);

  search(query?: {
    specializationId?: string;
    phcId?: string;
    day?: number;
  }): Observable<Doctor[]> {
    return this.http.get<Doctor[]>(`${base}/doctors/search`, { params: toParams(query) });
  }

  get(id: string): Observable<Doctor> {
    return this.http.get<Doctor>(`${base}/doctors/${id}`);
  }

  create(body: Partial<Doctor>): Observable<Doctor> {
    return this.http.post<Doctor>(`${base}/doctors`, body);
  }

  update(id: string, body: Partial<Doctor>): Observable<Doctor> {
    return this.http.put<Doctor>(`${base}/doctors/${id}`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${base}/doctors/${id}`);
  }

  setAvailability(id: string, slots: DoctorAvailabilitySlot[]): Observable<Doctor> {
    return this.http.put<Doctor>(`${base}/doctors/${id}/availability`, { slots });
  }
}
