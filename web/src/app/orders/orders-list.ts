import { DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import {
  Observable,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
} from 'rxjs';

import { ORDER_STATUSES, OrderStatus, OrderSummary } from './order.models';
import { OrdersApiService } from './orders-api.service';

/** Debounce for the search box, so the API is not called on every keystroke. */
export const SEARCH_DEBOUNCE_MS = 300;

export type OrdersViewState =
  | { kind: 'loading' }
  | { kind: 'loaded'; orders: OrderSummary[] }
  | { kind: 'error'; message: string };

@Component({
  selector: 'app-orders-list',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, SlicePipe],
  templateUrl: './orders-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersList {
  private readonly api = inject(OrdersApiService);

  protected readonly statuses = ORDER_STATUSES;
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly statusControl = new FormControl<OrderStatus | ''>('', { nonNullable: true });

  private readonly search$: Observable<string> = this.searchControl.valueChanges.pipe(
    debounceTime(SEARCH_DEBOUNCE_MS),
    map((text) => text.trim()),
    startWith(''),
    distinctUntilChanged(),
  );

  private readonly status$: Observable<OrderStatus | ''> = this.statusControl.valueChanges.pipe(
    startWith(this.statusControl.value),
    distinctUntilChanged(),
  );

  /**
   * combineLatest re-queries whenever either the search text or the status changes;
   * switchMap cancels the in-flight request so a slow, stale response can never
   * overwrite the result of a newer query. Errors are caught inside switchMap so
   * one failed request does not kill the stream.
   */
  protected readonly state = toSignal(
    combineLatest([this.search$, this.status$]).pipe(
      switchMap(([search, status]) =>
        this.api.list({ search, status }).pipe(
          map((orders): OrdersViewState => ({ kind: 'loaded', orders })),
          startWith<OrdersViewState>({ kind: 'loading' }),
          catchError((error: unknown) =>
            of<OrdersViewState>({ kind: 'error', message: describeError(error) }),
          ),
        ),
      ),
    ),
    { initialValue: { kind: 'loading' } as OrdersViewState },
  );

  protected clearFilters(): void {
    this.searchControl.setValue('');
    this.statusControl.setValue('');
  }

  protected statusClasses(status: OrderStatus): string {
    switch (status) {
      case 'Open':
        return 'bg-amber-100 text-amber-800 ring-amber-600/20';
      case 'Paid':
        return 'bg-emerald-100 text-emerald-800 ring-emerald-600/20';
      case 'Cancelled':
        return 'bg-slate-100 text-slate-600 ring-slate-500/20';
    }
  }
}

function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Cannot reach the API. Is it running on http://localhost:5191?';
    }
    const detail = (error.error as { detail?: string } | null)?.detail;
    return detail ?? `The API returned ${error.status} ${error.statusText}.`;
  }
  return 'Something went wrong while loading orders.';
}
