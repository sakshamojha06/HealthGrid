import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/guards';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/shell/shell.component').then((m) => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'visits',
        loadComponent: () =>
          import('./features/visits/visit-list.component').then((m) => m.VisitListComponent),
      },
      {
        path: 'visits/record',
        loadComponent: () =>
          import('./features/visits/record-visit.component').then((m) => m.RecordVisitComponent),
      },
      {
        path: 'inventory',
        loadComponent: () =>
          import('./features/inventory/stock-list.component').then((m) => m.StockListComponent),
      },
      {
        path: 'inventory/ledger',
        loadComponent: () =>
          import('./features/inventory/transaction-ledger.component').then(
            (m) => m.TransactionLedgerComponent,
          ),
      },
      {
        path: 'requests',
        loadComponent: () =>
          import('./features/requests/request-list.component').then((m) => m.RequestListComponent),
      },
      {
        path: 'requests/new',
        loadComponent: () =>
          import('./features/requests/new-request.component').then((m) => m.NewRequestComponent),
      },
      {
        path: 'requests/:id',
        loadComponent: () =>
          import('./features/requests/request-detail.component').then(
            (m) => m.RequestDetailComponent,
          ),
      },
      {
        path: 'doctors/search',
        loadComponent: () =>
          import('./features/doctors/specialist-search.component').then(
            (m) => m.SpecialistSearchComponent,
          ),
      },
      {
        path: 'alerts',
        loadComponent: () =>
          import('./features/alerts/alerts.component').then((m) => m.AlertsComponent),
      },
      {
        path: 'admin',
        canActivate: [roleGuard('SystemAdministrator', 'DistrictAdministrator', 'PhcAdministrator')],
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/admin/admin-home.component').then((m) => m.AdminHomeComponent),
          },
          {
            path: 'districts',
            canActivate: [roleGuard('SystemAdministrator')],
            loadComponent: () =>
              import('./features/admin/simple-admin-pages').then((m) => m.AdminDistrictsComponent),
          },
          {
            path: 'phcs',
            canActivate: [roleGuard('SystemAdministrator', 'DistrictAdministrator')],
            loadComponent: () =>
              import('./features/admin/phc-admin.component').then((m) => m.PhcAdminComponent),
          },
          {
            path: 'medicines',
            loadComponent: () =>
              import('./features/admin/simple-admin-pages').then((m) => m.AdminMedicinesComponent),
          },
          {
            path: 'diseases',
            loadComponent: () =>
              import('./features/admin/simple-admin-pages').then((m) => m.AdminDiseasesComponent),
          },
          {
            path: 'specializations',
            loadComponent: () =>
              import('./features/admin/simple-admin-pages').then(
                (m) => m.AdminSpecializationsComponent,
              ),
          },
          {
            path: 'doctors',
            loadComponent: () =>
              import('./features/doctors/doctor-admin.component').then((m) => m.DoctorAdminComponent),
          },
          {
            path: 'users',
            canActivate: [roleGuard('SystemAdministrator', 'DistrictAdministrator')],
            loadComponent: () =>
              import('./features/admin/user-admin.component').then((m) => m.UserAdminComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
