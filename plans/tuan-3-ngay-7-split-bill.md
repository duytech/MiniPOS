# Split bill by item: save, preview, delete + order detail section

Written by the `planner` agent (Week 3 Day 7) after two rounds of business questions; saved as-is by the main session.

## Rules used
| # | Rule | Source |
|---|---|---|
| R1 | Split by whole lines; every line in exactly one part; a missing, duplicate or unknown line, or an empty part â†’ 422. Discount, surcharge, tax and tip are each shared by subtotal share, rounded down; leftover cents go to the largest part (the earlier one on a tie) | Code (`Order.Split`) |
| R2 | Only the line â†’ part assignment is stored (part and part-line tables owned by Order, one migration). Amounts are always recomputed by `Order.Split` on read | User, round 1 |
| R3 | Save, replace and delete only when the order is Open (`EnsureOpen` â†’ 422). A saved split can still be read on Paid or Cancelled orders | User, round 1 |
| R4 | At most one split per order; PUT replaces it; `DELETE /split` â†’ 204 with no body; no saved split â†’ 404; not Open â†’ 422; missing order â†’ 404 | User, rounds 1 and 2 |
| R5 | Only a real change clears the saved split: add item, remove line, a different quantity, a different discount, removing an existing discount, a different tip, removing an existing tip, a different payment method. A no-op keeps it. Pay and Cancel keep it | User, round 2 (Pay/Cancel: planner, accepted) |
| R6 | Paying stays whole-order; the split is for display only | User, round 1 |
| R7 | At least 2 parts; the check lives in `Order.Split`, so preview and save both return 422 | User, round 1; placement: planner, accepted |
| R8 | Parts have no label; only their position (index in the array) is stored and returned | User, round 1 |
| R9 | Preview is `POST /api/orders/{id}/split/preview`, same body as the PUT, saves nothing, Open only (else 422) | Route: planner, accepted; status: user, round 2 |
| R10 | Saving and clearing a split update the orders row, so they go through the `xmin` check | Planner, accepted |
| R11 | DELETE checks in this order: missing order 404 â†’ no saved split 404 â†’ not Open 422 (a Paid order with no split â†’ 404) | Planner; approved by user |
| R12 | UI: 404 from `GET /split` means "no split". Errors: 422 shows the API `detail`; 409 shows "The order changed. Showing the latest version." and reloads; network or 500 shows "Something went wrong. Try again." and keeps the previous data. A 409 is not expected from preview (it doesn't change the order), so preview handles it like any other error: generic message | User, round 1 (404 = no split: planner, accepted; 409 on preview: Planner; approved by user) |
| R13 | UI builder: no saved split â†’ 2 parts, all lines unassigned. Saved split â†’ filled from it, part count = its number of parts. Part count choices go from 2 to the number of lines. Lowering the count unassigns the lines of removed parts. Preview runs automatically (switchMap) when the assignment changes and every line has a part. No confirmations. After a 409 reload, the builder is refilled from the saved split if there is one | User, round 2 (range: planner, accepted; refill after 409: Planner; approved by user) |
| R14 | UI: an Open order with fewer than 2 lines shows "Add at least 2 lines to split the bill." Paid or Cancelled: the saved split is read-only (no builder, no buttons); no saved split â†’ the section is hidden. After a line change, if a split was saved, show "The saved split was cleared because the order changed." The draft keeps the assignments of remaining lines, drops removed lines, and leaves new lines unassigned. Labels: section "Split bill", "Parts" select, per-line "Part N" select with an "Unassigned" option, buttons "Save split" and "Remove split" | User, round 2 (labels: Planner; approved by user) |

## Contract
Every money value is a JSON number with 2 decimals. Property names are camelCase. Each part is the existing
`SplitPartDto` (`lineIds, subtotal, discount, surcharge, tax, tip, total`). The new wrapper `OrderSplitDto` is
`{ "parts": SplitPartDto[] }`, with parts in saved or requested order.

Request body for PUT and preview (`SaveSplitRequest`):
```json
{ "parts": [ { "lineIds": ["3f1c...a1"] }, { "lineIds": ["9b2e...c4", "77d0...e9"] } ] }
```
Response example for PUT, preview and GET. The order has lines 60.00 and 40.00, tax 8.875%: order tax 8.875 â†’ 8.88.
Shares are 5.328 â†’ 5.32 and 3.552 â†’ 3.55, and the leftover 0.01 goes to the 60.00 part.
```json
{ "parts": [
  { "lineIds": ["3f1c...a1"], "subtotal": 60.00, "discount": 0.00, "surcharge": 0.00, "tax": 5.33, "tip": 0.00, "total": 65.33 },
  { "lineIds": ["9b2e...c4"], "subtotal": 40.00, "discount": 0.00, "surcharge": 0.00, "tax": 3.55, "tip": 0.00, "total": 43.55 } ] }
```

| Route | 2xx | Errors |
|---|---|---|
| `GET /api/orders/{id}/split` | 200 `OrderSplitDto` (any status) | 400 empty-guid id; 404 order missing (`"Order '{id}' was not found."`); 404 no saved split (`"Split '{id}' was not found."`) |
| `PUT /api/orders/{id}/split` | 200 `OrderSplitDto` (the saved split) | 400 body missing, `parts` null, a `lineIds` null, an empty-guid line id; 404 order; 409 concurrency (`"The data was changed by another request. Reload and try again."`); 422 Domain, see below |
| `POST /api/orders/{id}/split/preview` | 200 `OrderSplitDto` (nothing stored) | 400 as PUT; 404 order; 422 as PUT |
| `DELETE /api/orders/{id}/split` | 204, no body | 400 empty-guid id; 404 order; 404 no split; 409; 422 not Open |

422 `detail` texts (the UI shows them as they are):
- `"A split needs at least two parts."` (new; replaces "A split needs at least one part.")
- `"Every part of a split needs at least one line."`
- `"Line {id} is not on this order."`
- `"Line {id} is in more than one part."`
- `"Every line of the order must be in a part of the split."`
- `"The order is {Status}; only an open order can be changed."`

Existing order endpoints keep their shape. Their side effect on the saved split is R5.

## Files by layer

### Domain
- **Changed** `src/Pos.Domain/Orders/Order.cs`:
  - `Split` rejects fewer than 2 parts.
  - New `SaveSplit(lineIdsPerPart)`: `EnsureOpen`, validate through `Split`, then replace the stored parts.
  - New `PreviewSplit(lineIdsPerPart)`: `EnsureOpen` + `Split`.
  - New `RemoveSplit()`: `EnsureOpen`, then clear.
  - New `HasSavedSplit`, and `GetSavedSplit()`, which returns `Split(stored ids)` or null and works in any status.
  - New private `ClearSplit()`, called on real changes only. Each method compares old and new values first:
    `ApplyDiscount`, `RemoveDiscount`, `SetTip`, `RemoveTip`, `SetPaymentMethod`. `ChangeLineQuantity` after its
    existing early return. `AddItem` and `RemoveLine` always.
  - Copy the shape from `ApplyDiscount` and `_lines`.
- **New** `src/Pos.Domain/Orders/SavedSplitPart.cs`: entity with `Id` and `Position`, owning a collection of `SavedSplitLine`.
- **New** `src/Pos.Domain/Orders/SavedSplitLine.cs`: holds `LineId`. Copy the shape from `OrderLine.cs`.

### Application
- **New** `src/Pos.Application/Orders/OrderSplitDto.cs`: `{ Parts }`, built from `SplitPart` via `SplitPartDto.From`.
- **New** `src/Pos.Application/Orders/SaveSplit/` (Command, Handler, Validator): copy from `SetTip/`. Returns `OrderSplitDto`.
- **New** `src/Pos.Application/Orders/PreviewSplit/` (Query, Handler, Validator): copy from `SplitOrderEvenly/`.
- **New** `src/Pos.Application/Orders/GetSplit/` (Query, Handler, Validator): `GetSavedSplit() ?? throw NotFoundException("Split", orderId)`.
- **New** `src/Pos.Application/Orders/RemoveSplit/` (Command, Handler, Validator): copy from `RemoveTip/`. Applies R11
  (`!HasSavedSplit` â†’ NotFound before the Domain call). Result type is `Unit` or `bool`, whatever fits `ICommandHandler`.
- Validators check shape only: id not empty, `Parts` not null, each part's `LineIds` not null, each id not empty. Part
  counts are Domain's job.
- **Changed** `src/Pos.Application/DependencyInjection.cs`: register the 4 handlers.

### Infrastructure
- **New** `Persistence/Configurations/SavedSplitPartConfiguration.cs`: table `order_split_parts` (id, order_id FK
  cascade, position, unique(order_id, position)).
- **New** `Persistence/Configurations/SavedSplitLineConfiguration.cs`: table `order_split_part_lines` (part_id FK
  cascade, line_id FK â†’ `order_lines` cascade, PK(part_id, line_id)). Copy from `OrderLineConfiguration.cs`.
- **Changed** `Persistence/Configurations/OrderConfiguration.cs`: `HasMany` saved parts on a backing field.
- **Changed** `Persistence/PosDbContext.cs`: `TouchOrdersWithChangedLines` also touches the order when its
  `SavedSplitPart` rows are added or deleted (R10).
- **Changed** `Persistence/Repositories/OrderRepository.cs`: `GetByIdAsync` includes saved parts and their lines, with
  `AsSplitQuery`. `ListAsync` does not include them.
- **New** migration `OrderSplit` and the updated `PosDbContextModelSnapshot.cs`. There is no backfill: the tables start empty.

### Api
- **Changed** `src/Pos.Api/Controllers/OrdersController.cs`:
  - Actions `GetSplit`, `SaveSplit` (PUT), `PreviewSplit` (POST `split/preview`), `RemoveSplit` (DELETE â†’
    `NoContent()`), with `[ProducesResponseType]` per the contract.
  - New `SaveSplitRequest(IReadOnlyList<SplitPartRequest>? Parts)` and `SplitPartRequest(IReadOnlyList<Guid>? LineIds)`
    at the bottom of the file.
  - Copy from `SetTip`, `RemoveTip`, `SplitEvenly`.

### web
- **Changed** `web/src/app/orders/order.models.ts`: `SplitPart` and `OrderSplit` (money is `number`, computed by the API).
- **Changed** `web/src/app/orders/orders-api.ts`: `getSplit`, `previewSplit`, `saveSplit`, `removeSplit` (relative URLs).
- **New** `web/src/app/orders/order-split/order-split.ts` + `.html`: standalone OnPush component.
  - Input `order: Order`; output `changed` (fired on a 409 from save or remove, or a 404 from remove).
  - One `state` signal built with `toSignal(scan)`, holding saved split, draft, preview, loading and error.
  - Reads (GET split on each new `order` value, preview): `switchMap`.
  - Save and Remove: `exhaustMap`. A second click while one runs is a duplicate, and `switchMap` would only cancel the
    request on the client.
  - On a new `order` input: reconcile the draft per R14 and re-GET the split. If a split was shown before and the GET
    now returns 404, show the "cleared" message.
  - Parts table uses `| currency: 'USD'`. While a preview runs, the previous parts stay on screen with a small indicator.
  - Test ids: `split-section`, `split-part-count`, `split-line-part`, `split-part-row`,
    `split-part-subtotal|discount|surcharge|tax|tip|total`, `split-save`, `split-remove`, `split-loading`,
    `split-error`, `split-message`, `split-too-few-lines`.
  - Copy the pipeline style from `order-detail.ts`.
- **Changed** `web/src/app/orders/order-detail/order-detail.ts` + `.html`:
  - Render `<app-order-split [order]="order">` below `totals`, outside the `data-testid="totals"` block, so the order
    summary selectors don't match part amounts.
  - Add a reload trigger. On `(changed)` it re-GETs the order and shows `CHANGED_MESSAGE`.

## Tests

### Backend (backend task)
**Domain** (`tests/Pos.Domain.Tests/OrderSavedSplitTests.cs`, new; copy from `OrderSplitTests.cs`):
- Save and read back, with exact cents (the contract example, midpoint case): 60.00 / 40.00 at 8.875% â†’ tax 5.33 / 3.55.
- Discount, card and tip case: lines 6.00 + 4.00, 15% discount, card, 18% tip.
  - Order: 10.00 / 1.50 / 0.26 / 0.78 / 1.53 / 11.07.
  - Parts: discount 0.90 / 0.60, surcharge 0.16 / 0.10, tax 0.47 / 0.31, tip 0.92 / 0.61, total 6.65 / 4.42.
- `GetSavedSplit` still works after `Pay` and after `Cancel`.
- `SaveSplit`, `PreviewSplit` and `RemoveSplit` on Paid or Cancelled throw.
- A second `SaveSplit` replaces the first.
- For each R5 method, a pair of tests: the real change clears the split, the no-op keeps it. Pay and Cancel keep it.

**Integration** (`tests/Pos.Api.IntegrationTests/SplitByItemApiTests.cs`, new; copy from `SplitEvenApiTests.cs` and
`OrderAdjustmentsApiTests.cs`):
- Raw money strings for both cases above.
- GET after PUT equals the PUT body; GET with no split â†’ 404.
- GET on a Paid order with a split â†’ 200, same amounts.
- Part totals sum to the `GET /api/orders/{id}` total.
- PUT replaces: a reordered second PUT is what GET returns.
- Preview returns the same amounts and saves nothing (GET still 404).
- Every 400, 404 and 422 row of the contract, including 1 part â†’ 422 and preview on a Paid order â†’ 422.
- DELETE â†’ 204 with an empty body, then GET 404, then DELETE again 404. Paid with a split â†’ 422. Paid with no split â†’ 404.
- For each R5 endpoint, real change vs no-op, checking GET split 404 vs 200.

**Concurrency** (`tests/Pos.Api.IntegrationTests/ConcurrentSplitTests.cs`, new; copy from
`ConcurrentOrderAdjustmentTests.cs`):
- 8 parallel PUTs with different valid groupings of 4 lines: only 200/409, at least one 409. The stored part and
  part-line rows equal one of the accepted bodies.
- 8 PUTs interleaved with 8 add-item calls that each add a new line: only 200/409/422 (422 when a PUT misses a new
  line). Stored line count matches the accepted adds. The stored split is either absent or covers exactly the stored lines.
- Parallel DELETE and PUT: only 200/204/404/409, and the stored state is consistent.

**Validators** (`tests/Pos.Application.Tests/ValidatorTests.cs`, changed): theories for the SaveSplit and PreviewSplit
shapes; GetSplit and RemoveSplit need the order id.

### Frontend (frontend task)
`orders-api.spec.ts`: each of the 4 methods checks method, URL and body, and that the response body comes back unchanged.

`order-split.spec.ts`:
- 404 â†’ 2 parts, all lines unassigned, no preview request.
- Saved split â†’ builder filled from it, part count = its parts.
- Preview fires only when every line is assigned; two quick changes â†’ the first request is cancelled.
- Money shows the exact strings (`1.09` â†’ `$1.09`, `0` â†’ `$0.00`).
- Save sends parts in order. A second click while saving is ignored.
- Lowering the part count unassigns the lines of removed parts.
- Fewer than 2 lines shows the message.
- Paid with a split is read-only with no buttons; Paid with a 404 hides the section.
- A new `order` input while a split is saved â†’ GET returns 404 â†’ the "cleared" message. The draft keeps and drops lines per R14.
- 422 shows `detail`; 409 emits `changed`; 500 shows the generic message and keeps the previous parts.
- While a preview is pending, the previous parts and the loading indicator are both on screen.

`order-detail.spec.ts`: `(changed)` reloads the order and shows `CHANGED_MESSAGE` while the previous order stays on screen.

### Tests task (contract only, written by the `tester` agent)
- `tests/Pos.Api.IntegrationTests/SplitBillContractTests.cs` (new): the contract table row by row against the API, with
  private HTTP helpers in the class (it does not edit `ApiClient.cs`).
- `tests/e2e/specs/split-bill.spec.ts` (new): build a split on the detail page; amounts match `GET /split`; save,
  reload, the builder is filled; remove a line â†’ the "cleared" message; fewer than 2 lines â†’ the message; Paid â†’
  read-only; phone width has no horizontal scroll.
- `tests/e2e/support/split.ts` (new): API helpers for split.

## Tasks (disjoint file lists)
- **backend**: every Domain, Application, Infrastructure (including the migration and snapshot) and Api file above.
  - Tests: `tests/Pos.Domain.Tests/OrderSavedSplitTests.cs`, `tests/Pos.Domain.Tests/OrderSplitTests.cs`,
    `tests/Pos.Api.IntegrationTests/SplitByItemApiTests.cs`, `tests/Pos.Api.IntegrationTests/ConcurrentSplitTests.cs`,
    `tests/Pos.Api.IntegrationTests/ApiClient.cs` (new split helpers), `tests/Pos.Application.Tests/ValidatorTests.cs`.
- **frontend**: `web/src/app/orders/order.models.ts`, `orders-api.ts`, `orders-api.spec.ts`,
  `order-split/order-split.ts|html|spec.ts` (new), `order-detail/order-detail.ts|html|spec.ts`. Works from the Contract only.
- **tests**: `tests/Pos.Api.IntegrationTests/SplitBillContractTests.cs`, `tests/e2e/specs/split-bill.spec.ts`,
  `tests/e2e/support/split.ts`. Works from the Contract and Rules only.
  - The xUnit part can be written in parallel, but it only runs green after backend is merged.
  - The E2E part runs after backend and frontend are both merged.

No file is in two lists.

## Existing code to update
- `tests/Pos.Domain.Tests/OrderSplitTests.cs`:
  - `One_part_is_the_whole_order` becomes a 422/throws test.
  - `A_split_needs_a_part` changes to the "at least two parts" check.
  - `Every_line_must_be_in_a_part` currently uses a single part, so it would pass for the wrong reason. Change it to 3
    lines in 2 parts with one line left out.
- The `Order.Split` XML doc gains the 2-part minimum.
- `tests/e2e/support/pages.ts` `summaryValue` / `expectDetailMatchesApi` must still find only the order totals. The
  frontend keeps the split outside `totals`; the tests task confirms the existing E2E specs still pass.
- The `docs/journal.md` open questions "Split cÃ³ báº¯t buá»™c â‰¥ 2 pháº§n" and "CÃ³ cáº§n lÆ°u split" are now answered.

## Verify
- `docker compose up -d`; `dotnet build Pos.slnx` (0 warnings); `dotnet test tests/Pos.ArchitectureTests`;
  `dotnet test Pos.slnx`. The concurrency runs must show at least one 409.
- In `web/`: `npm run build`, `npm run lint`, `npx ng test --watch=false`.
- In `tests/e2e` (API and web running against `pos_e2e`): `npx playwright test`, `npx tsc -p .`.
- Running app: open an order with lines 60.00 + 40.00 and assign the parts; the preview shows $65.33 / $43.55 and
  one preview POST per change. Save, reload, check the builder is filled. Press + on a line and check the "cleared"
  message. Pay the order and check the split is read-only. An order with 1 line shows the message. Phone width.

## Out of scope
- Per-part payment or payment method (R6).
- Changing the even split's minimum (`SplitEvenly` keeps `1 â‰¤ parts â‰¤ 20`).
- A saved or unsaved indicator in the UI.
- Showing the split on the orders list.
- Limiting request size (number of line ids) beyond the 422 for unknown ids.
- A split for Paid or Cancelled orders that never had one.
