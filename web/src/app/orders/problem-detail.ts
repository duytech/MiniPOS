import { HttpErrorResponse } from '@angular/common/http';

export const GENERIC_ERROR = 'Something went wrong. Try again.';

export function problemDetail(error: HttpErrorResponse): string {
  const detail = (error.error as { detail?: unknown } | null)?.detail;
  return error.status === 422 && typeof detail === 'string' ? detail : GENERIC_ERROR;
}
