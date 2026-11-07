# Templates: copy from these files

They follow every rule in CLAUDE.md and pass build, lint and Vitest.

| Need | Copy from |
|---|---|
| Models (money as `number`, API computes it) | `web/src/app/orders/order.models.ts` |
| Service method + its test | `web/src/app/orders/orders-api.ts`, `orders-api.spec.ts` |
| Component with RxJS state in one signal | `web/src/app/orders/orders-list/orders-list.ts` |
| Template: Tailwind, loading indicator, error, empty state, a11y | `web/src/app/orders/orders-list/orders-list.html` |
| Component test: `HttpTestingController`, fake timers, `data-testid` helpers | `web/src/app/orders/orders-list/orders-list.spec.ts` |
| Lazy route | `web/src/app/app.routes.ts` |
| API shapes | `src/Pos.Api/Controllers/OrdersController.cs`, `src/Pos.Application/Orders/OrderDto.cs` |

## State in one signal

```ts
interface ScreenState {
  data: Thing | null;
  loading: boolean;
  error: string | null;
}

const INITIAL_STATE: ScreenState = { data: null, loading: true, error: null };

protected readonly state = toSignal(
  request$.pipe(
    switchMap((query) =>
      this.api.load(query).pipe(
        map((data): Partial<ScreenState> => ({ data, loading: false, error: null })),
        catchError(() => of<Partial<ScreenState>>({ loading: false, error: 'Could not load.' })),
        startWith<Partial<ScreenState>>({ loading: true, error: null }), // keeps `data`: previous data stays
      ),
    ),
    scan((state: ScreenState, patch: Partial<ScreenState>) => ({ ...state, ...patch }), INITIAL_STATE),
  ),
  { initialValue: INITIAL_STATE },
);
```

Writes go through a `Subject` of commands merged into the same pipeline, flattened with `concatMap` or `exhaustMap`
(not `switchMap`), each mapping the returned order into `data`.

## Reading an API error

`OrdersApi` methods end in `.pipe(unwrap())` (`orders/api-error.ts`): every expected answer is HTTP 200, and a failure
comes out as an `ApiError` with `code` and `message`. A network error or a 500 stays an `HttpErrorResponse`.

```ts
catchError((error: unknown) =>
  of<Partial<ScreenState>>(isNotFound(error) ? { loading: false, notFound: true } : { loading: false, error: errorMessage(error) }),
)
```

`errorMessage` shows the API's message only for a broken rule; anything else is the generic text. Use it only if step 0
agreed to show the API's message.

## Test skeleton

```ts
beforeEach(() => {
  vi.useFakeTimers();
  TestBed.configureTestingModule({
    imports: [ThingPage],
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  fixture = TestBed.createComponent(ThingPage);
  http = TestBed.inject(HttpTestingController);
});

afterEach(() => {
  http.verify();
  vi.useRealTimers();
});
```
