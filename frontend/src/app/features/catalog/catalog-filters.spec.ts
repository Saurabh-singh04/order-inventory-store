import { isPriceRangeInvalid, parseFilters, toProductQuery, toQueryParams } from './catalog-filters';

describe('catalog filters (URL <-> API query)', () => {
  it('reads filters from the query string and ignores junk values', () => {
    const filters = parseFilters({ search: 'ipad', minPrice: '100', maxPrice: 'abc', inStock: 'true', sort: 'nope', page: '-3' });

    expect(filters).toEqual({ search: 'ipad', minPrice: 100, maxPrice: null, inStock: true, sort: 'name-asc', page: 1 });
  });

  it('keeps shared links short by leaving defaults out of the URL', () => {
    const params = toQueryParams({ search: '', minPrice: null, maxPrice: 500, inStock: false, sort: 'name-asc', page: 1 });

    expect(params).toEqual({ search: null, minPrice: null, maxPrice: 500, inStock: null, sort: null, page: null });
  });

  it('maps the sort option to sortBy/sortDir for the API', () => {
    const query = toProductQuery(parseFilters({ sort: 'price-desc', page: '2' }));

    expect(query.sortBy).toBe('price');
    expect(query.sortDir).toBe('desc');
    expect(query.page).toBe(2);
  });

  it('detects an impossible price range', () => {
    expect(isPriceRangeInvalid(parseFilters({ minPrice: '500', maxPrice: '100' }))).toBeTrue();
    expect(isPriceRangeInvalid(parseFilters({ minPrice: '100', maxPrice: '500' }))).toBeFalse();
  });
});
