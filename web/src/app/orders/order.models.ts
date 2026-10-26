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
