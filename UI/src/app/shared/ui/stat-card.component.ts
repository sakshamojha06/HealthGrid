import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Compact KPI tile used across the dashboard. */
@Component({
  selector: 'hg-stat-card',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="stat" [class.accent]="accent()">
      <div class="stat__icon"><mat-icon>{{ icon() }}</mat-icon></div>
      <div class="stat__body">
        <div class="stat__value">{{ value() }}</div>
        <div class="stat__label">{{ label() }}</div>
        @if (hint()) {
          <div class="stat__hint">{{ hint() }}</div>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .stat {
        display: flex;
        gap: 14px;
        align-items: center;
        padding: 16px 18px;
        background: var(--hg-surface);
        border: 1px solid var(--hg-border);
        border-radius: 12px;
      }
      .stat.accent {
        border-color: color-mix(in srgb, var(--hg-primary) 40%, transparent);
      }
      .stat__icon {
        display: grid;
        place-items: center;
        width: 44px;
        height: 44px;
        border-radius: 10px;
        background: color-mix(in srgb, var(--hg-primary) 12%, transparent);
        color: var(--hg-primary);
      }
      .stat__value {
        font-size: 1.5rem;
        font-weight: 600;
        line-height: 1.1;
      }
      .stat__label {
        color: var(--hg-text-muted);
        font-size: 0.85rem;
      }
      .stat__hint {
        margin-top: 2px;
        font-size: 0.75rem;
        color: var(--hg-text-muted);
      }
    `,
  ],
})
export class StatCardComponent {
  readonly icon = input('insights');
  readonly value = input<string | number>('—');
  readonly label = input('');
  readonly hint = input<string | null>(null);
  readonly accent = input(false);
}
