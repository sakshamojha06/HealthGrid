import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationsService } from '../../core/services/notifications.service';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';
import { Role } from '../../core/models/models';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  roles?: Role[];
}

/** App frame: responsive sidenav + toolbar. Rendered for all authed routes. */
@Component({
  selector: 'hg-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    NotificationBellComponent,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(NotificationsService);
  private readonly realtime = inject(RealtimeService);

  readonly opened = signal(true);
  readonly user = this.auth.user;

  private readonly allNav: NavItem[] = [
    { label: 'Dashboard', icon: 'space_dashboard', route: '/dashboard' },
    { label: 'Record visit', icon: 'assignment_add', route: '/visits/record' },
    { label: 'Visits', icon: 'clinical_notes', route: '/visits' },
    { label: 'Inventory', icon: 'inventory_2', route: '/inventory' },
    { label: 'Medicine requests', icon: 'sync_alt', route: '/requests' },
    { label: 'Find a specialist', icon: 'stethoscope', route: '/doctors/search' },
    { label: 'Alerts', icon: 'warning', route: '/alerts' },
    {
      label: 'Administration',
      icon: 'admin_panel_settings',
      route: '/admin',
      roles: ['SystemAdministrator', 'DistrictAdministrator', 'PhcAdministrator'],
    },
  ];

  readonly nav = computed(() => {
    const role = this.auth.role();
    return this.allNav.filter((i) => !i.roles || (role && i.roles.includes(role)));
  });

  ngOnInit(): void {
    this.notifications.load().subscribe({ error: () => void 0 });
    void this.realtime.start();
  }

  logout(): void {
    void this.realtime.stop();
    this.auth.logout();
  }

  toggle(): void {
    this.opened.update((v) => !v);
  }
}
