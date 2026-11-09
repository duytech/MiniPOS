import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError, firstValueFrom } from 'rxjs';

import { ApiError, ApiResponse, GENERIC_ERROR, errorMessage, isConflict, isNotFound, unwrap } from './api-error';

describe('api-error', () => {
  it('unwrap gives the data of a success', async () => {
    const response: ApiResponse<number> = { isSuccess: true, data: 5, error: null };

    expect(await firstValueFrom(of(response).pipe(unwrap()))).toBe(5);
  });

  it('unwrap throws an ApiError with the code and message of a failure', async () => {
    const response: ApiResponse<number> = {
      isSuccess: false,
      data: null,
      error: { code: 'OrderNotOpen', message: 'Closed.' },
    };

    const error = await firstValueFrom(of(response).pipe(unwrap())).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('OrderNotOpen');
    expect((error as ApiError).message).toBe('Closed.');
  });

  it('unwrap leaves an HTTP error alone', async () => {
    const http = new HttpErrorResponse({ status: 500 });

    const error = await firstValueFrom(throwError(() => http).pipe(unwrap())).catch((e: unknown) => e);

    expect(error).toBe(http);
  });

  it('shows the message of a broken rule only', () => {
    for (const code of ['OrderNotOpen', 'ItemNotOnOrder', 'DiscountExceedsSubtotal', 'ItemQuantityTooLarge', 'InvalidSplit']) {
      expect(errorMessage(new ApiError(code, 'Why.'))).toBe('Why.');
    }
    for (const code of ['ValidationFailed', 'OrderNotFound', 'ConcurrentChange', 'SomethingNew']) {
      expect(errorMessage(new ApiError(code, 'Why.'))).toBe(GENERIC_ERROR);
    }
    expect(errorMessage(new HttpErrorResponse({ status: 500 }))).toBe(GENERIC_ERROR);
  });

  it('tells not found and conflict apart', () => {
    expect(isNotFound(new ApiError('SplitNotFound', ''))).toBe(true);
    expect(isNotFound(new ApiError('ConcurrentChange', ''))).toBe(false);
    expect(isNotFound(new HttpErrorResponse({ status: 404 }))).toBe(true);
    expect(isNotFound(new HttpErrorResponse({ status: 500 }))).toBe(false);
    expect(isConflict(new ApiError('ConcurrentChange', ''))).toBe(true);
    expect(isConflict(new ApiError('DataConflict', ''))).toBe(true);
    expect(isConflict(new HttpErrorResponse({ status: 409 }))).toBe(false);
  });
});
