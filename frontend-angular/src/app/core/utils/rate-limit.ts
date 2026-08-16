import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoService } from '@jsverse/transloco';

export function isRateLimited(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 429;
}

/** Falls back to the body because the header is unreadable cross-origin unless the
 * API names it in WithExposedHeaders. */
export function retryAfterSeconds(err: HttpErrorResponse): number | null {
  const header = Number(err.headers?.get('Retry-After'));
  if (Number.isFinite(header) && header > 0) return Math.ceil(header);

  const fromBody = Number(err.error?.params?.retryAfterSeconds);
  return Number.isFinite(fromBody) && fromBody > 0 ? Math.ceil(fromBody) : null;
}

/** Needs `<prefix>.tooManyAttempts` and `<prefix>.tooManyAttemptsIn` to exist, the
 * latter taking a `seconds` parameter. */
export function throttleMessage(
  err: HttpErrorResponse,
  transloco: TranslocoService,
  keyPrefix: string,
): string {
  const seconds = retryAfterSeconds(err);

  return seconds === null
    ? transloco.translate(`${keyPrefix}.tooManyAttempts`)
    : transloco.translate(`${keyPrefix}.tooManyAttemptsIn`, { seconds });
}
