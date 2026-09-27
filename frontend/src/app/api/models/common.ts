export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ApiErrorBody {
  type?: string;
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
