import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { NotificationsService } from '../../core/services/notifications.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { AiAlert } from '../../core/models/models';

@Component({
  selector: 'hg-alerts',
  standalone: true,
  imports: [
    DatePipe,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatBadgeModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './alerts.component.html',
  styleUrl: './alerts.component.scss',
})
export class AlertsComponent implements OnInit {
  readonly notifications = inject(NotificationsService);
  private readonly dashboard = inject(DashboardService);

  readonly aiAlerts = signal<AiAlert[]>([]);
  readonly loadingAi = signal(true);

  ngOnInit(): void {
    this.notifications.load().subscribe();
    this.dashboard.aiAlerts().subscribe({
      next: (a) => {
        this.aiAlerts.set(a);
        this.loadingAi.set(false);
      },
      error: () => this.loadingAi.set(false),
    });
  }

  icon(type: AiAlert['alertType']): string {
    return type === 'Stockout'
      ? 'medication'
      : type === 'DiseaseAnomaly'
        ? 'coronavirus'
        : 'trending_up';
  }

  ackAi(id: string): void {
    this.dashboard.acknowledgeAiAlert(id).subscribe(() =>
      this.aiAlerts.update((list) =>
        list.map((a) => (a.id === id ? { ...a, isAcknowledged: true } : a)),
      ),
    );
  }
}
