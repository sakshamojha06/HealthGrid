import { Component, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';

/**
 * Renders loading / error / empty placeholders. Wrap real content in a
 * `<ng-container *ngIf>` alongside this, or use the `ready` input.
 */
@Component({
  selector: 'hg-data-state',
  standalone: true,
  imports: [MatProgressSpinnerModule, MatIconModule],
  template: `
    @if (loading()) {
      <div class="ds"><mat-spinner diameter="34"></mat-spinner><span>Loading…</span></div>
    } @else if (error()) {
      <div class="ds ds--error">
        <mat-icon>error_outline</mat-icon>
        <span>{{ error() }}</span>
      </div>
    } @else if (empty()) {
      <div class="ds ds--empty">
        <mat-icon>inbox</mat-icon>
        <span>{{ emptyText() }}</span>
      </div>
    }
  `,
  styles: [
    `
      .ds {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 10px;
        padding: 40px 16px;
        color: var(--hg-text-muted);
        text-align: center;
      }
      .ds--error {
        color: var(--hg-danger);
      }
      mat-icon {
        font-size: 32px;
        width: 32px;
        height: 32px;
      }
    `,
  ],
})
export class DataStateComponent {
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly empty = input(false);
  readonly emptyText = input('Nothing here yet.');
}
