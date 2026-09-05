import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { finalize } from 'rxjs';
import { InventoryService } from '../../core/services/inventory.service';
import { InventoryRow, StockMutationRequest } from '../../core/models/models';

export interface StockMutationData {
  mode: 'receipt' | 'adjustment';
  row: InventoryRow;
}

/** Receipt (add stock) or adjustment (signed correction) for one inventory row. */
@Component({
  selector: 'hg-stock-mutation-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ data.mode === 'receipt' ? 'Receive stock' : 'Adjust stock' }} — {{ data.row.medicineName }}
    </h2>
    <mat-dialog-content>
      <p class="ctx">
        {{ data.row.phcName }} · on hand
        <strong>{{ data.row.quantityOnHand }} {{ data.row.unit }}</strong>
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ data.mode === 'receipt' ? 'Quantity received' : 'Signed change (+/-)' }}</mat-label>
          <input matInput type="number" formControlName="quantity" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Reference / reason</mat-label>
          <input matInput formControlName="reference" />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving()">
        {{ saving() ? 'Saving…' : 'Save' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .ctx {
        color: var(--hg-text-muted);
        margin-top: 0;
      }
      .form {
        display: flex;
        flex-direction: column;
        gap: 6px;
        min-width: 320px;
      }
    `,
  ],
})
export class StockMutationDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly inventory = inject(InventoryService);
  readonly data = inject<StockMutationData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<StockMutationDialogComponent>);

  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    quantity: [0, [Validators.required]],
    reference: [''],
  });

  save(): void {
    if (this.form.invalid) {
      return;
    }
    const { quantity, reference } = this.form.getRawValue();
    if (this.data.mode === 'receipt' && quantity <= 0) {
      this.form.controls.quantity.setErrors({ min: true });
      return;
    }
    const body: StockMutationRequest = {
      phcId: this.data.row.phcId,
      medicineId: this.data.row.medicineId,
      quantity,
      reference: reference || null,
      idempotencyKey: crypto.randomUUID(),
    };
    this.saving.set(true);
    const call =
      this.data.mode === 'receipt' ? this.inventory.receive(body) : this.inventory.adjust(body);
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (updated) => this.ref.close(updated),
    });
  }
}
