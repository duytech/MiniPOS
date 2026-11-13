export const ORDER_STATUSES = ['Open', 'Paid', 'Cancelled'] as const;

export type OrderStatus = (typeof ORDER_STATUSES)[number];

export function getStatusClass(status: OrderStatus): string {
  switch (status) {
    case 'Open':
      return 'bg-sky-50 text-sky-700 ring-sky-600/20';
    case 'Paid':
      return 'bg-emerald-50 text-emerald-700 ring-emerald-600/20';
    case 'Cancelled':
      return 'bg-slate-100 text-slate-600 ring-slate-500/20';
  }
}

/** A row of the orders list. `total` is computed by the API; the UI only displays it. */
export interface OrderSummary {
  id: string;
  number: number;
  status: OrderStatus;
  createdAt: string;
  itemNames: string[];
  total: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface OrderListQuery {
  search: string;
  status: OrderStatus | '';
  page: number;
  pageSize: number;
}

export type PaymentMethod = 'Cash' | 'Card';

/** One item of an order (`OrderMenuItemDto`). `unitPrice` and `itemTotal` are computed by the API; the UI only displays them. */
export interface OrderMenuItem {
  id: string;
  menuItemId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  notes: string | null;
  itemTotal: number;
}

/** An order (`OrderDto`). Every money field is computed by the API; the UI only displays them. */
export interface Order {
  id: string;
  number: number;
  status: OrderStatus;
  createdAt: string;
  paymentMethod: PaymentMethod;
  items: OrderMenuItem[];
  subtotal: number;
  discount: number;
  surcharge: number;
  tax: number;
  tip: number;
  total: number;
}

/** One part of a split (`SplitPartDto`). Every money field is computed by the API; the UI only displays them. */
export interface SplitPart {
  itemIds: string[];
  subtotal: number;
  discount: number;
  surcharge: number;
  tax: number;
  tip: number;
  total: number;
}

/** A split by item (`OrderSplitDto`): the parts in saved or requested order. */
export interface OrderSplit {
  parts: SplitPart[];
}

/** Body of `PUT /api/orders/{id}/split` and `POST /api/orders/{id}/split/preview`: the item ids of each part, in order. */
export interface SaveSplitRequest {
  parts: { itemIds: string[] }[];
}
