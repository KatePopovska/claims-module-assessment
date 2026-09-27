import { convertToParamMap } from '@angular/router';
import { DEFAULT_PAGE_SIZE, filtersFromQuery, hasActiveFilters, paramsFromFilters, queryFromParams } from './claims-list-query';

describe('claims list query mapping', () => {
  it('reads filters, ignores unknown statuses and defaults paging', () => {
    const query = queryFromParams(convertToParamMap({ status: ['Open', 'Bogus', 'Closed'], dateFrom: '2026-01-01', search: 'Meridian' }));

    expect(query.status).toEqual(['Open', 'Closed']);
    expect(query.dateFrom).toBe('2026-01-01');
    expect(query.search).toBe('Meridian');
    expect(query.page).toBe(1);
    expect(query.pageSize).toBe(DEFAULT_PAGE_SIZE);
  });

  it('rejects invalid page numbers and sizes', () => {
    const query = queryFromParams(convertToParamMap({ page: '-3', pageSize: '37' }));

    expect(query.page).toBe(1);
    expect(query.pageSize).toBe(DEFAULT_PAGE_SIZE);
  });

  it('round-trips dates as local calendar days and resets to page 1', () => {
    const filters = filtersFromQuery(queryFromParams(convertToParamMap({ dateFrom: '2026-03-05', dateTo: '2026-03-31' })));

    expect(filters.dateFrom).toEqual(new Date(2026, 2, 5));
    expect(paramsFromFilters(filters)).toMatchObject({ dateFrom: '2026-03-05', dateTo: '2026-03-31', page: null, status: null, search: null });
  });

  it('knows when any filter is active', () => {
    expect(hasActiveFilters(queryFromParams(convertToParamMap({})))).toBe(false);
    expect(hasActiveFilters(queryFromParams(convertToParamMap({ causeOfLossCode: 'COL-FIRE' })))).toBe(true);
  });
});
