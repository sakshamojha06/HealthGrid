import { Component, OnInit, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataStateComponent } from '../../shared/ui/data-state.component';
import { ReferenceService } from '../../core/services/reference.service';
import { District, Phc, UserAccount } from '../../core/models/models';
import { UserFormDialogComponent } from './user-form-dialog.component';

@Component({
  selector: 'hg-user-admin',
  standalone: true,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    PageHeaderComponent,
    DataStateComponent,
  ],
  templateUrl: './user-admin.component.html',
  styleUrl: './reference-crud.component.scss',
})
export class UserAdminComponent implements OnInit {
  private readonly ref = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);

  readonly columns = ['fullName', 'email', 'role', 'scope', 'actions'];
  readonly rows = signal<UserAccount[]>([]);
  readonly districts = signal<District[]>([]);
  readonly phcs = signal<Phc[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    forkJoin({
      users: this.ref.users({ pageSize: 200 }),
      districts: this.ref.districts(),
      phcs: this.ref.phcs(),
    }).subscribe({
      next: ({ users, districts, phcs }) => {
        this.rows.set(users.items);
        this.districts.set(districts);
        this.phcs.set(phcs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  scope(u: UserAccount): string {
    if (!u.districtId) {
      return 'System-wide';
    }
    const d = this.districts().find((x) => x.id === u.districtId)?.name ?? '?';
    const p = this.phcs().find((x) => x.id === u.phcId)?.name;
    return p ? `${d} · ${p}` : `${d} (district-wide)`;
  }

  open(user: UserAccount | null): void {
    this.dialog
      .open(UserFormDialogComponent, {
        data: { user, districts: this.districts(), phcs: this.phcs() },
      })
      .afterClosed()
      .subscribe((saved: UserAccount | undefined) => {
        if (saved) {
          this.rows.update((list) => {
            const idx = list.findIndex((u) => u.id === saved.id);
            return idx >= 0 ? list.map((u) => (u.id === saved.id ? saved : u)) : [saved, ...list];
          });
        }
      });
  }
}
