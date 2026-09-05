import { Component, input } from '@angular/core';

/** Standard page title + optional subtitle with a slot for actions. */
@Component({
  selector: 'hg-page-header',
  standalone: true,
  template: `
    <header class="ph">
      <div>
        <h1 class="ph__title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="ph__subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="ph__actions"><ng-content></ng-content></div>
    </header>
  `,
  styles: [
    `
      .ph {
        display: flex;
        justify-content: space-between;
        align-items: flex-end;
        gap: 16px;
        margin-bottom: 20px;
        flex-wrap: wrap;
      }
      .ph__title {
        margin: 0;
        font-size: 1.4rem;
        font-weight: 600;
      }
      .ph__subtitle {
        margin: 4px 0 0;
        color: var(--hg-text-muted);
        font-size: 0.9rem;
      }
      .ph__actions {
        display: flex;
        gap: 8px;
        align-items: center;
      }
    `,
  ],
})
export class PageHeaderComponent {
  readonly title = input('');
  readonly subtitle = input<string | null>(null);
}
