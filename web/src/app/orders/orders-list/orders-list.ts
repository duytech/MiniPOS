import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  scan,
  startWith,
  switchMap,
} from 'rxjs';

import { ORDER_STATUSES, OrderStatus, OrderSummary, PagedResult } from '../order.models';
import { OrdersApi } from '../orders-api';

export const SEARCH_DEBOUNCE_MS = 300;
export const PAGE_SIZE = 20;

interface ListState {
  result: PagedResult<OrderSummary> | null;
  loading: boolean;
  error: boolean;
}

const INITIAL_STATE: ListState = { result: null, loading: true, error: false };

@Component({
  selector: 'app-orders-list',
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './orders-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersList {
  private readonly api = inject(OrdersApi);

  protected readonly statuses = ORDER_STATUSES;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<OrderStatus | ''>('', { nonNullable: true });
  private readonly pageRequests = new Subject<number>();

  private readonly search$ = this.search.valueChanges.pipe(
    map((text) => text.trim()),
    debounceTime(SEARCH_DEBOUNCE_MS),
    startWith(this.search.value.trim()),
    distinctUntilChanged(),
  );

  private readonly status$ = this.status.valueChanges.pipe(
    startWith(this.status.value),
    distinctUntilChanged(),
  );

  /** Search text and status together; a change to either starts again at page 1. */
  private readonly query$ = combineLatest([this.search$, this.status$]).pipe(
    switchMap(([search, status]) =>
      this.pageRequests.pipe(
        startWith(1),
        map((page) => ({ search, status, page, pageSize: PAGE_SIZE })),
      ),
    ),
  );

  /**
   * Each query cancels the request still in flight (switchMap). While a request loads, the previous result stays
   * on screen with `loading` set.
   */
  protected readonly state = toSignal(
    this.query$.pipe(
      switchMap((query) =>
        this.api.list(query).pipe(
          map((result): Partial<ListState> => ({ result, loading: false, error: false })),
          catchError(() => of<Partial<ListState>>({ loading: false, error: true })),
          startWith<Partial<ListState>>({ loading: true, error: false }),
        ),
      ),
      scan((state: ListState, patch: Partial<ListState>) => ({ ...state, ...patch }), INITIAL_STATE),
    ),
    { initialValue: INITIAL_STATE },
  );

  protected readonly result = computed(() => this.state().result);
  protected readonly firstRow = computed(() => {
    const result = this.result();
    return result && result.items.length ? (result.page - 1) * result.pageSize + 1 : 0;
  });
  protected readonly lastRow = computed(() => {
    const result = this.result();
    return result ? (result.page - 1) * result.pageSize + result.items.length : 0;
  });
  protected readonly hasPrevious = computed(() => (this.result()?.page ?? 1) > 1);
  protected readonly hasNext = computed(() => {
    const result = this.result();
    return !!result && result.page * result.pageSize < result.totalCount;
  });

  protected goToPage(page: number): void {
    this.pageRequests.next(page);
  }

  protected statusClass(status: OrderStatus): string {
    switch (status) {
      case 'Open':
        return 'bg-sky-50 text-sky-700 ring-sky-600/20';
      case 'Paid':
        return 'bg-emerald-50 text-emerald-700 ring-emerald-600/20';
      case 'Cancelled':
        return 'bg-slate-100 text-slate-600 ring-slate-500/20';
    }
  }
}
