import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { catchError, debounceTime, distinctUntilChanged, filter, finalize, map, of, startWith, switchMap } from 'rxjs';
import { CauseOfLossCode, PolicySearchResult } from '../../../api/models';
import { PoliciesApi } from '../../../api/policies-api.service';
import { ReferenceApi } from '../../../api/reference-api.service';
import { controlValue } from '../../../shared/forms/control-value';
import { LOSS_DESCRIPTION_MIN_LENGTH, policyCoverState, PolicyLossForm } from '../fnol-form';

const POLICY_SEARCH_MIN_LENGTH = 2;

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
export class PolicyLossStep {
  private readonly policiesApi = inject(PoliciesApi);

  readonly form = input.required<PolicyLossForm>();

  protected readonly minDescriptionLength = LOSS_DESCRIPTION_MIN_LENGTH;
  protected readonly maxLossDate = new Date();
  protected readonly searching = signal(false);

  private readonly policyValue = controlValue(this.form, (f) => f.controls.policy, null);
  private readonly unknownPolicy = controlValue(this.form, (f) => f.controls.unknownPolicy, false);
  private readonly causeValue = controlValue(this.form, (f) => f.controls.causeOfLoss, null);
  private readonly description = controlValue(this.form, (f) => f.controls.lossDescription, '');
  private readonly lossDate = controlValue(this.form, (f) => f.controls.lossDate, null);
  private readonly causeOfLossCodes = toSignal(inject(ReferenceApi).causeOfLossCodes$.pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly selected = computed(() => {
    const value = this.policyValue();
    return !this.unknownPolicy() && value !== null && typeof value === 'object' ? value : null;
  });

  protected readonly descriptionLength = computed(() => this.description().trim().length);

  protected readonly filteredCauses = computed(() => {
    const value = this.causeValue();
    const term = typeof value === 'string' ? value.toLowerCase() : '';
    return this.causeOfLossCodes().filter((c) => c.name.toLowerCase().includes(term) || c.code.toLowerCase().includes(term));
  });

  protected readonly coverState = computed(() => {
    const policy = this.selected();
    return policy ? policyCoverState(policy, this.lossDate()) : null;
  });

  protected readonly policyResults = toSignal(
    toObservable(this.policyValue).pipe(
      map((value) => (typeof value === 'string' ? value.trim() : null)),
      filter((term): term is string => term !== null),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => {
        if (term.length < POLICY_SEARCH_MIN_LENGTH) {
          return of([]);
        }
        this.searching.set(true);
        return this.policiesApi.search(term).pipe(
          catchError(() => of([])),
          finalize(() => this.searching.set(false)),
        );
      }),
    ),
    { initialValue: [] },
  );

  protected readonly coverage = toSignal(
    toObservable(this.selected).pipe(
      switchMap((policy) =>
        policy
          ? this.policiesApi.coverage(policy.id).pipe(
              catchError(() => of(null)),
              startWith(null),
            )
          : of(null),
      ),
    ),
    { initialValue: null },
  );

  protected displayPolicy(policy: PolicySearchResult | string | null): string {
    return policy && typeof policy === 'object' ? `${policy.policyNumber} · ${policy.clientName}` : (policy ?? '');
  }

  protected displayCause(cause: CauseOfLossCode | string | null): string {
    return cause && typeof cause === 'object' ? cause.name : (cause ?? '');
  }
}
