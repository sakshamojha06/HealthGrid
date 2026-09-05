import { Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { MatListModule } from '@angular/material/list';
import { NotificationsService } from '../../core/services/notifications.service';
import { RealtimeService } from '../../core/realtime/realtime.service';

/** Toolbar bell showing unread notifications pushed over SignalR. */
@Component({
  selector: 'hg-notification-bell',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatBadgeModule,
    MatListModule,
  ],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="menu"
      [matBadge]="notifications.unreadCount()"
      [matBadgeHidden]="notifications.unreadCount() === 0"
      matBadgeColor="warn"
      matBadgeSize="small"
      aria-label="Notifications"
    >
      <mat-icon>{{ realtime.connected() ? 'notifications' : 'notifications_off' }}</mat-icon>
    </button>

    <mat-menu #menu="matMenu" class="bell-menu">
      <div class="bell-menu__head" (click)="$event.stopPropagation()">
        <span>Notifications</span>
        @if (notifications.unreadCount() > 0) {
          <button mat-button (click)="notifications.acknowledgeAll()">Mark all read</button>
        }
      </div>
      @if (notifications.items().length === 0) {
        <div class="bell-menu__empty">You're all caught up.</div>
      }
      @for (n of notifications.items().slice(0, 12); track n.id) {
        <button
          mat-menu-item
          class="bell-item"
          [class.bell-item--unread]="!n.isAcknowledged"
          (click)="notifications.acknowledge(n.id).subscribe()"
        >
          <span class="bell-item__dot" [attr.data-sev]="n.severity"></span>
          <span class="bell-item__text">
            <strong>{{ n.title }}</strong>
            <small>{{ n.message }}</small>
            <small class="bell-item__time">{{ n.createdAtUtc | date: 'short' }}</small>
          </span>
        </button>
      }
    </mat-menu>
  `,
  styles: [
    `
      .bell-menu__head {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 12px;
        font-weight: 600;
      }
      .bell-menu__empty {
        padding: 20px;
        color: var(--hg-text-muted);
        text-align: center;
      }
      .bell-item {
        height: auto;
        padding: 10px 12px;
        white-space: normal;
      }
      .bell-item__text {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .bell-item__text small {
        color: var(--hg-text-muted);
      }
      .bell-item__time {
        font-size: 0.7rem;
      }
      .bell-item--unread {
        background: color-mix(in srgb, var(--hg-primary) 8%, transparent);
      }
      .bell-item__dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        margin-right: 10px;
        flex: none;
        background: var(--hg-text-muted);
      }
      .bell-item__dot[data-sev='Warning'] {
        background: #f5a623;
      }
      .bell-item__dot[data-sev='Critical'] {
        background: var(--hg-danger);
      }
    `,
  ],
})
export class NotificationBellComponent {
  readonly notifications = inject(NotificationsService);
  readonly realtime = inject(RealtimeService);
}
