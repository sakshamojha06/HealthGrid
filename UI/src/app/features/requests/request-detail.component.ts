import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { MedicineRequestsService } from '../../core/services/medicine-requests.service';
import { AuthService } from '../../core/auth/auth.service';
import { MedicineRequest } from '../../core/models/models';

@Component({
  selector: 'hg-request-detail',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './request-detail.component.html',
  styleUrl: './request-detail.component.scss',
})
export class RequestDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(MedicineRequestsService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly request = signal<MedicineRequest | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly fulfilItems = new FormArray<FormGroup>([]);
  readonly fulfilForm = this.fb.group({ items: this.fulfilItems });

  /** True when the current user's PHC is the destination (can act on it). */
  readonly isDestination = computed(
    () => this.request()?.destinationPhcId === this.auth.user()?.phcId,
  );

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service
      .get(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (r) => this.setRequest(r),
        error: () => this.error.set('Request not found or outside your scope.'),
      });
  }

  private setRequest(r: MedicineRequest): void {
    this.request.set(r);
    this.fulfilItems.clear();
    for (const item of r.items) {
      this.fulfilItems.push(
        this.fb.nonNullable.group({
          medicineId: item.medicineId,
          medicineName: item.medicineName,
          quantityFulfilled: item.quantityRequested - item.quantityFulfilled,
        }),
      );
    }
  }

  accept(): void {
    this.act(this.service.accept(this.request()!.id), 'Request accepted.');
  }

  reject(): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Reject request?',
          message: 'The requesting PHC will be notified.',
          confirmText: 'Reject',
          danger: true,
        },
      })
      .afterClosed()
      .subscribe((ok) => {
        if (ok) {
          this.act(this.service.reject(this.request()!.id, 'Not available'), 'Request rejected.');
        }
      });
  }

  fulfill(): void {
    const items = (this.fulfilItems.getRawValue() as { medicineId: string; quantityFulfilled: number }[])
      .filter((x) => x.quantityFulfilled > 0)
      .map((x) => ({ medicineId: x.medicineId, quantityFulfilled: x.quantityFulfilled }));
    this.act(
      this.service.fulfill(this.request()!.id, { items }),
      'Stock transferred to the requesting PHC.',
    );
  }

  complete(): void {
    this.act(this.service.complete(this.request()!.id), 'Request completed.');
  }

  private act(obs: ReturnType<MedicineRequestsService['accept']>, message: string): void {
    this.busy.set(true);
    obs.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: (r) => {
        this.setRequest(r);
        this.snack.open(message, 'OK', { duration: 3500 });
      },
    });
  }
}
