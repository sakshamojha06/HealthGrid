import {
  AfterViewInit,
  Component,
  ElementRef,
  OnChanges,
  OnDestroy,
  ViewChild,
  input,
} from '@angular/core';
import { Chart, ChartConfiguration, ChartType, registerables } from 'chart.js';

Chart.register(...registerables);

/**
 * Thin wrapper around Chart.js so feature components can drop in a
 * `<hg-chart [type]="'line'" [data]="..." [options]="...">` without touching
 * the canvas lifecycle.
 */
@Component({
  selector: 'hg-chart',
  standalone: true,
  template: `<div class="hg-chart-host"><canvas #canvas></canvas></div>`,
  styles: [
    `
      .hg-chart-host {
        position: relative;
        width: 100%;
        height: 100%;
        min-height: 220px;
      }
    `,
  ],
})
export class ChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  readonly type = input<ChartType>('line');
  readonly data = input<ChartConfiguration['data']>({ datasets: [] });
  readonly options = input<ChartConfiguration['options']>({});

  @ViewChild('canvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;
  private chart?: Chart;

  ngAfterViewInit(): void {
    this.render();
  }

  ngOnChanges(): void {
    this.render();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private render(): void {
    if (!this.canvasRef) {
      return;
    }
    const config: ChartConfiguration = {
      type: this.type(),
      data: this.data(),
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'bottom' } },
        ...this.options(),
      },
    };
    this.chart?.destroy();
    this.chart = new Chart(this.canvasRef.nativeElement, config);
  }
}
