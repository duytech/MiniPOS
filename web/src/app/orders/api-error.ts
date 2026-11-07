import { HttpErrorResponse } from '@angular/common/http';
import { OperatorFunction, map } from 'rxjs';

export const GENERIC_ERROR = 'Something went wrong. Try again.';

export type ApiResponse<T> =
  | { isSuccess: true; data: T; error: null }
  | { isSuccess: false; data: null; error: { code: string; message: string } };

/** An expected failure the API answered with; a network error or a 500 stays an HttpErrorResponse. */
export class ApiError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

/** Every expected answer is HTTP 200: gives the data of a success and throws an ApiError for a failure. */
export function unwrap<T>(): OperatorFunction<ApiResponse<T>, T> {
  return map((response) => {
    if (response.isSuccess) {
      return response.data;
    }
    throw new ApiError(response.error.code, response.error.message);
  });
}

const NOT_FOUND_CODES = ['OrderNotFound', 'MenuItemNotFound', 'OrderLineNotFound', 'SplitNotFound'];
const CONFLICT_CODES = ['ConcurrentChange', 'DataConflict'];
const RULE_CODES = ['OrderNotOpen', 'LineNotOnOrder', 'DiscountExceedsSubtotal', 'LineQuantityTooLarge', 'InvalidSplit'];

const hasCode = (error: unknown, codes: string[]): error is ApiError =>
  error instanceof ApiError && codes.includes(error.code);

/** Also the framework's own 404, e.g. an id that is not a GUID never reaches the API's handlers. */
export const isNotFound = (error: unknown): boolean =>
  hasCode(error, NOT_FOUND_CODES) || (error instanceof HttpErrorResponse && error.status === 404);

export const isConflict = (error: unknown): boolean => hasCode(error, CONFLICT_CODES);

/** What to show the cashier: the API's message for a broken business rule, otherwise the generic one. */
export const errorMessage = (error: unknown): string => (hasCode(error, RULE_CODES) ? error.message : GENERIC_ERROR);
