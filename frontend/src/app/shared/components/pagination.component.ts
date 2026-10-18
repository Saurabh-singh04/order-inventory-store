import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  template: `
    @if (totalPages() > 1) {
      <nav class="pagination" aria-label="Pagination">
        <button type="button" class="btn btn-small" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">
          ‹ Prev
        </button>
        @for (p of pages(); track $index) {
          @if (p === null) {
            <span class="pagination-gap">…</span>
          } @else {
            <button
              type="button"
              class="btn btn-small"
              [class.active]="p === page()"
              [attr.aria-current]="p === page() ? 'page' : null"
              (click)="pageChange.emit(p)"
            >
              {{ p }}
            </button>
          }
        }
        <button
          type="button"
          class="btn btn-small"
          [disabled]="page() >= totalPages()"
          (click)="pageChange.emit(page() + 1)"
        >
          Next ›
        </button>
      </nav>
    }
    @if (totalCount() > 0) {
      <p class="pagination-summary">{{ totalCount() }} result{{ totalCount() === 1 ? '' : 's' }}</p>
    }
  `,
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly totalCount = input(0);
  readonly pageChange = output<number>();

  /** First, last and a window around the current page; null = gap. */
  protected readonly pages = computed<(number | null)[]>(() => {
    const total = this.totalPages();
    const current = this.page();
    const result: (number | null)[] = [];

    for (let p = 1; p <= total; p++) {
      if (p === 1 || p === total || Math.abs(p - current) <= 1) {
        result.push(p);
      } else if (result[result.length - 1] !== null) {
        result.push(null);
      }
    }
    return result;
  });
}
