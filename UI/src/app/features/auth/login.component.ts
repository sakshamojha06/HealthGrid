import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { switchMap } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'hg-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly hidePassword = signal(true);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly demoAccounts = [
    { label: 'System Admin', email: 'admin@healthgrid.local' },
    { label: 'Ranchi District Admin', email: 'ranchi.admin@healthgrid.local' },
    { label: 'Bokaro PHC Staff', email: 'bokaro.staff@healthgrid.local' },
  ];

  fill(email: string): void {
    this.form.patchValue({ email, password: 'HealthGrid!12345' });
  }

  submit(): void {
    if (this.form.invalid || this.loading()) {
      return;
    }
    this.loading.set(true);
    this.auth
      .login(this.form.getRawValue())
      .pipe(switchMap(() => this.auth.loadCurrentUser()))
      .subscribe({
        next: () => this.router.navigate(['/dashboard']),
        error: () => this.loading.set(false),
      });
  }
}
