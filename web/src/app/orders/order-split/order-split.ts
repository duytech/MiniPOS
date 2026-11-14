import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, Signal, computed, inject, input, output } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, Subject, catchError, concat, defer, exhaustMap, map, merge, of, scan, startWith, switchMap } from 'rxjs';

import { Order, OrderSplit, SaveSplitRequest } from '../order.models';
import { OrdersApi } from '../orders-api';
import { GENERIC_ERROR, getErrorMessage, isConflict, isNotFound } from '../api-error';

export const CLEARED_MESSAGE = 'The saved split was cleared because the order changed.';
export const TOO_FEW_ITEMS_MESSAGE = 'Add at least 2 items to split the bill.';

/** The smallest number of parts a split can have (R7). */
const MIN_PARTS = 2;

/** The builder's assignment: item id → part index (0-based), or null when the item is unassigned. */
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
  isLoadingSplit: boolean;
  isPreviewing: boolean;
  isWriting: boolean;
  error: string | null;
  message: string | null;
}

const EMPTY_DRAFT: Draft = { partCount: MIN_PARTS, assignments: {} };

const INITIAL_STATE: SplitState = {
  saved: null,
  draft: EMPTY_DRAFT,
  parts: null,
  isLoadingSplit: true,
  isPreviewing: false,
  isWriting: false,
  error: null,
  message: null,
};

/** Each event folds into the state as a function, so a late response can check it still belongs to the draft. */
type Reducer = (state: SplitState) => SplitState;

/** Keeps the assignments of items still on the order (if their part still exists); new items start unassigned. */
function reconcile(order: Order, draft: Draft): Draft {
  const partCount = Math.min(draft.partCount, Math.max(MIN_PARTS, order.items.length));
  const assignments: Record<string, number | null> = {};
  for (const item of order.items) {
    const part = draft.assignments[item.id] ?? null;
    assignments[item.id] = part !== null && part < partCount ? part : null;
  }
  return { partCount, assignments };
}

function buildDraftFromSaved(order: Order, split: OrderSplit): Draft {
  const assignments: Record<string, number | null> = {};
  for (const item of order.items) {
    const index = split.parts.findIndex((part) => part.itemIds.includes(item.id));
    assignments[item.id] = index < 0 ? null : index;
  }
  return { partCount: split.parts.length, assignments };
}

/** Preview runs only when the order can be split and every item has a part (R13). */
function isComplete(order: Order, draft: Draft): boolean {
  return (
    order.status === 'Open' &&
    order.items.length >= MIN_PARTS &&
    order.items.every((item) => (draft.assignments[item.id] ?? null) !== null)
  );
}

