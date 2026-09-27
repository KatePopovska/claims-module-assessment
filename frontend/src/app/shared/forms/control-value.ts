import { Signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { Observable, startWith, switchMap } from 'rxjs';

export function controlValue$<TSource, TValue>(source: Signal<TSource>, pick: (source: TSource) => AbstractControl<TValue>): Observable<TValue> {
  return toObservable(source).pipe(
    switchMap((value) => {
      const control = pick(value);
      return control.valueChanges.pipe(startWith(control.value));
    }),
  );
}

export function controlValue<TSource, TValue>(source: Signal<TSource>, pick: (source: TSource) => AbstractControl<TValue>, initialValue: TValue): Signal<TValue> {
  return toSignal(controlValue$(source, pick), { initialValue });
}
