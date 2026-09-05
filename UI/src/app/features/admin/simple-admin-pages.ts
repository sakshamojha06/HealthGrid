import { Component, inject } from '@angular/core';
import { map } from 'rxjs';
import { ReferenceCrudComponent, ReferenceCrudConfig } from './reference-crud.component';
import { ReferenceService } from '../../core/services/reference.service';
import { Disease, District, Medicine, Specialization } from '../../core/models/models';

/**
 * Thin wrappers that configure the generic {@link ReferenceCrudComponent} for
 * each simple reference entity. One class per admin route.
 */

@Component({
  selector: 'hg-admin-districts',
  standalone: true,
  imports: [ReferenceCrudComponent],
  template: `<hg-reference-crud [config]="config" />`,
})
export class AdminDistrictsComponent {
  private readonly ref = inject(ReferenceService);
  readonly config: ReferenceCrudConfig<District> = {
    title: 'Districts',
    subtitle: 'Top-level data isolation boundary',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'code', label: 'Code', required: true },
    ],
    list: () => this.ref.districts(),
    create: (b) => this.ref.createDistrict(b),
    update: (id, b) => this.ref.updateDistrict(id, b),
  };
}

@Component({
  selector: 'hg-admin-diseases',
  standalone: true,
  imports: [ReferenceCrudComponent],
  template: `<hg-reference-crud [config]="config" />`,
})
export class AdminDiseasesComponent {
  private readonly ref = inject(ReferenceService);
  readonly config: ReferenceCrudConfig<Disease> = {
    title: 'Diseases',
    subtitle: 'Diagnosis catalogue used for visits and anomaly detection',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'code', label: 'Code', required: true },
    ],
    list: () => this.ref.diseases(),
    create: (b) => this.ref.createDisease(b),
    update: (id, b) => this.ref.updateDisease(id, b),
  };
}

@Component({
  selector: 'hg-admin-specializations',
  standalone: true,
  imports: [ReferenceCrudComponent],
  template: `<hg-reference-crud [config]="config" />`,
})
export class AdminSpecializationsComponent {
  private readonly ref = inject(ReferenceService);
  readonly config: ReferenceCrudConfig<Specialization> = {
    title: 'Specializations',
    subtitle: 'Used by the specialist directory',
    fields: [{ key: 'name', label: 'Name', required: true }],
    list: () => this.ref.specializations(),
    create: (b) => this.ref.createSpecialization(b),
    update: (id, b) => this.ref.createSpecialization(b), // no update endpoint; recreate
  };
}

@Component({
  selector: 'hg-admin-medicines',
  standalone: true,
  imports: [ReferenceCrudComponent],
  template: `<hg-reference-crud [config]="config" />`,
})
export class AdminMedicinesComponent {
  private readonly ref = inject(ReferenceService);
  readonly config: ReferenceCrudConfig<Medicine> = {
    title: 'Medicines',
    subtitle: 'Formulary tracked in inventory and demand forecasts',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'genericName', label: 'Generic name', required: true },
      { key: 'unit', label: 'Unit', required: true },
      { key: 'isActive', label: 'Active', type: 'toggle' },
    ],
    list: () => this.ref.medicines({ pageSize: 200 }).pipe(map((p) => p.items)),
    create: (b) => this.ref.createMedicine(b),
    update: (id, b) => this.ref.updateMedicine(id, b),
  };
}
