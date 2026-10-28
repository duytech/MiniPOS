export const ORDER_STATUSES = ['Open', 'Paid', 'Cancelled'] as const;

export type OrderStatus = (typeof ORDER_STATUSES)[number];

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

/** One line of an order (`OrderLineDto`). `unitPrice` and `lineTotal` are computed by the API; the UI only displays them. */
export interface OrderLine {
  id: string;
  menuItemId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  notes: string | null;
  lineTotal: number;
}

/** An order (`OrderDto`). Every money field is computed by the API; the UI only displays them. */
export interface Order {
  id: string;
  number: number;
  status: OrderStatus;
  createdAt: string;
  paymentMethod: PaymentMethod;
  lines: OrderLine[];
  subtotal: number;
  discount: number;
  surcharge: number;
  tax: number;
  tip: number;
  total: number;
}

/** One part of a split (`SplitPartDto`). Every money field is computed by the API; the UI only displays them. */
export interface SplitPart {
  lineIds: string[];
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

/** Body of `PUT /api/orders/{id}/split` and `POST /api/orders/{id}/split/preview`: the line ids of each part, in order. */
export interface SaveSplitRequest {
  parts: { lineIds: string[] }[];
}
