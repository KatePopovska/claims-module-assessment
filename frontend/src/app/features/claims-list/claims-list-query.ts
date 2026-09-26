import { ParamMap, Params } from '@angular/router';
import { CLAIM_STATUSES, ClaimListQuery, ClaimStatus } from '../../api/models';

export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100];
export const DEFAULT_PAGE_SIZE = 20;

export interface ClaimsListFilters {
  search: string;
  status: ClaimStatus[];
  dateFrom: Date | null;
  dateTo: Date | null;
  assignedHandlerId: string;
  causeOfLossCode: string;
}

export function queryFromParams(params: ParamMap): ClaimListQuery {
  const page = Number(params.get('page'));
  const pageSize = Number(params.get('pageSize'));

  return {
    search: params.get('search') ?? undefined,
    status: params.getAll('status').filter((s): s is ClaimStatus => (CLAIM_STATUSES as readonly string[]).includes(s)),
    dateFrom: params.get('dateFrom') ?? undefined,
    dateTo: params.get('dateTo') ?? undefined,
    assignedHandlerId: params.get('assignedHandlerId') ?? undefined,
    causeOfLossCode: params.get('causeOfLossCode') ?? undefined,
    page: Number.isInteger(page) && page > 0 ? page : 1,
    pageSize: PAGE_SIZE_OPTIONS.includes(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
  };
}

export function filtersFromQuery(query: ClaimListQuery): ClaimsListFilters {
  return {
    search: query.search ?? '',
    status: query.status ?? [],
    dateFrom: parseDate(query.dateFrom),
    dateTo: parseDate(query.dateTo),
    assignedHandlerId: query.assignedHandlerId ?? '',
    causeOfLossCode: query.causeOfLossCode ?? '',
  };
}

export function paramsFromFilters(filters: ClaimsListFilters): Params {
  return {
    search: filters.search.trim() || null,
    status: filters.status.length > 0 ? filters.status : null,
    dateFrom: formatDate(filters.dateFrom),
    dateTo: formatDate(filters.dateTo),
    assignedHandlerId: filters.assignedHandlerId || null,
    causeOfLossCode: filters.causeOfLossCode || null,
    page: null,
  };
}

export function hasActiveFilters(query: ClaimListQuery): boolean {
  return !!(query.search || query.status?.length || query.dateFrom || query.dateTo || query.assignedHandlerId || query.causeOfLossCode);
}

function parseDate(value: string | undefined): Date | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null;
  }

  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function formatDate(value: Date | null): string | null {
  if (!value) {
    return null;
  }

  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}
