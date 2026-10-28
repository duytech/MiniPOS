import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, Signal, computed, inject, input, output } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, Subject, catchError, concat, defer, exhaustMap, map, merge, of, scan, startWith, switchMap } from 'rxjs';

import { Order, OrderSplit, SaveSplitRequest } from '../order.models';
import { OrdersApi } from '../orders-api';

export const CLEARED_MESSAGE = 'The saved split was cleared because the order changed.';
export const TOO_FEW_LINES_MESSAGE = 'Add at least 2 lines to split the bill.';
export const SPLIT_GENERIC_ERROR = 'Something went wrong. Try again.';

/** The smallest number of parts a split can have (R7). */
const MIN_PARTS = 2;

/** The builder's assignment: line id → part index (0-based), or null when the line is unassigned. */
interface Draft {
  partCount: number;
  assignments: Readonly<Record<string, number | null>>;
}

interface SplitState {
  /** The split the API says is saved; null when there is none (or not known yet). */
  saved: OrderSplit | null;
  draft: Draft;
  /** The parts on screen, exactly as the API returned them (GET, preview or PUT). */
  parts: OrderSplit | null;
  loadingSplit: boolean;
  previewing: boolean;
  writing: boolean;
  error: string | null;
  message: string | null;
}

const EMPTY_DRAFT: Draft = { partCount: MIN_PARTS, assignments: {} };

const INITIAL_STATE: SplitState = {
  saved: null,
  draft: EMPTY_DRAFT,
  parts: null,
  loadingSplit: true,
  previewing: false,
  writing: false,
  error: null,
  message: null,
};

/** Each event folds into the state as a function, so a late response can check it still belongs to the draft. */
type Reducer = (state: SplitState) => SplitState;

/** Keeps the assignments of lines still on the order (if their part still exists); new lines start unassigned. */
function reconcile(order: Order, draft: Draft): Draft {
  const partCount = Math.min(draft.partCount, Math.max(MIN_PARTS, order.lines.length));
  const assignments: Record<string, number | null> = {};
  for (const line of order.lines) {
    const part = draft.assignments[line.id] ?? null;
    assignments[line.id] = part !== null && part < partCount ? part : null;
  }
  return { partCount, assignments };
}

function fromSaved(order: Order, split: OrderSplit): Draft {
  const assignments: Record<string, number | null> = {};
  for (const line of order.lines) {
    const index = split.parts.findIndex((part) => part.lineIds.includes(line.id));
    assignments[line.id] = index < 0 ? null : index;
  }
  return { partCount: split.parts.length, assignments };
}

/** Preview runs only when the order can be split and every line has a part (R13). */
function isComplete(order: Order, draft: Draft): boolean {
  return (
    order.status === 'Open' &&
    order.lines.length >= MIN_PARTS &&
    order.lines.every((line) => (draft.assignments[line.id] ?? null) !== null)
  );
}

/** Parts in order 1..N, each with its lines in the order's line order. */
function toRequest(order: Order, draft: Draft): SaveSplitRequest {
  return {
    parts: Array.from({ length: draft.partCount }, (_, part) => ({
      lineIds: order.lines.filter((line) => draft.assignments[line.id] === part).map((line) => line.id),
    })),
  };
}

function problemDetail(error: HttpErrorResponse): string {
  const detail = (error.error as { detail?: unknown } | null)?.detail;
  return error.status === 422 && typeof detail === 'string' ? detail : SPLIT_GENERIC_ERROR;
}

