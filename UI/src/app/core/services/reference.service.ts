import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { toParams } from './http-params';
import {
  Disease,
  District,
  Medicine,
  Paged,
  PageQuery,
  Phc,
  Role,
  Specialization,
  UserAccount,
} from '../models/models';

const base = AppConfig.apiBaseUrl;

/** CRUD for the global reference / organisational data (Phase 4). */
@Injectable({ providedIn: 'root' })
export class ReferenceService {
  private readonly http = inject(HttpClient);

  // Districts -----------------------------------------------------------------
  districts(): Observable<District[]> {
    return this.http.get<District[]>(`${base}/districts`);
  }
  createDistrict(body: Partial<District>): Observable<District> {
    return this.http.post<District>(`${base}/districts`, body);
  }
  updateDistrict(id: string, body: Partial<District>): Observable<District> {
    return this.http.put<District>(`${base}/districts/${id}`, body);
  }
  deleteDistrict(id: string): Observable<void> {
    return this.http.delete<void>(`${base}/districts/${id}`);
  }

  // PHCs --------------------------------------------------------------------
  phcs(districtId?: string): Observable<Phc[]> {
    return this.http.get<Phc[]>(`${base}/phcs`, { params: toParams({ districtId }) });
  }
  createPhc(body: Partial<Phc>): Observable<Phc> {
    return this.http.post<Phc>(`${base}/phcs`, body);
  }
  updatePhc(id: string, body: Partial<Phc>): Observable<Phc> {
    return this.http.put<Phc>(`${base}/phcs/${id}`, body);
  }
  deletePhc(id: string): Observable<void> {
    return this.http.delete<void>(`${base}/phcs/${id}`);
  }

  // Medicines -------------------------------------------------------------
  medicines(query?: PageQuery): Observable<Paged<Medicine>> {
    return this.http.get<Paged<Medicine>>(`${base}/medicines`, { params: toParams(query) });
  }
  createMedicine(body: Partial<Medicine>): Observable<Medicine> {
    return this.http.post<Medicine>(`${base}/medicines`, body);
  }
  updateMedicine(id: string, body: Partial<Medicine>): Observable<Medicine> {
    return this.http.put<Medicine>(`${base}/medicines/${id}`, body);
  }

  // Diseases ------------------------------------------------------------
  diseases(): Observable<Disease[]> {
    return this.http.get<Disease[]>(`${base}/diseases`);
  }
  createDisease(body: Partial<Disease>): Observable<Disease> {
    return this.http.post<Disease>(`${base}/diseases`, body);
  }
  updateDisease(id: string, body: Partial<Disease>): Observable<Disease> {
    return this.http.put<Disease>(`${base}/diseases/${id}`, body);
  }

  // Specializations ---------------------------------------------------
  specializations(): Observable<Specialization[]> {
    return this.http.get<Specialization[]>(`${base}/specializations`);
  }
  createSpecialization(body: Partial<Specialization>): Observable<Specialization> {
    return this.http.post<Specialization>(`${base}/specializations`, body);
  }

  // Users -----------------------------------------------------------
  users(query?: PageQuery): Observable<Paged<UserAccount>> {
    return this.http.get<Paged<UserAccount>>(`${base}/users`, { params: toParams(query) });
  }
  createUser(body: {
    email: string;
    fullName: string;
    password: string;
    role: Role;
    districtId: string | null;
    phcId: string | null;
  }): Observable<UserAccount> {
    return this.http.post<UserAccount>(`${base}/users`, body);
  }
  updateUser(id: string, body: Partial<UserAccount>): Observable<UserAccount> {
    return this.http.put<UserAccount>(`${base}/users/${id}`, body);
  }
}
