import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { InventoryService } from '../../core/services/inventory.service';
import { InventoryTransaction } from '../../core/models/models';

@Component({
  selector: 'hg-transaction-ledger',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatSelectModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './transaction-ledger.component.html',
  styleUrl: './transaction-ledger.component.scss',
})
export class TransactionLedgerComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly inventory = inject(InventoryService);

  readonly columns = ['createdAtUtc', 'medicine', 'phc', 'type', 'quantity', 'balanceAfter', 'reference'];
  readonly rows = signal<InventoryTransaction[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly types = ['Receipt', 'PatientIssue', 'Adjustment', 'TransferOut', 'TransferIn'];

  page = 0;
  pageSize = 20;

  readonly filters = this.fb.nonNullable.group({ type: '' });

  ngOnInit(): void {
    this.filters.valueChanges.subscribe(() => {
      this.page = 0;
      this.load();
    });
    this.load();
  }

  onPage(e: PageEvent): void {
    this.page = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.inventory
      .transactions({
        page: this.page + 1,
        pageSize: this.pageSize,
        type: this.filters.value.type || undefined,
      })
      .subscribe({
        next: (res) => {
          this.rows.set(res.items);
          this.total.set(res.totalCount);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load the ledger.');
          this.loading.set(false);
        },
      });
  }
}
