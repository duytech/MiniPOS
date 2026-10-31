# Feature list

What Mini POS does today: API routes, the business rules already decided, and the web pages. Update this file when
you add or change a feature. The code is the source of truth; when this file and the code disagree, fix this file.

## API

All routes are under `/api`. Errors are ProblemDetails: 400 validation, 404 not found, 409 concurrency conflict
(Postgres `xmin`), 422 domain rule broken. Money is returned as numbers with exactly 2 decimals, USD only.

### Menu items
| Route | What it does |
|---|---|
| `POST /api/menu-items` | Create a menu item (name, price). Returns 201. |
| `GET /api/menu-items/{id}` | Get one menu item. |

There is no list, update or delete of menu items yet.

### Orders
| Route | What it does |
|---|---|
| `POST /api/orders` | Create an empty open order. The database assigns the sequential `number`. |
| `GET /api/orders` | Paged list (`page`, `pageSize`, `totalCount`), `search` by item name or order number (`#12`), filter by `status`. |
| `GET /api/orders/{id}` | One order with lines and totals. |
| `POST /api/orders/{id}/items` | Add a menu item (quantity, optional notes). |
| `PUT /api/orders/{id}/lines/{lineId}/quantity` | Set a line's quantity (at least 1). |
| `DELETE /api/orders/{id}/lines/{lineId}` | Remove a line. |
| `PUT /api/orders/{id}/discount` | Apply a discount: exactly one of `rate` (percent of subtotal) or `amount` (fixed). |
| `DELETE /api/orders/{id}/discount` | Remove the discount. |
| `PUT /api/orders/{id}/tip` | Set the tip rate (percent). |
| `DELETE /api/orders/{id}/tip` | Remove the tip. |
| `PUT /api/orders/{id}/payment-method` | Set the payment method: `Cash` (default) or `Card`. |
| `POST /api/orders/{id}/pay` | Close the order as paid, for the whole order, with the stored payment method. |
| `POST /api/orders/{id}/cancel` | Close the order as cancelled. |
| `GET /api/orders/{id}/split-even?parts=N` | Even split between N people (nothing is stored). |
| `POST /api/orders/{id}/split/preview` | Price a split by item without saving it. |
| `PUT /api/orders/{id}/split` | Save a split by item, replacing any saved one. |
| `GET /api/orders/{id}/split` | The saved split, priced with the current order; 404 when there is none. |
| `DELETE /api/orders/{id}/split` | Remove the saved split. |

## Business rules already decided

### Order status
- `Open` → `Paid` or `Cancelled`. Paid and Cancelled are final; `PaidAt` / `CancelledAt` record when.
- Items, discount, tip, payment method and the saved split change only while the order is open (else 422).
- An empty open order can be paid or cancelled.

### Lines
- Adding an item snapshots its current name and price. A line with the same menu item, same unit price and same notes
  gets its quantity increased; anything different becomes a new line. Blank notes count as no notes.
- Removing the last line leaves the order open and empty.

### Pricing (`Order.CalculateTotals`, the only place totals are calculated)
Each step is rounded to cents with `MidpointRounding.AwayFromZero` before the next step uses it.
1. line total = unit price × quantity
2. subtotal = sum of line totals
3. discount = percent of subtotal, or the fixed amount
4. surcharge = card surcharge rate × (subtotal − discount), only when paying by card
5. tax = tax rate × (subtotal − discount + surcharge): the surcharge is taxed
6. tip = tip rate × (subtotal − discount): before surcharge and tax

total = subtotal − discount + surcharge + tax + tip

- Tax rate and card surcharge rate come from configuration (`Pricing:TaxRatePercent`, `Pricing:CardSurchargePercent`
  in `src/Pos.Api/appsettings.json`) and are snapshotted on the order when it is created.
- A percent discount above 100% is rejected. A fixed discount can never be more than the subtotal: applying it, or a
  line change that would leave a smaller subtotal, is rejected with 422 (change or remove the discount first).
- A percent discount and the tip follow the subtotal when lines change.

### Split bill
- Even split: subtotal, discount, surcharge, tax and tip are each divided by N, rounded down to the cent; the leftover
  cents go to the first part. Parts hold no lines.
- Split by item: at least two parts, every part has at least one line, every line is in exactly one part. Discount,
  surcharge, tax and tip are shared by each part's share of the subtotal, rounded down to the cent; the leftover cents
  go to the part with the largest subtotal (the earlier part on a tie).
- In both splits the parts add up exactly to the order's amounts and total.
- Only the line assignment of a saved split is stored; its amounts are recomputed on every read. Any real change to
  lines, discount, tip or payment method clears it. Paying or cancelling keeps it (read-only).

## Web (`web/`, Angular)

### `/orders`: order list
- Table with order number (link to detail), created time, item names, status and total.
- Search by item name or order number, filter by status, previous/next paging with "Showing x–y of n".
- While reloading, the previous rows stay visible with an "Updating…" indicator.

### `/orders/:id`: order detail
- Order number, status badge, lines (name, notes, unit price, quantity, line total).
- Open order: decrease/increase quantity and remove a line.
- Totals: subtotal, discount, surcharge, tax, tip, total.
- Split bill section: choose the number of parts, assign each line to a part, live preview of each part's amounts,
  save or remove the split. Paid or cancelled orders show a saved split read-only. Needs at least 2 lines.

The web app does not yet create orders, add items, set discount, tip or payment method, or pay/cancel an order: those
exist only in the API.
