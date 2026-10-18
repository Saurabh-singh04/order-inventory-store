import { Params } from '@angular/router';
import { ProductQuery, ProductSort } from '../../core/models/api.models';

/** Filters as they live in the URL (?search=..&minPrice=..&page=..). */
export interface CatalogFilters {
  search: string;
  minPrice: number | null;
  maxPrice: number | null;
  inStock: boolean;
  sort: SortOption;
  page: number;
}

export const SORT_OPTIONS = [
  { value: 'name-asc', label: 'Name (A–Z)' },
  { value: 'name-desc', label: 'Name (Z–A)' },
  { value: 'price-asc', label: 'Price: low to high' },
  { value: 'price-desc', label: 'Price: high to low' },
  { value: 'newest', label: 'Newest first' },
] as const;

export type SortOption = (typeof SORT_OPTIONS)[number]['value'];

export const PAGE_SIZE = 12;

const toNumber = (value: string | null): number | null => {
  if (value === null || value.trim() === '') return null;
  const n = Number(value);
  return Number.isFinite(n) && n >= 0 ? n : null;
};

export function parseFilters(params: Params): CatalogFilters {
  const sort = SORT_OPTIONS.some((o) => o.value === params['sort']) ? (params['sort'] as SortOption) : 'name-asc';
  const page = Math.max(1, Math.floor(Number(params['page']) || 1));

  return {
    search: typeof params['search'] === 'string' ? params['search'] : '',
    minPrice: toNumber(params['minPrice'] ?? null),
    maxPrice: toNumber(params['maxPrice'] ?? null),
    inStock: params['inStock'] === 'true',
    sort,
    page,
  };
}

/** Only non-default values go into the URL, so links stay short. */
export function toQueryParams(filters: Partial<CatalogFilters>): Params {
  return {
    search: filters.search ? filters.search : null,
    minPrice: filters.minPrice ?? null,
    maxPrice: filters.maxPrice ?? null,
    inStock: filters.inStock ? 'true' : null,
    sort: filters.sort && filters.sort !== 'name-asc' ? filters.sort : null,
    page: filters.page && filters.page > 1 ? filters.page : null,
  };
}

export function toProductQuery(filters: CatalogFilters): ProductQuery {
  const [sortBy, sortDir] =
    filters.sort === 'newest' ? (['newest', 'desc'] as const) : (filters.sort.split('-') as [ProductSort, 'asc' | 'desc']);

  return {
    search: filters.search || undefined,
    minPrice: filters.minPrice ?? undefined,
    maxPrice: filters.maxPrice ?? undefined,
    inStock: filters.inStock ? true : undefined,
    sortBy,
    sortDir,
    page: filters.page,
    pageSize: PAGE_SIZE,
  };
}

export function sameFilters(a: CatalogFilters, b: CatalogFilters): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}
