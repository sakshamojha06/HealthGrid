import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

@Component({
  selector: 'hg-admin-home',
  standalone: true,
  imports: [RouterLink, MatCardModule, MatIconModule, PageHeaderComponent],
  template: `
    <hg-page-header title="Administration" subtitle="Reference data and user management" />
    <div class="grid">
      @for (item of items; track item.route) {
        <a class="tile" [routerLink]="item.route">
          <mat-icon>{{ item.icon }}</mat-icon>
          <strong>{{ item.label }}</strong>
          <span>{{ item.hint }}</span>
        </a>
      }
    </div>
  `,
  styles: [
    `
      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
        gap: 14px;
      }
      .tile {
        display: flex;
        flex-direction: column;
        gap: 6px;
        padding: 20px;
        border: 1px solid var(--hg-border);
        border-radius: 12px;
        background: var(--hg-surface);
        text-decoration: none;
        color: inherit;
        transition: border-color 0.15s;
      }
      .tile:hover {
        border-color: var(--hg-primary);
      }
      .tile mat-icon {
        color: var(--hg-primary);
      }
      .tile span {
        color: var(--hg-text-muted);
        font-size: 0.82rem;
      }
    `,
  ],
})
export class AdminHomeComponent {
  readonly items = [
    { label: 'Districts', route: '/admin/districts', icon: 'map', hint: 'Isolation boundaries' },
    { label: 'PHCs', route: '/admin/phcs', icon: 'local_hospital', hint: 'Health centres' },
    { label: 'Medicines', route: '/admin/medicines', icon: 'medication', hint: 'Formulary' },
    { label: 'Diseases', route: '/admin/diseases', icon: 'coronavirus', hint: 'Diagnosis catalogue' },
    {
      label: 'Specializations',
      route: '/admin/specializations',
      icon: 'stethoscope',
      hint: 'For the directory',
    },
    { label: 'Doctors', route: '/admin/doctors', icon: 'badge', hint: 'Staff & availability' },
    { label: 'Users', route: '/admin/users', icon: 'group', hint: 'Accounts & roles' },
  ];
}
