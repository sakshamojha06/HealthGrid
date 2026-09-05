import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { toParams } from './http-params';
import {
  InventoryRow,
  InventoryTransaction,
  Paged,
  StockMutationRequest,
} from '../models/models';

const base = AppConfig.apiBaseUrl;

/** Stock levels and the immutable transaction ledger (Phase 6). */
@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);

  /** District-wide stock (so PHCs can see what neighbours hold). */
  stock(query?: { phcId?: string; medicineId?: string; lowOnly?: boolean }): Observable<InventoryRow[]> {
    return this.http.get<InventoryRow[]>(`${base}/inventory`, { params: toParams(query) });
  }

  transactions(query?: {
    page?: number;
    pageSize?: number;
    phcId?: string;
    medicineId?: string;
    type?: string;
  }): Observable<Paged<InventoryTransaction>> {
    return this.http.get<Paged<InventoryTransaction>>(`${base}/inventory/transactions`, {
      params: toParams(query),
    });
  }

  receive(body: StockMutationRequest): Observable<InventoryRow> {
    return this.http.post<InventoryRow>(`${base}/inventory/receipts`, body);
  }

  adjust(body: StockMutationRequest): Observable<InventoryRow> {
    return this.http.post<InventoryRow>(`${base}/inventory/adjustments`, body);
  }
}
