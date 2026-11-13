# Coding Standards

Naming rules for C# (`src/`, `tests/`) and TypeScript (`web/`). Some existing code predates them: follow this file,
not the surrounding code.

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
