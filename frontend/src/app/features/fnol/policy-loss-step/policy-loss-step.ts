import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap, tap } from 'rxjs';
import { CauseOfLossCode, PolicyCoverage, PolicySearchResult } from '../../../api/models';
import { PoliciesApi } from '../../../api/policies-api.service';
import { ReferenceApi } from '../../../api/reference-api.service';
import { LOSS_DESCRIPTION_MIN_LENGTH, policyCoverState, PolicyLossForm, selectedPolicy } from '../fnol-form';

@Component({
  selector: 'app-policy-loss-step',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatTimepickerModule,
  ],
  templateUrl: './policy-loss-step.html',
  styleUrl: './policy-loss-step.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PolicyLossStep implements OnInit {
  private readonly policiesApi = inject(PoliciesApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = input.required<PolicyLossForm>();

  protected readonly minDescriptionLength = LOSS_DESCRIPTION_MIN_LENGTH;
  protected readonly maxLossDate = new Date();
  protected readonly searching = signal(false);
  protected readonly policyResults = signal<PolicySearchResult[]>([]);
  protected readonly coverage = signal<PolicyCoverage | null>(null);
  protected readonly selected = signal<PolicySearchResult | null>(null);
  protected readonly lossDate = signal<Date | null>(null);
  protected readonly descriptionLength = signal(0);
  protected readonly causeFilter = signal('');

  private readonly causeOfLossCodes = toSignal(inject(ReferenceApi).causeOfLossCodes$.pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly filteredCauses = computed(() => {
    const term = this.causeFilter().toLowerCase();
    return this.causeOfLossCodes().filter((c) => c.name.toLowerCase().includes(term) || c.code.toLowerCase().includes(term));
  });

  protected readonly coverState = computed(() => {
    const policy = this.selected();
    return policy ? policyCoverState(policy, this.lossDate()) : null;
  });

  ngOnInit(): void {
    const controls = this.form().controls;

    controls.policy.valueChanges
      .pipe(
        startWith(controls.policy.value),
        tap(() => this.selected.set(selectedPolicy(this.form()))),
        map((value) => (typeof value === 'string' ? value.trim() : null)),
        filter((term): term is string => term !== null),
        debounceTime(250),
        distinctUntilChanged(),
        tap(() => this.coverage.set(null)),
        switchMap((term) => {
          if (term.length < 2) {
            return of([]);
          }
          this.searching.set(true);
          return this.policiesApi.search(term).pipe(catchError(() => of([])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((results) => {
        this.policyResults.set(results);
        this.searching.set(false);
      });

    controls.lossDate.valueChanges.pipe(startWith(controls.lossDate.value), takeUntilDestroyed(this.destroyRef)).subscribe((date) => this.lossDate.set(date));

    controls.lossDescription.valueChanges
      .pipe(startWith(controls.lossDescription.value), takeUntilDestroyed(this.destroyRef))
      .subscribe((text) => this.descriptionLength.set(text.trim().length));

    controls.causeOfLoss.valueChanges
      .pipe(startWith(controls.causeOfLoss.value), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.causeFilter.set(typeof value === 'string' ? value : ''));

    controls.unknownPolicy.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((unknown) => {
      if (unknown) {
        controls.policy.reset(null);
        controls.policy.disable();
        this.coverage.set(null);
        this.selected.set(null);
      } else {
        controls.policy.enable();
      }
    });

    const current = selectedPolicy(this.form());
    if (current) {
      this.loadCoverage(current);
    }
  }

  protected displayPolicy(policy: PolicySearchResult | string | null): string {
    return policy && typeof policy === 'object' ? `${policy.policyNumber} · ${policy.clientName}` : (policy ?? '');
  }

  protected displayCause(cause: CauseOfLossCode | string | null): string {
    return cause && typeof cause === 'object' ? cause.name : (cause ?? '');
  }

  protected onPolicySelected(event: MatAutocompleteSelectedEvent): void {
    this.loadCoverage(event.option.value as PolicySearchResult);
  }

  private loadCoverage(policy: PolicySearchResult): void {
    this.coverage.set(null);
    this.policiesApi
      .coverage(policy.id)
      .pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef))
      .subscribe((coverage) => this.coverage.set(coverage));
  }
}
