import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, debounceTime, map, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api.service';
import { CLAIM_STATUSES, ClaimListQuery, ClaimStatus, ClaimSummary, PagedResult } from '../../api/models';
import { ReferenceApi } from '../../api/reference-api.service';
import { initials, MOCK_USERS } from '../../core/auth/mock-users';
import { claimStatusLabel } from '../../shared/claim-status/claim-status';
import { StatusBadge } from '../../shared/claim-status/status-badge';
import { PageHeader } from '../../shared/page-header/page-header';
import {
  ClaimsListFilters,
  filtersFromQuery,
  hasActiveFilters,
  PAGE_SIZE_OPTIONS,
  paramsFromFilters,
  queryFromParams,
} from './claims-list-query';

@Component({
  selector: 'app-claims-list-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './claims-list-page.html',
  styleUrl: './claims-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClaimsListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly claimsApi = inject(ClaimsApi);
  private readonly reload$ = new Subject<void>();

  protected readonly statuses = CLAIM_STATUSES;
  protected readonly handlers = MOCK_USERS;
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly columns = ['claimNumber', 'policyNumber', 'clientName', 'lossDate', 'causeOfLoss', 'status', 'totalReserves'];
  protected readonly statusLabel = claimStatusLabel;
  protected readonly initials = initials;

  protected readonly causeOfLossCodes = toSignal(inject(ReferenceApi).causeOfLossCodes$.pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly query = signal<ClaimListQuery>(queryFromParams(this.route.snapshot.queryParamMap));
  protected readonly result = signal<PagedResult<ClaimSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly hasActiveFilters = hasActiveFilters;

  protected readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    status: new FormControl<ClaimStatus[]>([], { nonNullable: true }),
    dateFrom: new FormControl<Date | null>(null),
    dateTo: new FormControl<Date | null>(null),
    assignedHandlerId: new FormControl('', { nonNullable: true }),
    causeOfLossCode: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    const destroyRef = inject(DestroyRef);

    combineLatest([this.route.queryParamMap.pipe(map(queryFromParams)), this.reload$.pipe(startWith(undefined))])
      .pipe(
        map(([query]) => query),
        tap((query) => {
          this.query.set(query);
          this.filters.setValue(filtersFromQuery(query), { emitEvent: false });
          this.loading.set(true);
          this.loadFailed.set(false);
        }),
        switchMap((query) =>
          this.claimsApi.list(query).pipe(
            catchError(() => {
              this.loadFailed.set(true);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });

    this.filters.valueChanges
      .pipe(debounceTime(350), takeUntilDestroyed(destroyRef))
      .subscribe(() => this.navigate(paramsFromFilters(this.filters.getRawValue() as ClaimsListFilters)));
  }

  protected clearFilters(): void {
    this.navigate({ search: null, status: null, dateFrom: null, dateTo: null, assignedHandlerId: null, causeOfLossCode: null, page: null });
  }

  protected onPage(event: PageEvent): void {
    this.navigate({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected retry(): void {
    this.reload$.next();
  }

  protected openClaim(claim: ClaimSummary): void {
    this.router.navigate(['/claims', claim.id]);
  }

  protected rangeStart(result: PagedResult<ClaimSummary>): number {
    return result.totalCount === 0 ? 0 : (result.page - 1) * result.pageSize + 1;
  }

  protected rangeEnd(result: PagedResult<ClaimSummary>): number {
    return Math.min(result.page * result.pageSize, result.totalCount);
  }

  private navigate(queryParams: Record<string, unknown>): void {
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }
}
