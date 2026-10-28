import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, Signal, computed, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, concatMap, defer, map, merge, of, scan, startWith, switchMap } from 'rxjs';

import { Order, OrderLine, OrderStatus } from '../order.models';
import { OrderSplitSection } from '../order-split/order-split';
import { OrdersApi } from '../orders-api';

export const CHANGED_MESSAGE = 'The order changed. Showing the latest version.';
export const GENERIC_ERROR = 'Something went wrong. Try again.';

/** What the cashier asked for. The quantity to send is worked out when the write runs, from the latest order. */
type LineWrite = { kind: 'remove'; lineId: string } | { kind: 'step'; lineId: string; step: 1 | -1 };

interface DetailState {
  order: Order | null;
  loading: boolean;
  notFound: boolean;
  error: string | null;
}

const INITIAL_STATE: DetailState = { order: null, loading: true, notFound: false, error: null };

type Patch = Partial<DetailState>;

@Component({
  selector: 'app-order-detail',
  imports: [CurrencyPipe, RouterLink, OrderSplitSection],
  templateUrl: './order-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetail {
  private readonly api = inject(OrdersApi);

  /** The `:id` route parameter (bound with `withComponentInputBinding`). */
  readonly id = input.required<string>();

  private readonly writes = new Subject<LineWrite>();
  private readonly reloads = new Subject<void>();

  /**
   * One pipeline for the screen. Loading uses switchMap: a new id makes the previous load useless.
   *
   * Writes use concatMap: every click counts and runs in order, one request at a time. Each write reads the order
   * the previous one returned, so three quick clicks on + send quantity 3, 4, 5 and add 3. exhaustMap would drop
   * clicks and switchMap would only cancel the request on the client, not on the server.
   *
   * Reloads (the split section saw a conflict) queue in the same concatMap: a reload read before a write committed
   * must not land after it and put the older quantity back on screen, or the next + would send a stale quantity.
   */
  protected readonly state: Signal<DetailState> = toSignal(
    toObservable(this.id).pipe(
      switchMap((id) =>
        merge(
          this.load(id),
          merge(this.writes, this.reloads.pipe(map(() => 'reload' as const))).pipe(
            concatMap((job) => (job === 'reload' ? this.reload(id) : this.runWrite(id, job))),
          ),
        ),
      ),
      scan((state: DetailState, patch: Patch) => ({ ...state, ...patch }), INITIAL_STATE),
    ),
    { initialValue: INITIAL_STATE },
  );

  protected readonly order = computed(() => this.state().order);
  protected readonly isOpen = computed(() => this.order()?.status === 'Open');

  protected removeLine(line: OrderLine): void {
    this.writes.next({ kind: 'remove', lineId: line.id });
  }

  protected increase(line: OrderLine): void {
    this.writes.next({ kind: 'step', lineId: line.id, step: 1 });
  }

  protected decrease(line: OrderLine): void {
    this.writes.next({ kind: 'step', lineId: line.id, step: -1 });
  }

  /** The split section saw a 409 (or its split vanished): show the latest order and say why. */
  protected splitChanged(): void {
    this.reloads.next();
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

  private load(id: string): Observable<Patch> {
    return this.api.get(id).pipe(
      map((order): Patch => ({ order, loading: false })),
      catchError((error: HttpErrorResponse) =>
        of<Patch>(error.status === 404 ? { loading: false, notFound: true } : { loading: false, error: GENERIC_ERROR }),
      ),
      // A different order: start from an empty screen, not the previous order's lines.
      startWith<Patch>(INITIAL_STATE),
    );
  }

  private reload(id: string): Observable<Patch> {
    return this.api.get(id).pipe(
      map((order): Patch => ({ order, loading: false, error: CHANGED_MESSAGE })),
      catchError(() => of<Patch>({ loading: false, error: GENERIC_ERROR })),
      startWith<Patch>({ loading: true }), // keeps `order`: the previous data stays on screen
    );
  }

  private runWrite(id: string, write: LineWrite): Observable<Patch> {
    return defer(() => {
      // The latest order on screen: the one the previous write (or reload) returned.
      const order = this.state().order;
      const line = order?.lines.find((l) => l.id === write.lineId);
      if (!order || order.status !== 'Open' || !line) {
        return EMPTY; // the line is already gone (e.g. a second click on Remove) or the order is read-only
      }

      let request: Observable<Order>;
      if (write.kind === 'remove') {
        request = this.api.removeLine(id, line.id);
      } else {
        const quantity = line.quantity + write.step;
        if (quantity < 1) {
          return EMPTY; // − stops at 1; removing a line is only done with Remove
        }
        request = this.api.changeLineQuantity(id, line.id, quantity);
      }

      return request.pipe(
        map((updated): Patch => ({ order: updated, loading: false, error: null })),
        catchError((error: HttpErrorResponse) => this.writeFailed(id, error)),
        startWith<Patch>({ loading: true }), // keeps `order`: the previous data stays on screen
      );
    });
  }

  private writeFailed(id: string, error: HttpErrorResponse): Observable<Patch> {
    if (error.status === 409 || error.status === 404) {
      // Someone else changed the order (or removed the line): show the latest version.
      return this.reload(id);
    }
    if (error.status === 422) {
      const detail = (error.error as { detail?: unknown } | null)?.detail;
      return of<Patch>({ loading: false, error: typeof detail === 'string' ? detail : GENERIC_ERROR });
    }
    return of<Patch>({ loading: false, error: GENERIC_ERROR });
  }
}
