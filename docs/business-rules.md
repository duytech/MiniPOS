# Business rules

The business rules already decided for Mini POS. Features and how each is verified: `docs/feature-list.json`. The code
is the source of truth; when this file and the code disagree, fix this file.

## API conventions

All routes are under `/api`. Every expected answer is HTTP 200 with `{ isSuccess, data, error }`; `error` has an `ErrorCode`:
`ValidationFailed`; `OrderNotFound`, `MenuItemNotFound`, `OrderLineNotFound`, `SplitNotFound`; `ConcurrentChange` (Postgres
`xmin`), `DataConflict`; `OrderNotOpen`, `LineNotOnOrder`, `DiscountExceedsSubtotal`, `LineQuantityTooLarge`, `InvalidSplit`.
Money is returned as numbers with exactly 2 decimals, USD only.

A route id is not validated: an unknown id, including the all-zero GUID, is not found. An empty id in the request body
is `ValidationFailed`: `menuItemId`, and each line id in `parts` of save split and preview split.

## Order status
- `Open` → `Paid` or `Cancelled`. Paid and Cancelled are final; `PaidAt` / `CancelledAt` record when.
- Items, discount, tip, payment method and the saved split change only while the order is open (else `OrderNotOpen`).
- An empty open order can be paid or cancelled.

## Lines
- Adding an item snapshots its current name and price. A line with the same menu item, same unit price and same notes
  gets its quantity increased; anything different becomes a new line. Blank notes count as no notes.
- Removing the last line leaves the order open and empty.

## Pricing (`OrderPricing.CalculateTotals`, the only place totals are calculated)
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
  line change that would leave a smaller subtotal, is rejected with `DiscountExceedsSubtotal` (change or remove the discount first).
- A percent discount and the tip follow the subtotal when lines change.

## Split bill
- Even split: subtotal, discount, surcharge, tax and tip are each divided by N, rounded down to the cent; the leftover
  cents go to the first part. Parts hold no lines.
- Split by item: at least two parts, every part has at least one line, every line is in exactly one part. Discount,
  surcharge, tax and tip are shared by each part's share of the subtotal, rounded down to the cent; the leftover cents
  go to the part with the largest subtotal (the earlier part on a tie).
- In both splits the parts add up exactly to the order's amounts and total.
- Only the line assignment of a saved split is stored; its amounts are recomputed on every read. Any real change to
  lines, discount, tip or payment method clears it. Paying or cancelling keeps it (read-only).

## Not built yet

- Menu items: there is no list, update or delete yet.
- Web: the app does not yet create orders, add items, set discount, tip or payment method, or pay/cancel an order:
  those exist only in the API.
