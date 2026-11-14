# Coding Standards

Naming and readability rules for C# (`src/`, `tests/`) and TypeScript (`web/`). Some existing code predates them:
follow this file, not the surrounding code.

## Methods start with a verb

Framework-required method names are exempt, such as Angular lifecycle hooks `ngOnInit` and `ngOnDestroy`. Also exempt:
static factory methods that create a value (`Money.Zero`, `OrderDto.From`, `OrderErrors.NotOpen`), `[LoggerMessage]`
log methods, and test method names, which read as behavior sentences.

| Do | Don't |
|----|-------|
| `CalculateTotals`, `AddItem`, `GetById` | `Totals`, `ItemAdd`, `ById` |
| `loadOrders`, `formatStatus` | `orders`, `statusText` |

## Booleans start with `Is`, `Has` or `Can`

Applies to variables, fields, properties, parameters, and methods that return a boolean. In camelCase use `is`, `has`,
`can`.

| Do | Don't |
|----|-------|
| `IsOpen`, `HasDiscount`, `CanSplit` | `Open`, `Discounted`, `Splittable` |
| `isLoading`, `hasError`, `canSave` | `loading`, `error`, `saveEnabled` |
| `bool CanFitFixedDiscount(...)` | `bool FixedDiscountFits(...)` |

A method that returns a boolean meets both rules: `Is`, `Has` or `Can` counts as its verb.

## Name a calculation that needs explaining

When a reader has to work out what an expression means, put it in a variable named for what it holds.

| Do | Don't |
|----|-------|
| `var otherSharesTotal = share * (parts - 1);`<br>`var firstShare = amount - otherSharesTotal;` | `shares[0] = amount - share * (parts - 1);` |

## Extract a block that does one job

When a loop or a few steps inside a method do one job, move them into a function named for that job. The calling
method then reads as a list of steps.

| Do | Don't |
|----|-------|
| `shares[FindIndexOfLargest(partSubtotals)] += leftover;` | `var largest = 0;`<br>`for (var i = 1; i < partSubtotals.Count; i++) { … }`<br>`shares[largest] += leftover;` |

## Name a long call before passing it on

When an argument is itself a long call, put its result in a variable named for what it holds, then pass the variable.
A call is long when it spans more than one line, or when it nests calls two or more levels deep inside the argument.

Stay inline:
- Short calls that read at a glance, such as static factories: `Result.Ok(OrderDto.From(order))`,
  `Result.Fail<OrderDto>(NotFoundErrors.Order(id))`.
- A one-line LINQ chain passed to a DTO constructor or factory: `order.Items.Select(OrderMenuItemDto.From).ToList()`.
- A one-line LINQ chain in a test assertion: `Assert.Equal(expected, parts.Select(p => p.Total))`.
- RxJS operators inside `pipe(...)`; the pipeline itself, when passed on (e.g. to `toSignal`), is a long call.

| Do | Don't |
|----|-------|
| `var splitParts = Enumerable.Range(0, parts)`<br>`    .Select(i => new SplitPart(…))`<br>`    .ToList();`<br>`return Result.Ok<IReadOnlyList<SplitPart>>(splitParts);` | `return Result.Ok<IReadOnlyList<SplitPart>>(Enumerable.Range(0, parts)`<br>`    .Select(i => new SplitPart(…))`<br>`    .ToList());` |
