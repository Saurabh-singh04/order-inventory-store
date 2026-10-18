import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  BehaviorSubject,
  Observable,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  merge,
  of,
  startWith,
  switchMap,
} from 'rxjs';
import { ProductApiService } from '../../core/api/product-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { CartService } from '../../core/cart/cart.service';
import { toApiError } from '../../core/errors/api-error';
import { PagedResult, Product } from '../../core/models/api.models';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { EmptyStateComponent, ErrorAlertComponent, LoadingComponent } from '../../shared/components/state-views.component';
import { ToastService } from '../../shared/toast/toast.service';
import {
  CatalogFilters,
  SORT_OPTIONS,
  isPriceRangeInvalid,
  parseFilters,
  sameFilters,
  toProductQuery,
  toQueryParams,
} from './catalog-filters';
import { ProductCardComponent } from './product-card.component';

type LoadState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ok'; data: PagedResult<Product> };

@Component({
  selector: 'app-catalog',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ProductCardComponent,
    PaginationComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorAlertComponent,
  ],
  template: `
    <header class="page-header">
      <h1>Products</h1>
      @if (cart.count() > 0 && !auth.isAdmin()) {
        <a routerLink="/cart" class="btn btn-primary">Go to cart ({{ cart.count() }})</a>
      }
    </header>

    <section class="filters card" aria-label="Filters">
      <label class="field grow">
        <span>Search</span>
        <input type="search" placeholder="Search by name or SKU" [formControl]="search" />
      </label>
      <label class="field">
        <span>Min price</span>
        <input type="number" min="0" inputmode="decimal" [formControl]="minPrice" />
      </label>
      <label class="field">
        <span>Max price</span>
        <input type="number" min="0" inputmode="decimal" [formControl]="maxPrice" />
      </label>
      <label class="field">
        <span>Sort by</span>
        <select [formControl]="sort">
          @for (option of sortOptions; track option.value) {
            <option [value]="option.value">{{ option.label }}</option>
          }
        </select>
      </label>
      <label class="checkbox">
        <input type="checkbox" [formControl]="inStock" />
        <span>In stock only</span>
      </label>
      @if (hasFilters()) {
        <button type="button" class="btn btn-link" (click)="clearFilters()">Clear</button>
      }
    </section>

    @switch (state().status) {
      @case ('loading') {
        <app-loading label="Loading products…" />
      }
      @case ('error') {
        <app-error-alert [message]="errorMessage()" [retryable]="!isPriceRangeInvalid(filters())" (retry)="reload()" />
      }
      @case ('ok') {
        @if (data(); as page) {
          @if (page.items.length === 0) {
            <app-empty-state title="No products found" message="Try a different search or clear the filters." />
          } @else {
            <div class="product-grid">
              @for (product of page.items; track product.id) {
                <app-product-card
                  [product]="product"
                  [inCart]="cart.quantityOf(product.id)"
                  [canBuy]="!auth.isAdmin()"
                  (addToCart)="add($event)"
                />
              }
            </div>
            <app-pagination
              [page]="page.page"
              [totalPages]="page.totalPages"
              [totalCount]="page.totalCount"
              (pageChange)="goToPage($event)"
            />
          }
        }
      }
    }
  `,
})
export class CatalogComponent {
  private readonly api = inject(ProductApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly cart = inject(CartService);
  protected readonly auth = inject(AuthService);

  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly isPriceRangeInvalid = isPriceRangeInvalid;

  private readonly initial = parseFilters(this.route.snapshot.queryParams);
  protected readonly search = new FormControl(this.initial.search, { nonNullable: true });
  protected readonly minPrice = new FormControl<number | null>(this.initial.minPrice);
  protected readonly maxPrice = new FormControl<number | null>(this.initial.maxPrice);
  protected readonly inStock = new FormControl(this.initial.inStock, { nonNullable: true });
  protected readonly sort = new FormControl(this.initial.sort, { nonNullable: true });

  private readonly reload$ = new BehaviorSubject<void>(undefined);

  /** The URL is the single source of truth for the filters. */
  private readonly filters$: Observable<CatalogFilters> = this.route.queryParams.pipe(
    map(parseFilters),
    distinctUntilChanged(sameFilters),
  );

  protected readonly filters = toSignal(this.filters$, { initialValue: this.initial });

  protected readonly state = toSignal(
    combineLatest([this.filters$, this.reload$]).pipe(
      // switchMap drops the previous request when filters change quickly.
      switchMap(([filters]) =>
        isPriceRangeInvalid(filters)
          ? of<LoadState>({ status: 'error', message: 'Min price cannot be higher than max price.' })
          : this.api.list(toProductQuery(filters)).pipe(
              map((data): LoadState => ({ status: 'ok', data })),
              catchError((err) => of<LoadState>({ status: 'error', message: toApiError(err).message })),
              startWith<LoadState>({ status: 'loading' }),
            ),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState },
  );

  constructor() {
    const destroyRef = inject(DestroyRef);

    // Typing: wait 300 ms after the last key stroke, then update the URL (resets to page 1).
    merge(
      this.search.valueChanges.pipe(
        debounceTime(300),
        map((value) => value.trim()),
        distinctUntilChanged(),
        map((search) => ({ search })),
      ),
      merge(this.minPrice.valueChanges, this.maxPrice.valueChanges).pipe(
        debounceTime(300),
        map(() => ({ minPrice: this.minPrice.value, maxPrice: this.maxPrice.value })),
      ),
      this.inStock.valueChanges.pipe(map((inStock) => ({ inStock }))),
      this.sort.valueChanges.pipe(map((sort) => ({ sort }))),
    )
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe((change) => this.updateUrl({ ...change, page: 1 }));

    // Back/forward buttons change the URL - mirror it into the inputs without re-triggering.
    this.filters$.pipe(takeUntilDestroyed(destroyRef)).subscribe((f) => {
      if (this.search.value.trim() !== f.search) this.search.setValue(f.search, { emitEvent: false });
      if (this.minPrice.value !== f.minPrice) this.minPrice.setValue(f.minPrice, { emitEvent: false });
      if (this.maxPrice.value !== f.maxPrice) this.maxPrice.setValue(f.maxPrice, { emitEvent: false });
      if (this.inStock.value !== f.inStock) this.inStock.setValue(f.inStock, { emitEvent: false });
      if (this.sort.value !== f.sort) this.sort.setValue(f.sort, { emitEvent: false });
    });
  }

  protected data(): PagedResult<Product> | null {
    const s = this.state();
    return s.status === 'ok' ? s.data : null;
  }

  protected errorMessage(): string {
    const s = this.state();
    return s.status === 'error' ? s.message : '';
  }

  protected hasFilters(): boolean {
    const f = this.filters();
    return !!f.search || f.minPrice !== null || f.maxPrice !== null || f.inStock || f.sort !== 'name-asc';
  }

  protected goToPage(page: number): void {
    this.updateUrl({ page });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected clearFilters(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  protected reload(): void {
    this.reload$.next();
  }

  protected add(product: Product): void {
    this.cart.add(product);
    this.toast.success(`${product.name} added to cart`);
  }

  private updateUrl(change: Partial<CatalogFilters>): void {
    const next = { ...this.filters(), ...change };
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(next),
      // Typing shouldn't flood the history with one entry per filter tweak.
      replaceUrl: change.page === 1,
    });
  }
}
