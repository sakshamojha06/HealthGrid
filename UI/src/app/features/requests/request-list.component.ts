import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { MedicineRequestsService } from '../../core/services/medicine-requests.service';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { MedicineRequest } from '../../core/models/models';

@Component({
  selector: 'hg-request-list',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatBadgeModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './request-list.component.html',
  styleUrl: './request-list.component.scss',
})
export class RequestListComponent implements OnInit, OnDestroy {
  private readonly service = inject(MedicineRequestsService);
  private readonly realtime = inject(RealtimeService);
  private readonly snack = inject(MatSnackBar);

  readonly incoming = signal<MedicineRequest[]>([]);
  readonly outgoing = signal<MedicineRequest[]>([]);
  readonly loading = signal(true);

  private sub?: Subscription;

  ngOnInit(): void {
    this.reload();
    this.sub = this.realtime.medicineRequest$.subscribe((r) => {
      this.snack.open(`New medicine request from ${r.sourcePhcName}`, 'View', { duration: 6000 });
      this.reload();
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  reload(): void {
    this.loading.set(true);
    this.service.list('incoming').subscribe((r) => {
      this.incoming.set(r);
      this.loading.set(false);
    });
    this.service.list('outgoing').subscribe((r) => this.outgoing.set(r));
  }

  pendingIncoming(): number {
    return this.incoming().filter((r) => r.status === 'Pending').length;
  }
}
