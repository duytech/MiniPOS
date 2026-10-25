/** Order statuses as returned by the API (see Pos.Domain.Orders.OrderStatus). */
export const ORDER_STATUSES = ['Open', 'Paid', 'Cancelled'] as const;

export type OrderStatus = (typeof ORDER_STATUSES)[number];

/** One row of GET /api/orders. */
export interface OrderSummary {
  id: string;
  createdAtUtc: string;
  status: OrderStatus;
  itemCount: number;
  total: number;
}

export interface OrderListQuery {
  /** Empty string means "any status". */
  status: OrderStatus | '';
  /** Empty string means "no search". */
  search: string;
}
