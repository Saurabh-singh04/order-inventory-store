import { CurrencyPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, merge, of, startWith, switchMap, tap } from 'rxjs';
import { ProductApiService } from '../../core/api/product-api.service';
import { toApiError } from '../../core/errors/api-error';
import { PagedResult, Product } from '../../core/models/api.models';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { EmptyStateComponent, ErrorAlertComponent, LoadingComponent } from '../../shared/components/state-views.component';

@Component({
  selector: 'app-admin-products',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, PaginationComponent, LoadingComponent, EmptyStateComponent, ErrorAlertComponent],
  template: `
    <header class="page-header">
      <h1>Manage products</h1>
      <a routerLink="/admin/products/new" class="btn btn-primary">+ New product</a>
    </header>

    <div class="filters card">
      <label class="field grow">
        <span>Search</span>
        <input type="search" placeholder="Name or SKU" [formControl]="search" />
      </label>
    </div>

    @if (loading()) {
      <app-loading />
    } @else if (error()) {
      <app-error-alert [message]="error()!" (retry)="reload()" />
    } @else if (result()) {
      @let data = result()!;
      @if (data.items.length === 0) {
        <app-empty-state title="No products" message="Create the first product or change the search." />
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th>SKU</th>
                <th>Name</th>
                <th class="num">Price</th>
                <th class="num">Stock</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (p of data.items; track p.id) {
                <tr [class.inactive]="!p.isActive">
                  <td data-label="SKU">{{ p.sku }}</td>
                  <td data-label="Name">{{ p.name }}</td>
                  <td data-label="Price" class="num">{{ p.price | currency: 'INR' }}</td>
                  <td data-label="Stock" class="num" [class.danger]="p.stockQuantity === 0">{{ p.stockQuantity }}</td>
                  <td data-label="Status">{{ p.isActive ? 'Active' : 'Inactive' }}</td>
                  <td class="actions"><a [routerLink]="['/admin/products', p.id]" class="btn btn-small">Edit</a></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <app-pagination [page]="data.page" [totalPages]="data.totalPages" [totalCount]="data.totalCount" (pageChange)="page$.next($event)" />
      }
    }
  `,
})
export class AdminProductsComponent {
  private readonly api = inject(ProductApiService);

  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly page$ = new Subject<number>();
  private readonly reload$ = new Subject<void>();

  protected readonly result = signal<PagedResult<Product> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  private currentPage = 1;
  private currentSearch = '';

  constructor() {
    const search$ = this.search.valueChanges.pipe(
      debounceTime(300),
      map((s) => s.trim()),
      distinctUntilChanged(),
      tap((s) => {
        this.currentSearch = s;
        this.currentPage = 1;
      }),
    );

    merge(search$, this.page$.pipe(tap((p) => (this.currentPage = p))), this.reload$)
      .pipe(
        startWith(null),
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.api
            .list({ search: this.currentSearch || undefined, page: this.currentPage, pageSize: 15, includeInactive: true, sortBy: 'name' })
            .pipe(catchError((err) => {
              this.error.set(toApiError(err).message);
              return of(null);
            })),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((data) => {
        this.result.set(data);
        this.loading.set(false);
      });
  }

  protected reload(): void {
    this.reload$.next();
  }
}