@Component({
  selector: 'app-order-split',
  imports: [CurrencyPipe],
  templateUrl: './order-split.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderSplitSection {
  private readonly api = inject(OrdersApi);

  readonly order = input.required<Order>();

  /** The order changed under us (409 on save or remove, 404 on remove): the parent reloads it. */
  readonly changed = output();

  private readonly edits = new Subject<Draft>();
  private readonly saves = new Subject<void>();
  private readonly removes = new Subject<void>();

  /**
   * Reads use switchMap: a new order makes the previous GET useless, and a newer assignment makes the previous
   * preview useless (its request is cancelled). Save and Remove use exhaustMap: a second click while one runs is a
   * duplicate and is ignored; switchMap would only cancel the request on the client, not on the server.
   */
  protected readonly state: Signal<SplitState> = toSignal(
    merge(
      toObservable(this.order).pipe(switchMap((order) => this.load(order))),
      this.edits.pipe(switchMap((draft) => this.edit(draft))),
      this.saves.pipe(exhaustMap(() => this.runSave())),
      this.removes.pipe(exhaustMap(() => this.runRemove())),
    ).pipe(scan((state: SplitState, reduce: Reducer) => reduce(state), INITIAL_STATE)),
    { initialValue: INITIAL_STATE },
  );

  protected readonly isOpen = computed(() => this.order().status === 'Open');
  protected readonly tooFewLines = computed(() => this.isOpen() && this.order().lines.length < MIN_PARTS);
  /** Open: always shown. Paid or Cancelled: only a saved split is shown, read-only (R14). */
  protected readonly visible = computed(
    () => this.isOpen() || this.state().saved !== null || this.state().error !== null,
  );
  protected readonly editable = computed(() => this.isOpen() && !this.tooFewLines());
  protected readonly busy = computed(() => {
    const state = this.state();
    return state.loadingSplit || state.previewing || state.writing;
  });
  /** Part count choices: 2 to the number of lines (R13). */
  protected readonly partCountChoices = computed(() =>
    Array.from({ length: Math.max(MIN_PARTS, this.order().lines.length) - 1 }, (_, i) => i + MIN_PARTS),
  );
  protected readonly partChoices = computed(() => Array.from({ length: this.state().draft.partCount }, (_, i) => i));
  protected readonly lineNames = computed(() => new Map(this.order().lines.map((line) => [line.id, line.name])));

  protected setPartCount(value: string): void {
    const partCount = Number(value);
    const { assignments } = this.state().draft;
    const kept: Record<string, number | null> = {};
    for (const [lineId, part] of Object.entries(assignments)) {
      kept[lineId] = part !== null && part < partCount ? part : null; // lines of removed parts become unassigned
    }
    this.edits.next({ partCount, assignments: kept });
  }

  protected assign(lineId: string, value: string): void {
    const draft = this.state().draft;
    this.edits.next({ ...draft, assignments: { ...draft.assignments, [lineId]: value === '' ? null : Number(value) } });
  }

  protected partOf(lineId: string): number | null {
    return this.state().draft.assignments[lineId] ?? null;
  }

  protected namesOf(lineIds: string[]): string {
    const names = this.lineNames();
    return lineIds.map((id) => names.get(id) ?? id).join(', ');
  }

  protected save(): void {
    this.saves.next();
  }

  protected removeSplit(): void {
    this.removes.next();
  }

  private load(order: Order): Observable<Reducer> {
    return this.api.getSplit(order.id).pipe(
      map(
        (split): Reducer =>
          (state) => ({
            ...state,
            saved: split,
            draft: fromSaved(order, split),
            parts: split,
            loadingSplit: false,
            previewing: false,
            error: null,
            message: null,
          }),
      ),
      catchError((error: HttpErrorResponse) =>
        error.status === 404
          ? this.noSavedSplit(order)
          : of<Reducer>((state) => ({ ...state, loadingSplit: false, error: SPLIT_GENERIC_ERROR })),
      ),
      // Lines may have changed: keep the assignments of remaining lines now; the previous parts stay on screen.
      startWith<Reducer>((state) => ({
        ...state,
        draft: reconcile(order, state.draft),
        loadingSplit: true,
        previewing: false,
      })),
    );
  }

  /** 404 from GET split: there is no saved split. If one was shown before, the order change cleared it. */
  private noSavedSplit(order: Order): Observable<Reducer> {
    return defer(() => {
      const draft = this.state().draft;
      const complete = isComplete(order, draft);
      const cleared: Reducer = (state) => ({
        ...state,
        saved: null,
        parts: complete ? state.parts : null,
        loadingSplit: false,
        error: null,
        message: state.saved === null ? null : CLEARED_MESSAGE,
      });
      return complete ? concat(of(cleared), this.preview(order, draft)) : of(cleared);
    });
  }

  private edit(draft: Draft): Observable<Reducer> {
    const order = this.order();
    const complete = isComplete(order, draft);
    const setDraft: Reducer = (state) => ({
      ...state,
      draft,
      parts: complete ? state.parts : null,
      previewing: false,
    });
    return complete ? concat(of(setDraft), this.preview(order, draft)) : of(setDraft);
  }

  /** A response only lands if the draft it was asked for is still the one on screen. */
  private preview(order: Order, draft: Draft): Observable<Reducer> {
    return this.api.previewSplit(order.id, toRequest(order, draft)).pipe(
      map(
        (split): Reducer =>
          (state) => (state.draft === draft ? { ...state, parts: split, previewing: false, error: null } : state),
      ),
      // 409 is not expected here (preview changes nothing): like any non-422 error, the generic message (R12).
      catchError((error: HttpErrorResponse) =>
        of<Reducer>((state) =>
          state.draft === draft ? { ...state, previewing: false, error: problemDetail(error) } : state,
        ),
      ),
      startWith<Reducer>((state) => ({ ...state, previewing: true })),
    );
  }

  private runSave(): Observable<Reducer> {
    return defer(() => {
      const order = this.order();
      const draft = this.state().draft;
      if (order.status !== 'Open') {
        return EMPTY;
      }
      return this.api.saveSplit(order.id, toRequest(order, draft)).pipe(
        map(
          (split): Reducer =>
            (state) => ({
              ...state,
              saved: split,
              parts: state.draft === draft ? split : state.parts,
              writing: false,
              error: null,
              message: null,
            }),
        ),
        catchError((error: HttpErrorResponse) => this.writeFailed(error, false)),
        startWith<Reducer>((state) => ({ ...state, writing: true })),
      );
    });
  }

  private runRemove(): Observable<Reducer> {
    return defer(() => {
      const order = this.order();
      if (order.status !== 'Open') {
        return EMPTY;
      }
      return this.api.removeSplit(order.id).pipe(
        map((): Reducer => (state) => ({ ...state, saved: null, writing: false, error: null, message: null })),
        catchError((error: HttpErrorResponse) => this.writeFailed(error, true)),
        startWith<Reducer>((state) => ({ ...state, writing: true })),
      );
    });
  }

  private writeFailed(error: HttpErrorResponse, isRemove: boolean): Observable<Reducer> {
    if (error.status === 409 || (isRemove && error.status === 404)) {
      // The parent reloads the order and shows its "order changed" message; the new order re-reads the split.
      this.changed.emit();
      return of<Reducer>((state) => ({ ...state, writing: false, error: null }));
    }
    return of<Reducer>((state) => ({ ...state, writing: false, error: problemDetail(error) }));
  }
}
