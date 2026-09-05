import { Injectable, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { AppConfig } from '../config/app-config';
import { AuthService } from '../auth/auth.service';
import { NotificationsService } from '../services/notifications.service';
import { AiAlert, AppNotification, MedicineRequest } from '../models/models';

/**
 * Wraps the SignalR notifications hub. Group membership is decided server-side
 * from the JWT claims (district-{id} / phc-{id}) — the client only listens.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(NotificationsService);

  private connection?: HubConnection;
  readonly connected = signal(false);

  /** Emitted when an incoming medicine request lands for the current PHC. */
  readonly medicineRequest$ = new Subject<MedicineRequest>();
  /** Emitted when a new AI alert is raised. */
  readonly aiAlert$ = new Subject<AiAlert>();

  async start(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(AppConfig.notificationsHubUrl, {
        accessTokenFactory: () => this.auth.accessToken ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('notification', (n: AppNotification) => this.notifications.prepend(n));
    this.connection.on('medicineRequest', (r: MedicineRequest) => this.medicineRequest$.next(r));
    this.connection.on('aiAlert', (a: AiAlert) => this.aiAlert$.next(a));

    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onclose(() => this.connected.set(false));

    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      this.connected.set(false);
    }
  }

  async stop(): Promise<void> {
    await this.connection?.stop();
    this.connection = undefined;
    this.connected.set(false);
  }
}
