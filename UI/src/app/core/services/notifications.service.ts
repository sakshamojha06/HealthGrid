import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AppConfig } from '../config/app-config';
import { AppNotification } from '../models/models';

const base = AppConfig.apiBaseUrl;

/**
 * Holds the notification list for the bell menu. The realtime service pushes
 * new items in via {@link prepend}; REST is used for the initial load and ack.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);

  private readonly _items = signal<AppNotification[]>([]);
  readonly items = this._items.asReadonly();
  readonly unreadCount = computed(() => this._items().filter((n) => !n.isAcknowledged).length);

  load(): Observable<AppNotification[]> {
    return this.http
      .get<AppNotification[]>(`${base}/notifications`)
      .pipe(tap((items) => this._items.set(items)));
  }

  acknowledge(id: string): Observable<void> {
    return this.http.post<void>(`${base}/notifications/${id}/acknowledge`, {}).pipe(
      tap(() =>
        this._items.update((items) =>
          items.map((n) => (n.id === id ? { ...n, isAcknowledged: true } : n)),
        ),
      ),
    );
  }

  acknowledgeAll(): void {
    for (const n of this._items().filter((x) => !x.isAcknowledged)) {
      this.acknowledge(n.id).subscribe({ error: () => void 0 });
    }
  }

  /** Called by the realtime service when the hub pushes a notification. */
  prepend(notification: AppNotification): void {
    this._items.update((items) => [notification, ...items.filter((n) => n.id !== notification.id)]);
  }
}
