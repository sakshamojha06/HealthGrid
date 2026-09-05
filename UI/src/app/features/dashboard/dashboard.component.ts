import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';
import type { ChartConfiguration } from 'chart.js';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatCardComponent } from '../../shared/ui/stat-card.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { ChartComponent } from '../../shared/chart/chart.component';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/auth/auth.service';
import { DashboardSummary } from '../../core/models/models';
import { chartPalette } from '../../shared/chart/palette';

@Component({
  selector: 'hg-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTableModule,
    PageHeaderComponent,
    StatCardComponent,
    DataStateComponent,
    ChartComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly service = inject(DashboardService);
  private readonly auth = inject(AuthService);
  private readonly snack = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly running = signal(false);
  readonly error = signal<string | null>(null);
  readonly data = signal<DashboardSummary | null>(null);

  readonly canRunAi = computed(() =>
    this.auth.hasAnyRole('SystemAdministrator', 'DistrictAdministrator'),
  );

  readonly volumeChart = computed<ChartConfiguration['data']>(() => {
    const d = this.data();
    if (!d) {
      return { datasets: [] };
    }
    const history = d.patientVolumeTrend ?? [];
    const forecast = d.patientVolumeForecast ?? [];
    const labels = [...history.map((p) => p.date), ...forecast.map((p) => p.date)];
    const pad = (arr: unknown[], atEnd = false) =>
      atEnd
        ? [...new Array(history.length).fill(null), ...arr]
        : [...arr, ...new Array(forecast.length).fill(null)];

    return {
      labels,
      datasets: [
        {
          label: 'Recorded visits',
          data: pad(history.map((p) => p.value)) as number[],
          borderColor: chartPalette[0],
          backgroundColor: 'transparent',
          tension: 0.3,
        },
        {
          label: 'Forecast',
          data: pad(forecast.map((p) => p.yhat), true) as number[],
          borderColor: chartPalette[1],
          borderDash: [6, 4],
          backgroundColor: 'transparent',
          tension: 0.3,
        },
        {
          label: 'Upper',
          data: pad(forecast.map((p) => p.upper), true) as number[],
          borderColor: 'transparent',
          backgroundColor: 'rgba(120,120,200,0.12)',
          fill: '+1',
          pointRadius: 0,
        },
        {
          label: 'Lower',
          data: pad(forecast.map((p) => p.lower), true) as number[],
          borderColor: 'transparent',
          backgroundColor: 'rgba(120,120,200,0.12)',
          fill: false,
          pointRadius: 0,
        },
      ],
    };
  });

  readonly diseaseChart = computed<ChartConfiguration['data']>(() => {
    const d = this.data();
    if (!d?.diseaseTrends?.length) {
      return { datasets: [] };
    }
    return {
      labels: d.diseaseTrends.map((t) => t.diseaseName),
      datasets: [
        {
          label: 'Cases today',
          data: d.diseaseTrends.map((t) => t.today),
          backgroundColor: d.diseaseTrends.map((_, i) => chartPalette[i % chartPalette.length]),
        },
      ],
    };
  });

  readonly stockoutColumns = ['medicine', 'phc', 'days', 'reorder', 'risk'];

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service
      .summary()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (d) => this.data.set(d),
        error: () => this.error.set('Could not load the dashboard.'),
      });
  }

  runAi(): void {
    this.running.set(true);
    this.service
      .runAi()
      .pipe(finalize(() => this.running.set(false)))
      .subscribe({
        next: () => {
          this.snack.open('AI pipeline started — predictions will refresh shortly.', 'OK', {
            duration: 4000,
          });
          setTimeout(() => this.reload(), 4000);
        },
      });
  }

  acknowledge(id: string): void {
    this.service.acknowledgeAiAlert(id).subscribe({
      next: () =>
        this.data.update((d) =>
          d
            ? { ...d, aiAlerts: d.aiAlerts.map((a) => (a.id === id ? { ...a, isAcknowledged: true } : a)) }
            : d,
        ),
    });
  }
}
