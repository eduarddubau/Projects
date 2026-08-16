import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoService } from '@jsverse/transloco';

export function isRateLimited(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 429;
}

/**
 * Prefers the Retry-After header, falling back to the body. Cross-origin the header is
 * only readable because the API exposes it, and a misconfigured deployment shouldn't
 * cost the user the number.
 */
export function retryAfterSeconds(err: HttpErrorResponse): number | null {
  const header = Number(err.headers?.get('Retry-After'));
  if (Number.isFinite(header) && header > 0) return Math.ceil(header);

  const fromBody = Number(err.error?.params?.retryAfterSeconds);
  return Number.isFinite(fromBody) && fromBody > 0 ? Math.ceil(fromBody) : null;
}

/**
 * A throttled caller must not be told their password was wrong — they would retry, which
 * is the one thing that makes it worse. Expects `<prefix>.tooManyAttempts` and
 * `<prefix>.tooManyAttemptsIn` to exist, the latter taking a `seconds` parameter.
 */
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