/** Parts in order 1..N, each with its items in the order's item order. */
function buildSaveRequest(order: Order, draft: Draft): SaveSplitRequest {
  return {
    parts: Array.from({ length: draft.partCount }, (_, part) => ({
      itemIds: order.items.filter((item) => draft.assignments[item.id] === part).map((item) => item.id),
    })),
  };
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

  /** The order changed under us (a conflict on save or remove, not found on remove): the parent reloads it. */
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
  protected readonly hasTooFewItems = computed(() => this.isOpen() && this.order().items.length < MIN_PARTS);
  /** Open: always shown. Paid or Cancelled: only a saved split is shown, read-only (R14). */
  protected readonly isVisible = computed(
    () => this.isOpen() || this.state().saved !== null || this.state().error !== null,
  );
  protected readonly canEdit = computed(() => this.isOpen() && !this.hasTooFewItems());
  protected readonly isBusy = computed(() => {
    const state = this.state();
    return state.isLoadingSplit || state.isPreviewing || state.isWriting;
  });
  /** Part count choices: 2 to the number of items (R13). */
  protected readonly partCountChoices = computed(() =>
    Array.from({ length: Math.max(MIN_PARTS, this.order().items.length) - 1 }, (_, i) => i + MIN_PARTS),
  );
  protected readonly partChoices = computed(() => Array.from({ length: this.state().draft.partCount }, (_, i) => i));
  protected readonly itemNames = computed(() => new Map(this.order().items.map((item) => [item.id, item.name])));

  protected setPartCount(value: string): void {
    const partCount = Number(value);
    const { assignments } = this.state().draft;
    const kept: Record<string, number | null> = {};
    for (const [itemId, part] of Object.entries(assignments)) {
      kept[itemId] = part !== null && part < partCount ? part : null; // items of removed parts become unassigned
    }
    this.edits.next({ partCount, assignments: kept });
  }

  protected assign(itemId: string, value: string): void {
    const draft = this.state().draft;
    this.edits.next({ ...draft, assignments: { ...draft.assignments, [itemId]: value === '' ? null : Number(value) } });
  }

  protected getPartOf(itemId: string): number | null {
    return this.state().draft.assignments[itemId] ?? null;
  }

  protected formatItemNames(itemIds: string[]): string {
    const names = this.itemNames();
    return itemIds.map((id) => names.get(id) ?? id).join(', ');
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
            draft: buildDraftFromSaved(order, split),
            parts: split,
            isLoadingSplit: false,
            isPreviewing: false,
            error: null,
            message: null,
          }),
      ),
      catchError((error: unknown) =>
        isNotFound(error)
          ? this.handleNoSavedSplit(order)
          : of<Reducer>((state) => ({ ...state, isLoadingSplit: false, error: GENERIC_ERROR })),
      ),
      // Items may have changed: keep the assignments of remaining items now; the previous parts stay on screen.
      startWith<Reducer>((state) => ({
        ...state,
        draft: reconcile(order, state.draft),
        isLoadingSplit: true,
        isPreviewing: false,
      })),
    );
  }

  /** Not found from GET split: there is no saved split. If one was shown before, the order change cleared it. */
  private handleNoSavedSplit(order: Order): Observable<Reducer> {
    return defer(() => {
      const draft = this.state().draft;
      const complete = isComplete(order, draft);
      const cleared: Reducer = (state) => ({
        ...state,
        saved: null,
        parts: complete ? state.parts : null,
        isLoadingSplit: false,
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
      isPreviewing: false,
    });
    return complete ? concat(of(setDraft), this.preview(order, draft)) : of(setDraft);
  }

  /** A response only lands if the draft it was asked for is still the one on screen. */
  private preview(order: Order, draft: Draft): Observable<Reducer> {
    return this.api.previewSplit(order.id, buildSaveRequest(order, draft)).pipe(
      map(
        (split): Reducer =>
          (state) => (state.draft === draft ? { ...state, parts: split, isPreviewing: false, error: null } : state),
      ),
      // A conflict is not expected here (preview changes nothing): like any error but a broken rule, the generic message (R12).
      catchError((error: unknown) =>
        of<Reducer>((state) =>
          state.draft === draft ? { ...state, isPreviewing: false, error: getErrorMessage(error) } : state,
        ),
      ),
      startWith<Reducer>((state) => ({ ...state, isPreviewing: true })),
    );
  }

  private runSave(): Observable<Reducer> {
    return defer(() => {
      const order = this.order();
      const draft = this.state().draft;
      if (order.status !== 'Open') {
        return EMPTY;
      }
      return this.api.saveSplit(order.id, buildSaveRequest(order, draft)).pipe(
        map(
          (split): Reducer =>
            (state) => ({
              ...state,
              saved: split,
              parts: state.draft === draft ? split : state.parts,
              isWriting: false,
              error: null,
              message: null,
            }),
        ),
        catchError((error: unknown) => this.handleWriteError(error, false)),
        startWith<Reducer>((state) => ({ ...state, isWriting: true })),
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
        map((): Reducer => (state) => ({ ...state, saved: null, isWriting: false, error: null, message: null })),
        catchError((error: unknown) => this.handleWriteError(error, true)),
        startWith<Reducer>((state) => ({ ...state, isWriting: true })),
      );
    });
  }

  private handleWriteError(error: unknown, isRemove: boolean): Observable<Reducer> {
    if (isConflict(error) || (isRemove && isNotFound(error))) {
      // The parent reloads the order and shows its "order changed" message; the new order re-reads the split.
      this.changed.emit();
      return of<Reducer>((state) => ({ ...state, isWriting: false, error: null }));
    }
    return of<Reducer>((state) => ({ ...state, isWriting: false, error: getErrorMessage(error) }));
  }
}
