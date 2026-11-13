# Coding Standards

Naming and readability rules for C# (`src/`, `tests/`) and TypeScript (`web/`). Some existing code predates them:
follow this file, not the surrounding code.

## Methods start with a verb

Framework-required method names are exempt, such as Angular lifecycle hooks `ngOnInit` and `ngOnDestroy`.

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
