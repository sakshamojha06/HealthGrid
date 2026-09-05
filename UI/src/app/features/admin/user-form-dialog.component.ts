import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { finalize } from 'rxjs';
import { ReferenceService } from '../../core/services/reference.service';
import { District, Phc, Role, UserAccount } from '../../core/models/models';

export interface UserFormData {
  user: UserAccount | null;
  districts: District[];
  phcs: Phc[];
}

const ROLES: Role[] = [
  'SystemAdministrator',
  'DistrictAdministrator',
  'PhcAdministrator',
  'Doctor',
  'PhcStaff',
];

@Component({
  selector: 'hg-user-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './user-form-dialog.component.html',
})
export class UserFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly ref = inject(ReferenceService);
  readonly data = inject<UserFormData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UserFormDialogComponent>);

  readonly roles = ROLES;
  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    fullName: [this.data.user?.fullName ?? '', Validators.required],
    email: [this.data.user?.email ?? '', [Validators.required, Validators.email]],
    password: ['', this.data.user ? [] : [Validators.required, Validators.minLength(12)]],
    role: [this.data.user?.role ?? ('PhcStaff' as Role), Validators.required],
    districtId: [this.data.user?.districtId ?? ''],
    phcId: [this.data.user?.phcId ?? ''],
  });

  readonly selectedDistrict = signal(this.data.user?.districtId ?? '');

  readonly phcsInDistrict = computed(() =>
    this.data.phcs.filter((p) => p.districtId === this.selectedDistrict()),
  );

  onDistrictChange(id: string): void {
    this.selectedDistrict.set(id);
    this.form.patchValue({ phcId: '' });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    const call = this.data.user
      ? this.ref.updateUser(this.data.user.id, {
          fullName: v.fullName,
          role: v.role,
          districtId: v.districtId || null,
          phcId: v.phcId || null,
        })
      : this.ref.createUser({
          email: v.email,
          fullName: v.fullName,
          password: v.password,
          role: v.role,
          districtId: v.districtId || null,
          phcId: v.phcId || null,
        });
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (u) => this.dialogRef.close(u),
    });
  }
}
