import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { InventoryService } from '../../core/services/inventory.service';
import { ReferenceService } from '../../core/services/reference.service';
import { AuthService } from '../../core/auth/auth.service';
import { InventoryRow, Phc } from '../../core/models/models';
import { StockMutationDialogComponent } from './stock-mutation-dialog.component';

@Component({
  selector: 'hg-stock-list',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatMenuModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './stock-list.component.html',
  styleUrl: './stock-list.component.scss',
})
export class StockListComponent implements OnInit {
  private readonly inventory = inject(InventoryService);
  private readonly reference = inject(ReferenceService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly columns = ['medicine', 'phc', 'onHand', 'safety', 'expiry', 'actions'];
  readonly rows = signal<InventoryRow[]>([]);
  readonly phcs = signal<Phc[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly phcFilter = new FormControl<string>('', { nonNullable: true });
  readonly lowOnly = new FormControl<boolean>(false, { nonNullable: true });
  readonly search = new FormControl<string>('', { nonNullable: true });

  readonly myPhcId = computed(() => this.auth.user()?.phcId ?? null);

  readonly filtered = computed(() => {
    const term = this.search.value.trim().toLowerCase();
    return this.rows().filter((r) => !term || r.medicineName.toLowerCase().includes(term));
  });

  ngOnInit(): void {
    this.reference.phcs().subscribe((p) => this.phcs.set(p));
    this.phcFilter.valueChanges.subscribe(() => this.load());
    this.lowOnly.valueChanges.subscribe(() => this.load());
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.inventory
      .stock({ phcId: this.phcFilter.value || undefined, lowOnly: this.lowOnly.value || undefined })
      .subscribe({
        next: (r) => {
          this.rows.set(r);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Could not load inventory.');
          this.loading.set(false);
        },
      });
  }

  canManage(row: InventoryRow): boolean {
    return (
      this.auth.hasAnyRole('SystemAdministrator', 'DistrictAdministrator') ||
      row.phcId === this.myPhcId()
    );
  }

  mutate(row: InventoryRow, mode: 'receipt' | 'adjustment'): void {
    this.dialog
      .open(StockMutationDialogComponent, { data: { mode, row } })
      .afterClosed()
      .subscribe((updated: InventoryRow | undefined) => {
        if (updated) {
          this.rows.update((rows) => rows.map((r) => (r.id === updated.id ? updated : r)));
        }
      });
  }
}
